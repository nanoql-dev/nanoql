// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A zero inflation index (<c>ZeroInflationIndex</c>): one of QuantLib's presets by its class (<c>EUHICP</c>), or a
/// user's own, on a region's name.
/// </summary>
public sealed record ZeroInflationIndexRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="ZeroInflationIndexRecord"/> class.</summary>
    /// <param name="name">The record's name: a preset's class name, or a user's own.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public ZeroInflationIndexRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>Gets the family name the index reports, or <see langword="null"/> to take the base's.</summary>
    public string? Family { get; init; }

    /// <summary>Gets the region's record name, or <see langword="null"/> to take the base's.</summary>
    public string? Region { get; init; }

    /// <summary>Gets whether its fixings are revised, or <see langword="null"/> to take the base's.</summary>
    public bool? Revised { get; init; }

    /// <summary>Gets the publication frequency, or <see langword="null"/> to take the base's.</summary>
    public Frequency? Frequency { get; init; }

    /// <summary>Gets the availability lag, or <see langword="null"/> to take the base's.</summary>
    public Period? AvailabilityLag { get; init; }

    /// <summary>Gets the currency's code, or <see langword="null"/> to take the base's.</summary>
    public string? Currency { get; init; }
}
