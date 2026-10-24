// SPDX-FileCopyrightText: (C) Copyright John Maddock 2008.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/next.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's floating-point neighbours (<c>special_functions/next.hpp</c>) for <c>double</c>: the next
/// representable value above or below an argument.
/// </summary>
/// <remarks>
/// Boost computes the neighbour as the argument plus or minus 2^(e − 53), e its <c>frexp</c> exponent less one at a
/// power of two on the side it crosses, or the smallest subnormal where that underflows, and shifts arguments whose
/// last bit is subnormal up and back: for every finite argument the adjacent <c>double</c>, which
/// <see cref="Math.BitIncrement"/> and <see cref="Math.BitDecrement"/> return. Boost's errors under the default policy
/// throw: a NaN argument, a domain error on the caller's argument, <see cref="ArgumentOutOfRangeException"/>; a step
/// past the largest finite value, an overflow, <see cref="NanoQLException"/>.
/// </remarks>
internal static class NextFunctions
{
    /// <summary><c>float_next</c>: the next representable value above <paramref name="val"/>.</summary>
    /// <param name="val">The argument.</param>
    /// <returns>The next value above; +∞ at +∞, −<see cref="double.MaxValue"/> at −∞.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="val"/> is NaN.</exception>
    /// <exception cref="NanoQLException"><paramref name="val"/> is the largest finite value.</exception>
    internal static double FloatNext(double val)
    {
        const string function = "float_next<double>(double)";
        if (double.IsInfinity(val))
        {
            return val < 0 ? -double.MaxValue : val;
        }
        else if (double.IsNaN(val))
        {
            throw new ArgumentOutOfRangeException(
                nameof(val), $"Error in function {function}: Argument must be finite, but got {Show(val)}");
        }

        if (val >= double.MaxValue)
        {
            throw new NanoQLException($"Error in function {function}: Overflow Error");
        }

        return Math.BitIncrement(val);
    }

    /// <summary><c>float_prior</c>: the next representable value below <paramref name="val"/>.</summary>
    /// <param name="val">The argument.</param>
    /// <returns>The next value below; −∞ at −∞, <see cref="double.MaxValue"/> at +∞.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="val"/> is NaN.</exception>
    /// <exception cref="NanoQLException"><paramref name="val"/> is the lowest finite value.</exception>
    internal static double FloatPrior(double val)
    {
        const string function = "float_prior<double>(double)";
        if (double.IsInfinity(val))
        {
            return val > 0 ? double.MaxValue : val;
        }
        else if (double.IsNaN(val))
        {
            throw new ArgumentOutOfRangeException(
                nameof(val), $"Error in function {function}: Argument must be finite, but got {Show(val)}");
        }

        if (val <= -double.MaxValue)
        {
            throw new NanoQLException($"Error in function {function}: Overflow Error");
        }

        return Math.BitDecrement(val);
    }

    private static string Show(double value) => value.ToString(CultureInfo.InvariantCulture);
}
