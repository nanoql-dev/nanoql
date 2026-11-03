// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2003 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/barrier/analyticbarrierengine.hpp ql/pricingengines/barrier/analyticbarrierengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for European barrier options: Haug's closed forms ("Option pricing formulas", 1998, pp. 69-72)
/// on a Black-Scholes process.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's formulas, with four changes, each a documented divergence that leaves QuantLib's bits where it doesn't
/// apply. At a variance below machine epsilon the underlying follows its forward, as QuantLib's master prices it
/// (<c>e29c64126</c>): a knock-out pays its rebate at the hit or the vanilla payoff, a knock-in the vanilla payoff or
/// its rebate at expiry, a spot on the barrier a hit. A negative rebate is priced, where QuantLib's <c>E</c> and
/// <c>F</c> test <c>rebate &gt; 0</c> and drop it. Each rate is read on the volatility's clock, the curve's zero rate
/// times its own time over the volatility's, nothing accrued before a structure's reference date, so curves on other
/// day counters or dates price as QuantLib's engine does on the same market restated on the volatility's (a factor of
/// 1.0, and QuantLib's bits, where the clocks agree; 1.0 where the volatility's time is 0). A knock-out's rebate whose
/// closed form has no real λ (<c>λ² = μ² + 2r/σ² &lt; 0</c>, at negative rates), where QuantLib's is NaN, is the
/// rebate's present value at the hit, integrated over the barrier's first-passage density.
/// </para>
/// <para>
/// The formulas are first-passage closed forms in one equivalent-constant carry and volatility, the variance and
/// discounts to expiry: no exact closed form exists for a first passage under general deterministic term structures,
/// and on shaped markets they miss QuantLib's finite-difference barrier engine by -1.6% to +0.6%; the
/// term-structure-exact routes are <see cref="FdBlackScholesBarrierEngine"/> and the Monte Carlo engines.
/// </para>
/// <para>QuantLib's references: Haug's formulas, and its <c>testHaugValues</c> table.</para>
/// </remarks>
public static class AnalyticBarrierEngine
{
    /// <summary>Prices a barrier option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <returns>The option's results: the value; the expired results when the option has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The payoff isn't a plain vanilla one or its strike isn't positive, the exercise isn't European, the spot isn't
    /// positive, or it lies beyond the barrier ("barrier touched").
    /// </exception>
    public static OneAssetOptionResults Calculate(
        BarrierOption option, EvaluationContext context, GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(Body(option, process));
    }

    internal static OneAssetOptionResults Body(BarrierOption option, GeneralizedBlackScholesProcess process)
    {
        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-plain payoff given");
        }

