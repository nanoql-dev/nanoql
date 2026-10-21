// SPDX-FileCopyrightText: Copyright (C) 2004 Decillion Pty(Ltd)
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/money.hpp ql/money.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL;

/// <summary>QuantLib's free functions of <c>money.hpp</c>: the closeness of two amounts.</summary>
public static class MoneyFunctions
{
    /// <summary>
    /// Whether two amounts are close within <paramref name="n"/> ulps, as <see cref="ComparisonFunctions.Close(double,
    /// double, int)"/> compares their values, converting as the context's money settings say.
    /// </summary>
    /// <param name="m1">The first amount.</param>
    /// <param name="m2">The second amount.</param>
    /// <param name="context">The evaluation context, whose money settings and exchange rates are read.</param>
    /// <param name="n">The number of ulps.</param>
    /// <returns>Whether the values, once converted, are close.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The currencies differ under <see cref="MoneyConversionType.NoConversion"/>; no base currency is set; or an
    /// exchange rate can't be found.
    /// </exception>
    public static bool Close(Money m1, Money m2, EvaluationContext context, int n = 42)
    {
        ArgumentNullException.ThrowIfNull(context);
        return m1.Apply(m2, context, (x, y) => ComparisonFunctions.Close(x, y, n));
    }

    /// <summary>
    /// Whether two amounts are close enough within <paramref name="n"/> ulps, as
    /// <see cref="ComparisonFunctions.CloseEnough(double, double, int)"/> compares their values, converting as the
    /// context's money settings say.
    /// </summary>
    /// <param name="m1">The first amount.</param>
    /// <param name="m2">The second amount.</param>
    /// <param name="context">The evaluation context, whose money settings and exchange rates are read.</param>
    /// <param name="n">The number of ulps.</param>
    /// <returns>Whether the values, once converted, are close enough.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The currencies differ under <see cref="MoneyConversionType.NoConversion"/>; no base currency is set; or an
    /// exchange rate can't be found.
    /// </exception>
    public static bool CloseEnough(Money m1, Money m2, EvaluationContext context, int n = 42)
    {
        ArgumentNullException.ThrowIfNull(context);
        return m1.Apply(m2, context, (x, y) => ComparisonFunctions.CloseEnough(x, y, n));
    }
}
