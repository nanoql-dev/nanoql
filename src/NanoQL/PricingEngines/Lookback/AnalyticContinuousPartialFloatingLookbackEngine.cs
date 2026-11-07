// SPDX-FileCopyrightText: Copyright (C) 2006 Warren Chou
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2014 Francois Botha
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/lookback/analyticcontinuouspartialfloatinglookback.hpp ql/pricingengines/lookback/analyticcontinuouspartialfloatinglookback.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for continuous partial-time floating-strike lookback options: Heynen and Kat's closed form, as
/// in Haug, "The Complete Guide to Option Pricing Formulas" (2007), pp. 146-147.
/// </summary>
/// <remarks>
/// <para>
/// The value alone, as QuantLib's, its bivariate normal QuantLib's We04DP.
/// </para>
/// <para>
/// Documented divergences. Each rate is read on the volatility's clock and each time is the volatility's, as
/// <see cref="AnalyticBarrierEngine"/> reads them, where QuantLib reads every structure at the risk-free curve's times
/// (<c>Divergences/LookbackMismatchedTimesTests.cs</c>). The extremum observed so far is read as the spot where it lies
/// beyond it, the spot being observed (<c>Divergences/LookbackExtremeBeyondSpotTests.cs</c>). Near zero carry, where
/// QuantLib's terms over 2b/σ² cancel and it is NaN at r = q, the value is the cubic in the carry through QuantLib's
/// values either side of a cut (<see cref="LookbackSmallCarry"/>, <c>Divergences/LookbackSmallCarryTests.cs</c>). At
/// small volatilities, where λ = 2b/σ² is large and QuantLib's power of s times its normal or bivariate normal factor
/// is +∞ times 0, NaN, the product is taken in logs (<see cref="LookbackTails"/>,
/// <c>Divergences/LookbackSmallVolatilityTests.cs</c>). A window closing from the evaluation date to the volatility
/// clock's origin, inclusive, is the European at λ times the extremum with the spot, where QuantLib's f₁ is 0/0 at the
/// money, and one closed before the evaluation date the European at λ times the extremum as given, the spot not in its
/// window, where QuantLib throws its bivariate normal's check of ρ
/// (<c>Divergences/LookbackPartialWindowTodayTests.cs</c>). And below machine-epsilon variance the underlying follows
/// its forward, where QuantLib divides by the volatility (<c>Divergences/LookbackZeroVolatilityTests.cs</c>).
/// </para>
/// <para>
/// The formula is a first-passage closed form in equivalent-constant rates and a volatility, the variance and discounts
/// to expiry: no exact closed form exists for a first passage under general deterministic term structures. It is exact
/// only where σ and r − q are flat, r and q themselves free: the engine reads the volatility and the rates at expiry
/// and splits the variance at the window's date in calendar time, so a time change doesn't carry it, and where the
/// volatility steps at the window's date under a constant (r − q)/σ² it misses by +8.1%
/// (<c>Conventions/ClosedFormExactnessTests.cs</c>). The term-structure-exact route is <see cref="MCLookbackEngine"/>,
/// which reads the extremum at its grid's dates.
/// </para>
/// </remarks>
public static class AnalyticContinuousPartialFloatingLookbackEngine
{
    private static readonly CumulativeNormalDistribution F = new();

    /// <summary>Prices a continuous partial-time floating-strike lookback option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <returns>The option's results: the value; the expired results when the option has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The option's terms fail its validation, its payoff isn't floating, or the spot isn't positive.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        ContinuousPartialFloatingLookbackOption option, EvaluationContext context, GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        if (option.Payoff is not FloatingTypePayoff payoff)
        {
            throw new NanoQLException("Non-floating payoff given");
        }

        if (!(process.X0 > 0.0))
        {
            throw new NanoQLException("negative or null underlying");
        }

        double eta = payoff.OptionType switch
        {
            OptionType.Call => 1.0,
            OptionType.Put => -1.0,
            _ => throw new NanoQLException("Unknown type"),
        };

