// SPDX-FileCopyrightText: Copyright (C) 2015 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/barrier/analyticdoublebarrierengine.hpp ql/pricingengines/barrier/analyticdoublebarrierengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for European double barrier options: Ikeda and Kunitomo's image series ("Pricing Options with
/// Curved Boundaries", Mathematical Finance 2/1992) as Haug gives it ("The complete guide to option pricing formulas",
/// 2nd ed., p. 156 and following), for flat barriers.
/// </summary>
/// <remarks>
/// <para>
/// The value alone, as QuantLib's. Strikes outside the corridor integrate only over terminal spots inside it,
/// upstream's fix after v1.44 (#2881; <c>Divergences/BarrierUpstreamFixesTests.cs</c>).
/// </para>
/// <para>
/// Documented divergences. Each rate is read on the volatility's clock and each time is the volatility's, as
/// <see cref="AnalyticBarrierEngine"/> reads them, where QuantLib reads every structure at the risk-free curve's times.
/// The series runs from −<c>series</c> to <c>series</c>, QuantLib's terms, and on while the next pair of terms exceeds
/// machine epsilon of the sum, where QuantLib stops, unconverged on narrow corridors and long terms
/// (<c>Divergences/DoubleBarrierSeriesTests.cs</c>). Where a term's power overflows, as at low volatility, it is taken
/// in logs with its normal difference, QuantLib's own technique in the binary barrier engine, where QuantLib's product
/// is NaN and its <c>max(0, ·)</c> prices 0; each difference of two normal probabilities whose arguments share a sign
/// is taken from the small tail, ½erfc, where the plain difference cancels
/// (<c>Divergences/DoubleBarrierLowVolatilityTests.cs</c>). QuantLib's value and bits stand wherever its five terms
/// agree with this converged series within 1e-12 on the value's scale, max(|value|, 1), which QuantLib's own rounding
/// of the plain sum's cancelling terms stays inside, QuantLib's cases among them. Below machine-epsilon variance the
/// underlying follows its forward, a knock-out paying its payoff where the forward stays inside the corridor. And the
/// rebate is priced, QuantLib's single-barrier convention, where QuantLib's engine reads none: a knock-out's paid at
/// the first touch of either barrier, through the double-barrier binary engine's KIKO and KOKI at unit cash; a
/// knock-in's paid at expiry where neither is touched, through its knock-out at unit cash
/// (<c>Divergences/DoubleBarrierRebateTests.cs</c>).
/// </para>
/// <para>
/// The formula is a first-passage closed form in equivalent-constant rates and a volatility, the variance and discounts
/// to expiry: no exact closed form exists for a first passage under general deterministic term structures, and on
/// shaped markets an independent Monte Carlo finds a knock-out within two of its standard errors, +0.04%; the
/// term-structure-exact route is a Monte Carlo on the process.
/// </para>
/// </remarks>
public static class AnalyticDoubleBarrierEngine
{
    // the relative agreement under which QuantLib's five terms stand
    private const double QuantLibAgreement = 1e-12;

    // the most pairs of terms the converged series adds past series: a safety net, the terms decaying as a Gaussian
    // in n
    private const int MaxExtraPairs = 10000;

    private static readonly CumulativeNormalDistribution F = new();

