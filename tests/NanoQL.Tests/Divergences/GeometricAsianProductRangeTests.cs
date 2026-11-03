// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The geometric running product outside double's range (fixed). QuantLib's Monte Carlo geometric pricers guard the
/// product against overflow only (<c>mc_discr_geom_av_price.cpp:45-57</c>), so 252 daily fixings at spot 0.01 price 0
/// where the analytic engine gives 0.059 × the spot; and the all-past-fixings constructor multiplies the past fixings
/// into the running product (<c>asianoption.cpp:95</c>), so 120 past fixings at 1,000 make the analytic and Monte Carlo
/// engines price ∞. nanoql flushes the product into the average where the next factor would underflow it, as where it
/// would overflow, and carries the past fixings' log-sum beside QuantLib's product, its readers taking the log where
/// the product isn't finite and normal. Check: homogeneity, the same contract with the market and the fixings scaled
/// into range and the price scaled back (r 5%, q 0, σ 20%, Act/365 Fixed).
/// </summary>
public class GeometricAsianProductRangeTests
{
    private static readonly Date Today = new(1, Month.January, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void TheMonteCarloAtASmallSpotIsTheScaledPrice()
    {
        Date[] daily = [.. Enumerable.Range(1, 252).Select(i => Today + i)];
        double Priced(double spot) => MCDiscreteGeometricAPEngine.Calculate<PseudoRandom, GeneralStatistics>(
            new DiscreteAveragingAsianOption(AverageType.Geometric, 1.0, 0, daily, new PlainVanillaPayoff(OptionType.Call, spot), new EuropeanExercise(daily[^1])),
            Context,
            Market(spot),
            false,
            false,
            20000,
            null,
            null,
            42).NPV!.Value;
        double small = Priced(0.01);
        Assert.True(small > 0.0);
        Assert.Equal(Priced(100.0) * 1e-4, small, 1e-9 * small);
    }

    [Fact]
    public void TheAnalyticEngineOnALargeSeasonedProductIsTheScaledPrice() =>
        CheckSeasoned((option, p) => AnalyticDiscreteGeometricAveragePriceAsianEngine.Calculate(option, Context, p).NPV!.Value);

    [Fact]
    public void TheMonteCarloOnALargeSeasonedProductIsTheScaledPrice() =>
        CheckSeasoned((option, p) => MCDiscreteGeometricAPEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option, Context, p, false, false, 20000, null, null, 42).NPV!.Value);

    // 120 past daily fixings at 1,000 (their product 1e360) and six monthly ones to come, against the same at 1
    private static void CheckSeasoned(Func<DiscreteAveragingAsianOption, GeneralizedBlackScholesProcess, double> price)
    {
        Date[] dates = [.. Enumerable.Range(0, 120).Select(i => Today - 120 + i), .. Enumerable.Range(1, 6).Select(i => Today + (30 * i))];
        double Priced(double level) => price(
            new DiscreteAveragingAsianOption(
                AverageType.Geometric, dates, new PlainVanillaPayoff(OptionType.Call, level), new EuropeanExercise(Today + 180), Enumerable.Repeat(level, 120)),
            Market(level));
        double large = Priced(1000.0);
        Assert.True(double.IsFinite(large) && large > 0.0);
        Assert.Equal(Priced(1.0) * 1000.0, large, 1e-10 * large);
    }

    private static GeneralizedBlackScholesProcess Market(double spot) =>
        new BlackScholesMertonProcess(
            spot, new FlatForward(Today, 0.0, A365), new FlatForward(Today, 0.05, A365), new BlackConstantVol(Today, new NullCalendar(), 0.20, A365));
}
