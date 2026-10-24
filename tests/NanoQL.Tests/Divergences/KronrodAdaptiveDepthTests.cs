// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The adaptive Gauss-Kronrod rule's recursion (the S-cms plan's CMR5 (vi)): QuantLib halves the interval by recursion
/// bounded by its evaluations only (<c>kronrodintegral.cpp:386-439</c>), so at a tolerance it can't reach it overflows
/// a default stack before the bound refuses: integrating e^(−x²) cos 3x and √|x − 1.3| over [0, 4], QuantLib's C++ dies
/// of a stack overflow at 1e-14 on its default stack (exit −1073741571), which in .NET ends the process uncaught.
/// nanoql iterates over an explicit stack in QuantLib's order. QuantLib's check: its own values, counts and refusals,
/// on a thread with a 1 GB stack (the S-cms planner's <c>kronrodstack.cpp</c>): at tolerances 1e-6 to 1e-13 the values
/// and counts below, and at 1e-14 and 1e-15 "maximum number of function evaluations exceeded".
/// </summary>
public class KronrodAdaptiveDepthTests
{
    private static readonly Func<double, double> Smooth = x => Math.Exp(-x * x) * Math.Cos(3 * x);
    private static readonly Func<double, double> Kink = x => Math.Sqrt(Math.Abs(x - 1.3));

    [Fact]
    public void QuantLibsValuesAtTolerancesItReaches()
    {
        foreach ((Func<double, double> f, double tolerance, double value, int evaluations) in new[]
        {
            (Smooth, 1e-6, 0.093407618144140753, 75),
            (Smooth, 1e-10, 0.093407618144140558, 165),
            (Smooth, 1e-12, 0.093407618144140558, 195),
            (Smooth, 1e-13, 0.093407618144140558, 225),
            (Kink, 1e-6, 3.9458538456137866, 1035),
            (Kink, 1e-10, 3.9458538456138053, 2355),
            (Kink, 1e-12, 3.9458538456138048, 9735),
            (Kink, 1e-13, 3.9458538456138048, 36555),
        })
        {
            foreach (int maxEvaluations in new[] { 100000, 1000000 })
            {
                IntegrationResult r = new GaussKronrodAdaptive(tolerance, maxEvaluations).IntegrateWithStatistics(f, 0.0, 4.0);
                Assert.True(Math.Abs(r.Value - value) <= 1e-15 * value, $"{tolerance}: {r.Value} against {value}");
                Assert.Equal(evaluations, r.NumberOfEvaluations);
                Assert.Null(r.AbsoluteError);
            }
        }
    }

    [Fact]
    public void TolerancesItCantReachAreRefused()
    {
        foreach (Func<double, double> f in new[] { Smooth, Kink })
        {
            foreach (double tolerance in new[] { 1e-14, 1e-15 })
            {
                foreach (int maxEvaluations in new[] { 100000, 1000000 })
                {
                    NanoQLException e = Assert.Throws<NanoQLException>(
                        () => new GaussKronrodAdaptive(tolerance, maxEvaluations).Integrate(f, 0.0, 4.0));
                    Assert.Equal("maximum number of function evaluations exceeded", e.Message);
                }
            }
        }
    }
}
