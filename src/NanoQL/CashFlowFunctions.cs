// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflow.hpp ql/cashflow.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL;

/// <summary>QuantLib's free functions of cash flows (<c>cashflow.hpp</c>).</summary>
public static class CashFlowFunctions
{
    /// <summary>
    /// Compares two cash flows by date, the earlier first: QuantLib's <c>earlier_than&lt;CashFlow&gt;</c>, a
    /// <see cref="Comparison{T}"/>, which also serves for QuantLib's form on shared pointers, since a reference is the
    /// cash flow itself.
    /// </summary>
    /// <param name="c1">The first cash flow.</param>
    /// <param name="c2">The second cash flow.</param>
    /// <returns>A negative number when <paramref name="c1"/> pays before <paramref name="c2"/>, zero on the same date,
    /// and a positive number after it.</returns>
    /// <exception cref="ArgumentNullException">A cash flow is <see langword="null"/>.</exception>
    public static int EarlierThan(CashFlow c1, CashFlow c2)
    {
        ArgumentNullException.ThrowIfNull(c1);
        ArgumentNullException.ThrowIfNull(c2);
        return c1.Date.CompareTo(c2.Date);
    }
}
