// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0
// Own-tested: Extensions/CommoditySetTests.cs (a kind with no QuantLib presets, its builds checked by hand)

using System.Diagnostics.CodeAnalysis;
using NanoQL.Experimental;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A commodity payment term (<c>PaymentTerm</c>), which <see cref="NanoQL.Experimental.PaymentTerm"/> takes.
/// </summary>
[Experimental("NQL0001")]
public sealed record PaymentTermRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="PaymentTermRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public PaymentTermRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>
    /// Gets the payment term's name, its identity, or <see langword="null"/> to take the base's: the record's by
    /// default.
    /// </summary>
    public string? TermName { get; init; }

    /// <summary>
    /// Gets the event the payment date is counted from, or <see langword="null"/> to take the base's.
    /// </summary>
    public PaymentTermEventType? EventType { get; init; }

    /// <summary>Gets the days from the event to the payment, or <see langword="null"/> to take the base's.</summary>
    public int? OffsetDays { get; init; }

    /// <summary>
    /// Gets the calendar the payment date is adjusted on, or <see langword="null"/> to take the base's.
    /// </summary>
    public string? Calendar { get; init; }

    // The record over a lower record of its chain: each field left out the lower's, a tied field with its field.
    internal PaymentTermRecord Over(PaymentTermRecord lower) =>
        this with
        {
            TermName = TermName ?? lower.TermName,
            EventType = EventType ?? lower.EventType,
            OffsetDays = OffsetDays ?? lower.OffsetDays,
            Calendar = Calendar ?? lower.Calendar,
        };

    /// <inheritdoc/>
    internal override IEnumerable<string?> References() =>
    [
        Calendar,
    ];

    /// <inheritdoc/>
    internal override IEnumerable<string> Given() =>
        new (string Field, bool Given)[]
        {
            (nameof(TermName), TermName is not null),
            (nameof(EventType), EventType is not null),
            (nameof(OffsetDays), OffsetDays is not null),
            (nameof(Calendar), Calendar is not null),
        }.Where(f => f.Given).Select(f => f.Field);

    // The record's fields under another name, with no base.
    internal PaymentTermRecord Named(string name) =>
        new(name)
        {
            TermName = TermName,
            EventType = EventType,
            OffsetDays = OffsetDays,
            Calendar = Calendar,
        };
}
