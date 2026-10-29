// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/pricingperiod.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A pricing period of an energy swap: its dates, its payment date and the quantity it delivers.</summary>
/// <remarks>Immutable. QuantLib's <c>PricingPeriods</c>, a vector of shared pointers, is a list of these.</remarks>
[Experimental("NQL0001")]
public sealed class PricingPeriod : DateInterval
{
    /// <summary>Initializes a new instance of the <see cref="PricingPeriod"/> class.</summary>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date, on or after the start date.</param>
    /// <param name="paymentDate">The payment date.</param>
    /// <param name="quantity">The quantity delivered over the period.</param>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The end date is before the start date.</exception>
    public PricingPeriod(Date startDate, Date endDate, Date paymentDate, Quantity quantity)
        : base(startDate, endDate)
    {
        Date.ThrowIfDefault(paymentDate, nameof(paymentDate));
        PaymentDate = paymentDate;
        Quantity = quantity;
    }

    /// <summary>Gets the payment date.</summary>
    public Date PaymentDate { get; }

    /// <summary>Gets the quantity delivered over the period.</summary>
    public Quantity Quantity { get; }
}
