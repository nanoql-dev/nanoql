// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Gaussian1d cap/floor engine's compounded overnight coupons (the S-g1d plan's G1R6 (xx)): an overnight index is
/// an Ibor index, so QuantLib's engine prices a compounded coupon on its last day's forward times the whole accrual
/// (<c>gaussian1dcapfloorengine.cpp:76-79</c>), no QuantLib route pricing a compounded caplet on a Gaussian1d model; on
/// the wheel, a year's SOFR caplet at the money a year forward on this curve's GSR model (a = 0.03, σ = 0.009) is worth
/// 5,781.30 per million, where the Monte Carlo below gives 3,949.84 ± 20.78 and nanoql 3,945.48. On GSR nanoql values
/// it as a Black option on the window's compounding factor, lognormal given the state at the window's start, its
/// log-variance the discrete sum's over the fixing dates. The checks, independent of QuantLib's engine: the variance
/// against its direct double sum over the fixing dates on QuantLib's own GSR process; a Monte Carlo of the daily
/// compounding under Hull-White on constant parameters (the same model), exact in the state and the path discount
/// between fixings (Mersenne Twister, a fixed seed), within three standard errors, for a future caplet over a month and
/// over a year, a seasoned one, and ones with a payment lag, an observation shift, a lookback without the shift and a
/// lockout; and the payoff at a vanishing volatility, paid on the index's day counter and on another. On the
/// Markov-functional model, which defines no short rate, the coupon is refused.
/// </summary>
public class CapFloorOvernightTests
{
    private const Real Reversion = 0.03;
    private const Real Volatility = 0.009;
    private const Real Nominal = 1e6;
    private static readonly Date Today = new(30, Month.June, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void TheWindowsVarianceIsTheDirectDoubleSum()
    {
        // A window starting today: the state there known, so the engine's value is the Black value at its variance.
        YieldTermStructure curve = Curve();
        var sofr = new Sofr(curve);
        OvernightIndexedCoupon coupon = Coupon(sofr, Today, sofr.FixingCalendar.Advance(Today, 3, TimeUnit.Months));
        Real strike = coupon.Rate(Context);
        var gsr = new Gsr(curve, [], [Volatility], Reversion);
        Real price = Caplet(coupon, strike, gsr);

        var process = (GsrProcess)gsr.StateProcess();
        ImmutableArray<Date> fixings = coupon.FixingDates();
        ImmutableArray<double> dt = coupon.Dt();
        var times = new List<double>();
        var c = new List<double>();
        Real forward = 1.0;
        for (int i = 0; i < fixings.Length; i++)
        {
            Date start = sofr.ValueDate(fixings[i]), end = sofr.MaturityDate(start);
            Real w = dt[i] / sofr.DayCounter.YearFraction(start, end);
            if (fixings[i] <= Today)
            {
                forward *= 1.0 + (sofr.Fixing(fixings[i], Context) * dt[i]);
                continue;
            }

            Real t = curve.TimeFromReference(fixings[i]);
            Real q = curve.Discount(start) / curve.Discount(end);
            Real g = process.G(t, curve.TimeFromReference(end), 0.0) - process.G(t, curve.TimeFromReference(start), 0.0);
            forward *= 1.0 + (w * (q - 1.0));
            times.Add(t);
            c.Add(g * w * q / (1.0 + (w * (q - 1.0))));
        }

        // Σᵢⱼ cᵢcⱼ Cov(x(tᵢ), x(tⱼ)): the variance to the earlier time, times the expectation's slope in its start.
        Real variance = 0.0;
        for (int i = 0; i < times.Count; i++)
        {
            for (int j = 0; j < times.Count; j++)
            {
                (Real ti, Real tj) = (Math.Min(times[i], times[j]), Math.Max(times[i], times[j]));
                Real slope = process.Expectation(ti, 1.0, tj - ti) - process.Expectation(ti, 0.0, tj - ti);
                variance += c[i] * c[j] * process.Variance(0.0, 0.0, ti) * slope;
            }
        }

        Real tau = sofr.DayCounter.YearFraction(coupon.InterestDates()[0], coupon.InterestDates()[^1]);
        Real expected = Nominal * coupon.AccrualPeriod() / tau
            * BlackFormula(OptionType.Call, 1.0 + (strike * tau), forward, Math.Sqrt(variance), 1.0) * curve.Discount(coupon.Date);
        Assert.True(Math.Abs((price / expected) - 1.0) <= 1e-10, $"{price} against {expected}");
    }

    [Fact]
    public void AFutureCapletOverAMonthIsTheMonteCarlos() => CheckMonteCarlo(1, TimeUnit.Months);

    [Fact]
    public void AFutureCapletOverAYearIsTheMonteCarlos() => CheckMonteCarlo(1, TimeUnit.Years);

    [Fact]
    public void ASeasonedCapletIsTheMonteCarlos()
    {
        // started a month ago, its fixings so far in the index's history
        YieldTermStructure curve = Curve();
        var sofr = new Sofr(curve);
        Calendar calendar = sofr.FixingCalendar;
        Date start = calendar.Advance(Today, -1, TimeUnit.Months);
        OvernightIndexedCoupon coupon = Coupon(sofr, start, calendar.Advance(start, 1, TimeUnit.Years));
        IndexFixings fixings = Context.Fixings;
        for (Date d = start; d < Today; d++)
        {
            if (calendar.IsBusinessDay(d))
            {
                fixings = fixings.AddFixing(sofr, d, 0.0305 + (0.00001 * (d - start)));
            }
        }

        var context = new EvaluationContext { EvaluationDate = Today, Fixings = fixings };
        CheckMonteCarlo(coupon, coupon.Rate(context), curve, context);
    }

    [Fact]
    public void ACapletPaidTwoDaysLateIsTheMonteCarlos() => CheckMonteCarlo(1, TimeUnit.Years, paymentLag: 2);

    [Fact]
    public void ACapletWithAnObservationShiftIsTheMonteCarlos() => CheckMonteCarlo(1, TimeUnit.Years, lookback: 2, shift: true);

    [Fact]
    public void ACapletWithALookbackWithoutTheShiftIsTheMonteCarlos() => CheckMonteCarlo(1, TimeUnit.Years, lookback: 2);

    [Fact]
    public void ACapletWithALockoutIsTheMonteCarlos() => CheckMonteCarlo(1, TimeUnit.Years, lockout: 2);

    [Fact]
    public void AVanishingVolatilityGivesThePayoff()
    {
        YieldTermStructure curve = Curve();
        var sofr = new Sofr(curve);
        Date start = sofr.FixingCalendar.Advance(Today, 1, TimeUnit.Years);

        // paid on the index's day counter, and on another, so the accrual differs from the rate's year fraction
        foreach (DayCounter? paymentDayCounter in new DayCounter?[] { null, new Actual365Fixed() })
        {
            OvernightIndexedCoupon coupon = Coupon(sofr, start, sofr.FixingCalendar.Advance(start, 1, TimeUnit.Years), paymentDayCounter: paymentDayCounter);
            Real forward = coupon.Rate(Context);
            Real strike = forward - 0.005;
            Real price = Caplet(coupon, strike, new Gsr(curve, [], [1e-7], Reversion));
            Real payoff = Nominal * coupon.AccrualPeriod() * (forward - strike) * curve.Discount(coupon.Date);
            Assert.True(Math.Abs((price / payoff) - 1.0) <= 1e-6, $"{paymentDayCounter?.Name}: {price} against {payoff}");
        }
    }

    [Fact]
    public void TheMarkovFunctionalModelRefusesTheCoupon()
    {
        YieldTermStructure curve = Curve();
        var sofr = new Sofr(curve);
        Date start = sofr.FixingCalendar.Advance(Today, 1, TimeUnit.Years);
        OvernightIndexedCoupon coupon = Coupon(sofr, start, sofr.FixingCalendar.Advance(start, 1, TimeUnit.Years));
        var target = new TARGET();
        var mf = new MarkovFunctional(
            curve, 0.01, [], [1.0],
            new ConstantOptionletVolatility(Today, target, BusinessDayConvention.ModifiedFollowing, 0.2, new Actual365Fixed()),
            [target.Advance(Today, 1, TimeUnit.Years), target.Advance(Today, 2, TimeUnit.Years)], new Euribor6M(curve), Context,
            new MarkovFunctionalModelSettings().WithYGridPoints(16).WithGaussHermitePoints(16).WithAdjustments(MarkovFunctionalModelSettingsAdjustments.AdjustNone));
        var cap = new CapFloor(CapFloorType.Cap, [coupon], [0.03]);
        NanoQLException e = Assert.Throws<NanoQLException>(() => Gaussian1dCapFloorEngine.Calculate(cap, Context, mf));
        Assert.Equal(
            $"the compounded overnight coupon paid on {coupon.Date} can't be priced on this model: it defines no short rate, so the compounding "
            + "factor has no distribution given its state",
            e.Message);
    }

    private static void CheckMonteCarlo(int length, TimeUnit unit, int paymentLag = 0, int? lookback = null, int lockout = 0, bool shift = false)
    {
        YieldTermStructure curve = Curve();
        var sofr = new Sofr(curve);
        Date start = sofr.FixingCalendar.Advance(Today, 1, TimeUnit.Years);
        OvernightIndexedCoupon coupon = Coupon(sofr, start, sofr.FixingCalendar.Advance(start, length, unit), paymentLag, lookback, lockout, shift);
        CheckMonteCarlo(coupon, coupon.Rate(Context), curve, Context);
    }

    private static void CheckMonteCarlo(OvernightIndexedCoupon coupon, Real strike, YieldTermStructure curve, EvaluationContext context)
    {
        var gsr = new Gsr(curve, [], [Volatility], Reversion);
        Real price = Caplet(coupon, strike, gsr, context);
        (Real mc, Real error) = HullWhiteMonteCarlo(curve, coupon, strike, context);
        Assert.True(Math.Abs(price - mc) <= 3.0 * error, $"{price} against the Monte Carlo's {mc} ± {error}");
    }

    private static Real Caplet(OvernightIndexedCoupon coupon, Real strike, Gaussian1dModel model, EvaluationContext? context = null)
    {
        var cap = new CapFloor(CapFloorType.Cap, [coupon], [strike]);
        return Gaussian1dCapFloorEngine.Calculate(cap, context ?? Context, model).NPV!.Value;
    }

    private static OvernightIndexedCoupon Coupon(
        OvernightIndex index,
        Date start,
        Date end,
        int paymentLag = 0,
        int? lookback = null,
        int lockout = 0,
        bool shift = false,
        DayCounter? paymentDayCounter = null)
    {
        var schedule = new Schedule([start, end], index.FixingCalendar);
        ImmutableArray<CashFlow> leg = new OvernightLeg(schedule, index)
            .WithNotionals(Nominal)
            .WithPaymentDayCounter(paymentDayCounter)
            .WithPaymentLag(paymentLag)
            .WithPaymentCalendar(index.FixingCalendar)
            .WithLookbackDays(lookback)
            .WithLockoutDays(lockout)
            .WithObservationShift(shift)
            .ToLeg();
        return (OvernightIndexedCoupon)leg[0];
    }

    // A rising zero curve from 2% to 3.5%, the GSR tests' Monte Carlo curve.
    private static YieldTermStructure Curve()
    {
        var dates = new List<Date> { Today };
        var rates = new List<Rate> { 0.02 };
        for (int i = 1; i <= 15; ++i)
        {
            dates.Add(Today + new Period(i, TimeUnit.Years));
            rates.Add(0.02 + (0.015 * (1.0 - Math.Exp(-(Real)i / 4.0))));
        }

        return new InterpolatedZeroCurve<Linear>(dates, rates, new Actual365Fixed());
    }

    // Hull-White's bond P(t, T | x) on the curve, x the zero-mean Ornstein-Uhlenbeck state under the risk-neutral
    // measure (the GSR tests' reference).
    private static Real Bond(YieldTermStructure curve, Real t, Real maturity, Real x)
    {
        Real a = Reversion, s = Volatility;
        Real b = (1.0 - Math.Exp(-a * (maturity - t))) / a;
        Real shift = s * s / (2.0 * a * a) * Math.Pow(1.0 - Math.Exp(-a * t), 2);
        Real variance = s * s / (4.0 * a) * (1.0 - Math.Exp(-2.0 * a * t));
        return curve.Discount(maturity) / curve.Discount(t) * Math.Exp((-b * x) - (b * shift) - (b * b * variance));
    }

    // The caplet by Monte Carlo of its daily compounding: the state and its integral stepped exactly between fixing
    // times, each fixing's rate the index's from the bonds over its own period, the payoff discounted along the path to
    // the last fixing and by the bond to the payment; antithetic pairs, their spread the standard error.
    private static (Real Price, Real Error) HullWhiteMonteCarlo(
        YieldTermStructure curve, OvernightIndexedCoupon coupon, Real strike, EvaluationContext context, int pairs = 20000)
    {
        var index = (OvernightIndex)coupon.Index;
        ImmutableArray<Date> fixings = coupon.FixingDates();
        ImmutableArray<double> dt = coupon.Dt();
        Real known = 1.0;
        var steps = new List<(Real T, Real Start, Real End, Real Tau, Real Dt)>();
        for (int i = 0; i < fixings.Length; i++)
        {
            if (fixings[i] <= context.EvaluationDate)
            {
                known *= 1.0 + (index.Fixing(fixings[i], context) * dt[i]);
                continue;
            }

            Date start = index.ValueDate(fixings[i]), end = index.MaturityDate(start);
            steps.Add((curve.TimeFromReference(fixings[i]), curve.TimeFromReference(start), curve.TimeFromReference(end),
                index.DayCounter.YearFraction(start, end), dt[i]));
        }

        ImmutableArray<Date> rateDates = coupon.ApplyObservationShift && coupon.FixingDays > 0 ? coupon.ValueDates() : coupon.InterestDates();
        Real tau = index.DayCounter.YearFraction(rateDates[0], rateDates[^1]);
        Real scale = Nominal * coupon.AccrualPeriod() / tau;
        Real strikeFactor = 1.0 + (strike * tau);
        Real payment = curve.TimeFromReference(coupon.Date);
        Real last = steps[^1].T;
        Real a = Reversion, s = Volatility;
        Real integralVariance = s * s / (a * a) * (last - (2.0 * (1.0 - Math.Exp(-a * last)) / a) + ((1.0 - Math.Exp(-2.0 * a * last)) / (2.0 * a)));

        var rng = new MersenneTwisterUniformRng(42);
        var normal = new InverseCumulativeNormal();
        var z = new double[2 * steps.Count];
        Real sum = 0.0, sumSquares = 0.0;
        for (int p = 0; p < pairs; p++)
        {
            for (int k = 0; k < z.Length; k++)
            {
                z[k] = normal.Evaluate(rng.NextReal());
            }

            Real pair = 0.5 * (Path(1.0) + Path(-1.0));
            sum += pair;
            sumSquares += pair * pair;
        }

        Real mean = sum / pairs;
        return (mean, Math.Sqrt(((sumSquares / pairs) - (mean * mean)) / pairs));

        Real Path(Real sign)
        {
            Real x = 0.0, integral = 0.0, t = 0.0, factor = known;
            for (int k = 0; k < steps.Count; k++)
            {
                Real h = steps[k].T - t;
                if (h > 0.0)
                {
                    Real e = Math.Exp(-a * h);
                    Real v1 = s * s / (2.0 * a) * (1.0 - (e * e));
                    Real v2 = s * s / (a * a) * (h - (2.0 * (1.0 - e) / a) + ((1.0 - (e * e)) / (2.0 * a)));
                    Real cov = s * s / (2.0 * a * a) * (1.0 - e) * (1.0 - e);
                    Real sd1 = Math.Sqrt(v1);
                    Real loading = cov / sd1;
                    Real residual = Math.Sqrt(Math.Max(v2 - (loading * loading), 0.0));
                    Real z1 = sign * z[2 * k], z2 = sign * z[(2 * k) + 1];
                    integral += (x * (1.0 - e) / a) + (loading * z1) + (residual * z2);
                    x = (x * e) + (sd1 * z1);
                    t = steps[k].T;
                }

                Real rate = ((Bond(curve, t, steps[k].Start, x) / Bond(curve, t, steps[k].End, x)) - 1.0) / steps[k].Tau;
                factor *= 1.0 + (rate * steps[k].Dt);
            }

            Real discount = curve.Discount(last) * Math.Exp(-integral - (0.5 * integralVariance)) * Bond(curve, last, payment, x);
            return scale * Math.Max(factor - strikeFactor, 0.0) * discount;
        }
    }
}
