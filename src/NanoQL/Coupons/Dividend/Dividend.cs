// SPDX-FileCopyrightText: Copyright (C) 2005 Joseph Wang
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/dividend.hpp ql/cashflows/dividend.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>A predetermined cash flow paid on a stock: a dividend.</summary>
/// <remarks>
/// An extension point, as in QuantLib: the fixed and fractional dividends derive from it. A dividend is immutable;
/// the engines take a schedule of them (<c>DividendSchedule</c>, an
/// <see cref="System.Collections.Immutable.ImmutableArray{T}"/> of dividends).
/// </remarks>
public abstract class Dividend : CashFlow
{
    private readonly Date date;

    /// <summary>Initializes a new instance of the <see cref="Dividend"/> class.</summary>
    /// <param name="date">The payment date.</param>
    /// <exception cref="ArgumentException"><paramref name="date"/> is <c>default(Date)</c>.</exception>
    protected Dividend(Date date)
    {
        Date.ThrowIfDefault(date, nameof(date));
        this.date = date;
    }

    /// <inheritdoc/>
    public override Date Date => date;

    /// <summary>The amount paid, given the underlying's value.</summary>
    /// <param name="underlying">The underlying's value.</param>
    /// <returns>The amount.</returns>
    public abstract double Amount(double underlying);
}
