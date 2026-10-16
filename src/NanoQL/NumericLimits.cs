// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL;

/// <summary>
/// The numeric limits QuantLib defines as macros in its platform header, under their macro names, so ported code reads
/// as QuantLib's does.
/// </summary>
internal static class NumericLimits
{
    /// <summary>QuantLib's <c>QL_MIN_INTEGER</c>: the smallest <see cref="int"/>.</summary>
    internal const int QL_MIN_INTEGER = int.MinValue;

    /// <summary>QuantLib's <c>QL_MAX_INTEGER</c>: the largest <see cref="int"/>.</summary>
    internal const int QL_MAX_INTEGER = int.MaxValue;

    /// <summary>QuantLib's <c>QL_MIN_REAL</c>: the most negative finite <see cref="double"/>, minus <c>QL_MAX_REAL</c>.</summary>
    internal const double QL_MIN_REAL = -double.MaxValue;

    /// <summary>QuantLib's <c>QL_MAX_REAL</c>: the largest finite <see cref="double"/>.</summary>
    internal const double QL_MAX_REAL = double.MaxValue;

    /// <summary>
    /// QuantLib's <c>QL_MIN_POSITIVE_REAL</c>: the smallest normal positive <see cref="double"/>, 2.2250738585072014E-308
    /// (<c>std::numeric_limits&lt;double&gt;::min()</c>), not <see cref="double.Epsilon"/>, which is subnormal.
    /// </summary>
    internal const double QL_MIN_POSITIVE_REAL = 2.2250738585072014E-308;

    /// <summary>
    /// QuantLib's <c>QL_EPSILON</c>: machine epsilon, the difference between 1 and the next double,
    /// 2.220446049250313E-16 (<c>std::numeric_limits&lt;double&gt;::epsilon()</c>), not <see cref="double.Epsilon"/>.
    /// </summary>
    internal const double QL_EPSILON = 2.220446049250313E-16;
}
