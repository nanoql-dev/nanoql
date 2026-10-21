// SPDX-FileCopyrightText: Copyright (C) 2006 Warren Chou
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/forward/replicatingvarianceswapengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The pricing engine for variance swaps by the cost of their replicating portfolio of out-of-the-money calls and puts,
/// as Demeterfi, Derman, Kamal and Zou describe it ("A Guide to Volatility and Variance Swaps", 1999).
/// </summary>
/// <remarks>
/// <para>
/// The portfolio replicates the log contract piecewise linearly between the strikes given, each option priced by the
/// analytic European engine on the process. The fair variance is the drift's, less the log contract's adjustment at the
/// forward, plus the portfolio's value carried to maturity; the NPV is the notional times the fair variance less the
/// strike, discounted on the risk-free curve, and signed by the position. The additional result "optionWeights" holds
/// each option's payoff and weight, calls first.
/// </para>
/// <para>
/// The drift is 2·(z_r − z_q) and the forward S·D_q/D_r, with z_q and D_q the dividend curve's zero rate and discount
/// read by the maturity date. QuantLib's drift is 2·z_r and its forward S/D_r, as if the dividend yield were zero,
/// which misprices the fair variance at any other yield; its value is that of a zero-dividend process whose risk-free
/// discount is D_r/D_q, which is the check (the S-eq plan's SEQR7 (ix),
/// <c>Divergences/ReplicatingVarianceSwapDividendTests.cs</c>). At a zero dividend yield the value is QuantLib's.
/// </para>
/// <para>
/// A put strike at or below the strike step refuses: the portfolio's added end strike is then 0, whose log payoff
/// QuantLib divides into a NaN or an infinite variance (SEQR7 (iv),
/// <c>Divergences/ReplicatingVarianceSwapRefusalTests.cs</c>).
/// </para>
/// <para>
/// The variance is per year of the swap's time on the risk-free curve's day counter from the earliest reference date
/// of the process's structures, each accruing nothing before its own. QuantLib's time is the risk-free curve's own, so
/// where a later risk-free curve beside an earlier volatility shortens it, QuantLib divides the volatility's variance
/// by the shorter time: 1.1% high at a year on a curve dated four days after the volatility, 2.3 times at a week. Where
/// no structure is dated before the risk-free curve, the time is QuantLib's (the S-eq plan's ruling of 2026-10-21,
/// <c>Divergences/VarianceSwapMismatchedTimesTests.cs</c>).
/// </para>
/// </remarks>
public static class ReplicatingVarianceSwapEngine
{
    /// <summary>Prices a variance swap.</summary>
    /// <param name="swap">The variance swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process the options are priced on.</param>
    /// <param name="dk">The strike step beyond the outermost strikes.</param>
    /// <param name="callStrikes">The call strikes.</param>
    /// <param name="putStrikes">The put strikes.</param>
    /// <returns>The swap's results: its NPV, its fair variance, and the additional result "optionWeights".</returns>
    /// <exception cref="ArgumentNullException">The swap, the context or the process is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// No call or no put strike is given, the lowest put strike isn't positive, the lowest call strike and the highest
    /// put strike differ, or a put strike is at or below <paramref name="dk"/>.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The swap's strike or notional isn't positive, or a curve or the volatility can't be read at the maturity.
    /// </exception>
    public static VarianceSwapResults Calculate(
        VarianceSwap swap,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        double dk = 5.0,
        ReadOnlySpan<double> callStrikes = default,
        ReadOnlySpan<double> putStrikes = default)
    {
        ArgumentNullException.ThrowIfNull(swap);
        ArgumentNullException.ThrowIfNull(context);
        if (process is null)
        {
            throw new ArgumentNullException(nameof(process), "no process given");
        }

        if (!(!callStrikes.IsEmpty && !putStrikes.IsEmpty))
        {
            throw new ArgumentException("no strike(s) given", callStrikes.IsEmpty ? nameof(callStrikes) : nameof(putStrikes));
        }

        double minPut = MinElement(putStrikes);
        if (!(minPut > 0.0))
        {
            throw new ArgumentException("min put strike must be positive", nameof(putStrikes));
        }

        if (!(MinElement(callStrikes) == MaxElement(putStrikes)))
        {
            throw new ArgumentException("min call and max put strikes differ", nameof(callStrikes));
        }

        // SEQR7 (iv): the end strike max(k - dk, 0) below the lowest put would be 0, whose log payoff is infinite
        if (!(minPut > dk))
        {
            throw new ArgumentException("put strikes must be greater than dk", nameof(putStrikes));
        }

        if (swap.IsExpired(context))
        {
            return swap.SetupExpired();
        }

        swap.Validate();
        return swap.FetchResults(Body(swap, context, process, dk, callStrikes, putStrikes));
    }

