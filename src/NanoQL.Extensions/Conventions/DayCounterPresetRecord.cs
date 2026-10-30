// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A day counter preset (<c>DayCounterPreset</c>): one of QuantLib's day counters by its type and convention, its
/// formula code, with QuantLib's name and, for <see cref="Time.Business252"/>, the calendar whose business days it
/// counts.
/// </summary>
public sealed record DayCounterPresetRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="DayCounterPresetRecord"/> class.</summary>
    /// <param name="name">The preset's name, its type and convention or flag (<c>Thirty360.BondBasis</c>,
    /// <c>Actual360.IncludeLastDay</c>).</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public DayCounterPresetRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>Gets QuantLib's name of the day counter, or <see langword="null"/> to take the base's.</summary>
    public string? QuantLibName { get; init; }

    /// <summary>Gets the calendar's name, for <c>Business252</c> only, or <see langword="null"/>.</summary>
    public string? Calendar { get; init; }
}
