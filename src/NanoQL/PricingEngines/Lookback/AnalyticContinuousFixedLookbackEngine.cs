// SPDX-FileCopyrightText: Copyright (C) 2006 Warren Chou
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/lookback/analyticcontinuousfixedlookback.hpp ql/pricingengines/lookback/analyticcontinuousfixedlookback.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for continuous fixed-strike lookback options: Conze and Viswanathan's closed form, as in Haug,
/// "Option Pricing Formulas" (1998), pp. 63-64.
/// </summary>
/// <remarks>
/// <para>
/// The value alone, as QuantLib's. At zero carry, where the formula's ratio is 0/0, its limit, upstream's fix after
/// v1.44 (#2856, the S-bar plan's BR6 (v)).
/// </para>
/// <para>
/// Documented divergences (BR5). Each rate is read on the volatility's clock and each time is the volatility's, as
/// <see cref="AnalyticBarrierEngine"/> reads them, where QuantLib reads every structure at the risk-free curve's times
/// (BR5 (xix), <c>Divergences/LookbackMismatchedTimesTests.cs</c>). The extremum observed so far is read as the spot
/// where it lies beyond it, the spot being observed, where QuantLib prices the impossible extremum (BR5 (xxii),
/// <c>Divergences/LookbackExtremeBeyondSpotTests.cs</c>). Near zero carry the formula is evaluated in double-double,
/// where QuantLib's double cancels and #2856 switches to its limit below |λ| of 1e-7 (BR5 (xxiii),
/// <see cref="LookbackSmallCarry"/>, <c>Divergences/LookbackSmallCarryTests.cs</c>). At small volatilities, where λ =
/// 2b/σ² is large and QuantLib's power of s times its normal factor is +∞ times 0, NaN, the product is taken in logs
/// (BR5 (xxiv)'s class, the S-bar code review's B2-1, <see cref="LookbackTails"/>,
/// <c>Divergences/LookbackSmallVolatilityTests.cs</c>). And below machine-epsilon variance the underlying follows its
/// forward, where QuantLib divides by the volatility (BR5 (xxiv), <c>Divergences/LookbackZeroVolatilityTests.cs</c>).
/// </para>
/// <para>
/// The formula is a first-passage closed form in equivalent-constant rates and a volatility, the variance and discounts
/// to expiry: no exact closed form exists for a first passage under general deterministic term structures (the S-bar
/// plan's BR4 (d)); the term-structure-exact route is <see cref="MCLookbackEngine"/>.
/// </para>
/// </remarks>
public static class AnalyticContinuousFixedLookbackEngine
{
    private static readonly CumulativeNormalDistribution F = new();

