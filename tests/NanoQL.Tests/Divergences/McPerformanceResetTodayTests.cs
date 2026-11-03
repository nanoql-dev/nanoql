// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Time;
using static NanoQL.Tests.Divergences.ExoticTermStructureMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Monte Carlo performance engine on a cliquet with a reset today. Its grid takes the resets as mandatory times
/// (<c>mcperformanceengine.hpp:141-149</c>) and the time grid merges a reset at time 0 into its start, so QuantLib's
/// path pricer refuses on its count check ("discounts/options mismatch", <c>mcperformanceengine.cpp:33</c>), where the
/// analytic engine prices 0.125531. nanoql's path pricer reads each reset's index from the grid, as QuantLib's Monte
/// Carlo forward engines do. The checks: the analytic engine (QuantLib's bits) within two of the Monte Carlo error
/// estimates; with the first reset a day later, QuantLib's Monte Carlo value, bit for bit.
/// </summary>
public class McPerformanceResetTodayTests
{
    [Fact]
    public void AResetTodayIsPricedFromTheGridsStart()
    {
        GeneralizedBlackScholesProcess p = Flat(100.0, 0.04, 0.01, 0.20);
        CliquetOption today = Cliquet(Today);
        Assert.Equal(0.12553119952706235, AnalyticPerformanceEngine.Calculate(today, Context, p).NPV!.Value);
        OneAssetOptionResults mc = Engine(p)(today, Context);
        Assert.True(
            Math.Abs(mc.NPV!.Value - 0.12553119952706235) <= 2.0 * mc.ErrorEstimate!.Value,
            $"Monte Carlo {mc.NPV} ± {mc.ErrorEstimate}");

        // a day later, QuantLib's Monte Carlo value
        Assert.Equal(0.12496123878757268, Engine(p)(Cliquet(Today + 1), Context).NPV!.Value);
    }

    private static CliquetOption Cliquet(Date firstReset) =>
        new(new PercentageStrikePayoff(OptionType.Call, 1.0), new EuropeanExercise(Today + 365), [firstReset, Today + 182]);

    private static Func<CliquetOption, EvaluationContext, OneAssetOptionResults> Engine(GeneralizedBlackScholesProcess p) =>
        new MakeMCPerformanceEngine<PseudoRandom, GeneralStatistics>(p, 42).WithSamples(65536).ToEngine();
}
