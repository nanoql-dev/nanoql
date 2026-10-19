// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL;

/// <summary>
/// C++ conversions whose result the standard leaves undefined, as MSVC computes them on x64, the platform QuantLib's
/// vectors are generated on, so a port refuses or computes where QuantLib does.
/// </summary>
internal static class MsvcConversions
{
    /// <summary>
    /// C++'s conversion of a <c>double</c> to a 32-bit <c>int</c> (<c>Integer(x)</c>, <c>static_cast&lt;int&gt;</c>):
    /// truncation toward zero, and, for NaN and for a value outside the integers, where the standard leaves the result
    /// undefined, the smallest integer, the result of MSVC's <c>cvttsd2si</c>.
    /// </summary>
    /// <param name="x">The value.</param>
    /// <returns>The integer.</returns>
    /// <remarks>
    /// .NET's own conversion saturates instead (NaN to 0, a large value to <see cref="int.MaxValue"/>), so it isn't
    /// used where the value can leave the integers.
    /// </remarks>
    internal static int ToInt32(double x) => x > -2147483649.0 && x < 2147483648.0 ? (int)x : int.MinValue;
}