        if (!(payoff.Strike > 0.0))
        {
            throw new NanoQLException("strike must be positive");
        }

        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("only european style option are supported");
        }

        double spot = process.X0;
        if (!(spot > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        if (option.Triggered(spot))
        {
            throw new NanoQLException("barrier touched");
        }

        return new OneAssetOptionResults { NPV = new Formulas(option, process, payoff, spot).Value() };
    }

    // A curve's zero rate to a date re-expressed on a volatility's clock: its own continuous rate to the date times its
    // own time over the volatility's, the rate at which its discount accrues per unit of the volatility's time; 0 where
    // the curve accrues nothing (a date on or before its reference date); the curve's own rate where the volatility's
    // time is 0, as there is no other clock to read it on. The barrier family's closed forms read their rates through
    // it.
    internal static double RateOnVolatilityClock(YieldTermStructure curve, BlackVolTermStructure volatility, Date date)
    {
        double curveTime = curve.AccrualTimeTo(date);
        double volTime = volatility.AccrualTimeTo(date);
        if (volTime == 0.0)
        {
            return date < curve.ReferenceDate
                ? 0.0
                : curve.ZeroRate(date, curve.DayCounter, Compounding.Continuous, Frequency.NoFrequency).Rate;
        }

        if (curveTime == 0.0)
        {
            return 0.0;
        }

        double rate = curve.ZeroRate(date, curve.DayCounter, Compounding.Continuous, Frequency.NoFrequency).Rate;
        return rate * (curveTime / volTime);
    }

    // The rebate's value alone, the formulas' rebate terms with the variance read at the given strike: a knock-in's paid
    // at expiry where the barrier is never hit (E), a knock-out's at the hit (F); with no variance, the forward's.
    // The binary barrier engine prices the rebate QuantLib's drops through it.
    internal static double RebateValue(BarrierOption option, GeneralizedBlackScholesProcess process, double strike) =>
        new Formulas(option, process, new PlainVanillaPayoff(OptionType.Call, strike), process.X0).RebateValue();

    // QuantLib's engine's members, each read once: every value is the expression QuantLib's member computes.
    private sealed class Formulas
    {
        private const int RebateIntegralEvaluations = 100000;
        private const double RebateIntegralAccuracy = 1e-15;

        private readonly CumulativeNormalDistribution f = new();
        private readonly BarrierType barrierType;
        private readonly OptionType optionType;
        private readonly PlainVanillaPayoff payoff;
        private readonly double underlying;
        private readonly double strike;
        private readonly double barrier;
        private readonly double rebate;
        private readonly double variance;
        private readonly double stdDeviation;
        private readonly double volatilityTime;
        private readonly double riskFreeRate;
        private readonly double riskFreeDiscount;
        private readonly double dividendYield;
        private readonly double dividendDiscount;
        private readonly BlackVolTermStructure volatilityStructure;
        private readonly Date lastDate;

        internal Formulas(
            BarrierOption option, GeneralizedBlackScholesProcess process, PlainVanillaPayoff payoff, double spot)
        {
            this.payoff = payoff;
            barrierType = option.BarrierType;
            optionType = payoff.OptionType;
            underlying = spot;
            strike = payoff.Strike;
            barrier = option.Barrier;
            rebate = option.Rebate;
            lastDate = option.Exercise.LastDate();
            volatilityStructure = process.BlackVolatility;

            // nothing accrues before a structure's reference date, where QuantLib's reads throw
            variance = volatilityStructure.AccruedVariance(lastDate, strike);
            stdDeviation = Math.Sqrt(variance);
            volatilityTime = volatilityStructure.AccrualTimeTo(lastDate);
            riskFreeRate = OnVolatilityClock(process.RiskFreeRate);
            riskFreeDiscount = process.RiskFreeRate.AccruedDiscount(lastDate);
            dividendYield = OnVolatilityClock(process.DividendYield);
            dividendDiscount = process.DividendYield.AccruedDiscount(lastDate);
        }

        internal double Value()
        {
            if (variance < NumericLimits.QL_EPSILON)
            {
                return ZeroVolatilityValue();
            }

            return optionType switch
            {
                OptionType.Call => barrierType switch
                {
                    BarrierType.DownIn => strike >= barrier ? C(1, 1) + E(1) : A(1) - B(1) + D(1, 1) + E(1),
                    BarrierType.UpIn => strike >= barrier ? A(1) + E(-1) : B(1) - C(-1, 1) + D(-1, 1) + E(-1),
                    BarrierType.DownOut => strike >= barrier ? A(1) - C(1, 1) + F(1) : B(1) - D(1, 1) + F(1),
                    _ => strike >= barrier ? F(-1) : A(1) - B(1) + C(-1, 1) - D(-1, 1) + F(-1),
                },
                OptionType.Put => barrierType switch
                {
                    BarrierType.DownIn => strike >= barrier ? B(-1) - C(1, -1) + D(1, -1) + E(1) : A(-1) + E(1),
                    BarrierType.UpIn => strike >= barrier ? A(-1) - B(-1) + D(-1, -1) + E(-1) : C(-1, -1) + E(-1),
                    BarrierType.DownOut => strike >= barrier ? A(-1) - B(-1) + C(1, -1) - D(1, -1) + F(1) : F(1),
                    _ => strike >= barrier ? B(-1) - D(-1, -1) + F(-1) : A(-1) - C(-1, -1) + F(-1),
                },
                _ => throw new NanoQLException("unknown type"),
            };
        }

        internal double RebateValue()
        {
            bool down = barrierType is BarrierType.DownIn or BarrierType.DownOut;
            bool knockIn = barrierType is BarrierType.DownIn or BarrierType.UpIn;
            if (variance < NumericLimits.QL_EPSILON)
            {
                double forward = underlying * dividendDiscount / riskFreeDiscount;
                bool hit = (underlying == barrier) || (down ? forward <= barrier : forward >= barrier);
                if (knockIn)
                {
                    return hit ? 0.0 : rebate * riskFreeDiscount;
                }

                if (!hit)
                {
                    return 0.0;
                }

                double logHS = Math.Log(barrier / underlying);
                double hitTime = (logHS == 0.0) ? 0.0 : logHS / (riskFreeRate - dividendYield);
                return rebate * Math.Exp(-riskFreeRate * hitTime);
            }

            double eta = down ? 1.0 : -1.0;
            return knockIn ? E(eta) : F(eta);
        }

        // A curve's zero rate re-expressed on the volatility's clock: its own continuous rate to the exercise times its
        // own time over the volatility's, the rate at which its discount accrues per unit of the volatility's time; 0
        // where the curve accrues nothing (an exercise on or before its reference date); the curve's own rate where
        // the volatility's time is 0, as there is no other clock to read it on.
        private double OnVolatilityClock(YieldTermStructure curve) =>
            RateOnVolatilityClock(curve, volatilityStructure, lastDate);

        // QuantLib's master (e29c64126): with no variance the underlying follows its forward, and hits the barrier, if
        // at all, when the forward reaches it; a spot on the barrier hits it now
        private double ZeroVolatilityValue()
        {
            double forward = underlying * dividendDiscount / riskFreeDiscount;
            bool down = barrierType is BarrierType.DownIn or BarrierType.DownOut;
            bool hit = (underlying == barrier) || (down ? forward <= barrier : forward >= barrier);
            double vanilla = riskFreeDiscount * payoff.Evaluate(forward);
            if (barrierType is BarrierType.DownIn or BarrierType.UpIn)
            {
                return hit ? vanilla : rebate * riskFreeDiscount;
            }

            if (hit)
            {
                // a knock-out rebate is paid when the barrier is hit
                double logHS = Math.Log(barrier / underlying);
                double hitTime = (logHS == 0.0) ? 0.0 : logHS / (riskFreeRate - dividendYield);
                return rebate * Math.Exp(-riskFreeRate * hitTime);
            }

            return vanilla;
        }

        private double Volatility() => volatilityStructure.BlackVol(lastDate, strike);

        private double Mu()
        {
            double vol = Volatility();
            return ((riskFreeRate - dividendYield) / (vol * vol)) - 0.5;
        }

        private double MuSigma() => (1 + Mu()) * stdDeviation;

        private double A(double phi)
        {
            double x1 = (Math.Log(underlying / strike) / stdDeviation) + MuSigma();
            double n1 = f.Evaluate(phi * x1);
            double n2 = f.Evaluate(phi * (x1 - stdDeviation));

            return phi * ((underlying * dividendDiscount * n1) - (strike * riskFreeDiscount * n2));
        }

        private double B(double phi)
        {
            double x2 = (Math.Log(underlying / barrier) / stdDeviation) + MuSigma();
            double n1 = f.Evaluate(phi * x2);
            double n2 = f.Evaluate(phi * (x2 - stdDeviation));
            return phi * ((underlying * dividendDiscount * n1) - (strike * riskFreeDiscount * n2));
        }

        private double C(double eta, double phi)
        {
            double hs = barrier / underlying;
            double powHS0 = Math.Pow(hs, 2 * Mu());
            double powHS1 = powHS0 * hs * hs;
            double y1 = (Math.Log(barrier * hs / strike) / stdDeviation) + MuSigma();
            double n1 = f.Evaluate(eta * y1);
            double n2 = f.Evaluate(eta * (y1 - stdDeviation));

            // when N1 or N2 are zero, the corresponding powHS might
            // be infinity, resulting in a NaN for their products.  The limit should be 0.
            return phi * ((underlying * dividendDiscount * (n1 == 0.0 ? 0.0 : powHS1 * n1))
                - (strike * riskFreeDiscount * (n2 == 0.0 ? 0.0 : powHS0 * n2)));
        }

        private double D(double eta, double phi)
        {
            double hs = barrier / underlying;
            double powHS0 = Math.Pow(hs, 2 * Mu());
            double powHS1 = powHS0 * hs * hs;
            double y2 = (Math.Log(barrier / underlying) / stdDeviation) + MuSigma();
            double n1 = f.Evaluate(eta * y2);
            double n2 = f.Evaluate(eta * (y2 - stdDeviation));

            // when N1 or N2 are zero, the corresponding powHS might
            // be infinity, resulting in a NaN for their products.  The limit should be 0.
            return phi * ((underlying * dividendDiscount * (n1 == 0.0 ? 0.0 : powHS1 * n1))
                - (strike * riskFreeDiscount * (n2 == 0.0 ? 0.0 : powHS0 * n2)));
        }

        // The rebate paid at expiry where the barrier was never hit; a negative rebate priced, where QuantLib's
        // rebate() > 0 drops it.
        private double E(double eta)
        {
            if (rebate != 0.0)
            {
                double powHS0 = Math.Pow(barrier / underlying, 2 * Mu());
                double x2 = (Math.Log(underlying / barrier) / stdDeviation) + MuSigma();
                double y2 = (Math.Log(barrier / underlying) / stdDeviation) + MuSigma();
                double n1 = f.Evaluate(eta * (x2 - stdDeviation));
                double n2 = f.Evaluate(eta * (y2 - stdDeviation));

                // when N2 is zero, powHS0 might be infinity, resulting in
                // a NaN for their product.  The limit should be 0.
                return rebate * riskFreeDiscount * (n1 - (n2 == 0.0 ? 0.0 : powHS0 * n2));
            }

            return 0.0;
        }

        // The rebate paid at the hit; a negative rebate priced, and the rebate integrated where λ has no real value.
        private double F(double eta)
        {
            if (rebate != 0.0)
            {
                double m = Mu();
                double vol = Volatility();
                double lambdaSquared = (m * m) + (2.0 * riskFreeRate / (vol * vol));
                if (lambdaSquared < 0.0)
                {
                    return RebateAtHit(vol);
                }

                double lambda = Math.Sqrt(lambdaSquared);
                double hs = barrier / underlying;
                double powHSplus = Math.Pow(hs, m + lambda);
                double powHSminus = Math.Pow(hs, m - lambda);

                double sigmaSqrtT = stdDeviation;
                double z = (Math.Log(barrier / underlying) / sigmaSqrtT) + (lambda * sigmaSqrtT);

                double n1 = f.Evaluate(eta * z);
                double n2 = f.Evaluate(eta * (z - (2.0 * lambda * sigmaSqrtT)));

                // when N1 or N2 are zero, the corresponding powHS might
                // be infinity, resulting in a NaN for their product.  The limit should be 0.
                return rebate * ((n1 == 0.0 ? 0.0 : powHSplus * n1) + (n2 == 0.0 ? 0.0 : powHSminus * n2));
            }

            return 0.0;
        }

        // The rebate's present value at the hit, R ∫₀ᵀ e^(−rt) f(t) dt, f the density of the time at which the
        // log-price, drifting at r − q − σ²/2 with volatility σ on the volatility's clock, first reaches the barrier's
        // level; with the spot on the barrier the rebate is due now. QuantLib's closed form is this integral where λ is
        // real.
        private double RebateAtHit(double vol)
        {
            double b = Math.Log(barrier / underlying);
            if (b == 0.0)
            {
                return rebate;
            }

            double r = riskFreeRate;
            double nu = r - dividendYield - (0.5 * vol * vol);
            double Integrand(double t)
            {
                if (!(t > 0.0))
                {
                    return 0.0;
                }

                double density = Math.Abs(b) / (vol * Math.Sqrt(2.0 * Math.PI * t * t * t))
                    * Math.Exp(-((b - (nu * t)) * (b - (nu * t))) / (2.0 * vol * vol * t));
                return Math.Exp(-r * t) * density;
            }

            var integral = new GaussLobattoIntegral(RebateIntegralEvaluations, RebateIntegralAccuracy);
            return rebate * integral.Integrate(Integrand, 0.0, volatilityTime);
        }
    }
}
