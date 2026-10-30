// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/unitofmeasureconversionmanager.hpp ql/experimental/commodities/unitofmeasureconversionmanager.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>
/// The registry of conversion factors between units of measure: QuantLib's process-wide
/// <c>UnitOfMeasureConversionManager</c>, renamed, as an immutable value that
/// <see cref="EvaluationContext.UnitOfMeasureConversions"/> holds.
/// </summary>
/// <remarks>
/// <para>
/// Every change returns a new registry, as .NET's immutable collections do. <see cref="Default"/> holds QuantLib's
/// known factors between its petroleum units, which its manager holds at start and after <c>clear()</c>, with two of
/// them stored the right way round: QuantLib stores one litre as worth 3.78541 gallons and one gallon as worth 42,000
/// thousand-barrels (<c>unitofmeasureconversionmanager.cpp:115-122</c>), where one gallon is 3.785 411 784 litres and,
/// "by custom", 42 gallons a barrel (NIST Handbook 44, Appendix C), so a direct lookup converts 1,000 litres into
/// 3,785.41 gallons there and 264.17 here (the S-xrt plan's XRR4; <c>Divergences/CommodityKnownFactorsTests.cs</c>).
/// <see cref="Add"/> replaces a conversion of the same commodity type between the same two units, either way round,
/// and puts the new one last, as QuantLib's <c>add</c> does.
/// </para>
/// <para>
/// <see cref="Lookup"/> finds a direct, triangulated or chained conversion in QuantLib's order. QuantLib's chained
/// search forbids each unit it starts from to the searches below it, to avoid cycles, but drops the list when it
/// recurses (<c>:190</c>), so a search whose first candidate leads back recurses without end: its known factors' first
/// entry pairs thousand-barrels and barrels, so every chained search from either overflows the stack (barrels to metric
/// tonnes, with or without a path). Here the list is passed down, as QuantLib's <c>ExchangeRateManager</c> passes its
/// own; wherever QuantLib's search returns it never revisits a unit above it, so the answer is QuantLib's
/// (<c>Divergences/UnitConversionSearchTests.cs</c>).
/// </para>
/// <para>
/// Two registries are equal when they hold equal conversions in the same order; the hash is computed when the registry
/// is built.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class UnitOfMeasureConversions : IEquatable<UnitOfMeasureConversions>
{
    private readonly ImmutableList<UnitOfMeasureConversion> data;
    private readonly int hash;

    private UnitOfMeasureConversions(ImmutableList<UnitOfMeasureConversion> data)
    {
        this.data = data;
        var h = default(HashCode);
        h.Add(data.Count);
        foreach (UnitOfMeasureConversion c in data)
        {
            h.Add(c);
        }

        hash = h.ToHashCode();
    }

    /// <summary>Gets the registry holding QuantLib's known factors alone, as XRR4 stores them: its state at start and
    /// after <c>clear()</c>.</summary>
    public static UnitOfMeasureConversions Default { get; } = ConventionProvider.BuildUnderQuantLib(KnownConversionFactors);

    /// <summary>Gets the conversions held, in the order lookups try them.</summary>
    public ImmutableList<UnitOfMeasureConversion> Conversions => data;

    /// <summary>
    /// A registry with one more conversion: QuantLib's <c>add</c>, which first removes a conversion of the same
    /// commodity type between the same two units, either way round.
    /// </summary>
    /// <param name="conversion">The conversion, a direct one.</param>
    /// <returns>The new registry, the conversion last; this one is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="conversion"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The conversion is a derived one, which has no commodity type: QuantLib's <c>add</c> reads its empty type and
    /// fails.
    /// </exception>
    public UnitOfMeasureConversions Add(UnitOfMeasureConversion conversion)
    {
        ArgumentNullException.ThrowIfNull(conversion);
        if (conversion.CommodityType is not CommodityType type)
        {
            throw new ArgumentException("a derived conversion has no commodity type to file it under", nameof(conversion));
        }

        ImmutableList<UnitOfMeasureConversion> list = data;
        int i = list.FindIndex(c => Matches(c, type, conversion.Source, conversion.Target));
        if (i >= 0)
        {
            list = list.RemoveAt(i);
        }

        return new UnitOfMeasureConversions(list.Add(conversion));
    }

    /// <summary>The registry without the added conversions: <see cref="Default"/>, QuantLib's known factors
    /// alone.</summary>
    /// <returns><see cref="Default"/>; this registry is unchanged.</returns>
    public UnitOfMeasureConversions Clear() => Default;

    /// <summary>
    /// The conversion between two units for a commodity type. Direct conversions are preferred; with
    /// <see cref="UnitOfMeasureConversionType.Derived"/>, a conversion is triangulated through a unit's triangulation
    /// unit, or chained through other units, where no direct one is held.
    /// </summary>
    /// <param name="commodityType">The commodity type; a conversion is found only among that type's.</param>
    /// <param name="source">The source unit.</param>
    /// <param name="target">The target unit.</param>
    /// <param name="type">Whether only a direct conversion is returned, or derived ones too.</param>
    /// <returns>The conversion; a direct one is returned as stored, whichever way round.</returns>
    /// <exception cref="ArgumentNullException">The type or a unit is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its enum.</exception>
    /// <exception cref="NanoQLException">No conversion is held ("no direct conversion available", "no conversion
    /// available"), or two found ones can't be chained.</exception>
    public UnitOfMeasureConversion Lookup(
        CommodityType commodityType,
        UnitOfMeasure source,
        UnitOfMeasure target,
        UnitOfMeasureConversionType type = UnitOfMeasureConversionType.Derived)
    {
        ArgumentNullException.ThrowIfNull(commodityType);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        EnumArguments.Check(type, nameof(type));
        if (type == UnitOfMeasureConversionType.Direct)
        {
            return DirectLookup(commodityType, source, target);
        }

        if (source.TriangulationUnitOfMeasure is UnitOfMeasure sourceLink)
        {
            if (sourceLink == target)
            {
                return DirectLookup(commodityType, source, sourceLink);
            }

            return UnitOfMeasureConversion.Chain(
                DirectLookup(commodityType, source, sourceLink),
                Lookup(commodityType, sourceLink, target));
        }

        if (target.TriangulationUnitOfMeasure is UnitOfMeasure targetLink)
        {
            if (source == targetLink)
            {
                return DirectLookup(commodityType, targetLink, target);
            }

            return UnitOfMeasureConversion.Chain(
                Lookup(commodityType, source, targetLink),
                DirectLookup(commodityType, targetLink, target));
        }

        return SmartLookup(commodityType, source, target, []);
    }

    /// <inheritdoc/>
    public bool Equals(UnitOfMeasureConversions? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null && hash == other.hash && data.SequenceEqual(other.data);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is UnitOfMeasureConversions other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => hash;

    private static UnitOfMeasureConversions KnownConversionFactors()
    {
        var none = new NullCommodityType();
        var r = new UnitOfMeasureConversions([]);
        r = r.Add(new UnitOfMeasureConversion(none, new MBUnitOfMeasure(), new BarrelUnitOfMeasure(), 1000));
        r = r.Add(new UnitOfMeasureConversion(none, new BarrelUnitOfMeasure(), new GallonUnitOfMeasure(), 42));

        // QuantLib's (GallonUnitOfMeasure, MBUnitOfMeasure, 1000 * 42) and (LitreUnitOfMeasure, GallonUnitOfMeasure,
        // 3.78541), stored the right way round (XRR4)
        r = r.Add(new UnitOfMeasureConversion(none, new MBUnitOfMeasure(), new GallonUnitOfMeasure(), 1000 * 42));
        r = r.Add(new UnitOfMeasureConversion(none, new GallonUnitOfMeasure(), new LitreUnitOfMeasure(), 3.78541));
        r = r.Add(new UnitOfMeasureConversion(none, new BarrelUnitOfMeasure(), new LitreUnitOfMeasure(), 158.987));
        r = r.Add(new UnitOfMeasureConversion(none, new KilolitreUnitOfMeasure(), new BarrelUnitOfMeasure(), 6.28981));
        r = r.Add(new UnitOfMeasureConversion(none, new TokyoKilolitreUnitOfMeasure(), new BarrelUnitOfMeasure(), 6.28981));
        return r;
    }

    // QuantLib's matches(c, commodityType, source, target): the pair either way round.
    private static bool Matches(UnitOfMeasureConversion c, CommodityType commodityType, UnitOfMeasure source, UnitOfMeasure target) =>
        c.CommodityType == commodityType
        && ((c.Source == source && c.Target == target) || (c.Source == target && c.Target == source));

    // QuantLib's directLookup.
    private UnitOfMeasureConversion DirectLookup(CommodityType commodityType, UnitOfMeasure source, UnitOfMeasure target)
    {
        foreach (UnitOfMeasureConversion c in data)
        {
            if (Matches(c, commodityType, source, target))
            {
                return c;
            }
        }

        throw new NanoQLException(
            $"no direct conversion available from {commodityType.Code} {source.Code} to {target.Code}");
    }

    // QuantLib's smartLookup, its forbidden list passed down to the searches below it.
    private UnitOfMeasureConversion SmartLookup(
        CommodityType commodityType,
        UnitOfMeasure source,
        UnitOfMeasure target,
        ImmutableList<UnitOfMeasure> forbidden)
    {
        try
        {
            return DirectLookup(commodityType, source, target);
        }
        catch (NanoQLException)
        {
            // no direct conversion available; turn to smart lookup
        }

        forbidden = forbidden.Add(source);
        foreach (UnitOfMeasureConversion c in data)
        {
            if (c.CommodityType == commodityType && (c.Source == source || c.Target == source))
            {
                UnitOfMeasure other = source == c.Source ? c.Target : c.Source;
                if (!forbidden.Contains(other))
                {
                    try
                    {
                        UnitOfMeasureConversion tail = SmartLookup(commodityType, other, target, forbidden);
                        return UnitOfMeasureConversion.Chain(c, tail);
                    }
                    catch (NanoQLException)
                    {
                        // otherwise, we just discard this conversion
                    }
                }
            }
        }

        throw new NanoQLException(
            $"no conversion available for {commodityType.Code} from {source.Code} to {target.Code}");
    }
}
