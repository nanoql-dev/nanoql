// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/commodity.hpp ql/experimental/commodities/commodity.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>The base of commodity instruments: an instrument with secondary costs.</summary>
/// <remarks>
/// <para>
/// An extension point: <see cref="EnergyCommodity"/> derives from it. QuantLib's <c>SecondaryCosts</c>, a map from a
/// name to a <c>std::any</c>, is a dictionary from a name to a <see cref="CommodityUnitCost"/> (a cost per unit
/// delivered) or a <see cref="Money"/> amount (a fixed cost), kept in QuantLib's key order (ordinal); a value of any
/// other type is ignored, as QuantLib's calculation skips it.
/// </para>
/// <para>
/// QuantLib keeps the secondary cost amounts and pricing errors on the instrument, mutable, its pricing errors never
/// cleared between calculations, and lets any caller add one (<c>addPricingError</c>); here each calculation returns
/// its own, in <see cref="EnergyCommodityResults"/>, and the instrument holds none.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public abstract class Commodity : Instrument
{
    /// <summary>Initializes a new instance of the <see cref="Commodity"/> class.</summary>
    /// <param name="secondaryCosts">The secondary costs by name; <see langword="null"/> for none, QuantLib's null
    /// pointer.</param>
    /// <exception cref="ArgumentException">A name is null.</exception>
    protected Commodity(IReadOnlyDictionary<string, object>? secondaryCosts)
    {
        SecondaryCosts = secondaryCosts?.ToImmutableSortedDictionary(StringComparer.Ordinal);
    }

    /// <summary>Gets the secondary costs by name, in ordinal order; <see langword="null"/> for none.</summary>
    public ImmutableSortedDictionary<string, object>? SecondaryCosts { get; }
}
