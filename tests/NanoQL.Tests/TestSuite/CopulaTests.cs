// SPDX-FileCopyrightText: Copyright (C) 2026 LW Osterbrink
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/copulas.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Experimental;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class CopulaTests
{
    private const long Seed = 42;

    [Fact]
    public void TestFarlieGumbelMorgensternCopulaRng()
    {
        // Testing Farlie-Gumbel-Morgenstern copula random-number generator...
        foreach (Real theta in new Real[] { -1.0, -0.5, 0.0, 0.5, 1.0 })
        {
            var rng = new FarlieGumbelMorgensternCopulaRng<MersenneTwisterUniformRng>(new MersenneTwisterUniformRng(Seed), theta);
            CheckSamples("Farlie-Gumbel-Morgenstern copula", theta, rng.Next, new FarlieGumbelMorgensternCopula(theta).Evaluate);
        }
    }

    [Fact]
    public void TestClaytonCopulaRng()
    {
        // Testing Clayton copula random-number generator...

        // the conditional-inversion algorithm is only valid for positive theta
        foreach (Real theta in new Real[] { 0.5, 2.0, 5.0 })
        {
            var rng = new ClaytonCopulaRng<MersenneTwisterUniformRng>(new MersenneTwisterUniformRng(Seed), theta);
            CheckSamples("Clayton copula", theta, rng.Next, new ClaytonCopula(theta).Evaluate);
        }
    }

    [Fact]
    public void TestFrankCopulaRng()
    {
        // Testing Frank copula random-number generator...
        foreach (Real theta in new Real[] { -5.0, -1.0, 1.0, 5.0 })
        {
            var rng = new FrankCopulaRng<MersenneTwisterUniformRng>(new MersenneTwisterUniformRng(Seed), theta);
            CheckSamples("Frank copula", theta, rng.Next, new FrankCopula(theta).Evaluate);
        }
    }

    // Draws from a copula random-number generator and compares the empirical copula against the analytical one on a
    // grid of the unit square. This also covers the marginals, which have to come out uniform.
    private static void CheckSamples(
        string name,
        Real theta,
        Func<Sample<ImmutableArray<double>>> next,
        Func<double, double, double> copula)
    {
        const Size samples = 50000;

        // the empirical CDF has a standard error sqrt(p(1-p)/n) < 0.5/sqrt(samples), i.e. <0.0023 here. The tolerance
        // is about four times that.
        const Real tolerance = 0.01;

        var u = new Real[samples];
        var v = new Real[samples];
        Real meanU = 0.0, meanV = 0.0;
        for (Size i = 0; i < samples; ++i)
        {
            ImmutableArray<double> s = next().Value;
            Assert.True(s.Length == 2, $"{name}: expected a 2-dimensional sample, got {s.Length} dimensions");
            if (s[0] < 0.0 || s[0] > 1.0 || s[1] < 0.0 || s[1] > 1.0)
            {
                Assert.Fail($"{name} with theta = {theta}: sample ({s[0]}, {s[1]}) lies outside the unit square");
            }

            u[i] = s[0];
            v[i] = s[1];
            meanU += s[0];
            meanV += s[1];
        }

        meanU /= samples;
        meanV /= samples;

        // marginals uniform on [0,1] => mean = 0.5
        Assert.False(
            Math.Abs(meanU - 0.5) > tolerance || Math.Abs(meanV - 0.5) > tolerance,
            $"{name} with theta = {theta}: non-uniform marginals\n    calculated means: {meanU}, {meanV}\n"
            + $"    expected means:   0.5, 0.5\n    tolerance:        {tolerance}");

        // interior grid points i/(gridPoints+1), i = 1..gridPoints, in each dimension
        const Size gridPoints = 4;
        const Real gridStep = 1.0 / (gridPoints + 1);

        // check copula on a grid of the unit square
        for (Size i = 1; i <= gridPoints; ++i)
        {
            for (Size j = 1; j <= gridPoints; ++j)
            {
                Real x = gridStep * i, y = gridStep * j;
                Size count = 0;
                for (Size k = 0; k < samples; ++k)
                {
                    if (u[k] <= x && v[k] <= y)
                    {
                        ++count;
                    }
                }

                Real empirical = (Real)count / samples;
                Real expected = copula(x, y);
                Assert.False(
                    Math.Abs(empirical - expected) > tolerance,
                    $"{name} with theta = {theta}: wrong copula at ({x}, {y})\n    calculated: {empirical}\n"
                    + $"    expected:   {expected}\n    tolerance:  {tolerance}");
            }
        }
    }
}
