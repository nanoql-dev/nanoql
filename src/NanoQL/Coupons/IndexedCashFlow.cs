// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2022 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/indexedcashflow.hpp ql/cashflows/indexedcashflow.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// A cash flow paying an index ratio: the notional times <c>I(T)/I(0)</c>, or times <c>I(T)/I(0) - 1</c> when only the
/// growth is paid. It isn't a coupon: it has no accruals.
/// </summary>
/// <remarks>
/// <para>
/// Meant for use inside an instrument that does the date adjustments: it takes its dates and doesn't change them.
/// Paying the whole ratio is a bond's setting, the growth only a swap's.
/// </para>
/// <para>
/// The amount is computed on each call, where QuantLib caches it (R1). An extension point: the CPI and zero-inflation
/// cash flows derive from it, as will an equity cash flow.
/// </para>
/// </remarks>
public class IndexedCashFlow : CashFlow
{
    private readonly double notional;
    private readonly Date? baseDate;
    private readonly Date fixingDate;
    private readonly Date paymentDate;

    /// <summary>Initializes a new instance of the <see cref="IndexedCashFlow"/> class.</summary>
    /// <param name="notional">The notional.</param>
    /// <param name="index">The index whose ratio is paid.</param>
    /// <param name="baseDate">The date of the base fixing, or <see langword="null"/>, QuantLib's <c>Date()</c>, for a
    /// derived flow that gives its base fixing otherwise.</param>
    /// <param name="fixingDate">The date of the fixing.</param>
    /// <param name="paymentDate">The payment date.</param>
    /// <param name="growthOnly">Whether only the growth, the ratio less one, is paid.</param>
    /// <exception cref="ArgumentNullException"><paramref name="index"/> is null, QuantLib's "no index
    /// provided".</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    public IndexedCashFlow(
        double notional,
        FinancialIndex index,
        Date? baseDate,
        Date fixingDate,
        Date paymentDate,
        bool growthOnly = false)
    {
        Date.ThrowIfDefault(baseDate, nameof(baseDate));
        Date.ThrowIfDefault(fixingDate, nameof(fixingDate));
        Date.ThrowIfDefault(paymentDate, nameof(paymentDate));
        this.notional = notional;
        Index = index ?? throw new ArgumentNullException(nameof(index), "no index provided");
        this.baseDate = baseDate;
        this.fixingDate = fixingDate;
        this.paymentDate = paymentDate;
        GrowthOnly = growthOnly;
    }

    /// <inheritdoc/>
    public override Date Date => paymentDate;

    /// <summary>Gets the notional.</summary>
    public virtual double Notional => notional;

    /// <summary>Gets the date of the fixing.</summary>
    public virtual Date FixingDate => fixingDate;

    /// <summary>Gets the index whose ratio is paid.</summary>
    public virtual FinancialIndex Index { get; }

    /// <summary>Gets a value indicating whether only the growth, the ratio less one, is paid.</summary>
    public virtual bool GrowthOnly { get; }

    /// <summary>The date of the base fixing.</summary>
    /// <returns>The date, or <see langword="null"/>, QuantLib's <c>Date()</c>, when none was given.</returns>
    public virtual Date? BaseDate() => baseDate;

    /// <summary>The base fixing: the index's fixing on the base date.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The fixing.</returns>
    /// <exception cref="NanoQLException">No base date was given, with <see cref="CPICashFlow"/>'s "no base date
    /// specified": QuantLib reads the index's fixing on <c>Date()</c>, which is undefined.</exception>
    public virtual double BaseFixing(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date d = BaseDate() ?? throw new NanoQLException("no base date specified");
        return Index.Fixing(d, context);
    }

    /// <summary>The index's fixing on the fixing date.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The fixing.</returns>
    public virtual double IndexFixing(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Index.Fixing(fixingDate, context);
    }

    /// <summary>The amount: the notional times the index ratio, less one when only the growth is paid.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The amount.</returns>
    public override double Amount(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        double i0 = BaseFixing(context);
        double i1 = IndexFixing(context);
        return GrowthOnly ? Notional * ((i1 / i0) - 1.0) : Notional * (i1 / i0);
    }
}
