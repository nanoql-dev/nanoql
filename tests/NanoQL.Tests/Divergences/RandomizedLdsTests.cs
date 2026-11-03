// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The randomized sequence's modulo: QuantLib wraps a coordinate's sum only where it exceeds 1.0, so a sum of exactly
/// 1.0 stays outside [0, 1); nanoql wraps it to 0.0. With a low-discrepancy and a pseudo-random stand-in each
/// returning 0.5 in every coordinate, QuantLib's point is (1, 1, 1), nanoql's (0, 0, 0). Elsewhere QuantLib's values,
/// bit for bit (the harness vectors).
/// </summary>
public class RandomizedLdsTests
{
    [Fact]
    public void ASumOfExactlyOneWrapsToZero()
    {
        var g = new RandomizedLDS<Half, Half>(new Half(3), new Half(3));
        Assert.Equal([0.0, 0.0, 0.0], g.NextSequence().Value.ToArray()); // QuantLib 1, 1, 1
    }

    // a sequence generator returning 0.5 in every coordinate
    private sealed class Half(int dimension) : IRsg
    {
        private readonly double[] values = [.. Enumerable.Repeat(0.5, dimension)];

        public int Dimension => values.Length;

        public Sample<ReadOnlyMemory<double>> NextSequence() => new(values, 1.0);

        public Sample<ReadOnlyMemory<double>> LastSequence() => new(values, 1.0);

        public IRsg Clone() => new Half(values.Length);
    }
}
