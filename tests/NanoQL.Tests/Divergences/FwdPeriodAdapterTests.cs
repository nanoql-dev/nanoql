// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The period adapter's default displacements (fixed) and its pseudo-roots on the fixed Y matrix. With no displacements
/// given, QuantLib's k-th long rate's is the running sum of the first (k + 1)·period short ones over the period, its
/// <c>sum</c> never reset and its index from 0, not the offset (<c>fwdperiodadapter.cpp:54-64</c>): on twelve
/// semi-annual rates displaced 1%, 0.01, 0.02, … 0.06 in periods of 2, and on displacements 0.1% to 1.2%, 0.0015,
/// 0.005, 0.0105 … where the periods' means are 0.0015, 0.0035, 0.0055 … (the C++ harness's <c>FwdPeriodAdapter</c>).
/// nanoql takes each period's own short displacements' mean, from the offset. And the adapter's pseudo-roots are the Y
/// matrix times the short model's, where QuantLib's Y is its defective one, its long forwards' variances 3 times too
/// high on quarterly forwards in periods of 4. Checks: equal short displacements give QuantLib's own model for that one
/// displacement given (the one-displacement branch), bit for bit; the means; and each step's long covariance the short
/// covariance through the long forwards' elasticities by central bumps of QuantLib's restricted state.
/// </summary>
public class FwdPeriodAdapterTests
{
    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    public void EqualDisplacementsAreTheOneGiven(int period, int offset)
    {
        MarketModel small = Model(0.5, 12, [.. Enumerable.Repeat(0.01, 12)]);
        var byDefault = new FwdPeriodAdapter(small, period, offset, []);
        var given = new FwdPeriodAdapter(small, period, offset, [0.01]);
        Assert.Equal(given.Displacements, byDefault.Displacements);
        Assert.Equal(given.InitialRates, byDefault.InitialRates);
        for (int k = 0; k < given.NumberOfSteps; ++k)
        {
            Assert.Equal(Rows(given.PseudoRoot(k)), Rows(byDefault.PseudoRoot(k)));
        }

        // QuantLib's running sum: 0.01, 0.02, ... (the C++ harness)
        Assert.NotEqual(0.02, byDefault.Displacements[1], 1e-3);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    public void EachLongDisplacementIsItsPeriodsMean(int period, int offset)
    {
        double[] d = [.. Enumerable.Range(0, 12).Select(i => 0.001 * (i + 1))];
        var adapter = new FwdPeriodAdapter(Model(0.5, 12, d), period, offset, []);
        for (int k = 0; k < adapter.NumberOfRates; ++k)
        {
            double sum = 0.0;
            for (int l = 0; l < period; ++l)
            {
                sum += d[offset + (k * period) + l];
            }

            Assert.Equal(sum / period, adapter.Displacements[k]);
        }

        // QuantLib's: 0.0015, 0.005, 0.0105 at period 2, offset 0 (the C++ harness)
        if (period == 2 && offset == 0)
        {
            Assert.Equal(0.0035, adapter.Displacements[1], 1e-15);
            Assert.NotEqual(0.005, adapter.Displacements[1], 1e-4);
        }
    }

    [Theory]
    [InlineData(0.5, 12, 2)]
    [InlineData(0.25, 12, 4)]
    public void TheLongCovariancesAreTheShortOnesThroughTheElasticities(double tau, int n, int period)
    {
        double[] d = [.. Enumerable.Repeat(0.01, n)];
        MarketModel small = Model(tau, n, d);
        var adapter = new FwdPeriodAdapter(small, period, 0, []);
        int k = adapter.NumberOfRates;
        Matrix y = BumpedElasticities(small, period, d, adapter.Displacements.ToArray());
        for (int step = 0; step < adapter.NumberOfSteps; ++step)
        {
            Matrix expected = y * small.Covariance(step) * MatrixFunctions.Transpose(y);
            Matrix actual = adapter.Covariance(step);
            int alive = adapter.Evolution.FirstAliveRate[step];
            for (int i = alive; i < k; ++i)
            {
                for (int j = alive; j < k; ++j)
                {
                    Assert.Equal(expected[i, j], actual[i, j], 1e-7 * Math.Abs(expected[i, i]));
                }
            }
        }
    }

    // Central bumps of log(F_l + D_l) of QuantLib's restricted state in log(f_j + d_j).
    private static Matrix BumpedElasticities(MarketModel small, int period, double[] d, double[] longD)
    {
        const double h = 1e-6;
        double[] f = [.. small.InitialRates];
        double[] times = [.. small.Evolution.RateTimes];
        int k = f.Length / period;
        var result = new Matrix(k, f.Length);
        for (int j = 0; j < f.Length; ++j)
        {
            double[] up = (double[])f.Clone(), down = (double[])f.Clone();
            up[j] = ((f[j] + d[j]) * Math.Exp(h)) - d[j];
            down[j] = ((f[j] + d[j]) * Math.Exp(-h)) - d[j];
            LMMCurveState ru = Restricted(times, up, period), rd = Restricted(times, down, period);
            for (int l = 0; l < k; ++l)
            {
                result[l, j] = (Math.Log(ru.ForwardRate(l) + longD[l]) - Math.Log(rd.ForwardRate(l) + longD[l])) / (2 * h);
            }
        }

        return result;
    }

    private static LMMCurveState Restricted(double[] times, double[] f, int period)
    {
        var cs = new LMMCurveState(times);
        cs.SetOnForwardRates(f);
        return ForwardForwardMappings.RestrictCurveState(cs, period, 0);
    }

    private static FlatVol Model(double tau, int n, double[] d)
    {
        double[] times = [.. Enumerable.Range(1, n + 1).Select(i => tau * i)];
        return new FlatVol(
            [.. Enumerable.Range(0, n).Select(i => 0.18 + (0.005 * i))],
            new ExponentialForwardCorrelation(times, 0.5, 0.2),
            new EvolutionDescription(times),
            n,
            [.. Enumerable.Range(0, n).Select(i => 0.03 + (0.001 * i))],
            d);
    }

    private static double[][] Rows(Matrix m) => [.. Enumerable.Range(0, m.Rows).Select(i => m.Row(i).ToArray())];
}