    /// <summary>Prices a continuous fixed-strike lookback option.</summary>
    /// <param name="option">The option, not a partial-time one.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <returns>The option's results: the value; the expired results when the option has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The option is a <see cref="ContinuousPartialFixedLookbackOption"/> ("wrong argument type", QuantLib's refusal of
    /// its arguments), its extremum is negative, its payoff isn't plain vanilla, the spot isn't positive, or the strike
    /// is negative (a call) or not positive (a put).
    /// </exception>
    public static OneAssetOptionResults Calculate(
        ContinuousFixedLookbackOption option, EvaluationContext context, GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option is ContinuousPartialFixedLookbackOption)
        {
            throw new NanoQLException("wrong argument type");
        }

        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("Non-plain payoff given");
        }

        if (!(process.X0 > 0.0))
        {
            throw new NanoQLException("negative or null underlying");
        }

        double strike = payoff.Strike;
        double value;
        switch (payoff.OptionType)
        {
            case OptionType.Call:
            {
                if (!(strike >= 0.0))
                {
                    throw new NanoQLException("Strike must be positive or null");
                }

                var f = new Formulas(option, process, strike, 1.0);
                value = strike <= f.Minmax ? f.A() + f.C() : f.B();
                break;
            }

            case OptionType.Put:
            {
                if (!(strike > 0.0))
                {
                    throw new NanoQLException("Strike must be positive");
                }

                var f = new Formulas(option, process, strike, -1.0);
                value = strike >= f.Minmax ? f.A() + f.C() : f.B();
                break;
            }

            default:
                throw new NanoQLException("Unknown type");
        }

        return option.FetchResults(new OneAssetOptionResults { NPV = value });
    }

    // QuantLib's members, each read once on the volatility's clock (BR5 (xix))
    private sealed class Formulas
    {
        private readonly double eta;
        private readonly double underlying;
        private readonly double strike;
        private readonly double residualTime;
        private readonly double volatility;
        private readonly double riskFreeRate;
        private readonly double riskFreeDiscount;
        private readonly double dividendYield;
        private readonly double dividendDiscount;

        internal Formulas(ContinuousFixedLookbackOption option, GeneralizedBlackScholesProcess process, double strike, double eta)
        {
            this.eta = eta;
            this.strike = strike;
            underlying = process.X0;

            // a call's maximum and a put's minimum, the spot among the values observed (BR5 (xxii))
            Minmax = eta > 0.0 ? Math.Max(option.Minmax, underlying) : Math.Min(option.Minmax, underlying);
            Date lastDate = option.Exercise.LastDate();
            BlackVolTermStructure vol = process.BlackVolatility;
            residualTime = vol.TimeFromReference(lastDate);
            volatility = vol.BlackVol(lastDate, strike);
            riskFreeRate = AnalyticBarrierEngine.RateOnVolatilityClock(process.RiskFreeRate, vol, lastDate);
            riskFreeDiscount = process.RiskFreeRate.AccruedDiscount(lastDate);
            dividendYield = AnalyticBarrierEngine.RateOnVolatilityClock(process.DividendYield, vol, lastDate);
            dividendDiscount = process.DividendYield.AccruedDiscount(lastDate);
        }

        // the extremum read: QuantLib's minmax(), the spot where it lies beyond it
        internal double Minmax { get; }

        private double StdDeviation => volatility * Math.Sqrt(residualTime);

        private bool Deterministic => volatility * volatility * residualTime < NumericLimits.QL_EPSILON;

        // QuantLib's A(eta), on the extremum
        internal double A() => Deterministic ? DeterministicValue() - C() : Term(Minmax, Minmax);

        // QuantLib's B(eta), on the strike
        internal double B() => Deterministic ? DeterministicValue() : Term(strike, strike);

        // QuantLib's C(eta)
        internal double C() => eta * riskFreeDiscount * (Minmax - strike);

        // eta (S Dq N1 - level D N2 - S D ratio), with ss = S/level: QuantLib's in double above the small-carry cut, in
        // double-double below it, the cubic through double-double values below its floor, and #2856's limit at zero
        // carry (BR5 (xxiii))
        private double Term(double level, double ratioLevel)
        {
            double carry = riskFreeRate - dividendYield;
            double u = LookbackSmallCarry.U(carry, volatility, residualTime);
            if (!(u < LookbackSmallCarry.FullCut))
            {
                return Value(RealDouble.Of(carry), RealDouble.Of(dividendDiscount), level, ratioLevel).Value;
            }

            if (carry == 0.0)
            {
                return ZeroCarryValue(level, ratioLevel);
            }

            double DoubleDoubleValue(double b)
            {
                DoubleDouble bb = DoubleDouble.Of(b);
                return Value(bb, riskFreeDiscount * DoubleDouble.Exp(bb * residualTime), level, ratioLevel).Value;
            }

            if (!(u < LookbackSmallCarry.FullFloor))
            {
                return DoubleDoubleValue(carry);
            }

            return LookbackSmallCarry.Cubic(
                DoubleDoubleValue, carry, LookbackSmallCarry.CarryAt(LookbackSmallCarry.FullFloor, volatility, residualTime));
        }

        // QuantLib's expression at a carry and a dividend discount, in the arithmetic
        private T Value<T>(T carry, T dividendDiscount, double level, double ratioLevel)
            where T : struct, IRealArithmetic<T>
        {
            T vol = T.Of(volatility);
            T stdDeviation = vol * T.Sqrt(T.Of(residualTime));
            T lambda = 2.0 * carry / (vol * vol);
            T ss = T.Of(underlying) / ratioLevel;
            T d1 = (T.Log(ss) / stdDeviation) + (0.5 * (lambda + 1.0) * stdDeviation);
            T n1 = LookbackSmallCarry.N(eta * d1);
            T n2 = LookbackSmallCarry.N(eta * (d1 - stdDeviation));
            T z3 = eta * (d1 - (lambda * stdDeviation));
            T n3 = LookbackSmallCarry.N(z3);
            T n4 = LookbackSmallCarry.N(eta * d1);
            T powss = LookbackSmallCarry.Power(ss, -lambda);
            T ratio = (LookbackSmallCarry.PowerTimesN(powss * n3, ss, -lambda, z3) - (dividendDiscount * n4 / riskFreeDiscount)) / lambda;
            return eta * ((T.Of(underlying) * dividendDiscount * n1) - (T.Of(level) * riskFreeDiscount * n2)
                - (T.Of(underlying) * riskFreeDiscount * ratio));
        }

        // QuantLib's ratio at zero carry (#2856): lambda vanishes and the ratio is 0/0; its limit is
        // -stdDev * (d1 * N(eta * d1) + eta * n(d1))
        private double ZeroCarryValue(double level, double ratioLevel)
        {
            double ss = underlying / ratioLevel;
            double d1 = (Math.Log(ss) / StdDeviation) + (0.5 * StdDeviation);
            double n1 = F.Evaluate(eta * d1);
            double n2 = F.Evaluate(eta * (d1 - StdDeviation));
            double n4 = F.Evaluate(eta * d1);
            double ratio = -StdDeviation * ((d1 * n4) + (eta * F.Derivative(d1)));
            return eta * ((underlying * dividendDiscount * n1) - (level * riskFreeDiscount * n2)
                - (underlying * riskFreeDiscount * ratio));
        }

        // the forward path's payoff (BR5 (xxiv)): the path is monotone, so its extremum is at an end
        private double DeterministicValue()
        {
            double forward = underlying * dividendDiscount / riskFreeDiscount;
            double extremum = eta > 0.0 ? Math.Max(Minmax, forward) : Math.Min(Minmax, forward);
            return riskFreeDiscount * Math.Max(eta * (extremum - strike), 0.0);
        }
    }
}