    /// <summary>Prices a double barrier option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="series">The number of terms either side of 0 the series takes at least; QuantLib's default 5.</param>
    /// <returns>The option's results: the value; the expired results when the option has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="series"/> is negative.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't plain vanilla or its strike isn't positive, the spot isn't
    /// positive or lies on or beyond a barrier ("barrier(s) already touched"), or the type is KIKO or KOKI.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        DoubleBarrierOption option, EvaluationContext context, GeneralizedBlackScholesProcess process, int series = 5)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentOutOfRangeException.ThrowIfNegative(series);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(Body(option, process, series));
    }

    internal static OneAssetOptionResults Body(DoubleBarrierOption option, GeneralizedBlackScholesProcess process, int series)
    {
        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("this engine handles only european options");
        }

        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-plain payoff given");
        }

        double strike = payoff.Strike;
        if (!(strike > 0.0))
        {
            throw new NanoQLException("strike must be positive");
        }

        double spot = process.X0;
        if (!(spot > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        if (option.Triggered(spot))
        {
            throw new NanoQLException("barrier(s) already touched");
        }

        DoubleBarrierType barrierType = option.BarrierType;
        if (barrierType is DoubleBarrierType.KIKO or DoubleBarrierType.KOKI)
        {
            throw new NanoQLException($"unsupported double-barrier type: {DoubleBarrierTypeFunctions.ToString(barrierType)}");
        }

        var f = new Formulas(option, payoff, process, series);
        double value = payoff.OptionType switch
        {
            OptionType.Call => barrierType == DoubleBarrierType.KnockIn ? f.CallKI() : f.CallKO(),
            OptionType.Put => barrierType == DoubleBarrierType.KnockIn ? f.PutKI() : f.PutKO(),
            _ => throw new NanoQLException("unknown type"),
        };

        if (option.Rebate != 0.0)
        {
            value += f.RebateValue();
        }

        return new OneAssetOptionResults { NPV = value };
    }

    // The knock-out's value without its rebate on this engine's routes (its converged series in logs, its deterministic
    // limit), each read on the volatility's clock: the Suo-Wang engine's value where its own series overflows
    internal static double KnockOutValue(
        DoubleBarrierOption option, PlainVanillaPayoff payoff, GeneralizedBlackScholesProcess process, int series)
    {
        var f = new Formulas(option, payoff, process, series);
        return payoff.OptionType == OptionType.Call ? f.CallKO() : f.PutKO();
    }

    // The rebate's value as this engine prices it: the Suo-Wang engine's rebate
    internal static double RebateValue(
        DoubleBarrierOption option, PlainVanillaPayoff payoff, GeneralizedBlackScholesProcess process, int series) =>
        new Formulas(option, payoff, process, series).RebateValue();

    // The image series' sum Σₙ [Pₙᵐ (N(a₁ + s) − N(a₂ + s)) − Qₙᵐ (N(a₃ + s) − N(a₄ + s))] over the corridor [L, U],
    // for terminal spots in [lo, hi]: Pₙ = (U/L)ⁿ, Qₙ = L^(n+1)/(Uⁿ S), a₁, a₃ at lo and a₂, a₄ at hi, the exponent m
    // μ₁ for the asset part and μ₁ − 2 for the cash part, the shift s 0 and −σ√T. QuantLib's callKO and putKO sum it as
    // acc1 and acc2. Here each term is taken in logs, its normal difference from the small tail where its arguments
    // share a sign, summed from −series to series and on while the next pair exceeds machine epsilon of the sum; the
    // double-barrier binary engine's image route reads it too.
    internal static double ImageSum(
        double logS,
        double logL,
        double logU,
        double lo,
        double hi,
        double sd,
        double bsigma,
        double m,
        double shift,
        int series)
    {
        double la = Math.Log(lo);
        double lb = Math.Log(hi);
        double sum = 0.0;
        for (int n = -series; n <= series; ++n)
        {
            sum += ImageTerm(logS, logL, logU, la, lb, sd, bsigma, m, shift, n);
        }

        for (int k = series + 1; k <= series + MaxExtraPairs; ++k)
        {
            double pair = ImageTerm(logS, logL, logU, la, lb, sd, bsigma, m, shift, -k)
                + ImageTerm(logS, logL, logU, la, lb, sd, bsigma, m, shift, k);
            if (!(Math.Abs(pair) > NumericLimits.QL_EPSILON * Math.Abs(sum)))
            {
                return sum + pair;
            }

            sum += pair;
        }

        throw new NanoQLException("double-barrier image series did not converge");
    }

    // One term of ImageSum, in logs: the power times the normal difference, each as a sign and a logarithm
    private static double ImageTerm(
        double logS,
        double logL,
        double logU,
        double la,
        double lb,
        double sd,
        double bsigma,
        double m,
        double shift,
        int n)
    {
        double a1 = ((logS + (2 * n * logU) - la - (2 * n * logL)) / sd) + bsigma + shift;
        double a2 = ((logS + (2 * n * logU) - lb - (2 * n * logL)) / sd) + bsigma + shift;
        double a3 = ((((2 * n) + 2) * logL - la - logS - (2 * n * logU)) / sd) + bsigma + shift;
        double a4 = ((((2 * n) + 2) * logL - lb - logS - (2 * n * logU)) / sd) + bsigma + shift;
        double lf1 = m * n * (logU - logL);
        double lf2 = m * (((n + 1) * logL) - (n * logU) - logS);
        return Product(lf1, a1, a2) - Product(lf2, a3, a4);
    }

    // e^lf (N(a) − N(b)), in logs
    private static double Product(double lf, double a, double b)
    {
        (int sign, double logAbs) = LogNormalDifference(a, b);
        return sign == 0 ? 0.0 : sign * Math.Exp(lf + logAbs);
    }

    // N(a) − N(b) as a sign and the logarithm of its magnitude: from the small tail where a and b share a sign, Q(b) −
    // Q(a) for positive ones and N(a) − N(b) = Q(−a) − Q(−b) for negative ones, Q(x) = ½erfc(x/√2), each in logs so
    // neither underflows; plainly where they don't, which doesn't cancel
    internal static (int Sign, double LogAbs) LogNormalDifference(double a, double b)
    {
        if (a == b || (double.IsNaN(a) || double.IsNaN(b)))
        {
            return (0, double.NegativeInfinity);
        }

        double x, y;
        int sign;
        if (a >= 0.0 && b >= 0.0)
        {
            // N(a) − N(b) = Q(b) − Q(a)
            (x, y, sign) = a > b ? (b, a, 1) : (a, b, -1);
        }
        else if (a <= 0.0 && b <= 0.0)
        {
            // N(a) − N(b) = Q(−a) − Q(−b)
            (x, y, sign) = -a < -b ? (-a, -b, 1) : (-b, -a, -1);
        }
        else
        {
            double plain = TailNormal(a) - TailNormal(b);
            return plain == 0.0 ? (0, double.NegativeInfinity) : (Math.Sign(plain), Math.Log(Math.Abs(plain)));
        }

        // Q(x) − Q(y), 0 <= x < y
        double lqx = LogQ(x);
        double lqy = LogQ(y);
        if (double.IsNegativeInfinity(lqx))
        {
            return (0, double.NegativeInfinity);
        }

        return (sign, lqx + StdLog1p(-Math.Exp(lqy - lqx)));
    }

    // N(x) as 1 − Q(x) or Q(−x), from erfc
    private static double TailNormal(double x) => x >= 0.0 ? 1.0 - Q(x) : Q(-x);

    private static double Q(double x) => 0.5 * ErfFunctions.Erfc(x / MathConstants.M_SQRT2);

    // log Q(x) for x >= 0: from erfc while it doesn't underflow, else the asymptotic expansion QuantLib's binary barrier
    // engine's logCumNormal takes
    private static double LogQ(double x)
    {
        if (x < 30.0)
        {
            return Math.Log(Q(x));
        }

        double z2 = 1.0 / (x * x);
        return (-0.5 * x * x) - Math.Log(x) - (0.5 * Math.Log(2.0 * Math.PI))
            + StdLog1p(-z2 * (1.0 - (z2 * (3.0 - (15.0 * z2)))));
    }

    // std::log1p, which .NET doesn't provide (double.LogP1 computes Math.Log(1 + x), which loses x's low digits): the
    // logarithm of u = 1 + x, rounded, scaled by x/(u − 1), Goldberg's Theorem 4
    private static double StdLog1p(double x)
    {
        double u = 1.0 + x;
        if (u == 1.0)
        {
            return x;
        }

        if (double.IsPositiveInfinity(u))
        {
            return Math.Log(u);
        }

        return Math.Log(u) * x / (u - 1.0);
    }

    // QuantLib's members, each read once on the volatility's clock
    private sealed class Formulas
    {
        private readonly DoubleBarrierOption option;
        private readonly PlainVanillaPayoff payoff;
        private readonly GeneralizedBlackScholesProcess process;
        private readonly int series;
        private readonly double underlying;
        private readonly double strike;
        private readonly double barrierLo;
        private readonly double barrierHi;
        private readonly double residualTime;
        private readonly double volatility;
        private readonly double riskFreeRate;
        private readonly double riskFreeDiscount;
        private readonly double dividendYield;
        private readonly double dividendDiscount;

        internal Formulas(
            DoubleBarrierOption option, PlainVanillaPayoff payoff, GeneralizedBlackScholesProcess process, int series)
        {
            this.option = option;
            this.payoff = payoff;
            this.process = process;
            this.series = series;
            underlying = process.X0;
            strike = payoff.Strike;
            barrierLo = option.BarrierLo;
            barrierHi = option.BarrierHi;
            Date lastDate = option.Exercise.LastDate();
            BlackVolTermStructure vol = process.BlackVolatility;
            residualTime = vol.TimeFromReference(lastDate);
            volatility = vol.BlackVol(lastDate, strike);
            riskFreeRate = AnalyticBarrierEngine.RateOnVolatilityClock(process.RiskFreeRate, vol, lastDate);
            riskFreeDiscount = process.RiskFreeRate.AccruedDiscount(lastDate);
            dividendYield = AnalyticBarrierEngine.RateOnVolatilityClock(process.DividendYield, vol, lastDate);
            dividendDiscount = process.DividendYield.AccruedDiscount(lastDate);
        }

        private double VolatilitySquared => volatility * volatility;

        private double StdDeviation => volatility * Math.Sqrt(residualTime);

        private double CostOfCarry => riskFreeRate - dividendYield;

        private bool DeterministicVariance => volatility * volatility * residualTime < NumericLimits.QL_EPSILON;

        internal double CallKO()
        {
            if (strike >= barrierHi)
            {
                return 0.0;
            }

            // Integrate only over terminal spots inside the barriers; keep the payoff strike (#2881).
            double bound = Max(strike, barrierLo);
            if (DeterministicVariance)
            {
                return DeterministicKnockOut();
            }

            double rend = Math.Exp(-dividendYield * residualTime);
            double quantLib = QuantLibCallKO(bound, rend);
            double mu1 = (2 * CostOfCarry / VolatilitySquared) + 1;
            double bsigma = (CostOfCarry + (VolatilitySquared / 2.0)) * residualTime / StdDeviation;
            double logS = Math.Log(underlying), logL = Math.Log(barrierLo), logU = Math.Log(barrierHi);
            double asset = ImageSum(logS, logL, logU, bound, barrierHi, StdDeviation, bsigma, mu1, 0.0, series);
            double cash = ImageSum(logS, logL, logU, bound, barrierHi, StdDeviation, bsigma, mu1 - 2, -StdDeviation, series);
            double converged = (underlying * rend * asset) - (strike * riskFreeDiscount * cash);
            return Max(0.0, Agreed(quantLib, converged));
        }

        internal double CallKI() =>

            // Haug p. 154 KI = vanilla - KO
            Max(0.0, VanillaEquivalent() - CallKO());

        internal double PutKO()
        {
            if (strike <= barrierLo)
            {
                return 0.0;
            }

            double bound = Min(strike, barrierHi);
            if (DeterministicVariance)
            {
                return DeterministicKnockOut();
            }

            double rend = Math.Exp(-dividendYield * residualTime);
            double quantLib = QuantLibPutKO(bound, rend);
            double mu1 = (2 * CostOfCarry / VolatilitySquared) + 1;
            double bsigma = (CostOfCarry + (VolatilitySquared / 2.0)) * residualTime / StdDeviation;
            double logS = Math.Log(underlying), logL = Math.Log(barrierLo), logU = Math.Log(barrierHi);
            double cash = ImageSum(logS, logL, logU, barrierLo, bound, StdDeviation, bsigma, mu1 - 2, -StdDeviation, series);
            double asset = ImageSum(logS, logL, logU, barrierLo, bound, StdDeviation, bsigma, mu1, 0.0, series);
            double converged = (strike * riskFreeDiscount * cash) - (underlying * rend * asset);
            return Max(0.0, Agreed(quantLib, converged));
        }

        internal double PutKI() =>

            // Haug p. 154 KI = vanilla - KO
            Max(0.0, VanillaEquivalent() - PutKO());

        // The rebate: a knock-out's paid at the first touch of either barrier, R × (KIKO + KOKI) at unit cash; a
        // knock-in's at expiry where neither is touched, R × the unit-cash knock-out at strike 0, through the
        // double-barrier binary engine's routes, the variance read at the option's strike
        internal double RebateValue()
        {
            Date lastDate = option.Exercise.LastDate();
            if (option.BarrierType == DoubleBarrierType.KnockOut)
            {
                return option.Rebate * (
                    AnalyticDoubleBarrierBinaryEngine.HitValue(
                        process, lastDate, barrierLo, barrierHi, strike, DoubleBarrierType.KIKO)
                    + AnalyticDoubleBarrierBinaryEngine.HitValue(
                        process, lastDate, barrierLo, barrierHi, strike, DoubleBarrierType.KOKI));
            }

            return option.Rebate * AnalyticDoubleBarrierBinaryEngine.NoTouchValue(
                process, lastDate, barrierLo, barrierHi, strike);
        }

        // QuantLib's value where its five terms (series) agree with the converged series within 1e-12 on the value's
        // scale, max(|value|, 1); the converged series elsewhere
        private static double Agreed(double quantLib, double converged) =>
            double.IsFinite(quantLib)
            && Math.Abs(quantLib - converged) <= QuantLibAgreement * Math.Max(Math.Abs(converged), 1.0)
                ? quantLib
                : converged;

        // QuantLib's callKO, with #2881's bound
        private double QuantLibCallKO(double bound, double rend)
        {
            // N.B. for flat barriers mu3=mu1 and mu2=0
            double mu1 = (2 * CostOfCarry / VolatilitySquared) + 1;
            double bsigma = (CostOfCarry + (VolatilitySquared / 2.0)) * residualTime / StdDeviation;

            double acc1 = 0;
            double acc2 = 0;
            for (int n = -series; n <= series; ++n)
            {
                double l2n = Math.Pow(barrierLo, 2 * n);
                double u2n = Math.Pow(barrierHi, 2 * n);
                double d1 = (Math.Log(underlying * u2n / (bound * l2n)) / StdDeviation) + bsigma;
                double d2 = (Math.Log(underlying * u2n / (barrierHi * l2n)) / StdDeviation) + bsigma;
                double d3 = (Math.Log(Math.Pow(barrierLo, (2 * n) + 2) / (bound * underlying * u2n)) / StdDeviation) + bsigma;
                double d4 = (Math.Log(Math.Pow(barrierLo, (2 * n) + 2) / (barrierHi * underlying * u2n)) / StdDeviation) + bsigma;

                acc1 += (Math.Pow(Math.Pow(barrierHi, n) / Math.Pow(barrierLo, n), mu1)
                        * (F.Evaluate(d1) - F.Evaluate(d2)))
                    - (Math.Pow(Math.Pow(barrierLo, n + 1) / (Math.Pow(barrierHi, n) * underlying), mu1)
                        * (F.Evaluate(d3) - F.Evaluate(d4)));

                acc2 += (Math.Pow(Math.Pow(barrierHi, n) / Math.Pow(barrierLo, n), mu1 - 2)
                        * (F.Evaluate(d1 - StdDeviation) - F.Evaluate(d2 - StdDeviation)))
                    - (Math.Pow(Math.Pow(barrierLo, n + 1) / (Math.Pow(barrierHi, n) * underlying), mu1 - 2)
                        * (F.Evaluate(d3 - StdDeviation) - F.Evaluate(d4 - StdDeviation)));
            }

            return (underlying * rend * acc1) - (strike * riskFreeDiscount * acc2);
        }

        // QuantLib's putKO, with #2881's bound
        private double QuantLibPutKO(double bound, double rend)
        {
            double mu1 = (2 * CostOfCarry / VolatilitySquared) + 1;
            double bsigma = (CostOfCarry + (VolatilitySquared / 2.0)) * residualTime / StdDeviation;

            double acc1 = 0;
            double acc2 = 0;
            for (int n = -series; n <= series; ++n)
            {
                double l2n = Math.Pow(barrierLo, 2 * n);
                double u2n = Math.Pow(barrierHi, 2 * n);
                double y1 = (Math.Log(underlying * u2n / Math.Pow(barrierLo, (2 * n) + 1)) / StdDeviation) + bsigma;
                double y2 = (Math.Log(underlying * u2n / (bound * l2n)) / StdDeviation) + bsigma;
                double y3 = (Math.Log(Math.Pow(barrierLo, (2 * n) + 2) / (barrierLo * underlying * u2n)) / StdDeviation) + bsigma;
                double y4 = (Math.Log(Math.Pow(barrierLo, (2 * n) + 2) / (bound * underlying * u2n)) / StdDeviation) + bsigma;

                acc1 += (Math.Pow(Math.Pow(barrierHi, n) / Math.Pow(barrierLo, n), mu1 - 2)
                        * (F.Evaluate(y1 - StdDeviation) - F.Evaluate(y2 - StdDeviation)))
                    - (Math.Pow(Math.Pow(barrierLo, n + 1) / (Math.Pow(barrierHi, n) * underlying), mu1 - 2)
                        * (F.Evaluate(y3 - StdDeviation) - F.Evaluate(y4 - StdDeviation)));

                acc2 += (Math.Pow(Math.Pow(barrierHi, n) / Math.Pow(barrierLo, n), mu1)
                        * (F.Evaluate(y1) - F.Evaluate(y2)))
                    - (Math.Pow(Math.Pow(barrierLo, n + 1) / (Math.Pow(barrierHi, n) * underlying), mu1)
                        * (F.Evaluate(y3) - F.Evaluate(y4)));
            }

            return (strike * riskFreeDiscount * acc1) - (underlying * rend * acc2);
        }

        // QuantLib's vanillaEquivalent: the European, floored at 0
        private double VanillaEquivalent()
        {
            // Call KI equates to vanilla - callKO
            double forwardPrice = underlying * dividendDiscount / riskFreeDiscount;
            var black = new BlackCalculator(payoff, forwardPrice, StdDeviation, riskFreeDiscount);
            double vanilla = black.Value;
            if (vanilla < 0.0)
            {
                vanilla = 0.0;
            }

            return vanilla;
        }

        // Below machine-epsilon variance the underlying follows its forward, which stays inside the corridor or leaves
        // it, the forward path monotone: a knock-out pays its payoff at the forward where it stays inside
        private double DeterministicKnockOut()
        {
            double forward = underlying * dividendDiscount / riskFreeDiscount;
            bool inside = forward > barrierLo && forward < barrierHi;
            return inside ? riskFreeDiscount * payoff.Evaluate(forward) : 0.0;
        }

        // std::min and std::max, which return their first argument where neither is less than the other
        private static double Min(double a, double b) => b < a ? b : a;

        private static double Max(double a, double b) => a < b ? b : a;
    }
}
