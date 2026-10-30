// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// An Ibor index (<c>IborIndex</c>): one of QuantLib's presets by its class (<c>Euribor6M</c>,
/// <c>DailyTenorUSDLibor</c>, <c>BMAIndex</c>), usually its family at a tenor, or a user's own: an
/// <see cref="Indexes.IborIndex"/>, a <see cref="Indexes.Libor"/> or a <see cref="Indexes.CustomIborIndex"/>
/// by its <see cref="Style"/>.
/// </summary>
public sealed record IborIndexRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="IborIndexRecord"/> class.</summary>
    /// <param name="name">The record's name: a preset's class name, or a user's own.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public IborIndexRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>Gets the family name the index reports (QuantLib's <c>familyName</c>), or <see langword="null"/> to take the base's.</summary>
    public string? Family { get; init; }

    /// <summary>Gets the tenor, or <see langword="null"/> to take the base's.</summary>
    public Period? Tenor { get; init; }

    /// <summary>Gets the fixing days, or <see langword="null"/> to take the base's.</summary>
    public int? FixingDays { get; init; }

    /// <summary>Gets the currency's code, or <see langword="null"/> to take the base's.</summary>
    public string? Currency { get; init; }

    /// <summary>Gets the fixing calendar's name, or <see langword="null"/> to take the base's.</summary>
    public string? FixingCalendar { get; init; }

    /// <summary>Gets the business-day convention, or <see langword="null"/> to take the base's.</summary>
    public BusinessDayConvention? Convention { get; init; }

    /// <summary>Gets whether the end-of-month rule applies, or <see langword="null"/> to take the base's.</summary>
    public bool? EndOfMonth { get; init; }

    /// <summary>Gets the day counter's name, or <see langword="null"/> to take the base's.</summary>
    public string? DayCounter { get; init; }

    /// <summary>Gets a user's own index's class, or the base's, or <see langword="null"/> to take the base's.</summary>
    public IborIndexStyle? Style { get; init; }

    /// <summary>Gets a Libor's financial-centre calendar's name, which London's joins, or <see langword="null"/> to take the base's.</summary>
    public string? FinancialCenterCalendar { get; init; }

    /// <summary>Gets a custom index's value calendar's name, or <see langword="null"/> to take the base's.</summary>
    public string? ValueCalendar { get; init; }

    /// <summary>Gets a custom index's maturity calendar's name, or <see langword="null"/> to take the base's.</summary>
    public string? MaturityCalendar { get; init; }
}
