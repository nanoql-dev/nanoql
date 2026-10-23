// SPDX-FileCopyrightText: Copyright (C) 2013, 2015 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/onefactormodels/gaussian1dmodel.hpp ql/models/shortrate/onefactormodels/gaussian1dmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Models;

/// <summary>
/// The interface of one-factor interest-rate models whose state is Gaussian: a derived model implements the numeraire
/// and the zero bond for a value of the state variable y, the standardized (zero mean, unit variance) form of the
/// model's state x.
/// </summary>
/// <remarks>
/// <para>
/// The variance of the state process given x(t) = x must not depend on x.
/// </para>
/// <para>
/// Immutable, unsealed: GSR and the Markov-functional model derive from it, as a model of one's own may (an extension
/// point). QuantLib's model is a <c>LazyObject</c> that copies the evaluation date and the historic-fixings flag at its
/// calculation and keeps every swap it builds; here the members that read the date or the flag take the
/// <see cref="EvaluationContext"/> (<c>ForwardRate</c>, <c>SwapRate</c>, and <c>SwapAnnuity</c>,
/// whose swap is built under it), and what one call computes twice it computes once, through a memo that lives for the
/// call: each engine call builds one and passes it through the internal members, and each public member builds its
/// own, so nothing outlives the call (the S-g1d plan's G1R1; master plan §2.2's Gaussian1d row).
/// </para>
/// <para>
/// A derived model of another assembly implements <see cref="NumeraireImpl"/> and <see cref="ZerobondImpl"/>,
/// QuantLib's hooks, and sets its state process with <see cref="SetStateProcess"/> while its constructor runs.
/// </para>
/// <para>
/// A curve given on another clock than the model's own (another day counter or reference date) is read at the
/// instant QuantLib's argument denotes: by the date QuantLib derives a time from, where it has one, and the numeraire
/// time, which has no date, at the instant it denotes on the model's clock; where the curves share a clock, QuantLib's
/// reads and bits stand. QuantLib reads such a curve at the model curve's times: a documented divergence (the plan's
/// G1R6 (xvi); <c>Divergences/Gaussian1dMismatchedTimesTests.cs</c>).
/// </para>
/// </remarks>
public abstract class Gaussian1dModel : ITermStructureConsistentModel
{
    private static readonly ErrorFunction Erf = new();

    // Written by SetStateProcess while a derived constructor runs only.
    private StochasticProcess1D? stateProcess;

    /// <summary>Initializes a new instance of the <see cref="Gaussian1dModel"/> class.</summary>
    /// <param name="yieldTermStructure">The model's curve.</param>
    /// <exception cref="ArgumentNullException"><paramref name="yieldTermStructure"/> is null.</exception>
    protected Gaussian1dModel(YieldTermStructure yieldTermStructure)
    {
        ArgumentNullException.ThrowIfNull(yieldTermStructure);
        TermStructure = yieldTermStructure;
    }

    /// <inheritdoc/>
    public YieldTermStructure TermStructure { get; }

