// SPDX-FileCopyrightText: Copyright (C) 2006 Warren Chou
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2014 Francois Botha
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/lookback/analyticcontinuouspartialfixedlookback.hpp ql/pricingengines/lookback/analyticcontinuouspartialfixedlookback.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for continuous partial-time fixed-strike lookback options: Heynen and Kat's closed form, as in
/// Haug, "The Complete Guide to Option Pricing Formulas" (2007), pp. 148-149.
/// </summary>
/// <remarks>
/// <para>
/// The value alone, as QuantLib's, its bivariate normal QuantLib's We04DP.
/// </para>
/// <para>
/// Documented divergences (BR5). Each rate is read on the volatility's clock and each time is the volatility's, as
/// <see cref="AnalyticBarrierEngine"/> reads them, where QuantLib reads every structure at the risk-free curve's times
/// (BR5 (xix), <c>Divergences/LookbackMismatchedTimesTests.cs</c>). Near zero carry, where QuantLib's terms over 2b/σ²
/// cancel and it is NaN at r = q, the value is the cubic in the carry through QuantLib's values either side of a cut
/// (BR5 (xxiii), <see cref="LookbackSmallCarry"/>, <c>Divergences/LookbackSmallCarryTests.cs</c>). At small
/// volatilities, where λ = 2b/σ² is large and QuantLib's power of s times its normal or bivariate normal factor is +∞
/// times 0, NaN, the product is taken in logs (BR5 (xxiv)'s class, the S-bar code review's B2-1,
/// <see cref="LookbackTails"/>, <c>Divergences/LookbackSmallVolatilityTests.cs</c>). A window opening from the
/// evaluation date to the volatility clock's origin, inclusive, is the full fixed lookback, the spot its maximum,
/// nothing accruing before the origin, where QuantLib's f₁ is 0/0 at the money, and one opened before the evaluation
/// date is refused, the option holding no maximum observed since its start, where QuantLib throws its bivariate
/// normal's check of ρ (the review's B2-2 and B2-3, <c>Divergences/LookbackPartialWindowTodayTests.cs</c>). And below
/// machine-epsilon variance the underlying follows its forward, where QuantLib divides by the volatility (BR5 (xxiv),
/// <c>Divergences/LookbackZeroVolatilityTests.cs</c>).
/// </para>
/// <para>
/// The formula is a first-passage closed form in equivalent-constant rates and a volatility, the variance and discounts
/// to expiry: no exact closed form exists for a first passage under general deterministic term structures (the S-bar
/// plan's BR4 (d)); the term-structure-exact route is <see cref="MCLookbackEngine"/>.
/// </para>
/// </remarks>
public static class AnalyticContinuousPartialFixedLookbackEngine
{
    private static readonly CumulativeNormalDistribution F = new();

    /// <summary>Prices a continuous partial-time fixed-strike lookback option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <returns>The option's results: the value; the expired results when the option has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The option's terms fail its validation, its payoff isn't plain vanilla, the spot isn't positive, the strike is
    /// negative (a call) or not positive (a put), or the lookback period started before the evaluation date.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        ContinuousPartialFixedLookbackOption option, EvaluationContext context, GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
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

        double eta;
        switch (payoff.OptionType)
        {
            case OptionType.Call:
                if (!(payoff.Strike >= 0.0))
                {
                    throw new NanoQLException("Strike must be positive or null");
                }

                eta = 1.0;
                break;
            case OptionType.Put:
                if (!(payoff.Strike > 0.0))
                {
                    throw new NanoQLException("Strike must be positive");
                }

                eta = -1.0;
                break;
            default:
                throw new NanoQLException("Unknown type");
        }

        // a window opening before the evaluation date is refused, the option holding no extremum observed since it
        // opened; one opening from the evaluation date to the volatility clock's origin, inclusive, is the full fixed
        // lookback with the spot as its extremum, nothing accruing before the origin (the S-bar code review's B2-2 and
        // B2-3), where QuantLib's formulas take 0/0 or NaN times
        if (option.LookbackPeriodStart < context.EvaluationDate)
        {
            throw new NanoQLException(
                "lookback period started before the evaluation date: no extremum observed since its start is given");
        }

        if (!(process.BlackVolatility.TimeFromReference(option.LookbackPeriodStart) > 0.0))
        {
            var full = new ContinuousFixedLookbackOption(process.X0, payoff, option.Exercise);
            return option.FetchResults(new OneAssetOptionResults
            {
                NPV = AnalyticContinuousFixedLookbackEngine.Calculate(full, context, process).NPV,
            });
        }

