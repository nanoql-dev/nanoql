// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="StochasticProcessArray"/>'s in-place pair (the S-exo plan's ER1 (i)): <c>InitialValues(Span)</c> and
/// <c>Evolve(t0, x0, dt, dw, Span)</c> give the allocating forms' bits, QuantLib's arithmetic (the correlated
/// increments row by row in the matrix product's order, then each process's own step), on two and three Black-Scholes
/// processes, and refuse increments of another size than the correlation's as the matrix product does.
/// </summary>
public class StochasticProcessArrayInPlaceTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void TheInPlaceFormsGiveTheAllocatingFormsBits(int n)
    {
        StochasticProcessArray array = Array(n);
        double[] initial = new double[n];
        array.InitialValues(initial);
        Assert.Equal(array.InitialValues().ToArray(), initial);

        var rng = new MersenneTwisterUniformRng(42);
        var x0 = new Vector(n);
        var dw = new Vector(n);
        for (int step = 0; step < 20; ++step)
        {
            for (int i = 0; i < n; ++i)
            {
                x0[i] = 80.0 + (40.0 * rng.Next().Value);
                dw[i] = (2.0 * rng.Next().Value) - 1.0;
            }

            double t0 = 0.05 * step;
            double[] result = new double[n];
            array.Evolve(t0, x0.AsSpan(), 0.05, dw.AsSpan(), result);
            Assert.Equal(array.Evolve(t0, x0, 0.05, dw).ToArray(), result);
        }
    }

    [Fact]
    public void IncrementsOfAnotherSizeAreRefused()
    {
        StochasticProcessArray array = Array(2);
        double[] result = new double[2];
        ArgumentException e = Assert.Throws<ArgumentException>(() => array.Evolve(0.0, [100.0, 100.0], 0.1, [0.1, 0.2, 0.3], result));
        Assert.Equal("dw", e.ParamName);
    }

    private static StochasticProcessArray Array(int n)
    {
        StochasticProcess1D[] processes = new StochasticProcess1D[n];
        for (int i = 0; i < n; ++i)
        {
            processes[i] = new BlackScholesMertonProcess(
                100.0 + (5.0 * i),
                new FlatForward(Today, 0.01 * i, A365),
                new FlatForward(Today, 0.04, A365),
                new BlackConstantVol(Today, new NullCalendar(), 0.2 + (0.05 * i), A365));
        }

        var rho = new Matrix(n, n, 0.3);
        for (int i = 0; i < n; ++i)
        {
            rho[i, i] = 1.0;
        }

        return new StochasticProcessArray([.. processes], rho);
    }
}
