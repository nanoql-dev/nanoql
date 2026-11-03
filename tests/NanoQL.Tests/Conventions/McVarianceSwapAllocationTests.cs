// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The Monte Carlo variance-swap engine's loop joins the one-dimensional pin (<see cref="McVanillaAllocationTests"/>):
/// <see cref="VariancePathPricer"/> integrates over the path it is given, where QuantLib's integrand copies the path
/// per sample, so a batch allocates nothing per sample after the first.
/// </summary>
public class McVarianceSwapAllocationTests
{
    private const int First = 100;
    private const int Batch = 2000;
    private static readonly Date Today = new(15, October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly NanoQL.TimeGrid Grid = new(1.0, 52);

    [Fact]
    public void TheVarianceLoopAllocatesNothingPerSample()
    {
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.01, A365), new FlatForward(Today, 0.05, A365),
            new BlackConstantVol(Today, new NullCalendar(), 0.2, A365));
        foreach (bool antithetic in new[] { false, true })
        {
            AllocationAssert.AllocatesNothing(
                () =>
                {
                    IPathGenerator<MonteCarloPath> generator = SingleVariate<PseudoRandom>.MakePathGenerator(
                        process, Grid, PseudoRandom.MakeSequenceGenerator(Grid.Size - 1, 42), false);
                    var model = new MonteCarloModel<MonteCarloPath, IncrementalStatistics>(
                        generator, new VariancePathPricer(process), new IncrementalStatistics(), antithetic);
                    model.AddSamples(First);
                    return model;
                },
                model => model.AddSamples(Batch));
        }
    }
}
