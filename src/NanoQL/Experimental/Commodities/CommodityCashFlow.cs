// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/commoditycashflow.hpp ql/experimental/commodities/commoditycashflow.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>An energy swap's payment for one pricing period, discounted and undiscounted, in the base currency and the
/// payment's.</summary>
/// <remarks>
/// <see cref="Amount"/> is the undiscounted amount, as <see cref="CashFlow.Amount"/> is documented ("the actual amount
/// paid at the cash flow date", <c>cashflow.hpp:62-66</c>); QuantLib's <c>amount()</c> returns the discounted one
/// (<c>commoditycashflow.hpp:56</c>), so a consumer reading these as cash flows discounts them twice. The discounted
/// amount is <see cref="DiscountedAmount"/> (the S-xrt plan's XRR9; <c>Divergences/CommodityCashFlowAmountTests.cs</c>).
/// </remarks>
[Experimental("NQL0001")]
public sealed class CommodityCashFlow : CashFlow
{
    private readonly Date date;

    /// <summary>Initializes a new instance of the <see cref="CommodityCashFlow"/> class.</summary>
    /// <param name="date">The payment date.</param>
    /// <param name="discountedAmount">The amount discounted to the evaluation date, in the base currency.</param>
    /// <param name="undiscountedAmount">The amount, in the base currency.</param>
    /// <param name="discountedPaymentAmount">The leg-discounted amount, in the payment's currency.</param>
    /// <param name="undiscountedPaymentAmount">The amount, in the payment's currency.</param>
    /// <param name="discountFactor">The discount factor of the base amounts.</param>
    /// <param name="paymentDiscountFactor">The discount factor of the payment's leg.</param>
    /// <param name="finalized">Whether the payment date is on or before the evaluation date.</param>
    /// <exception cref="ArgumentException"><paramref name="date"/> is <c>default(Date)</c>.</exception>
    public CommodityCashFlow(
        Date date,
        Money discountedAmount,
        Money undiscountedAmount,
        Money discountedPaymentAmount,
        Money undiscountedPaymentAmount,
        double discountFactor,
        double paymentDiscountFactor,
        bool finalized)
    {
        Date.ThrowIfDefault(date, nameof(date));
        this.date = date;
        DiscountedAmount = discountedAmount;
        UndiscountedAmount = undiscountedAmount;
        DiscountedPaymentAmount = discountedPaymentAmount;
        UndiscountedPaymentAmount = undiscountedPaymentAmount;
        DiscountFactor = discountFactor;
        PaymentDiscountFactor = paymentDiscountFactor;
        Finalized = finalized;
    }

    /// <inheritdoc/>
    public override Date Date => date;

    /// <summary>Gets the base currency the base amounts are in; <see langword="null"/> for QuantLib's empty one.</summary>
    public Currency? Currency => DiscountedAmount.Currency;

    /// <summary>Gets the amount discounted to the evaluation date, in the base currency.</summary>
    public Money DiscountedAmount { get; }

    /// <summary>Gets the amount, in the base currency.</summary>
    public Money UndiscountedAmount { get; }

    /// <summary>Gets the leg-discounted amount, in the payment's currency.</summary>
    public Money DiscountedPaymentAmount { get; }

    /// <summary>Gets the amount, in the payment's currency.</summary>
    public Money UndiscountedPaymentAmount { get; }

    /// <summary>Gets the discount factor of the base amounts.</summary>
    public double DiscountFactor { get; }

    /// <summary>Gets the discount factor of the payment's leg.</summary>
    public double PaymentDiscountFactor { get; }

    /// <summary>Gets a value indicating whether the payment date is on or before the evaluation date.</summary>
    public bool Finalized { get; }

    /// <inheritdoc/>
    /// <remarks>The undiscounted amount in the base currency, where QuantLib's returns the discounted one.</remarks>
    public override double Amount(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return UndiscountedAmount.Value;
    }
}