    /// <summary>
    /// Computes the integral (2π)^(−1/2)·∫ₐᵇ p(x)·exp(−x²/2) dx with p(x) = ax⁴ + bx³ + cx² + dx + e, from
    /// <paramref name="x0"/> to <paramref name="x1"/>.
    /// </summary>
    /// <param name="a">The coefficient of x⁴.</param>
    /// <param name="b">The coefficient of x³.</param>
    /// <param name="c">The coefficient of x².</param>
    /// <param name="d">The coefficient of x.</param>
    /// <param name="e">The constant.</param>
    /// <param name="x0">The lower end.</param>
    /// <param name="x1">The upper end.</param>
    /// <returns>The integral.</returns>
    /// <remarks>
    /// QuantLib's <c>std::erf</c> is QuantLib's own <see cref="ErrorFunction"/> here, which .NET lacks; the C runtime's
    /// differs from it by a few ulp on some arguments, so an output that reaches it can differ from QuantLib's in its
    /// last places, within the tolerance (the plan's G1R3; master plan §3.3's C runtime classes).
    /// </remarks>
    public static double GaussianPolynomialIntegral(double a, double b, double c, double d, double e, double x0, double x1)
    {
        double aa = 4.0 * a, ba = 2.0 * MathConstants.M_SQRT2 * b, ca = 2.0 * c, da = MathConstants.M_SQRT2 * d;
        double y0 = x0 * MathConstants.M_SQRT1_2, y1 = x1 * MathConstants.M_SQRT1_2;
        return ((0.125 * ((3.0 * aa) + (2.0 * ca) + (4.0 * e)) * Erf.Evaluate(y1))
                - (1.0 / (4.0 * MathConstants.M_SQRTPI) * Math.Exp(-y1 * y1)
                    * ((2.0 * aa * y1 * y1 * y1) + (3.0 * aa * y1) + (2.0 * ba * ((y1 * y1) + 1.0)) + (2.0 * ca * y1) + (2.0 * da))))
            - ((0.125 * ((3.0 * aa) + (2.0 * ca) + (4.0 * e)) * Erf.Evaluate(y0))
                - (1.0 / (4.0 * MathConstants.M_SQRTPI) * Math.Exp(-y0 * y0)
                    * ((2.0 * aa * y0 * y0 * y0) + (3.0 * aa * y0) + (2.0 * ba * ((y0 * y0) + 1.0)) + (2.0 * ca * y0) + (2.0 * da))));
    }

    /// <summary>
    /// Computes the integral (2π)^(−1/2)·∫ₐᵇ p(x)·exp(−x²/2) dx with
    /// p(x) = a(x − h)⁴ + b(x − h)³ + c(x − h)² + d(x − h) + e, from <paramref name="x0"/> to <paramref name="x1"/>.
    /// </summary>
    /// <param name="a">The coefficient of (x − h)⁴.</param>
    /// <param name="b">The coefficient of (x − h)³.</param>
    /// <param name="c">The coefficient of (x − h)².</param>
    /// <param name="d">The coefficient of (x − h).</param>
    /// <param name="e">The constant.</param>
    /// <param name="h">The shift.</param>
    /// <param name="x0">The lower end.</param>
    /// <param name="x1">The upper end.</param>
    /// <returns>The integral.</returns>
    public static double GaussianShiftedPolynomialIntegral(double a, double b, double c, double d, double e, double h, double x0, double x1) =>
        GaussianPolynomialIntegral(
            a,
            (-4.0 * a * h) + b,
            (6.0 * a * h * h) - (3.0 * b * h) + c,
            (-4 * a * h * h * h) + (3.0 * b * h * h) - (2.0 * c * h) + d,
            (a * h * h * h * h) - (b * h * h * h) + (c * h * h) - (d * h) + e,
            x0,
            x1);

    /// <summary>The model's state process.</summary>
    /// <returns>The process.</returns>
    /// <exception cref="NanoQLException">The derived model set no state process.</exception>
    public StochasticProcess1D StateProcess() => stateProcess ?? throw new NanoQLException("state process not set");

    /// <summary>The numeraire at a time, for a value of the standardized state.</summary>
    /// <param name="t">The time.</param>
    /// <param name="y">The standardized state; 0 by default.</param>
    /// <param name="yts">The curve to read instead of the model's; <see langword="null"/>, QuantLib's empty handle, by
    /// default.</param>
    /// <returns>The numeraire.</returns>
    public double Numeraire(double t, double y = 0.0, YieldTermStructure? yts = null) => NumeraireCore(t, null, y, yts, new Gaussian1dMemo());

