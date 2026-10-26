// SPDX-FileCopyrightText: Copyright (C) 2014 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/exotic/analytictwoassetcorrelationengine.hpp ql/pricingengines/exotic/analytictwoassetcorrelationengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for a European two-asset correlation option: M. Zhang, "Exotic Options", World Scientific
/// (1998); E. G. Haug, "The Complete Guide to Option Pricing Formulas", 2nd edition (2007).
/// </summary>
/// <remarks>
/// <para>
/// The value alone, as QuantLib's. The bivariate normal is QuantLib's default,
/// <see cref="BivariateCumulativeNormalDistributionWe04DP"/>, where QuantLib's engine reads Drezner's 1978 algorithm,
/// to six decimal places (the S-exo plan's ER7, <c>Divergences/TwoAssetCorrelationBivariateTests.cs</c>). The second
/// asset's volatility is read at its own strike X₂, where QuantLib reads it at X₁
/// (<c>Divergences/TwoAssetCorrelationSmileTests.cs</c>). At X₂ &lt; 0 the call is linear in the second asset where the
/// first is in the money and the put worth nothing, where QuantLib takes the log of a negative ratio
/// (<c>Divergences/TwoAssetCorrelationNegativeStrikeTests.cs</c>).
/// </para>
/// <para>
/// Each asset's carry reads its own process's curves and the payoff is discounted on the first process's risk-free
/// curve, where QuantLib takes the rate from the first process alone
/// (<c>Divergences/TwoAssetTwoRiskFreeCurvesTests.cs</c>).
/// Every rate and volatility is read over the time to expiry on the clock across both processes
/// (<see cref="FdmBlackScholesOp.ClockAcross"/>), each structure at the instants that time denotes, accruing nothing
/// before its date, where QuantLib reads each volatility at its own process's time and everything else at the second
/// process's; a structure sharing the clock's times is read as QuantLib reads it
/// (<c>Divergences/TwoAssetMismatchedTimesTests.cs</c>).
/// </para>
/// </remarks>
public static class AnalyticTwoAssetCorrelationEngine
{
    private static readonly CumulativeNormalDistribution N = new();

    /// <summary>Prices a European two-asset correlation option: QuantLib's <c>calculate</c>.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="p1">The first asset's process.</param>
    /// <param name="p2">The second asset's process.</param>
    /// <param name="correlation">The correlation of the two assets, in [−1, 1].</param>
    /// <returns>The option's results: the value.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The correlation is outside [−1, 1].</exception>
    /// <exception cref="NanoQLException">The first strike or the first spot isn't positive.</exception>
    public static MultiAssetOptionResults Calculate(
        TwoAssetCorrelationOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess p1,
        GeneralizedBlackScholesProcess p2,
        double correlation)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(p1);
        ArgumentNullException.ThrowIfNull(p2);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(new MultiAssetOptionResults { NPV = Value(option, p1, p2, correlation) });
    }

    // QuantLib's calculate().
    private static double Value(
        TwoAssetCorrelationOption option,
        GeneralizedBlackScholesProcess p1,
        GeneralizedBlackScholesProcess p2,
        double rho)
    {
        var m = new BivariateCumulativeNormalDistributionWe04DP(rho);
        var payoff = (PlainVanillaPayoff)option.Payoff!;
        if (!(payoff.Strike > 0.0))
        {
            throw new NanoQLException("strike must be positive");
        }

        double strike = payoff.Strike; // X1
        double spot = p1.X0;
        if (!(spot > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        double x2 = option.X2;

        // the time to expiry on the clock across both processes, every structure read at it
        TermStructure clock = FdmBlackScholesOp.ClockAcross([p1, p2]);
        double bigT = clock.TimeFromReference(option.Exercise.LastDate());

        // each volatility at its own strike
        double sigma1 = Volatility(p1.BlackVolatility, clock, bigT, strike);
        double sigma2 = Volatility(p2.BlackVolatility, clock, bigT, x2);

        double s1 = p1.X0;
        double s2 = p2.X0;
        double q1 = ZeroRate(p1.DividendYield, clock, bigT);
        double q2 = ZeroRate(p2.DividendYield, clock, bigT);
        double r = ZeroRate(p1.RiskFreeRate, clock, bigT);

        // each asset's carry on its own process's curves, the discount on the first's
        double b1 = r - q1;
        double b2 = ZeroRate(p2.RiskFreeRate, clock, bigT) - q2;
        double y1 = (Math.Log(s1 / strike) + ((b1 - (sigma1 * sigma1 / 2)) * bigT)) / (sigma1 * Math.Sqrt(bigT));

        // at a negative second strike the second leg's payoff is linear: the call pays S2 - X2 > 0 where the first
        // asset is in the money, and the put nothing
        if (x2 < 0.0)
        {
            return payoff.OptionType switch
            {
                OptionType.Call => (s2 * Math.Exp((b2 - r) * bigT) * N.Evaluate(y1 + (rho * sigma2 * Math.Sqrt(bigT))))
                    - (x2 * Math.Exp(-r * bigT) * N.Evaluate(y1)),
                OptionType.Put => 0.0,
                _ => throw new NanoQLException("unknown option type"),
            };
        }

        double y2 = (Math.Log(s2 / x2) + ((b2 - (sigma2 * sigma2 / 2)) * bigT)) / (sigma2 * Math.Sqrt(bigT));

        return payoff.OptionType switch
        {
            OptionType.Call => (s2 * Math.Exp((b2 - r) * bigT)
                    * m.Evaluate(y2 + (sigma2 * Math.Sqrt(bigT)), y1 + (rho * sigma2 * Math.Sqrt(bigT))))
                - (x2 * Math.Exp(-r * bigT) * m.Evaluate(y2, y1)),
            OptionType.Put => (x2 * Math.Exp(-r * bigT) * m.Evaluate(-y2, -y1))
                - (s2 * Math.Exp((b2 - r) * bigT)
                    * m.Evaluate(-y2 - (sigma2 * Math.Sqrt(bigT)), -y1 - (rho * sigma2 * Math.Sqrt(bigT)))),
            _ => throw new NanoQLException("unknown option type"),
        };
    }

    // QuantLib's blackVol(T, strike) where the volatility shares the clock's times; otherwise the volatility of the
    // variance it has accrued by the clock's time T
    private static double Volatility(BlackVolTermStructure volatility, TermStructure clock, double bigT, double strike) =>
        volatility.SharesTimes(clock)
            ? volatility.BlackVol(bigT, strike)
            : Math.Sqrt(volatility.BlackForwardVarianceAt(clock, 0.0, bigT, strike) / bigT);

    // QuantLib's zeroRate(T, Continuous, NoFrequency) where the curve shares the clock's times; otherwise the
    // continuous rate of the discount it has accrued by the clock's time T
    private static double ZeroRate(YieldTermStructure curve, TermStructure clock, double bigT) =>
        curve.SharesTimes(clock)
            ? curve.ZeroRate(bigT, Compounding.Continuous, Frequency.NoFrequency).Rate
            : curve.ContinuousForwardRateAt(clock, 0.0, bigT);
}
