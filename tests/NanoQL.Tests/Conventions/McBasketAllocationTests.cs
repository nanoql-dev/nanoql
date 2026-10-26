// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The zero-allocation pin of the European basket's draw-price-add loop (the S-exo plan's ER1 (iv); master §2.2's
/// Monte Carlo row): on a <see cref="StochasticProcessArray"/> of two and three Black-Scholes processes, pseudo-random
/// and Sobol, the paths step through the array's in-place pair over the generator's held buffers and
/// <see cref="EuropeanMultiPathPricer"/> passes its own buffer, so a batch allocates nothing per sample after the
/// first; each measured on fresh values after a warm-up (<see cref="AllocationAssert"/>).
/// </summary>
public class McBasketAllocationTests
{
    private const int First = 100;
    private const int Batch = 2000;
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly NanoQL.TimeGrid Grid = new(1.0, 12);

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void TheEuropeanBasketDrawPriceAddAllocatesNothingPerSample(int n, bool sobol)
    {
        foreach (bool antithetic in sobol ? new[] { false } : new[] { false, true })
        {
            AllocationAssert.AllocatesNothing(() => Model(n, sobol, antithetic), model => model.AddSamples(Batch));
        }
    }

    private static MonteCarloModel<MultiPath, IncrementalStatistics> Model(int n, bool sobol, bool antithetic)
    {
        StochasticProcessArray array = Array(n);
        int dimension = n * (Grid.Size - 1);
        IRsg generator = sobol
            ? LowDiscrepancy.MakeSequenceGenerator(dimension, 42)
            : PseudoRandom.MakeSequenceGenerator(dimension, 42);
        IPathGenerator<MultiPath> paths = sobol
            ? MultiVariate<LowDiscrepancy>.MakePathGenerator(array, Grid, generator, false)
            : MultiVariate<PseudoRandom>.MakePathGenerator(array, Grid, generator, false);
        var pricer = new EuropeanMultiPathPricer(new MaxBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 100.0)), 0.96);
        var model = new MonteCarloModel<MultiPath, IncrementalStatistics>(paths, pricer, new IncrementalStatistics(), antithetic);
        model.AddSamples(First);
        return model;
    }

    private static StochasticProcessArray Array(int n)
    {
        StochasticProcess1D[] processes = new StochasticProcess1D[n];
        for (int i = 0; i < n; ++i)
        {
            processes[i] = new BlackScholesMertonProcess(
                100.0, new FlatForward(Today, 0.01 * i, A365), new FlatForward(Today, 0.04, A365),
                new BlackConstantVol(Today, new NullCalendar(), 0.2 + (0.05 * i), A365));
        }

        var rho = new Matrix(n, n, 0.4);
        for (int i = 0; i < n; ++i)
        {
            rho[i, i] = 1.0;
        }

        return new StochasticProcessArray([.. processes], rho);
    }
}