        return option.FetchResults(new OneAssetOptionResults { NPV = new Formulas(option, process, payoff.Strike, eta).A() });
    }

    // QuantLib's members, each read once on the volatility's clock (BR5 (xix))
    private sealed class Formulas
    {
        private readonly double eta;
        private readonly double underlying;
        private readonly double strike;
        private readonly double residualTime;
        private readonly double lookbackPeriodStartTime;
        private readonly double volatility;
        private readonly double riskFreeRate;
        private readonly double riskFreeDiscount;
        private readonly double dividendYield;
        private readonly double dividendDiscount;

        internal Formulas(ContinuousPartialFixedLookbackOption option, GeneralizedBlackScholesProcess process, double strike, double eta)
        {
            this.eta = eta;
            this.strike = strike;
            underlying = process.X0;
            Date lastDate = option.Exercise.LastDate();
            BlackVolTermStructure vol = process.BlackVolatility;
            residualTime = vol.TimeFromReference(lastDate);
            lookbackPeriodStartTime = vol.TimeFromReference(option.LookbackPeriodStart);
            volatility = vol.BlackVol(lastDate, strike);
            riskFreeRate = AnalyticBarrierEngine.RateOnVolatilityClock(process.RiskFreeRate, vol, lastDate);
            riskFreeDiscount = process.RiskFreeRate.AccruedDiscount(lastDate);
            dividendYield = AnalyticBarrierEngine.RateOnVolatilityClock(process.DividendYield, vol, lastDate);
            dividendDiscount = process.DividendYield.AccruedDiscount(lastDate);
        }

        private double StdDeviation => volatility * Math.Sqrt(residualTime);

        // QuantLib's A(eta), and below the small-carry cut the cubic in the carry through its values at the cut and
        // twice it, either side of 0, the dividend discount moved with the carry (BR5 (xxiii))
        internal double A()
        {
            if (volatility * volatility * residualTime < NumericLimits.QL_EPSILON)
            {
                return DeterministicValue();
            }

            double carry = riskFreeRate - dividendYield;
            double cut = LookbackSmallCarry.PartialCut(StdDeviation);
            if (!(LookbackSmallCarry.U(carry, volatility, residualTime) < cut))
            {
                return Value(carry, dividendDiscount);
            }

            return LookbackSmallCarry.Cubic(
                b => Value(b, dividendDiscount * Math.Exp((b - carry) * residualTime)),
                carry,
                LookbackSmallCarry.CarryAt(cut, volatility, residualTime));
        }

        // QuantLib's A(eta) at a carry and a dividend discount
        private double Value(double carry, double dq)
        {
            bool differentStartOfLookback = lookbackPeriodStartTime != residualTime;
            double vol = volatility;
            double x = 2.0 * carry / (vol * vol);
            double s = underlying / strike;
            double ls = Math.Log(s);
            double d1 = (ls / StdDeviation) + (0.5 * (x + 1.0) * StdDeviation);
            double d2 = d1 - StdDeviation;

            double e1 = 0, e2 = 0;
            if (differentStartOfLookback)
            {
                e1 = (carry + (vol * vol / 2)) * (residualTime - lookbackPeriodStartTime)
                    / (vol * Math.Sqrt(residualTime - lookbackPeriodStartTime));
                e2 = e1 - (vol * Math.Sqrt(residualTime - lookbackPeriodStartTime));
            }

            double f1 = (ls + ((carry + (vol * vol / 2)) * lookbackPeriodStartTime)) / (vol * Math.Sqrt(lookbackPeriodStartTime));
            double f2 = f1 - (vol * Math.Sqrt(lookbackPeriodStartTime));

            double n1 = F.Evaluate(eta * d1);
            double n2 = F.Evaluate(eta * d2);

            var cnbn1 = new BivariateCumulativeNormalDistributionWe04DP(-1);
            var cnbn2 = new BivariateCumulativeNormalDistributionWe04DP(0);
            var cnbn3 = new BivariateCumulativeNormalDistributionWe04DP(0);
            if (differentStartOfLookback)
            {
                cnbn1 = new BivariateCumulativeNormalDistributionWe04DP(-Math.Sqrt(lookbackPeriodStartTime / residualTime));
                cnbn2 = new BivariateCumulativeNormalDistributionWe04DP(Math.Sqrt(1 - (lookbackPeriodStartTime / residualTime)));
                cnbn3 = new BivariateCumulativeNormalDistributionWe04DP(-Math.Sqrt(1 - (lookbackPeriodStartTime / residualTime)));
            }

            double h3 = eta * (d1 - (x * StdDeviation));
            double k3 = eta * (-f1 + (2.0 * carry * Math.Sqrt(lookbackPeriodStartTime) / vol));
            double n3 = cnbn1.Evaluate(h3, k3);
            double n4 = cnbn2.Evaluate(eta * e1, eta * d1);
            double n5 = cnbn3.Evaluate(-eta * e1, eta * d1);
            double n6 = cnbn1.Evaluate(eta * f2, -eta * d2);
            double n7 = F.Evaluate(eta * f1);
            double n8 = F.Evaluate(-eta * e2);

            double powS = Math.Pow(s, -x);

            // QuantLib's power product, in logs where it isn't finite, as at small volatilities, where QuantLib's is
            // NaN (the S-bar code review's B2-1)
            double rho3 = differentStartOfLookback ? -Math.Sqrt(lookbackPeriodStartTime / residualTime) : -1.0;
            double product3 = LookbackTails.PowerTimesM(powS * n3, 1.0, s, -x, h3, k3, rho3);
            double carryDiscount = Math.Exp(-carry * (residualTime - lookbackPeriodStartTime));
            return eta * ((underlying * dq * n1)
                - (strike * riskFreeDiscount * n2)
                + (underlying * riskFreeDiscount / x * (-product3 + (dq / riskFreeDiscount * n4)))
                - (underlying * dq * n5)
                - (strike * riskFreeDiscount * n6)
                + (carryDiscount * dq * (1 - (0.5 * vol * vol / carry)) * underlying * n7 * n8));
        }

        // the forward path's payoff (BR5 (xxiv)): the path is monotone, so its extremum over the window [t₁, T] is at
        // an end
        private double DeterministicValue()
        {
            double forward = underlying * dividendDiscount / riskFreeDiscount;
            double carry = forward > 0.0 ? Math.Log(forward / underlying) / residualTime : 0.0;
            double atWindowStart = underlying * Math.Exp(carry * lookbackPeriodStartTime);
            double extremum = eta > 0.0 ? Math.Max(atWindowStart, forward) : Math.Min(atWindowStart, forward);
            return riskFreeDiscount * Math.Max(eta * (extremum - strike), 0.0);
        }
    }
}
