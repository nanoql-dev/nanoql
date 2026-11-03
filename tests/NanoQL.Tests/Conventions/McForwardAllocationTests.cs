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
/// The zero-allocation pins of the forward engines' Monte Carlo loops: the forward Black-Scholes and performance path
/// pricers on Black-Scholes paths join the one-dimensional pin (<see cref="McVanillaAllocationTests"/>), and the
/// forward Heston path pricer's draw-price-add on <see cref="HestonProcess"/> paths the Heston pin (<see
/// cref="HestonAllocationTests"/>). Each pricer evaluates the plain vanilla payoff inline, where QuantLib builds one
/// per path, so a batch allocates nothing per sample after the first.
/// </summary>
public class McForwardAllocationTests
{
    private const int First = 100;
    private const int Batch = 2000;

    private static readonly Date Today = new(15, October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly YieldTermStructure RiskFree = new FlatForward(Today, 0.05, A365);
    private static readonly YieldTermStructure Dividends = new FlatForward(Today, 0.02, A365);
    private static readonly NanoQL.TimeGrid Grid = new(1.0, 20);

    [Fact]
    public void TheForwardAndPerformanceLoopsAllocateNothingPerSample()
    {
        var process = new BlackScholesMertonProcess(
            100.0, Dividends, RiskFree, new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));
        foreach (bool antithetic in new[] { false, true })
        {
            AllocationAssert.AllocatesNothing(
                () => Model(process, new ForwardEuropeanBSPathPricer(OptionType.Call, 1.05, 10, 0.95), antithetic),
                model => model.AddSamples(Batch));
            AllocationAssert.AllocatesNothing(
                () => Model(
                    process, new PerformanceOptionPathPricer(OptionType.Put, 1.0, [.. Enumerable.Repeat(0.99, 20)]), antithetic),
                model => model.AddSamples(Batch));
            AllocationAssert.AllocatesNothing(
                () => Model(
                    process, new PerformanceOptionPathPricer(OptionType.Call, 1.0, [1.0, 0.98, 0.96], [0, 10, 20]), antithetic),
                model => model.AddSamples(Batch));
        }
    }

    [Fact]
    public void TheForwardHestonLoopAllocatesNothingPerSample()
    {
        var process = new HestonProcess(RiskFree, Dividends, 100.0, 0.04, 1.5, 0.04, 0.5, -0.7);
        foreach (bool antithetic in new[] { false, true })
        {
            AllocationAssert.AllocatesNothing(
                () =>
                {
                    IPathGenerator<MultiPath> generator = MultiVariate<PseudoRandom>.MakePathGenerator(
                        process, Grid, PseudoRandom.MakeSequenceGenerator(process.Factors * (Grid.Size - 1), 42), false);
                    var model = new MonteCarloModel<MultiPath, IncrementalStatistics>(
                        generator, new ForwardEuropeanHestonPathPricer(OptionType.Put, 0.95, 10, 0.95),
                        new IncrementalStatistics(), antithetic);
                    model.AddSamples(First);
                    return model;
                },
                model => model.AddSamples(Batch));
        }
    }

    private static MonteCarloModel<MonteCarloPath, IncrementalStatistics> Model(
        GeneralizedBlackScholesProcess process, PathPricer<MonteCarloPath> pricer, bool antithetic)
    {
        IPathGenerator<MonteCarloPath> generator = SingleVariate<PseudoRandom>.MakePathGenerator(
            process, Grid, PseudoRandom.MakeSequenceGenerator(Grid.Size - 1, 42), false);
        var model = new MonteCarloModel<MonteCarloPath, IncrementalStatistics>(generator, pricer, new IncrementalStatistics(), antithetic);
        model.AddSamples(First);
        return model;
    }
}