    /// <summary>The zero bond maturing at a time, at another time, for a value of the standardized state.</summary>
    /// <param name="T">The maturity.</param>
    /// <param name="t">The time; 0 by default.</param>
    /// <param name="y">The standardized state at <paramref name="t"/>; 0 by default.</param>
    /// <param name="yts">The curve to read instead of the model's; <see langword="null"/>, QuantLib's empty handle, by
    /// default.</param>
    /// <returns>The zero bond.</returns>
    public double Zerobond(double T, double t = 0.0, double y = 0.0, YieldTermStructure? yts = null) =>
        ZerobondCore(T, null, t, null, y, yts, new Gaussian1dMemo());

    /// <summary>The numeraire at a date, for a value of the standardized state.</summary>
    /// <param name="referenceDate">The date.</param>
    /// <param name="y">The standardized state; 0 by default.</param>
    /// <param name="yts">The curve to read instead of the model's; <see langword="null"/>, QuantLib's empty handle, by
    /// default.</param>
    /// <returns>The numeraire.</returns>
    public double Numeraire(Date referenceDate, double y = 0.0, YieldTermStructure? yts = null) =>
        Numeraire(referenceDate, y, yts, new Gaussian1dMemo());

    /// <summary>The zero bond maturing at a date, at another date, for a value of the standardized state.</summary>
    /// <param name="maturity">The maturity.</param>
    /// <param name="referenceDate">The date; <see langword="null"/>, QuantLib's empty date, for the model curve's
    /// reference date.</param>
    /// <param name="y">The standardized state at <paramref name="referenceDate"/>; 0 by default.</param>
    /// <param name="yts">The curve to read instead of the model's; <see langword="null"/>, QuantLib's empty handle, by
    /// default.</param>
    /// <returns>The zero bond.</returns>
    public double Zerobond(Date maturity, Date? referenceDate = null, double y = 0.0, YieldTermStructure? yts = null) =>
        Zerobond(maturity, referenceDate, y, yts, new Gaussian1dMemo());

    /// <summary>
    /// The price of an option on a zero bond, at a date and a value of the standardized state, by the integration of
    /// the payoff's cubic spline over a grid of the state at the expiry.
    /// </summary>
    /// <param name="type">Call or put.</param>
    /// <param name="expiry">The expiry.</param>
    /// <param name="valueDate">The date the strike is paid at.</param>
    /// <param name="maturity">The bond's maturity.</param>
    /// <param name="strike">The strike, a price of the bond at <paramref name="valueDate"/>.</param>
    /// <param name="referenceDate">The date; <see langword="null"/>, QuantLib's empty date, for the model curve's
    /// reference date.</param>
    /// <param name="y">The standardized state at <paramref name="referenceDate"/>; 0 by default.</param>
    /// <param name="yts">The curve to read instead of the model's; <see langword="null"/>, QuantLib's empty handle, by
    /// default.</param>
    /// <param name="yStdDevs">The grid's width, in standard deviations; 7 by default.</param>
    /// <param name="yGridPoints">The grid's points on each side; 64 by default.</param>
    /// <param name="extrapolatePayoff">Whether to integrate the payoff past the grid; <see langword="true"/> by
    /// default.</param>
    /// <param name="flatPayoffExtrapolation">Whether past the grid the payoff is flat, at its boundary node's value;
    /// <see langword="false"/> by default.</param>
    /// <returns>The option's price.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type, or
    /// <paramref name="yGridPoints"/> is negative.</exception>
    /// <remarks>
    /// With <paramref name="flatPayoffExtrapolation"/>, the right tail integrates the payoff's last node, where
    /// QuantLib's integrates its second-last: a documented divergence (the plan's G1R6 (xxi);
    /// <c>Divergences/FlatPayoffExtrapolationTests.cs</c>). The numeraire at the expiry reads a curve on another clock
    /// at the expiry's date (G1R6 (xvi)).
    /// </remarks>
    public double ZerobondOption(
        OptionType type,
        Date expiry,
        Date valueDate,
        Date maturity,
        double strike,
        Date? referenceDate = null,
        double y = 0.0,
        YieldTermStructure? yts = null,
        double yStdDevs = 7.0,
        int yGridPoints = 64,
        bool extrapolatePayoff = true,
        bool flatPayoffExtrapolation = false) =>
        ZerobondOption(
            type, expiry, valueDate, maturity, strike, referenceDate, y, yts, yStdDevs, yGridPoints, extrapolatePayoff, flatPayoffExtrapolation, new Gaussian1dMemo());

