// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A swap index family (<c>SwapIndex</c>): one of QuantLib's families by its class (<c>EuriborSwapIsdaFixA</c>), or
/// a user's own, built at a tenor, its floating leg's index an Ibor index's name.
/// </summary>
public sealed record SwapIndexRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="SwapIndexRecord"/> class.</summary>
    /// <param name="name">The record's name: a family's class name, or a user's own.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public SwapIndexRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>Gets the family name the index reports, or <see langword="null"/> to take the base's.</summary>
    public string? Family { get; init; }

    /// <summary>Gets the settlement days, or <see langword="null"/> to take the base's.</summary>
    public int? SettlementDays { get; init; }

    /// <summary>Gets the currency's code, or <see langword="null"/> to take the base's.</summary>
    public string? Currency { get; init; }

    /// <summary>Gets the fixing calendar's name, or <see langword="null"/> to take the base's.</summary>
    public string? FixingCalendar { get; init; }

    /// <summary>Gets the fixed leg's tenor, or <see langword="null"/> to take the base's.</summary>
    public Period? FixedLegTenor { get; init; }

    /// <summary>Gets the fixed leg's business-day convention, or <see langword="null"/> to take the base's.</summary>
    public BusinessDayConvention? FixedLegConvention { get; init; }

    /// <summary>Gets the fixed leg's day counter's name, or <see langword="null"/> to take the base's.</summary>
    public string? FixedLegDayCounter { get; init; }

    /// <summary>Gets the floating leg's Ibor index's name, or a family's, or <see langword="null"/> to take the base's.</summary>
    public string? IborIndex { get; init; }

    /// <summary>Gets the tenor of the family <see cref="IborIndex"/> names, or <see langword="null"/> to take the base's
    /// or, for an index, none.</summary>
    public Period? IborIndexTenor { get; init; }

    /// <summary>Gets the floating leg's Ibor index's name, or a family's, up to and including one year, where it differs,
    /// or <see langword="null"/> to take the base's.</summary>
    public string? ShortIborIndex { get; init; }

    /// <summary>Gets the tenor of the family <see cref="ShortIborIndex"/> names, or <see langword="null"/> to take the
    /// base's or, for an index, none.</summary>
    public Period? ShortIborIndexTenor { get; init; }

    /// <summary>Gets the fixed leg's tenor up to and including one year, where it differs, or <see langword="null"/> to take the base's.</summary>
    public Period? ShortFixedLegTenor { get; init; }
}
