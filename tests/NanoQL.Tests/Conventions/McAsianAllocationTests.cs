// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Reflection;
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
/// The Monte Carlo Asian engines' loops and the finite-difference Asian step allocate nothing per sample or step after
/// their first. The loops: the three Black-Scholes pricers' draw-price-add, the arithmetic average-price pricer with
/// and without its geometric control, the average-strike and the geometric pricers, each on the engine's route, its
/// fixings' multiplicities and, for the geometric, the past fixings' log (internal, so built through reflection); the
/// two Heston pricers on fixing indices with one given twice; calls and puts, plain and antithetic. Each batch goes
/// into an <see cref="IncrementalStatistics"/>, which keeps no sample. The step:
/// <see cref="FdmArithmeticAverageCondition"/> at an averaging time, its spline's buffers held.
/// </summary>
public class McAsianAllocationTests
{
    private const int First = 100;
    private const int Batch = 2000;

    private static readonly Date Today = new(15, October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05, A365), new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    private static readonly HestonProcess Heston = new(
        new FlatForward(Today, 0.05, A365), new FlatForward(Today, 0.02, A365), 100.0, 0.04, 1.5, 0.04, 0.3, -0.7);

    private static readonly NanoQL.TimeGrid Grid = new([0.25, 0.5, 0.75, 1.0]);
    private static readonly ImmutableArray<int> Counts = [0, 1, 2, 1, 1];

    [Theory]
    [InlineData(OptionType.Call)]
    [InlineData(OptionType.Put)]
    public void TheBlackScholesLoopsAllocateNothingPerSample(OptionType type)
    {
        PathPricer<MonteCarloPath> arithmetic = Internal<ArithmeticAPOPathPricer>(type, 100.0, 0.95, 200.0, 2, Counts);
        PathPricer<MonteCarloPath> geometric = Internal<GeometricAPOPathPricer>(type, 100.0, 0.95, 1.0, 650.0, 2, Counts);
        PathPricer<MonteCarloPath> strike = Internal<ArithmeticASOPathPricer>(type, 0.95, 200.0, 2, (int?)null, Counts);
        PathPricer<MonteCarloPath> control = Internal<GeometricAPOPathPricer>(type, 100.0, 0.95, 1.0, double.NaN, 0, Counts);
        foreach (bool antithetic in new[] { false, true })
        {
            AllocationAssert.AllocatesNothing(() => Model(arithmetic, antithetic, null), model => model.AddSamples(Batch));
            AllocationAssert.AllocatesNothing(() => Model(arithmetic, antithetic, control), model => model.AddSamples(Batch));
            AllocationAssert.AllocatesNothing(() => Model(strike, antithetic, null), model => model.AddSamples(Batch));
            AllocationAssert.AllocatesNothing(() => Model(geometric, antithetic, null), model => model.AddSamples(Batch));
        }
    }

    [Theory]
    [InlineData(OptionType.Call)]
    [InlineData(OptionType.Put)]
    public void TheHestonLoopsAllocateNothingPerSample(OptionType type)
    {
        ImmutableArray<int> indices = [1, 2, 2, 3, 4];
        PathPricer<MultiPath>[] pricers =
        [
            new ArithmeticAPOHestonPathPricer(type, 100.0, 0.95, indices, 200.0, 2),
            new GeometricAPOHestonPathPricer(type, 100.0, 0.95, indices, 10000.0, 2),
        ];
        foreach (PathPricer<MultiPath> pricer in pricers)
        {
            foreach (bool antithetic in new[] { false, true })
            {
                AllocationAssert.AllocatesNothing(() => HestonModel(pricer, antithetic), model => model.AddSamples(Batch));
            }
        }
    }

    [Fact]
    public void TheFiniteDifferenceStepAllocatesNothing()
    {
        var mesher = new FdmMesherComposite([new Uniform1dMesher(Math.Log(50.0), Math.Log(200.0), 30), new Uniform1dMesher(Math.Log(50.0), Math.Log(200.0), 20)]);
        var condition = new FdmArithmeticAverageCondition([0.25, 0.5, 1.0], 0.0, 0, mesher, 0);
        double[] initial = [.. Enumerable.Range(0, 600).Select(i => 1.0 + (0.01 * i))];
        double[] warm = (double[])initial.Clone();
        condition.ApplyTo(warm, 0.5);
        double[] values = AllocationAssert.AllocatesNothing(() => (double[])initial.Clone(), fresh => condition.ApplyTo(fresh, 0.5));
        Assert.Equal(warm, values);
    }

    // the engine's route, an internal constructor
    private static PathPricer<MonteCarloPath> Internal<T>(params object?[] arguments)
        where T : PathPricer<MonteCarloPath>
    {
        ConstructorInfo constructor = typeof(T).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
        return (PathPricer<MonteCarloPath>)constructor.Invoke(arguments);
    }

    private static MonteCarloModel<MonteCarloPath, IncrementalStatistics> Model(
        PathPricer<MonteCarloPath> pricer, bool antithetic, PathPricer<MonteCarloPath>? control)
    {
        IPathGenerator<MonteCarloPath> generator = SingleVariate<PseudoRandom>.MakePathGenerator(
            Process, Grid, PseudoRandom.MakeSequenceGenerator(Grid.Size - 1, 42), true);
        var model = new MonteCarloModel<MonteCarloPath, IncrementalStatistics>(
            generator, pricer, new IncrementalStatistics(), antithetic, control, control is null ? 0.0 : 5.0);
        model.AddSamples(First);
        return model;
    }

    private static MonteCarloModel<MultiPath, IncrementalStatistics> HestonModel(PathPricer<MultiPath> pricer, bool antithetic)
    {
        IPathGenerator<MultiPath> generator = MultiVariate<PseudoRandom>.MakePathGenerator(
            Heston, Grid, PseudoRandom.MakeSequenceGenerator(Heston.Factors * (Grid.Size - 1), 42), false);
        var model = new MonteCarloModel<MultiPath, IncrementalStatistics>(generator, pricer, new IncrementalStatistics(), antithetic);
        model.AddSamples(First);
        return model;
    }
}