    /// <summary>The forward rate of an Ibor index at a fixing date, at a date and a value of the standardized
    /// state.</summary>
    /// <param name="fixing">The fixing date.</param>
    /// <param name="context">The evaluation context: a fixing on or before its date is read from its fixings.</param>
    /// <param name="referenceDate">The date; <see langword="null"/>, QuantLib's empty date, for the model curve's
    /// reference date.</param>
    /// <param name="y">The standardized state at <paramref name="referenceDate"/>; 0 by default.</param>
    /// <param name="iborIdx">The index; required, where QuantLib's default is an empty pointer it refuses.</param>
    /// <returns>The forward rate.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="iborIdx"/> is
    /// null.</exception>
    /// <remarks>
    /// A fixing before the evaluation date, or on it when the context enforces today's historic fixings, is the index's
    /// fixing. Otherwise the period ends at the index's maturity date for its value date, as QuantLib's master computes
    /// it after the pin (#2848), where QuantLib 1.44 advances the value date by the tenor itself, which differs for an
    /// index that overrides its maturity date (<c>Divergences/Gaussian1dForwardRateEndDateTests.cs</c>).
    /// </remarks>
    public double ForwardRate(Date fixing, EvaluationContext context, Date? referenceDate = null, double y = 0.0, IborIndex? iborIdx = null) =>
        ForwardRate(fixing, context, referenceDate, y, iborIdx, new Gaussian1dMemo());

    /// <summary>
    /// The compounded rate of an overnight-indexed coupon, at a date and a value of the standardized state: the zero
    /// bonds' ratio over its rate period, the part before the date read from the projection curve.
    /// </summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="referenceDate">The date; <see langword="null"/>, QuantLib's empty date, for the model curve's
    /// reference date.</param>
    /// <param name="y">The standardized state at <paramref name="referenceDate"/>; 0 by default.</param>
    /// <param name="yts">The projection curve; <see langword="null"/>, QuantLib's empty handle, for the
    /// model's.</param>
    /// <returns>The compounded rate over the coupon's rate accrual.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="coupon"/> is null.</exception>
    /// <remarks>
    /// Under an observation shift with fixing days, the rate period runs over the coupon's value dates; otherwise over
    /// its interest dates.
    /// </remarks>
    public double CompoundedRate(OvernightIndexedCoupon coupon, Date? referenceDate = null, double y = 0.0, YieldTermStructure? yts = null) =>
        CompoundedRate(coupon, referenceDate, y, yts, new Gaussian1dMemo());

    /// <summary>The swap rate of a swap index at a fixing date and tenor, at a date and a value of the standardized
    /// state.</summary>
    /// <param name="fixing">The fixing date.</param>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="context">The evaluation context: a fixing on or before its date is read from its fixings, and the
    /// swap is built under it.</param>
    /// <param name="referenceDate">The date; <see langword="null"/>, QuantLib's empty date, for the model curve's
    /// reference date.</param>
    /// <param name="y">The standardized state at <paramref name="referenceDate"/>; 0 by default.</param>
    /// <param name="swapIdx">The index; required, where QuantLib's default is an empty pointer it refuses.</param>
    /// <returns>The swap rate.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="swapIdx"/> is
    /// null.</exception>
    public double SwapRate(Date fixing, Period tenor, EvaluationContext context, Date? referenceDate = null, double y = 0.0, SwapIndex? swapIdx = null) =>
        SwapRate(fixing, tenor, context, referenceDate, y, swapIdx, new Gaussian1dMemo());

