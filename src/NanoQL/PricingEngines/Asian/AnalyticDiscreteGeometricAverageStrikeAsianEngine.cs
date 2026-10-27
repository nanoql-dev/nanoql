// SPDX-FileCopyrightText: Copyright (C) 2009 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/asian/analytic_discr_geom_av_strike.hpp ql/pricingengines/asian/analytic_discr_geom_av_strike.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for discrete geometric average-strike Asian options: the option to exchange the geometric
/// average of log-normal fixings for the terminal spot, both log-normal, priced in closed form.
/// </summary>
/// <remarks>
/// <para>
/// The value alone, as QuantLib's; an option with past fixings is refused, as QuantLib refuses it.
/// </para>
/// <para>
/// Documented divergences (the S-bar plan's BR4 (a), BR5 (xxvi), (xxxiii)). The exchange is priced exactly under
/// deterministic term structures: ln S_T and ln G are jointly normal, ln G with mean (1/N)Σ[ln F(tᵢ) − V(tᵢ)/2] and
/// variance (1/N²)ΣΣV(min(tᵢ, tⱼ)), their covariance (1/N)ΣV(min(tᵢ, T)), F the forward and V the Black variance at the
/// spot, each read by date, where QuantLib reads one volatility at the exercise date and the zero rates to it
/// (<c>Divergences/GeometricAsianTermStructureTests.cs</c>). Every time is measured from the evaluation date, where
/// QuantLib measures the fixing times from the first fixing, so a forward-starting average misses e^(−q t₁)
/// (<c>Divergences/GeometricAverageStrikeForwardStartTests.cs</c>).
/// </para>
/// </remarks>
public static class AnalyticDiscreteGeometricAverageStrikeAsianEngine
{
    private static readonly CumulativeNormalDistribution F = new();

    /// <summary>Prices a discrete geometric average-strike Asian option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <returns>The option's results: the value; the expired results when the option has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The average isn't geometric, the exercise isn't European, the option has past fixings, the payoff isn't plain
    /// vanilla, the spot isn't positive, or the option's past fixings fail its checks.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        DiscreteAveragingAsianOption option, EvaluationContext context, GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(new OneAssetOptionResults { NPV = Value(option, option.Arguments(context), process) });
    }

    private static double Value(
        DiscreteAveragingAsianOption option, DiscreteAveragingAsianArguments arguments, GeneralizedBlackScholesProcess process)
    {
        if (!(arguments.AverageType == AverageType.Geometric))
        {
            throw new NanoQLException("not a geometric average option");
        }

        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not an European option");
        }

        if (!(arguments.RunningAccumulator > 0.0))
        {
            throw new NanoQLException(
                $"positive running product required: {arguments.RunningAccumulator.ToString(System.Globalization.CultureInfo.InvariantCulture)}not allowed");
        }

        if (!(arguments.PastFixings == 0))
        {
            throw new NanoQLException("past fixings currently not managed");
        }

        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-plain payoff given");
        }

        double underlying = process.StateVariable;
        if (!(underlying > 0.0))
        {
            throw new NanoQLException("positive underlying value required");
        }

        YieldTermStructure riskFree = process.RiskFreeRate;
        YieldTermStructure dividends = process.DividendYield;
        BlackVolTermStructure volatility = process.BlackVolatility;
        Date exDate = option.Exercise.LastDate();
        double varianceT = volatility.AccruedVariance(exDate, underlying);

        int n = arguments.FixingDates.Length;
        double meanSum = 0.0, varianceSum = 0.0, covarianceSum = 0.0;
        for (int k = 0; k < n; ++k)
        {
            Date d = arguments.FixingDates[k];
            double forward = underlying * dividends.AccruedDiscount(d) / riskFree.AccruedDiscount(d);
            double v = volatility.AccruedVariance(d, underlying);
            meanSum += Math.Log(forward) - (0.5 * v);
            varianceSum += (1.0 + (2.0 * (n - 1 - k))) * v;
            covarianceSum += d <= exDate ? v : varianceT;
        }

        double muG = meanSum / n;
        double variance = varianceSum / n / n;
        double covariance = covarianceSum / n;
        double sigmaSum2 = varianceT + variance - (2.0 * covariance);

        double riskFreeDiscount = riskFree.AccruedDiscount(exDate);
        double forwardT = underlying * dividends.AccruedDiscount(exDate) / riskFreeDiscount;
        double averageForward = Math.Exp(muG + (variance / 2.0));

        double y1 = (Math.Log(forwardT / averageForward) + (sigmaSum2 / 2.0)) / Math.Sqrt(sigmaSum2);
        double y2 = y1 - Math.Sqrt(sigmaSum2);

        return payoff.OptionType switch
        {
            OptionType.Call => riskFreeDiscount * ((forwardT * F.Evaluate(y1)) - (averageForward * F.Evaluate(y2))),
            OptionType.Put => riskFreeDiscount * (-(forwardT * F.Evaluate(-y1)) + (averageForward * F.Evaluate(-y2))),
            _ => throw new NanoQLException("invalid option type"),
        };
    }
}