    // QuantLib's calculate: the variance from the replicating portfolio, and the NPV
    private static VarianceSwapResults Body(
        VarianceSwap swap,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        double dk,
        ReadOnlySpan<double> callStrikes,
        ReadOnlySpan<double> putStrikes)
    {
        // the variance per year of the swap's time on the risk-free curve's day counter from the earliest reference
        // date of the structures, none accruing before its own: QuantLib's, the risk-free curve's own time, where none
        // is dated before it; QuantLib divides an earlier volatility's variance by the risk-free curve's shorter time
        // (the S-eq plan's ruling of 2026-10-21, under R8)
        Date origin = process.EarliestReferenceDate();
        bool fromRiskFreeDate = origin == process.RiskFreeRate.ReferenceDate;
        double residualTime = fromRiskFreeDate
            ? process.Time(swap.MaturityDate)
            : process.RiskFreeRate.DayCounter.YearFraction(origin, swap.MaturityDate);
        var optionWeigths = ImmutableArray.CreateBuilder<(StrikedTypePayoff Payoff, double Weight)>();
        ComputeOptionWeights(callStrikes, OptionType.Call, dk, residualTime, optionWeigths);
        ComputeOptionWeights(putStrikes, OptionType.Put, dk, residualTime, optionWeigths);

        double variance = ComputeReplicatingPortfolio(swap, context, process, residualTime, fromRiskFreeDate, optionWeigths);
        double riskFreeDiscount = process.RiskFreeRate.AccruedDiscount(swap.MaturityDate);

        double multiplier = swap.Position switch
        {
            PositionType.Long => 1.0,
            PositionType.Short => -1.0,
            _ => throw new NanoQLException("Unknown position"),
        };

        return new VarianceSwapResults
        {
            NPV = multiplier * riskFreeDiscount * swap.Notional * (variance - swap.Strike),
            Variance = variance,
            AdditionalResults = ImmutableSortedDictionary<string, object>.Empty.Add("optionWeights", optionWeigths.ToImmutable()),
        };
    }

    private static void ComputeOptionWeights(
        ReadOnlySpan<double> availStrikes,
        OptionType type,
        double dk,
        double residualTime,
        ImmutableArray<(StrikedTypePayoff Payoff, double Weight)>.Builder optionWeights)
    {
        if (availStrikes.IsEmpty)
        {
            return;
        }

        var strikes = new List<double>(availStrikes.ToArray());

        // add end-strike for piecewise approximation
        switch (type)
        {
            case OptionType.Call:
                strikes.Sort();
                strikes.Add(strikes[^1] + dk);
                break;
            case OptionType.Put:
                strikes.Sort((a, b) => b.CompareTo(a));
                strikes.Add(Max(strikes[^1] - dk, 0.0));
                break;
            default:
                throw new NanoQLException("invalid option type");
        }

        // remove duplicate strikes
        for (int i = strikes.Count - 1; i > 0; i--)
        {
            if (strikes[i] == strikes[i - 1])
            {
                strikes.RemoveAt(i);
            }
        }

        // compute weights
        double f = strikes[0];
        double prevSlope = 0.0;

        // added end-strike discarded
        for (int k = 0; k < strikes.Count - 1; ++k)
        {
            double slope = Math.Abs(
                (ComputeLogPayoff(strikes[k + 1], f, residualTime) - ComputeLogPayoff(strikes[k], f, residualTime))
                / (strikes[k + 1] - strikes[k]));
            var payoff = new PlainVanillaPayoff(type, strikes[k]);
            if (k == 0)
            {
                optionWeights.Add((payoff, slope));
            }
            else
            {
                optionWeights.Add((payoff, slope - prevSlope));
            }

            prevSlope = slope;
        }
    }

    private static double ComputeLogPayoff(double strike, double callPutStrikeBoundary, double residualTime)
    {
        double f = callPutStrikeBoundary;
        return 2.0 / residualTime * (((strike - f) / f) - Math.Log(strike / f));
    }

    private static double ComputeReplicatingPortfolio(
        VarianceSwap swap,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        double residualTime,
        bool fromRiskFreeDate,
        ImmutableArray<(StrikedTypePayoff Payoff, double Weight)>.Builder optionWeights)
    {
        var exercise = new EuropeanExercise(swap.MaturityDate);
        double optionsValue = 0.0;
        foreach ((StrikedTypePayoff payoff, double weight) in optionWeights)
        {
            var option = new EuropeanOption(payoff, exercise);
            optionsValue += AnalyticEuropeanEngine.Calculate(option, context, process).NPV!.Value * weight;
        }

        double f = optionWeights[0].Payoff.Strike;
        double underlying = process.X0;

        // the risk-free curve read at its own time where the swap's time is it; otherwise its discount by the maturity
        // date and its rate per the swap's time
        double riskFreeDiscount = fromRiskFreeDate
            ? process.RiskFreeRate.Discount(residualTime)
            : process.RiskFreeRate.AccruedDiscount(swap.MaturityDate);
        double riskFreeRate = fromRiskFreeDate
            ? process.RiskFreeRate.ZeroRate(residualTime, Compounding.Continuous, Frequency.NoFrequency, true).Rate
            : Math.Log(1.0 / riskFreeDiscount) / residualTime;

        // SEQR7 (ix): the dividend curve, read by the maturity date, in the drift and the forward
        double dividendDiscount = process.DividendYield.AccruedDiscount(swap.MaturityDate);
        double dividendRate = Math.Log(1.0 / dividendDiscount) / residualTime;

        return (2.0 * (riskFreeRate - dividendRate))
               - (2.0 / residualTime * ((((underlying * dividendDiscount / riskFreeDiscount) - f) / f) + Math.Log(f / underlying)))
               + (optionsValue / riskFreeDiscount);
    }

    // std::min_element and std::max_element: the first element no other is less, or greater, than
    private static double MinElement(ReadOnlySpan<double> values)
    {
        double result = values[0];
        foreach (double x in values[1..])
        {
            if (x < result)
            {
                result = x;
            }
        }

        return result;
    }

    private static double MaxElement(ReadOnlySpan<double> values)
    {
        double result = values[0];
        foreach (double x in values[1..])
        {
            if (result < x)
            {
                result = x;
            }
        }

        return result;
    }

    // std::max, which returns its first argument where neither is less than the other
    private static double Max(double a, double b) => a < b ? b : a;
}