    /// <summary>The annuity of a swap index's swap at a fixing date and tenor, at a date and a value of the
    /// standardized state.</summary>
    /// <param name="fixing">The fixing date.</param>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="context">The evaluation context the swap is built under.</param>
    /// <param name="referenceDate">The date; <see langword="null"/>, QuantLib's empty date, for the model curve's
    /// reference date.</param>
    /// <param name="y">The standardized state at <paramref name="referenceDate"/>; 0 by default.</param>
    /// <param name="swapIdx">The index; required, where QuantLib's default is an empty pointer it refuses.</param>
    /// <returns>The annuity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="swapIdx"/> is
    /// null.</exception>
    /// <remarks>
    /// QuantLib's reads neither the evaluation date nor the flag; its swap is built under the context, which nanoql's
    /// swap index needs for the underlying swap's coupons.
    /// </remarks>
    public double SwapAnnuity(Date fixing, Period tenor, EvaluationContext context, Date? referenceDate = null, double y = 0.0, SwapIndex? swapIdx = null) =>
        SwapAnnuity(fixing, tenor, context, referenceDate, y, swapIdx, new Gaussian1dMemo());

    /// <summary>
    /// A grid of the standardized state at a time <paramref name="T"/>, given its value at <paramref name="t"/>,
    /// covering <paramref name="stdDevs"/> standard deviations in 2·<paramref name="gridPoints"/> + 1 points.
    /// </summary>
    /// <param name="stdDevs">The grid's width, in standard deviations.</param>
    /// <param name="gridPoints">The points on each side.</param>
    /// <param name="T">The time of the grid; 1 by default.</param>
    /// <param name="t">The time of the given state; 0 by default.</param>
    /// <param name="y">The standardized state at <paramref name="t"/>; 0 by default.</param>
    /// <returns>A new vector holding the grid.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="gridPoints"/> is negative, where QuantLib's array
    /// of 2·n + 1 points can't be allocated.</exception>
    /// <exception cref="NanoQLException">The model has no state process.</exception>
    public Vector YGrid(double stdDevs, int gridPoints, double T = 1.0, double t = 0, double y = 0) =>
        YGrid(stdDevs, gridPoints, T, t, y, new Gaussian1dMemo());

    // The members over a call's memo, which the engines share across one call.
    internal double Numeraire(Date referenceDate, double y, YieldTermStructure? yts, Gaussian1dMemo memo) =>
        NumeraireCore(TermStructure.TimeFromReference(referenceDate), referenceDate, y, yts, memo);

    internal double Zerobond(Date maturity, Date? referenceDate, double y, YieldTermStructure? yts, Gaussian1dMemo memo) =>
        ZerobondCore(
            TermStructure.TimeFromReference(maturity),
            maturity,
            referenceDate is Date r ? TermStructure.TimeFromReference(r) : 0.0,
            referenceDate,
            y,
            yts,
            memo);

