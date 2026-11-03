// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

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
/// The Monte Carlo lookback engine's loop allocates nothing per sample after its first batch: its four pricers'
/// draw-price-add, the fixed-strike pricer from the second node with the observed extremum folded in and from a
/// window's node with none, the floating-strike pricer to the last node and to a window's at λ 1.2, calls and puts,
/// plain and antithetic. Each batch goes into an <see cref="IncrementalStatistics"/>, which keeps no sample. The
/// pricers are private to the engine, so they are built through reflection.
/// </summary>
public class McLookbackAllocationTests
{
    private const int First = 100;
    private const int Batch = 2000;

    private static readonly Date Today = new(15, October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05, A365), new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    private static readonly NanoQL.TimeGrid Grid = new([0.25, 1.0], 20);

    [Theory]
    [InlineData(OptionType.Call)]
    [InlineData(OptionType.Put)]
    public void TheFourPricersLoopsAllocateNothingPerSample(OptionType type)
    {
        int window = Grid.Index(0.25);
        PathPricer<MonteCarloPath>[] pricers =
        [
            Pricer("FixedPathPricer", type, 100.0, 100.0, 1, 0.95),
            Pricer("FixedPathPricer", type, 100.0, double.NaN, window, 0.95),
            Pricer("FloatingPathPricer", type, 100.0, 1.0, Grid.Size - 1, 0.95),
            Pricer("FloatingPathPricer", type, 100.0, type == OptionType.Call ? 1.2 : 0.8, window, 0.95),
        ];
        foreach (PathPricer<MonteCarloPath> pricer in pricers)
        {
            foreach (bool antithetic in new[] { false, true })
            {
                AllocationAssert.AllocatesNothing(() => Model(pricer, antithetic), model => model.AddSamples(Batch));
            }
        }
    }

    private static PathPricer<MonteCarloPath> Pricer(string name, params object[] arguments)
    {
        Type type = typeof(MCLookbackEngine).GetNestedType(name, BindingFlags.NonPublic)!;
        ConstructorInfo constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single();
        return (PathPricer<MonteCarloPath>)constructor.Invoke(arguments);
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
