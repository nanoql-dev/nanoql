// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0
// Own-tested: Extensions/IndexSetTests.cs (nanoql's own record kind, which QuantLib's presets don't use: the round trip, and the set's and sensitivity facts)

using NanoQL.Coupons;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// An overnight-indexed swap index (<c>OvernightIndexedSwapIndex</c>), a user's own, built at a tenor on an
/// overnight index's name.
/// </summary>
public sealed record OvernightIndexedSwapIndexRecord : ConventionRecord
{
    /// <summary>Initializes a new instance of the <see cref="OvernightIndexedSwapIndexRecord"/> class.</summary>
    /// <param name="name">The record's name.</param>
    /// <param name="base">The record it is based on, or <see langword="null"/>.</param>
    public OvernightIndexedSwapIndexRecord(string name, string? @base = null)
        : base(name, @base)
    {
    }

    /// <summary>Gets the family name the index reports, or <see langword="null"/> to take the base's.</summary>
    public string? Family { get; init; }

    /// <summary>Gets the settlement days, or <see langword="null"/> to take the base's.</summary>
    public int? SettlementDays { get; init; }

    /// <summary>Gets the currency's code, or <see langword="null"/> to take the base's.</summary>
    public string? Currency { get; init; }

    /// <summary>Gets the overnight index's name, or <see langword="null"/> to take the base's.</summary>
    public string? OvernightIndex { get; init; }

    /// <summary>Gets whether the swap's value dates telescope, or <see langword="null"/> to take the base's.</summary>
    public bool? TelescopicValueDates { get; init; }

    /// <summary>Gets how the overnight rates are averaged, or <see langword="null"/> to take the base's.</summary>
    public RateAveragingType? Averaging { get; init; }
}