    internal double ZerobondOption(
        OptionType type,
        Date expiry,
        Date valueDate,
        Date maturity,
        double strike,
        Date? referenceDate,
        double y,
        YieldTermStructure? yts,
        double yStdDevs,
        int yGridPoints,
        bool extrapolatePayoff,
        bool flatPayoffExtrapolation,
        Gaussian1dMemo memo)
    {
        EnumArguments.Check(type, nameof(type));
        double fixingTime = TermStructure.TimeFromReference(expiry);
        double referenceTime = referenceDate is Date r ? TermStructure.TimeFromReference(r) : 0.0;

        Vector yg = YGrid(yStdDevs, yGridPoints, fixingTime, referenceTime, y, memo);
        Vector z = YGrid(yStdDevs, yGridPoints, 1.0, 0.0, 0.0, memo);

        double[] p = new double[yg.Size];

        for (int i = 0; i < yg.Size; i++)
        {
            double expValDsc = Zerobond(valueDate, expiry, yg[i], yts, memo);
            double discount = Zerobond(maturity, expiry, yg[i], yts, memo) / expValDsc;
            double payoff = (type == OptionType.Call ? 1.0 : -1.0) * (discount - strike);
            p[i] = (payoff < 0.0 ? 0.0 : payoff) / NumeraireCore(fixingTime, expiry, yg[i], yts, memo) * expValDsc;
        }

        var payoffSpline = new CubicInterpolation(
            z.AsSpan(), p, CubicInterpolationDerivativeApprox.Spline, true, CubicInterpolationBoundaryCondition.Lagrange, 0.0, CubicInterpolationBoundaryCondition.Lagrange, 0.0);

        double price = 0.0;
        for (int i = 0; i < z.Size - 1; i++)
        {
            price += GaussianShiftedPolynomialIntegral(
                0.0, payoffSpline.CCoefficients()[i], payoffSpline.BCoefficients()[i], payoffSpline.ACoefficients()[i], p[i], z[i], z[i], z[i + 1]);
        }

        if (extrapolatePayoff)
        {
            if (flatPayoffExtrapolation)
            {
                // the right tail at the last node's value, where QuantLib's takes the second-last's (G1R6 (xxi))
                price += GaussianShiftedPolynomialIntegral(0.0, 0.0, 0.0, 0.0, p[z.Size - 1], z[z.Size - 2], z[z.Size - 1], 100.0);
                price += GaussianShiftedPolynomialIntegral(0.0, 0.0, 0.0, 0.0, p[0], z[0], -100.0, z[0]);
            }
            else
            {
                if (type == OptionType.Call)
                {
                    price += GaussianShiftedPolynomialIntegral(
                        0.0,
                        payoffSpline.CCoefficients()[z.Size - 2],
                        payoffSpline.BCoefficients()[z.Size - 2],
                        payoffSpline.ACoefficients()[z.Size - 2],
                        p[z.Size - 2],
                        z[z.Size - 2],
                        z[z.Size - 1],
                        100.0);
                }

                if (type == OptionType.Put)
                {
                    price += GaussianShiftedPolynomialIntegral(
                        0.0, payoffSpline.CCoefficients()[0], payoffSpline.BCoefficients()[0], payoffSpline.ACoefficients()[0], p[0], z[0], -100.0, z[0]);
                }
            }
        }

        return NumeraireCore(referenceTime, referenceDate, y, yts, memo) * price;
    }

    internal double ForwardRate(Date fixing, EvaluationContext context, Date? referenceDate, double y, IborIndex? iborIdx, Gaussian1dMemo memo)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (iborIdx is null)
        {
            throw new ArgumentNullException(nameof(iborIdx), "no ibor index given");
        }

        if (fixing <= context.EvaluationDate + (context.EnforcesTodaysHistoricFixings ? 0 : -1))
        {
            return iborIdx.Fixing(fixing, context);
        }

        // might be empty, then use model curve
        YieldTermStructure? yts = iborIdx.ForwardingTermStructure;

        // the period's end at the index's maturity date, as QuantLib's master (#2848)
        Date valueDate = iborIdx.ValueDate(fixing);
        Date endDate = iborIdx.MaturityDate(valueDate);
        double dcf = iborIdx.DayCounter.YearFraction(valueDate, endDate);

