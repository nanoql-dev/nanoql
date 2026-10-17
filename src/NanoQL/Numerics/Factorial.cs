// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/factorial.hpp ql/math/factorial.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>Factorial numbers: n! and ln(n!), tabulated up to 27! and from the gamma function above.</summary>
public static class Factorial
{
    // QuantLib's tabulated count, the table's last index.
    private const uint tabulated = 27;

    private static ReadOnlySpan<double> FirstFactorials =>
    [
        1.0, 1.0,
        2.0, 6.0,
        24.0, 120.0,
        720.0, 5040.0,
        40320.0, 362880.0,
        3628800.0, 39916800.0,
        479001600.0, 6227020800.0,
        87178291200.0, 1307674368000.0,
        20922789888000.0, 355687428096000.0,
        6402373705728000.0, 121645100408832000.0,
        2432902008176640000.0, 51090942171709440000.0,
        1124000727777607680000.0, 25852016738884976640000.0,
        620448401733239439360000.0, 15511210043330985984000000.0,
        403291461126605635584000000.0, 10888869450418352160768000000.0,
    ];

    /// <summary>n!: tabulated up to 27!, and exp(ln Γ(n + 1)) above.</summary>
    /// <param name="n">The number, QuantLib's <c>Natural</c>.</param>
    /// <returns>n!, infinite from 171 on.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    public static double Get(int n)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        return Get((uint)n);
    }

    /// <summary>ln(n!): tabulated up to 27!, and ln Γ(n + 1) above.</summary>
    /// <param name="n">The number, QuantLib's <c>Natural</c>.</param>
    /// <returns>ln(n!).</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    public static double Ln(int n)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        return Ln((uint)n);
    }

    // Factorial::get on QuantLib's Natural, 32 bits unsigned, for callers holding QuantLib's unsigned values (Poisson's
    // BigNatural): i + 1 wraps at 2^32, as QuantLib's does, so i = 4294967295 reaches logValue(0), which throws.
    internal static double Get(uint i)
    {
        if (i <= tabulated)
        {
            return FirstFactorials[(int)i];
        }
        else
        {
            return Math.Exp(new GammaFunction().LogValue(unchecked(i + 1)));
        }
    }

    // Factorial::ln on QuantLib's Natural, as Get(uint) is.
    internal static double Ln(uint i)
    {
        if (i <= tabulated)
        {
            return Math.Log(FirstFactorials[(int)i]);
        }
        else
        {
            return new GammaFunction().LogValue(unchecked(i + 1));
        }
    }
}
