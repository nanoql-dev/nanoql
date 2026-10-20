// SPDX-FileCopyrightText: Copyright (C) 2005 Joseph Wang
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/dividend.hpp ql/cashflows/dividend.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>The free functions of QuantLib's <c>dividend.hpp</c>.</summary>
public static class DividendFunctions
{
    /// <summary>A schedule of fixed dividends from their dates and amounts.</summary>
    /// <param name="dividendDates">The payment dates.</param>
    /// <param name="dividends">The amounts, one per date.</param>
    /// <returns>The dividends, in the given order.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The dates and amounts differ in number, or a date is <c>default(Date)</c>.
    /// </exception>
    public static ImmutableArray<Dividend> DividendVector(IEnumerable<Date> dividendDates, IEnumerable<double> dividends)
    {
        ArgumentNullException.ThrowIfNull(dividendDates);
        ArgumentNullException.ThrowIfNull(dividends);
        Date[] dates = [.. dividendDates];
        double[] amounts = [.. dividends];
        if (!(dates.Length == amounts.Length))
        {
            throw new ArgumentException("size mismatch between dividend dates and amounts", nameof(dividends));
        }

        ImmutableArray<Dividend>.Builder items = ImmutableArray.CreateBuilder<Dividend>(dates.Length);
        for (int i = 0; i < dates.Length; ++i)
        {
            Date.ThrowIfDefault(dates[i], nameof(dividendDates));
            items.Add(new FixedDividend(amounts[i], dates[i]));
        }

        return items.MoveToImmutable();
    }
}
