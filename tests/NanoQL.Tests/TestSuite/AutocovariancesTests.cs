// SPDX-FileCopyrightText: Copyright (C) 2010 Liquidnet Holdings, Inc.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/autocovariances.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class AutocovariancesTests
{
    [Fact]
    public void TestConvolutions()
    {
        // Testing convolutions...
        var x = new Vector(10, 1, 1);
        var conv = new Vector(6);
        AutocovarianceFunctions.Convolutions(x.AsSpan(), conv.AsSpan(), 5);
        Real[] expected = [385, 330, 276, 224, 175, 130];
        Vector delta = conv - new Vector(expected);
        if (ArrayFunctions.DotProduct(delta, delta) > 1.0e-6)
        {
            Assert.Fail($"Convolution: \n    calculated:   {conv}\n    expected:     {new Vector(expected)}");
        }
    }

    [Fact]
    public void TestAutoCovariances()
    {
        // Testing auto-covariances...
        var x = new Vector(10, 1, 1);
        var acovf = new Vector(6);
        Real mean = AutocovarianceFunctions.Autocovariances(x.AsSpan(), acovf.AsSpan(), 5, false);
        Real[] expected = [8.25, 6.416667, 4.25, 1.75, -1.08333, -4.25];
        if (Math.Abs(mean - 5.5) > 1.0e-6)
        {
            Assert.Fail($"Mean: \n    calculated:   {mean}\n    expected:     {5.5}");
        }

        Vector delta = acovf - new Vector(expected);
        if (ArrayFunctions.DotProduct(delta, delta) > 1.0e-6)
        {
            Assert.Fail($"Autocovariances: \n    calculated:   {acovf}\n    expected:     {new Vector(expected)}");
        }
    }

    [Fact]
    public void TestAutoCorrelations()
    {
        // Testing auto-correlations...
        var x = new Vector(10, 1, 1);
        var acorf = new Vector(6);
        Real mean = AutocovarianceFunctions.Autocorrelations(x.AsSpan(), acorf.AsSpan(), 5, true);
        Real[] expected = [9.166667, 0.77777778, 0.51515152, 0.21212121, -0.13131313, -0.51515152];
        if (Math.Abs(mean - 5.5) > 1.0e-6)
        {
            Assert.Fail($"Mean: \n    calculated:   {mean}\n    expected:     {5.5}");
        }

        Vector delta = acorf - new Vector(expected);
        if (ArrayFunctions.DotProduct(delta, delta) > 1.0e-6)
        {
            Assert.Fail($"Autocovariances: \n    calculated:   {acorf}\n    expected:     {new Vector(expected)}");
        }

        delta = x - new Vector(10, -4.5, 1);
        if (ArrayFunctions.DotProduct(delta, delta) > 1.0e-6)
        {
            Assert.Fail($"Centering: \n    calculated:   {x}\n    expected:     {new Vector(10, -4.5, 1)}");
        }
    }
}
