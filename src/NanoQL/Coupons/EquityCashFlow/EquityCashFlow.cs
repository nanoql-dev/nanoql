// SPDX-FileCopyrightText: Copyright (C) 2023 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/equitycashflow.hpp ql/cashflows/equitycashflow.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// A cash flow paying an equity index's ratio, <c>I(T)/I(0)</c>, or its growth, <c>I(T)/I(0) - 1</c>, the default,
/// times the notional; with a pricer, the ratio the pricer gives, such as a quanto-adjusted one.
/// </summary>
/// <remarks>
/// The flow holds its pricer, given at construction or by <see cref="SetPricer"/>, which returns a copy holding it,
/// where QuantLib's <c>setPricer</c> sets it in place. Without one it pays the index's own ratio, as QuantLib's.
/// </remarks>
public sealed class EquityCashFlow : IndexedCashFlow
{
    /// <summary>Initializes a new instance of the <see cref="EquityCashFlow"/> class.</summary>
    /// <param name="notional">The notional.</param>
    /// <param name="index">The equity index.</param>
    /// <param name="baseDate">The date of the base fixing.</param>
    /// <param name="fixingDate">The date of the fixing.</param>
    /// <param name="paymentDate">The payment date.</param>
    /// <param name="growthOnly">Whether only the growth, the ratio less one, is paid.</param>
    /// <param name="pricer">The pricer, or <see langword="null"/> for the index's own ratio; an argument QuantLib's
    /// constructor doesn't have, since its <c>setPricer</c> changes the flow in place.</param>
    /// <exception cref="ArgumentNullException"><paramref name="index"/> is null, QuantLib's "no index
    /// provided".</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    public EquityCashFlow(
        double notional,
        EquityIndex index,
        Date baseDate,
        Date fixingDate,
        Date paymentDate,
        bool growthOnly = true,
        EquityCashFlowPricer? pricer = null)
        : base(notional, index, baseDate, fixingDate, paymentDate, growthOnly)
    {
        EquityIndex = index;
        Pricer = pricer;
    }

    /// <summary>Gets the equity index.</summary>
    public EquityIndex EquityIndex { get; }

    /// <summary>Gets the pricer, or <see langword="null"/> for none.</summary>
    public EquityCashFlowPricer? Pricer { get; private init; }

    /// <summary>The amount: the notional times the pricer's ratio, or without a pricer the index's own.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The amount.</returns>
    public override double Amount(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Pricer is null ? base.Amount(context) : Notional * Pricer.Price(this, context);
    }

    /// <summary>A copy of the flow holding another pricer.</summary>
    /// <param name="pricer">The pricer, or <see langword="null"/> for none.</param>
    /// <returns>The copy; this flow is unchanged.</returns>
    public EquityCashFlow SetPricer(EquityCashFlowPricer? pricer) => new(
        Notional, EquityIndex, BaseDate()!.Value, FixingDate, Date, GrowthOnly, pricer);
}
