// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Pascal's triangle on Windows (the S-core plan's CR7 (xiv)): QuantLib's coefficients are <c>BigNatural</c>, a 32-bit
/// <c>unsigned long</c> under MSVC, so its rows wrap from order 35; nanoql's are 64-bit, exact to order 67, Linux
/// QuantLib's values. QuantLib's values are its own on the wheel's platform (this run's <c>probes\c4_facts.cpp</c>,
/// <c>c4_facts.out</c>).
/// </summary>
public class PascalTriangleTests
{
    [Fact]
    public void FromOrderThirtyFiveTheRowsAreExact()
    {
        Assert.Equal(2333606220UL, PascalTriangle.Get(34)[17]);    // QuantLib's too
        Assert.Equal(4537567650UL, PascalTriangle.Get(35)[17]);    // QuantLib 242600354
        Assert.Equal(9075135300UL, PascalTriangle.Get(36)[18]);    // QuantLib 485200708
        Assert.Equal(14226520737620288370UL, PascalTriangle.Get(67)[33]);
    }

    [Fact]
    public void EachRowIsTheBinomialCoefficients()
    {
        for (int n = 0; n <= PascalTriangle.MaxOrder; ++n)
        {
            ulong c = 1;
            for (int k = 0; k <= n; ++k)
            {
                Assert.Equal(c, PascalTriangle.Get(n)[k]);

                // C(n, k + 1) = C(n, k)·(n − k)/(k + 1), in 128 bits
                c = (ulong)((UInt128)c * (ulong)(n - k) / (ulong)(k + 1));
            }
        }
    }
}
