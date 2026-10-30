// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// A region (<c>Region</c>): one of QuantLib's regions by its class (<c>EURegion</c>), keeping its name, or a
/// user's own, built as a <see cref="Indexes.CustomRegion"/>.
/// </summary>
public sealed record RegionRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="RegionRecord"/> class.</summary>
    /// <param name="name">The record's name: a region preset's class name, or a user's own.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public RegionRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>Gets the region's name, QuantLib's for a preset, which is its identity, or <see langword="null"/> to take the base's.</summary>
    public string? RegionName { get; init; }

    /// <summary>Gets the region's code, or <see langword="null"/> to take the base's.</summary>
    public string? Code { get; init; }
}
