// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// QuantLib's fix after v1.44, #2880 (<c>ca8e70946</c>), to Drezner's bivariate normal, taken as a fix, with its three
/// cases in <c>test-suite/distributions.cpp</c> on master, here until the pin moves: at ρ = ±1, where QuantLib 1.44
/// divides by zero, the probabilities are Φ(min(a, b)) and max(0, Φ(a) + Φ(b) − 1); near zero bounds the recursion's
/// derived correlations, by hypot forms, stay continuous and symmetric; and near |ρ| = 1 a one-ulp overshoot of a
/// derived correlation is clamped, where QuantLib 1.44 throws.
/// </summary>
public class BivariateDr78DegenerateCorrelationsTests
{
    private static readonly CumulativeNormalDistribution Normal = new();

    [Fact]
    public void TestBivariateDr78DegenerateCorrelations()
    {
        double[][] arguments =
        [
            [0.1, 0.0], [-0.5, 0.3], [0.5, 0.5], [-0.5, -0.5], [0.0, 0.0],
            [0.5, -0.5], [0.3, 0.5], [0.5, 0.3], [5.6e-17, 0.766], [-0.766, -1e-9],
        ];
        foreach (double rho in new[] { -1.0, 1.0 })
        {
            var drezner = new BivariateCumulativeNormalDistributionDr78(rho);
            foreach (double[] argumentsPair in arguments)
            {
                double a = argumentsPair[0], b = argumentsPair[1];

                // At rho = 1, Y = X; at rho = -1, Y = -X.
                double expected = rho == 1.0
                    ? Normal.Evaluate(Math.Min(a, b))
                    : Math.Max(0.0, Normal.Evaluate(a) + Normal.Evaluate(b) - 1.0);
                double calculated = drezner.Evaluate(a, b);
                Assert.True(double.IsFinite(calculated));
                Assert.True(Math.Abs(calculated - expected) <= 1e-15, $"a = {a}, b = {b}, rho = {rho}");
            }
        }
    }

    [Fact]
    public void TestBivariateDr78NearZero()
    {
        // Dr78's quadrature is only accurate to about 1e-6.
        const double tolerance = 1e-6;
        foreach (double rho in new[] { -0.707, 0.707 })
        {
            var drezner = new BivariateCumulativeNormalDistributionDr78(rho);
            var west = new BivariateCumulativeNormalDistributionWe04DP(rho);
            foreach (double tiny in new[] { 5.6e-17, 1e-9, 3e-9 })
            {
                foreach (double a in new[] { -tiny, tiny })
                {
                    foreach (double b in new[] { -1.0, -0.766, 0.766, 1.0 })
                    {
                        double calculated = drezner.Evaluate(a, b);
                        double swapped = drezner.Evaluate(b, a);
                        Assert.True(double.IsFinite(calculated));
                        Assert.True(double.IsFinite(swapped));
                        Assert.True(Math.Abs(calculated - west.Evaluate(a, b)) <= tolerance);
                        Assert.True(Math.Abs(swapped - west.Evaluate(b, a)) <= tolerance);
                        Assert.True(Math.Abs(calculated - drezner.Evaluate(0.0, b)) <= tolerance);
                        Assert.True(Math.Abs(swapped - drezner.Evaluate(b, 0.0)) <= tolerance);
                        Assert.True(Math.Abs(calculated - swapped) <= 1e-15, $"a = {a}, b = {b}, rho = {rho}");
                    }
                }
            }
        }
    }

    [Fact]
    public void TestBivariateDr78NearDegenerateCorrelations()
    {
        // Dr78's quadrature is only accurate to about 1e-6.
        const double tolerance = 1e-6;
        double nearOne = Math.BitDecrement(1.0);
        const double a = 0.0001;
        foreach (double rho in new[] { -nearOne, nearOne })
        {
            double b = rho > 0.0 ? a : -a;
            var drezner = new BivariateCumulativeNormalDistributionDr78(rho);
            var west = new BivariateCumulativeNormalDistributionWe04DP(rho);
            double calculated = drezner.Evaluate(a, b);
            double swapped = drezner.Evaluate(b, a);
            Assert.True(double.IsFinite(calculated));
            Assert.True(double.IsFinite(swapped));
            Assert.True(Math.Abs(calculated - west.Evaluate(a, b)) <= tolerance);
            Assert.True(Math.Abs(swapped - west.Evaluate(b, a)) <= tolerance);
            Assert.True(Math.Abs(calculated - swapped) <= 1e-15, $"a = {a}, b = {b}, rho = {rho}");
        }
    }
}
