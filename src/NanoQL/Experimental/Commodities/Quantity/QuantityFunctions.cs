// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/quantity.hpp ql/experimental/commodities/quantity.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>QuantLib's free functions of <c>quantity.hpp</c>: the closeness of two quantities.</summary>
[Experimental("NQL0001")]
public static class QuantityFunctions
{
    /// <summary>
    /// Whether two quantities are close within <paramref name="n"/> ulps, as <see cref="ComparisonFunctions.Close(double,
    /// double, int)"/> compares their amounts, converting as the context's quantity settings say.
    /// </summary>
    /// <param name="q1">The first quantity.</param>
    /// <param name="q2">The second quantity.</param>
    /// <param name="context">The evaluation context, whose quantity settings and unit conversions are read.</param>
    /// <param name="n">The number of ulps.</param>
    /// <returns>Whether the amounts, once converted, are close.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The units differ under <see cref="QuantityConversionType.NoConversion"/>; no base unit is set; or a conversion
    /// can't be found.
    /// </exception>
    public static bool Close(Quantity q1, Quantity q2, EvaluationContext context, int n = 42)
    {
        ArgumentNullException.ThrowIfNull(context);
        return q1.Apply(q2, context, (x, y) => ComparisonFunctions.Close(x, y, n));
    }

    /// <summary>
    /// Whether two quantities are close enough within <paramref name="n"/> ulps, as
    /// <see cref="ComparisonFunctions.CloseEnough(double, double, int)"/> compares their amounts, converting as the
    /// context's quantity settings say.
    /// </summary>
    /// <param name="q1">The first quantity.</param>
    /// <param name="q2">The second quantity.</param>
    /// <param name="context">The evaluation context, whose quantity settings and unit conversions are read.</param>
    /// <param name="n">The number of ulps.</param>
    /// <returns>Whether the amounts, once converted, are close enough.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The units differ under <see cref="QuantityConversionType.NoConversion"/>; no base unit is set; or a conversion
    /// can't be found.
    /// </exception>
    public static bool CloseEnough(Quantity q1, Quantity q2, EvaluationContext context, int n = 42)
    {
        ArgumentNullException.ThrowIfNull(context);
        return q1.Apply(q2, context, (x, y) => ComparisonFunctions.CloseEnough(x, y, n));
    }
}
