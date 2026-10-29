// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/energycommodity.hpp ql/experimental/commodities/energycommodity.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Equality-tested: tests/NanoQL.Tests/Conventions/CommodityConventionTests.cs

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The results of an energy commodity's calculation: the NPV, in the base currency, with what QuantLib keeps on the
/// instrument (its pricing errors, secondary cost amounts, daily positions and payment cash flows).
/// </summary>
/// <remarks>
/// QuantLib's <c>EnergyCommodity::results</c> also declares a currency and a unit of measure, which nothing sets, and
/// isn't ported with them. A swap's daily positions are also its additional result <c>dailyPositions</c>, as QuantLib
/// writes it. Equality compares every member by value, the collections element by element, but the payment cash flows,
/// which compare by reference, as every <see cref="CashFlow"/> does.
/// </remarks>
[Experimental("NQL0001")]
public sealed record EnergyCommodityResults : InstrumentResults
{
    /// <summary>Gets the calculation's pricing errors, warnings among them.</summary>
    public ImmutableArray<PricingError> PricingErrors { get; init; } = [];

    /// <summary>Gets the secondary costs' amounts by name, in the base currency.</summary>
    public ImmutableSortedDictionary<string, Money> SecondaryCostAmounts { get; init; } =
        ImmutableSortedDictionary.Create<string, Money>(StringComparer.Ordinal);

    /// <summary>Gets a swap's daily positions by day; none for a future.</summary>
    public ImmutableSortedDictionary<Date, EnergyDailyPosition> DailyPositions { get; init; } =
        ImmutableSortedDictionary<Date, EnergyDailyPosition>.Empty;

    /// <summary>Gets a swap's payment cash flows by payment date; none for a future.</summary>
    public ImmutableSortedDictionary<Date, CommodityCashFlow> PaymentCashFlows { get; init; } =
        ImmutableSortedDictionary<Date, CommodityCashFlow>.Empty;

    /// <inheritdoc/>
    public bool Equals(EnergyCommodityResults? other) =>
        other is not null
        && base.Equals(other)
        && PricingErrors.SequenceEqual(other.PricingErrors)
        && SecondaryCostAmounts.SequenceEqual(other.SecondaryCostAmounts)
        && DailyPositions.SequenceEqual(other.DailyPositions)
        && PaymentCashFlows.SequenceEqual(other.PaymentCashFlows);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(base.GetHashCode(), PricingErrors.Length, SecondaryCostAmounts.Count, DailyPositions.Count);
}
