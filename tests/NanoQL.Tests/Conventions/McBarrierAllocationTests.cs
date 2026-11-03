// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The Monte Carlo barrier engine's loop allocates nothing per sample after its first batch: the Brownian-bridge
/// pricer's draw-price-add, its bridge uniforms drawn as a view of its generator's buffer, and the biased pricer's,
/// each on a down-and-out and an up-and-in call. Each batch goes into an <see cref="IncrementalStatistics"/>, which
/// keeps no sample.
/// </summary>
public class McBarrierAllocationTests
{
    private const int First = 100;
    private const int Batch = 2000;

    private static readonly Date Today = new(15, October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly YieldTermStructure RiskFree = new FlatForward(Today, 0.05, A365);
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Today, 0.02, A365), RiskFree, new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    private static readonly NanoQL.TimeGrid Grid = new(1.0, 20);

    [Fact]
    public void BothPricersLoopsAllocateNothingPerSample()
    {
        double[] discounts = [.. Enumerable.Range(0, Grid.Size).Select(i => RiskFree.Discount(Grid[i]))];
        foreach ((BarrierType type, double barrier) in new[] { (BarrierType.DownOut, 90.0), (BarrierType.UpIn, 115.0) })
        {
            foreach (bool antithetic in new[] { false, true })
            {
                AllocationAssert.AllocatesNothing(
                    () => Model(
                        new BarrierPathPricer(
                            type, barrier, 1.0, OptionType.Call, 100.0, discounts, Process,
                            new MersenneTwisterUniformRsg(Grid.Size - 1, 5)),
                        antithetic),
                    model => model.AddSamples(Batch));
                AllocationAssert.AllocatesNothing(
                    () => Model(new BiasedBarrierPathPricer(type, barrier, 1.0, OptionType.Call, 100.0, discounts), antithetic),
                    model => model.AddSamples(Batch));
            }
        }
    }

    private static MonteCarloModel<MonteCarloPath, IncrementalStatistics> Model(PathPricer<MonteCarloPath> pricer, bool antithetic)
    {
        IPathGenerator<MonteCarloPath> generator = SingleVariate<PseudoRandom>.MakePathGenerator(
            Process, Grid, PseudoRandom.MakeSequenceGenerator(Grid.Size - 1, 42), false);
        var model = new MonteCarloModel<MonteCarloPath, IncrementalStatistics>(generator, pricer, new IncrementalStatistics(), antithetic);
        model.AddSamples(First);
        return model;
    }
}
