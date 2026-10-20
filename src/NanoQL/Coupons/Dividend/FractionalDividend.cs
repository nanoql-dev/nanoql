// SPDX-FileCopyrightText: Copyright (C) 2005 Joseph Wang
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/dividend.hpp ql/cashflows/dividend.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>A predetermined fraction of the underlying's value, paid on a stock.</summary>
public sealed class FractionalDividend : Dividend
{
    /// <summary>Initializes a new instance of the <see cref="FractionalDividend"/> class, with no nominal.</summary>
    /// <param name="rate">The fraction of the underlying paid.</param>
    /// <param name="date">The payment date.</param>
    /// <exception cref="ArgumentException"><paramref name="date"/> is <c>default(Date)</c>.</exception>
    public FractionalDividend(double rate, Date date)
        : base(date)
    {
        Rate = rate;
        Nominal = null;
    }

    /// <summary>Initializes a new instance of the <see cref="FractionalDividend"/> class.</summary>
    /// <param name="rate">The fraction of the underlying paid.</param>
    /// <param name="nominal">The nominal the cash amount is the fraction of.</param>
    /// <param name="date">The payment date.</param>
    /// <exception cref="ArgumentException"><paramref name="date"/> is <c>default(Date)</c>.</exception>
    public FractionalDividend(double rate, double nominal, Date date)
        : base(date)
    {
        Rate = rate;
        Nominal = nominal;
    }

    /// <summary>Gets the fraction of the underlying paid.</summary>
    public double Rate { get; }

    /// <summary>Gets the nominal, or <see langword="null"/> where none was given.</summary>
    public double? Nominal { get; }

    /// <summary>The amount paid: the rate times the nominal.</summary>
    /// <param name="context">The evaluation context, which the amount doesn't read.</param>
    /// <returns>The amount.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">No nominal was given.</exception>
    public override double Amount(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        double nominal = Nominal ?? throw new NanoQLException("no nominal given");
        return Rate * nominal;
    }

    /// <summary>The amount paid, given the underlying's value: the rate times that value.</summary>
    /// <param name="underlying">The underlying's value.</param>
    /// <returns>The amount.</returns>
    public override double Amount(double underlying) => Rate * underlying;
}
