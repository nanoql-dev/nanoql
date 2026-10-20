// SPDX-FileCopyrightText: Copyright (C) 2005 Joseph Wang
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/dividend.hpp ql/cashflows/dividend.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>A predetermined cash amount paid on a stock.</summary>
public sealed class FixedDividend : Dividend
{
    private readonly double amount;

    /// <summary>Initializes a new instance of the <see cref="FixedDividend"/> class.</summary>
    /// <param name="amount">The amount.</param>
    /// <param name="date">The payment date.</param>
    /// <exception cref="ArgumentException"><paramref name="date"/> is <c>default(Date)</c>.</exception>
    public FixedDividend(double amount, Date date)
        : base(date)
    {
        this.amount = amount;
    }

    /// <summary>The amount paid.</summary>
    /// <param name="context">The evaluation context, which a fixed amount doesn't read.</param>
    /// <returns>The amount.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override double Amount(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return amount;
    }

    /// <summary>The amount paid, whatever the underlying's value.</summary>
    /// <param name="underlying">The underlying's value, which a fixed amount doesn't read.</param>
    /// <returns>The amount.</returns>
    public override double Amount(double underlying) => amount;
}
