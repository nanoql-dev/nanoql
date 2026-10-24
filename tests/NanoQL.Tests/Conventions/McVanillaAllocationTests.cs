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
/// The Monte Carlo vanilla engines' loops allocate nothing per sample after their first batch (the S-van plan's VD12,
/// S3's closing ruling, S-sr's one-dimensional form): the European pricer's and the digital pricer's draw-price-add on
/// Black-Scholes paths, the digital pricer's uniform numbers drawn as a view of its generator's buffer, and the
/// Longstaff-Schwartz pricer's pricing phase after its calibration. Each batch goes into an
/// <see cref="IncrementalStatistics"/>, which keeps no sample; the Black-Scholes process reads its rates without
/// building the rate objects. The calibration phase copies each path by design and isn't pinned.
/// </summary>
public class McVanillaAllocationTests
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
    public void TheEuropeanAndDigitalLoopsAllocateNothingPerSample()
    {
        foreach (bool antithetic in new[] { false, true })
        {
            AllocationAssert.AllocatesNothing(
                () => Model(new EuropeanPathPricer(OptionType.Call, 100.0, 0.95), antithetic),
                model => model.AddSamples(Batch));
            AllocationAssert.AllocatesNothing(
                () => Model(
                    new DigitalPathPricer(
                        new CashOrNothingPayoff(OptionType.Call, 110.0, 10.0), new AmericanExercise(Today, Today + 365), RiskFree,
                        Process, new MersenneTwisterUniformRsg(Grid.Size - 1, 76)),
                    antithetic),
                model => model.AddSamples(Batch));
        }
    }

    [Fact]
    public void TheLongstaffSchwartzPricingPhaseAllocatesNothingPerSample()
    {
        AllocationAssert.AllocatesNothing(
            () =>
            {
                var pricer = new LongstaffSchwartzPathPricer<MonteCarloPath, double>(
                    Grid,
                    new AmericanPathPricer(new PlainVanillaPayoff(OptionType.Put, 100.0), 2, LsmBasisSystemPolynomialType.Monomial),
                    RiskFree,
                    [.. Enumerable.Range(1, Grid.Size - 1).Select(i => Grid[i])]);
                MonteCarloModel<MonteCarloPath, IncrementalStatistics> calibration = Model(pricer, false, 7);
                calibration.AddSamples(500);
                pricer.Calibrate();
                return Model(pricer, false);
            },
            model => model.AddSamples(Batch));
    }

    private static MonteCarloModel<MonteCarloPath, IncrementalStatistics> Model(
        PathPricer<MonteCarloPath> pricer, bool antithetic, long seed = 42)
    {
        IPathGenerator<MonteCarloPath> generator = SingleVariate<PseudoRandom>.MakePathGenerator(
            Process, Grid, PseudoRandom.MakeSequenceGenerator(Grid.Size - 1, seed), false);
        var model = new MonteCarloModel<MonteCarloPath, IncrementalStatistics>(generator, pricer, new IncrementalStatistics(), antithetic);
        model.AddSamples(First);
        return model;
    }
}
