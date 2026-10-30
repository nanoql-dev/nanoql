// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// An overnight index (<c>OvernightIndex</c>): one of QuantLib's presets by its class (<c>Sofr</c>), or a user's
/// own; its convention is Following, without end of month.
/// </summary>
public sealed record OvernightIndexRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="OvernightIndexRecord"/> class.</summary>
    /// <param name="name">The record's name: a preset's class name, or a user's own.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public OvernightIndexRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>Gets the family name the index reports, or <see langword="null"/> to take the base's.</summary>
    public string? Family { get; init; }

    /// <summary>Gets the fixing days, or <see langword="null"/> to take the base's.</summary>
    public int? FixingDays { get; init; }

    /// <summary>Gets the currency's code, or <see langword="null"/> to take the base's.</summary>
    public string? Currency { get; init; }

    /// <summary>Gets the fixing calendar's name, or <see langword="null"/> to take the base's.</summary>
    public string? FixingCalendar { get; init; }

    /// <summary>Gets the day counter's name, or <see langword="null"/> to take the base's.</summary>
    public string? DayCounter { get; init; }
}
