// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The multi-cubic spline is safe to read from many threads (the S-exo plan's ER3 (ii)): its evaluation computes
/// QuantLib's coefficients in scratch of its own, where QuantLib's evaluates through <c>mutable</c> members, so one
/// spline read concurrently gives a fresh spline's bits at every point; and it copies its grid's values, so changing
/// the array it was built from changes nothing.
/// </summary>
public class MultiCubicSplineThreadTests
{
    private static readonly ImmutableArray<ImmutableArray<double>> Grid =
        [[0.0, 0.3, 0.6, 0.9, 1.2], [1.0, 1.5, 2.0, 2.5], [-0.5, 0.0, 0.5, 1.0, 1.5]];

    [Fact]
    public void OneSplineReadFromManyThreadsGivesAFreshSplinesBits()
    {
        double[] values = Values();
        var shared = new MultiCubicSpline(Grid, values);
        var rng = new MersenneTwisterUniformRng(42);
        double[][] points = new double[400][];
        for (int i = 0; i < points.Length; ++i)
        {
            points[i] = [1.2 * rng.Next().Value, 1.0 + (1.5 * rng.Next().Value), -0.5 + (2.0 * rng.Next().Value)];
        }

        double[] expected = [.. points.Select(x => new MultiCubicSpline(Grid, values).Evaluate(x))];
        double[] actual = new double[points.Length];
        Parallel.For(0, points.Length * 8, new ParallelOptions { MaxDegreeOfParallelism = 8 }, k =>
        {
            int i = k % points.Length;
            actual[i] = shared.Evaluate(points[i]);
        });
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ChangingTheValuesAfterwardsChangesNothing()
    {
        double[] values = Values();
        var spline = new MultiCubicSpline(Grid, values);
        double before = spline.Evaluate([0.45, 1.7, 0.2]);
        Array.Fill(values, 0.0);
        Assert.Equal(before, spline.Evaluate([0.45, 1.7, 0.2]));
    }

    private static double[] Values() =>
        [.. Grid[0].SelectMany(x => Grid[1].SelectMany(y => Grid[2].Select(z => Math.Sin(x) + (y * Math.Cos(z)))))];
}
