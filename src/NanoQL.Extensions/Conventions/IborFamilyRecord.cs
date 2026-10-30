// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// An Ibor index family (<c>IborFamily</c>): one of QuantLib's families by its class (<c>Euribor</c>,
/// <c>USDLibor</c>), QuantLib building it at any tenor, its convention and end of month by the tenor's unit.
/// </summary>
public sealed record IborFamilyRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="IborFamilyRecord"/> class.</summary>
    /// <param name="name">The record's name: a family's class name, or a user's own.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public IborFamilyRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>Gets the family name the index reports, or <see langword="null"/> to take the base's.</summary>
    public string? Family { get; init; }

    /// <summary>Gets the fixing days, or <see langword="null"/> to take the base's.</summary>
    public int? FixingDays { get; init; }

    /// <summary>Gets the fixing days at a tenor of one day, where they differ, or <see langword="null"/> to take the base's.</summary>
    public int? FixingDaysAtOneDay { get; init; }

    /// <summary>Gets the currency's code, or <see langword="null"/> to take the base's.</summary>
    public string? Currency { get; init; }

    /// <summary>Gets the fixing calendar's name; a Libor's, London's, or <see langword="null"/> to take the base's.</summary>
    public string? FixingCalendar { get; init; }

    /// <summary>Gets a Libor's financial-centre calendar's name, or <see langword="null"/> to take the base's.</summary>
    public string? FinancialCenterCalendar { get; init; }

    /// <summary>Gets the day counter's name, or <see langword="null"/> to take the base's.</summary>
    public string? DayCounter { get; init; }

    /// <summary>Gets the tenor units the family takes, each with its convention and end of month; empty to take the base's.</summary>
    public ImmutableArray<IborFamilyUnit> Units { get; init; } = [];

    /// <inheritdoc/>
    public bool Equals(IborFamilyRecord? other) =>
        other is not null && base.Equals(other)
        && Family == other.Family
        && FixingDays == other.FixingDays
        && FixingDaysAtOneDay == other.FixingDaysAtOneDay
        && Currency == other.Currency
        && FixingCalendar == other.FixingCalendar
        && FinancialCenterCalendar == other.FinancialCenterCalendar
        && DayCounter == other.DayCounter
        && RecordLists.Equal(Units, other.Units);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(base.GetHashCode());
        hash.Add(Family);
        hash.Add(FixingDays);
        hash.Add(FixingDaysAtOneDay);
        hash.Add(Currency);
        hash.Add(FixingCalendar);
        hash.Add(FinancialCenterCalendar);
        hash.Add(DayCounter);
        RecordLists.Add(ref hash, Units);
        return hash.ToHashCode();
    }
}
