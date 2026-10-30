// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A year-on-year inflation index (<c>YoYInflationIndex</c>): one of QuantLib's quoted presets by its class
/// (<c>YYEUHICP</c>), or a user's own, quoted from its fields or the ratio of an underlying zero index's.
/// </summary>
public sealed record YoYInflationIndexRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="YoYInflationIndexRecord"/> class.</summary>
    /// <param name="name">The record's name: a preset's class name, or a user's own.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public YoYInflationIndexRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>Gets a ratio index's zero inflation index's name; none for a quoted index, or <see langword="null"/> to take the base's.</summary>
    public string? UnderlyingIndex { get; init; }

    /// <summary>Gets a quoted index's family name, or <see langword="null"/> to take the base's.</summary>
    public string? Family { get; init; }

    /// <summary>Gets a quoted index's region's record name, or <see langword="null"/> to take the base's.</summary>
    public string? Region { get; init; }

    /// <summary>Gets whether a quoted index's fixings are revised, or <see langword="null"/> to take the base's.</summary>
    public bool? Revised { get; init; }

    /// <summary>Gets a quoted index's publication frequency, or <see langword="null"/> to take the base's.</summary>
    public Frequency? Frequency { get; init; }

    /// <summary>Gets a quoted index's availability lag, or <see langword="null"/> to take the base's.</summary>
    public Period? AvailabilityLag { get; init; }

    /// <summary>Gets a quoted index's currency's code, or <see langword="null"/> to take the base's.</summary>
    public string? Currency { get; init; }
}
