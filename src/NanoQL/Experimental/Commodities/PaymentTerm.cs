// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/paymentterm.hpp ql/experimental/commodities/paymentterm.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A payment term, such as "Pricing end + 5 days": an offset in days from an event, on a calendar.</summary>
/// <remarks>
/// <para>
/// Immutable. QuantLib's empty <c>PaymentTerm()</c> is a <c>PaymentTerm?</c> null. QuantLib keeps a process-wide
/// registry keyed by name, so a second construction with a known name returns the first construction's event, offset
/// and calendar whatever it was given (<c>paymentterm.cpp:27-40</c>). There is no registry here, so every term holds the
/// data it is given, a documented divergence (<c>Divergences/CommodityRegistryTests.cs</c>).
/// </para>
/// <para>
/// Two terms are equal when their names are, compared ordinally, as QuantLib's <c>operator==</c> compares them
/// (<c>paymentterm.hpp:113-115</c>); the hash is the name's.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class PaymentTerm : IEquatable<PaymentTerm>
{
    /// <summary>Initializes a new instance of the <see cref="PaymentTerm"/> class.</summary>
    /// <param name="name">The term's name, such as "Pricing end + 5 days".</param>
    /// <param name="eventType">The event the term counts from.</param>
    /// <param name="offsetDays">The days from the event to the payment, before the calendar's adjustment.</param>
    /// <param name="calendar">The calendar adjusting the payment date.</param>
    /// <exception cref="ArgumentNullException">The name or the calendar is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="eventType"/> isn't a member of its enum.</exception>
    public PaymentTerm(string name, PaymentTermEventType eventType, int offsetDays, Calendar calendar)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(calendar);
        EnumArguments.Check(eventType, nameof(eventType));
        Name = name;
        EventType = eventType;
        OffsetDays = offsetDays;
        Calendar = calendar;
    }

    /// <summary>Gets the term's name, such as "Pricing end + 5 days".</summary>
    public string Name { get; }

    /// <summary>Gets the event the term counts from.</summary>
    public PaymentTermEventType EventType { get; }

    /// <summary>Gets the days from the event to the payment, before the calendar's adjustment.</summary>
    public int OffsetDays { get; }

    /// <summary>Gets the calendar adjusting the payment date.</summary>
    public Calendar Calendar { get; }

    /// <summary>Whether two terms are equal: their names are.</summary>
    /// <param name="left">A term, or <see langword="null"/>.</param>
    /// <param name="right">Another term, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both are null or their names are equal.</returns>
    public static bool operator ==(PaymentTerm? left, PaymentTerm? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Whether two terms differ.</summary>
    /// <param name="left">A term, or <see langword="null"/>.</param>
    /// <param name="right">Another term, or <see langword="null"/>.</param>
    /// <returns>The negation of <c>==</c>.</returns>
    public static bool operator !=(PaymentTerm? left, PaymentTerm? right) => !(left == right);

    /// <summary>The payment date for an event's date: the date plus the offset, adjusted on the calendar (Following).</summary>
    /// <param name="date">The event's date.</param>
    /// <returns>The payment date.</returns>
    /// <exception cref="ArgumentException"><paramref name="date"/> is <c>default(Date)</c>.</exception>
    public Date GetPaymentDate(Date date)
    {
        Date.ThrowIfDefault(date, nameof(date));
        return Calendar.Adjust(date + OffsetDays);
    }

    /// <inheritdoc/>
    public bool Equals(PaymentTerm? other) =>
        other is not null && string.Equals(Name, other.Name, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PaymentTerm other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Name);

    /// <summary>The term's name, as QuantLib's <c>operator&lt;&lt;</c> writes it.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => Name;
}