        // a window closing before the evaluation date, or from it to the volatility clock's origin, inclusive: the
        // European at λ times the extremum, the spot among the values observed unless the window closed before the
        // evaluation date, where QuantLib's formulas take 0/0 or NaN times
        bool closed = option.LookbackPeriodEnd < context.EvaluationDate;
        if (closed || !(process.BlackVolatility.TimeFromReference(option.LookbackPeriodEnd) > 0.0))
        {
            double spot = process.X0;
            double extremum = closed
                ? option.Minmax
                : eta > 0.0 ? Math.Min(option.Minmax, spot) : Math.Max(option.Minmax, spot);
            var european = new VanillaOption(
                new PlainVanillaPayoff(payoff.OptionType, option.Lambda * extremum), new EuropeanExercise(option.Exercise.LastDate()));
            return option.FetchResults(new OneAssetOptionResults
            {
                NPV = AnalyticEuropeanEngine.Calculate(european, context, process).NPV,
            });
        }

        return option.FetchResults(new OneAssetOptionResults { NPV = new Formulas(option, process, eta).A() });
    }

    // QuantLib's members, each read once on the volatility's clock
    private sealed class Formulas
    {
        private readonly double eta;
        private readonly double underlying;
        private readonly double minmax;
        private readonly double lambda;
        private readonly double residualTime;
        private readonly double lookbackPeriodEndTime;
        private readonly double volatility;
        private readonly double riskFreeRate;
        private readonly double riskFreeDiscount;
        private readonly double dividendYield;
        private readonly double dividendDiscount;

        internal Formulas(ContinuousPartialFloatingLookbackOption option, GeneralizedBlackScholesProcess process, double eta)
        {
            this.eta = eta;
            underlying = process.X0;

            // a call's minimum and a put's maximum, the spot among the values observed
            minmax = eta > 0.0 ? Math.Min(option.Minmax, underlying) : Math.Max(option.Minmax, underlying);
            lambda = option.Lambda;
            Date lastDate = option.Exercise.LastDate();
            BlackVolTermStructure vol = process.BlackVolatility;
            residualTime = vol.TimeFromReference(lastDate);
            lookbackPeriodEndTime = vol.TimeFromReference(option.LookbackPeriodEnd);
            volatility = vol.BlackVol(lastDate, minmax);
            riskFreeRate = AnalyticBarrierEngine.RateOnVolatilityClock(process.RiskFreeRate, vol, lastDate);
            riskFreeDiscount = process.RiskFreeRate.AccruedDiscount(lastDate);
            dividendYield = AnalyticBarrierEngine.RateOnVolatilityClock(process.DividendYield, vol, lastDate);
            dividendDiscount = process.DividendYield.AccruedDiscount(lastDate);
        }

        private double StdDeviation => volatility * Math.Sqrt(residualTime);

        // QuantLib's A(eta), and below the small-carry cut the cubic in the carry through its values at the cut and
        // twice it, either side of 0, the dividend discount moved with the carry
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
            bool fullLookbackPeriod = lookbackPeriodEndTime == residualTime;
            double vol = volatility;
            double x = 2.0 * carry / (vol * vol);
            double s = underlying / minmax;

            double ls = Math.Log(s);
            double d1 = (ls / StdDeviation) + (0.5 * (x + 1.0) * StdDeviation);
            double d2 = d1 - StdDeviation;

            double e1 = 0, e2 = 0;
            if (!fullLookbackPeriod)
            {
                e1 = (carry + (vol * vol / 2)) * (residualTime - lookbackPeriodEndTime)
                    / (vol * Math.Sqrt(residualTime - lookbackPeriodEndTime));
                e2 = e1 - (vol * Math.Sqrt(residualTime - lookbackPeriodEndTime));
            }

            double f1 = (ls + ((carry + (vol * vol / 2)) * lookbackPeriodEndTime)) / (vol * Math.Sqrt(lookbackPeriodEndTime));
            double f2 = f1 - (vol * Math.Sqrt(lookbackPeriodEndTime));

            double l1 = Math.Log(lambda) / vol;
            double g1 = l1 / Math.Sqrt(residualTime);

            double n1 = F.Evaluate(eta * (d1 - g1));
            double n2 = F.Evaluate(eta * (d2 - g1));

            var cnbn1 = new BivariateCumulativeNormalDistributionWe04DP(1);
            var cnbn2 = new BivariateCumulativeNormalDistributionWe04DP(0);
            var cnbn3 = new BivariateCumulativeNormalDistributionWe04DP(-1);
            if (!fullLookbackPeriod)
            {
                cnbn1 = new BivariateCumulativeNormalDistributionWe04DP(Math.Sqrt(lookbackPeriodEndTime / residualTime));
                cnbn2 = new BivariateCumulativeNormalDistributionWe04DP(-Math.Sqrt(1 - (lookbackPeriodEndTime / residualTime)));
                cnbn3 = new BivariateCumulativeNormalDistributionWe04DP(-Math.Sqrt(lookbackPeriodEndTime / residualTime));
            }

            double h3 = eta * (-f1 + (2.0 * carry * Math.Sqrt(lookbackPeriodEndTime) / vol));
            double k3 = eta * (-d1 + (x * StdDeviation) - g1);
            double n3 = cnbn1.Evaluate(h3, k3);
            double n4, n5 = 0, n6 = 0, n7 = 0;
            double h4 = -eta * (d1 + g1), k4 = 0.0;
            if (!fullLookbackPeriod)
            {
                double g2 = l1 / Math.Sqrt(residualTime - lookbackPeriodEndTime);
                k4 = eta * (e1 + g2);
                n4 = cnbn2.Evaluate(h4, k4);
                n5 = cnbn2.Evaluate(-eta * (d1 - g1), eta * (e1 - g2));
                n6 = cnbn3.Evaluate(eta * -f2, eta * (d2 - g1));
                n7 = F.Evaluate(eta * (e2 - g2));
            }
            else
            {
                n4 = F.Evaluate(-eta * (d1 + g1));
            }

            double n8 = F.Evaluate(-eta * f1);
            double powS = Math.Pow(s, -x);
            double powL = Math.Pow(lambda, x);

            // QuantLib's two power products, each in logs where it isn't finite, as at small volatilities, where
            // QuantLib's are NaN
            double rho3 = fullLookbackPeriod ? 1.0 : Math.Sqrt(lookbackPeriodEndTime / residualTime);
            double product3 = LookbackTails.PowerTimesM(powS * n3, 1.0, s, -x, h3, k3, rho3);
            double product4 = fullLookbackPeriod
                ? LookbackTails.PowerTimesM(dq / riskFreeDiscount * powL * n4, dq / riskFreeDiscount, lambda, x, h4, h4, 1.0)
                : LookbackTails.PowerTimesM(
                    dq / riskFreeDiscount * powL * n4, dq / riskFreeDiscount, lambda, x, h4, k4, -Math.Sqrt(1 - (lookbackPeriodEndTime / residualTime)));

            if (!fullLookbackPeriod)
            {
                return eta * ((underlying * dq * n1)
                    - (lambda * minmax * riskFreeDiscount * n2)
                    + (underlying * riskFreeDiscount * lambda / x
                        * (product3 - product4))
                    + (underlying * dq * n5)
                    + (riskFreeDiscount * lambda * minmax * n6)
                    - (Math.Exp(-carry * (residualTime - lookbackPeriodEndTime))
                        * dq * (1 + (0.5 * vol * vol / carry)) * lambda
                        * underlying * n7 * n8));
            }

            // Simpler calculation
            return eta * ((underlying * dq * n1)
                - (lambda * minmax * riskFreeDiscount * n2)
                + (underlying * riskFreeDiscount * lambda / x
                    * (product3 - product4)));
        }

        // the forward path's payoff: the path is monotone, so its extremum over the window [0, t₁] is at an end
        private double DeterministicValue()
        {
            double forward = underlying * dividendDiscount / riskFreeDiscount;
            double carry = forward > 0.0 ? Math.Log(forward / underlying) / residualTime : 0.0;
            double atWindowEnd = underlying * Math.Exp(carry * lookbackPeriodEndTime);
            double extremum = eta > 0.0 ? Math.Min(minmax, atWindowEnd) : Math.Max(minmax, atWindowEnd);
            return riskFreeDiscount * Math.Max(eta * (forward - (lambda * extremum)), 0.0);
        }
    }
}