        return (Zerobond(valueDate, referenceDate, y, yts, memo) - Zerobond(endDate, referenceDate, y, yts, memo))
            / (dcf * Zerobond(endDate, referenceDate, y, yts, memo));
    }

    internal double CompoundedRate(OvernightIndexedCoupon coupon, Date? referenceDate, double y, YieldTermStructure? yts, Gaussian1dMemo memo)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        bool observationShift = coupon.ApplyObservationShift && coupon.FixingDays > 0;
        var rateDates = observationShift ? coupon.ValueDates() : coupon.InterestDates();
        Date rateStart = rateDates[0];
        Date rateEnd = rateDates[^1];
        double rateAccrualTime = coupon.Index.DayCounter.YearFraction(rateStart, rateEnd);
        YieldTermStructure projectionCurve = yts ?? TermStructure;
        double compounding = referenceDate is Date r && rateStart < r
            ? projectionCurve.Discount(rateStart, true) / projectionCurve.Discount(r, true)
            : Zerobond(rateStart, referenceDate, y, yts, memo);
        return ((compounding / Zerobond(rateEnd, referenceDate, y, yts, memo)) - 1.0) / rateAccrualTime;
    }

    internal double SwapRate(Date fixing, Period tenor, EvaluationContext context, Date? referenceDate, double y, SwapIndex? swapIdx, Gaussian1dMemo memo)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (swapIdx is null)
        {
            throw new ArgumentNullException(nameof(swapIdx), "no swap index given");
        }

        if (fixing <= context.EvaluationDate + (context.EnforcesTodaysHistoricFixings ? 0 : -1))
        {
            return swapIdx.Fixing(fixing, context);
        }

        // either might be empty, then use model curve
        YieldTermStructure? ytsf = swapIdx.IborIndex.ForwardingTermStructure;
        YieldTermStructure? ytsd = swapIdx.DiscountingTermStructure;

        VanillaSwap underlying = memo.UnderlyingSwap(swapIdx, fixing, tenor, context);

        Schedule sched = underlying.FixedSchedule;
        Schedule floatSched = swapIdx is OvernightIndexedSwapIndex ? sched : underlying.FloatingSchedule;

        // should be fine for overnight indexed swap indices as well
        double annuity = SwapAnnuity(fixing, tenor, context, referenceDate, y, swapIdx, memo);
        double floatleg = 0.0;
        if (ytsf is null && ytsd is null)
        {
            // simple 100-formula can be used only in one curve setup
            floatleg = Zerobond(sched.Dates[0], referenceDate, y, null, memo)
                - Zerobond(CalendarOf(sched).Adjust(sched.Dates[^1], underlying.PaymentConvention), referenceDate, y, null, memo);
        }
        else
        {
            for (int i = 1; i < floatSched.Size; i++)
            {
                floatleg += ((Zerobond(floatSched[i - 1], referenceDate, y, ytsf, memo) / Zerobond(floatSched[i], referenceDate, y, ytsf, memo)) - 1.0)
                    * Zerobond(CalendarOf(floatSched).Adjust(floatSched[i], underlying.PaymentConvention), referenceDate, y, ytsd, memo);
            }
        }

        return floatleg / annuity;
    }

    internal double SwapAnnuity(Date fixing, Period tenor, EvaluationContext context, Date? referenceDate, double y, SwapIndex? swapIdx, Gaussian1dMemo memo)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (swapIdx is null)
        {
            throw new ArgumentNullException(nameof(swapIdx), "no swap index given");
        }

        // might be empty, then use model curve
        YieldTermStructure? ytsd = swapIdx.DiscountingTermStructure;

        VanillaSwap underlying = memo.UnderlyingSwap(swapIdx, fixing, tenor, context);

        Schedule sched = underlying.FixedSchedule;

        double annuity = 0.0;
        for (int j = 1; j < sched.Size; j++)
        {
            annuity += Zerobond(CalendarOf(sched).Adjust(sched.Date(j), underlying.PaymentConvention), referenceDate, y, ytsd, memo)
                * swapIdx.DayCounter.YearFraction(sched.Date(j - 1), sched.Date(j));
        }

        return annuity;
    }

    internal Vector YGrid(double stdDevs, int gridPoints, double T, double t, double y, Gaussian1dMemo memo)
    {
        // we use that the standard deviation is independent of $x$ here !
        StateProcess();
        ArgumentOutOfRangeException.ThrowIfNegative(gridPoints);

        var result = new Vector((2 * gridPoints) + 1, 0.0);

        double xT, e0t, etT, stdDev0t, stdDevtT;
        double stdDev0T = StateStdDeviation(0.0, 0.0, T, memo);
        double e0T = StateExpectation(0.0, 0.0, T, memo);

        if (t < NumericLimits.QL_EPSILON)
        {
            stdDevtT = stdDev0T;
            etT = e0T;
        }
        else
        {
            stdDev0t = StateStdDeviation(0.0, 0.0, t, memo);
            stdDevtT = StateStdDeviation(t, 0.0, T - t, memo);
            e0t = StateExpectation(0.0, 0.0, t, memo);
            xT = (y * stdDev0t) + e0t;
            etT = StateExpectation(t, xT, T - t, memo);
        }

        double h = stdDevs / gridPoints;

        for (int j = -gridPoints; j <= gridPoints; j++)
        {
            result[j + gridPoints] = (etT + (stdDevtT * j * h) - e0T) / stdDev0T;
        }

        return result;
    }

    // A curve's discount factor at an instant of the model's clock: QuantLib's read at the model's time where the curve
    // shares its clock, or where none is given the model's own; otherwise by the date the time comes from, nothing
    // accrued before the curve's reference date, and for a time without a date, the numeraire time, at the instant the
    // time denotes (the plan's G1R6 (xvi)). With extrapolation by default, as QuantLib reads its own curve, where it
    // reads a given one at the numeraire time without it (G1R6 (i)); without it where QuantLib reads both curves
    // without it (the Markov-functional model's ratios).
    internal double Discount(YieldTermStructure? yts, double time, Date? date, bool extrapolate = true)
    {
        if (yts is null || yts.SharesTimes(TermStructure))
        {
            return (yts ?? TermStructure).Discount(time, extrapolate);
        }

        if (date is Date d)
        {
            return d < yts.ReferenceDate ? 1.0 : yts.DiscountOn(d, extrapolate);
        }

        return yts.Discount(yts.AccrualTime(TermStructure, time), extrapolate);
    }

    /// <summary>The numeraire at a time, for a value of the standardized state: the hook a derived model
    /// implements.</summary>
    /// <param name="t">The time.</param>
    /// <param name="y">The standardized state.</param>
    /// <param name="yts">The curve to read instead of the model's, or <see langword="null"/>.</param>
    /// <returns>The numeraire.</returns>
    protected abstract double NumeraireImpl(double t, double y, YieldTermStructure? yts);

    /// <summary>The zero bond maturing at a time, at another time, for a value of the standardized state: the hook a
    /// derived model implements.</summary>
    /// <param name="T">The maturity.</param>
    /// <param name="t">The time.</param>
    /// <param name="y">The standardized state at <paramref name="t"/>.</param>
    /// <param name="yts">The curve to read instead of the model's, or <see langword="null"/>.</param>
    /// <returns>The zero bond.</returns>
    protected abstract double ZerobondImpl(double T, double t, double y, YieldTermStructure? yts);

    /// <summary>Sets the model's state process, while a derived constructor runs: QuantLib's
    /// <c>stateProcess_</c>.</summary>
    /// <param name="process">The process.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is null.</exception>
    protected void SetStateProcess(StochasticProcess1D process)
    {
        ArgumentNullException.ThrowIfNull(process);
        stateProcess = process;
    }

    // The hooks over a call's memo, given the dates the times come from where the caller has them; a derived model of
    // another assembly gets QuantLib's hooks and process.
    internal virtual double NumeraireCore(double t, Date? tDate, double y, YieldTermStructure? yts, Gaussian1dMemo memo) =>
        NumeraireImpl(t, y, yts);

    internal virtual double ZerobondCore(double T, Date? maturity, double t, Date? tDate, double y, YieldTermStructure? yts, Gaussian1dMemo memo) =>
        ZerobondImpl(T, t, y, yts);

    internal virtual double StateExpectation(double t0, double x0, double dt, Gaussian1dMemo memo) => StateProcess().Expectation(t0, x0, dt);

    internal virtual double StateStdDeviation(double t0, double x0, double dt, Gaussian1dMemo memo) => StateProcess().StdDeviation(t0, x0, dt);

    // A schedule's calendar, QuantLib's empty one where it has none.
    internal static Calendar CalendarOf(Schedule schedule) => schedule.Calendar ?? EmptyCalendar.Instance;
}
