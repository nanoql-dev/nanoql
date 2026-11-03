// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/unitofmeasureconversion.hpp ql/experimental/commodities/unitofmeasureconversion.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>
/// A conversion factor between two units of measure, for a commodity type: a unit of the source is worth
/// <see cref="ConversionFactor"/> units of the target.
/// </summary>
/// <remarks>
/// <para>
/// Immutable. A conversion given by the user is <see cref="UnitOfMeasureConversionType.Direct"/>; <see cref="Chain"/>
/// derives one from two others. A derived conversion converts through the conversions it was chained from, and, as
/// QuantLib's, has no commodity type (a <see langword="null"/> <see cref="CommodityType"/>) and an empty
/// <see cref="Code"/>.
/// </para>
/// <para>
/// Two conversions are equal when their commodity types, units, factors, types and codes are, and those they were
/// chained from; QuantLib defines no equality, and <see cref="UnitOfMeasureConversions"/>' needs one.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class UnitOfMeasureConversion : IEquatable<UnitOfMeasureConversion>
{
    private readonly UnitOfMeasureConversion? first;
    private readonly UnitOfMeasureConversion? second;

    /// <summary>Initializes a new instance of the <see cref="UnitOfMeasureConversion"/> class, a direct conversion.</summary>
    /// <param name="commodityType">The commodity type it holds for.</param>
    /// <param name="source">The source unit.</param>
    /// <param name="target">The target unit.</param>
    /// <param name="conversionFactor">How many units of the target a unit of the source is worth.</param>
    /// <exception cref="ArgumentNullException">The type or a unit is null, where QuantLib's empty ones throw.</exception>
    public UnitOfMeasureConversion(
        CommodityType commodityType,
        UnitOfMeasure source,
        UnitOfMeasure target,
        double conversionFactor)
    {
        ArgumentNullException.ThrowIfNull(commodityType);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        CommodityType = commodityType;
        Source = source;
        Target = target;
        ConversionFactor = conversionFactor;
        Type = UnitOfMeasureConversionType.Direct;
        Code = commodityType.Name + source.Code + target.Code;
    }

    private UnitOfMeasureConversion(
        UnitOfMeasureConversion r1,
        UnitOfMeasureConversion r2,
        UnitOfMeasure source,
        UnitOfMeasure target,
        double conversionFactor)
    {
        first = r1;
        second = r2;
        Source = source;
        Target = target;
        ConversionFactor = conversionFactor;
        Type = UnitOfMeasureConversionType.Derived;
        Code = string.Empty;
    }

    /// <summary>Gets the source unit.</summary>
    public UnitOfMeasure Source { get; }

    /// <summary>Gets the target unit.</summary>
    public UnitOfMeasure Target { get; }

    /// <summary>Gets the commodity type; <see langword="null"/> for a derived conversion, as QuantLib leaves it
    /// empty.</summary>
    public CommodityType? CommodityType { get; }

    /// <summary>Gets whether the conversion was given or derived.</summary>
    public UnitOfMeasureConversionType Type { get; }

    /// <summary>Gets how many units of the target a unit of the source is worth.</summary>
    public double ConversionFactor { get; }

    /// <summary>Gets the code: the commodity type's name and the two units' codes; empty for a derived
    /// conversion.</summary>
    public string Code { get; }

    /// <summary>
    /// Chains two conversions sharing a unit into one between their other units: QuantLib's
    /// <c>UnitOfMeasureConversion::chain</c>.
    /// </summary>
    /// <param name="r1">The first conversion.</param>
    /// <param name="r2">The second conversion.</param>
    /// <returns>The derived conversion.</returns>
    /// <exception cref="ArgumentNullException">A conversion is null.</exception>
    /// <exception cref="NanoQLException">The two share no unit ("conversion factors not chainable").</exception>
    public static UnitOfMeasureConversion Chain(UnitOfMeasureConversion r1, UnitOfMeasureConversion r2)
    {
        ArgumentNullException.ThrowIfNull(r1);
        ArgumentNullException.ThrowIfNull(r2);
        if (r1.Source == r2.Source)
        {
            return new UnitOfMeasureConversion(r1, r2, r1.Target, r2.Target, r2.ConversionFactor / r1.ConversionFactor);
        }

        if (r1.Source == r2.Target)
        {
            return new UnitOfMeasureConversion(
                r1, r2, r1.Target, r2.Source, 1.0 / (r1.ConversionFactor * r2.ConversionFactor));
        }

        if (r1.Target == r2.Source)
        {
            return new UnitOfMeasureConversion(r1, r2, r1.Source, r2.Target, r1.ConversionFactor * r2.ConversionFactor);
        }

        if (r1.Target == r2.Target)
        {
            return new UnitOfMeasureConversion(r1, r2, r1.Source, r2.Source, r1.ConversionFactor / r2.ConversionFactor);
        }

        throw new NanoQLException("conversion factors not chainable");
    }

    /// <summary>
    /// Converts a quantity in the source or the target unit into the other: a direct conversion multiplies a source
    /// quantity by the factor and divides a target one; a derived one converts through the two conversions it was
    /// chained from.
    /// </summary>
    /// <param name="quantity">The quantity.</param>
    /// <returns>The converted quantity, of the same commodity type.</returns>
    /// <exception cref="NanoQLException">The quantity's unit is neither of the conversion's ("direct conversion not
    /// applicable", "derived conversion factor not applicable").</exception>
    public Quantity Convert(Quantity quantity)
    {
        if (Type == UnitOfMeasureConversionType.Direct)
        {
            if (quantity.UnitOfMeasure == Source)
            {
                return new Quantity(quantity.CommodityType, Target, quantity.Amount * ConversionFactor);
            }

            if (quantity.UnitOfMeasure == Target)
            {
                return new Quantity(quantity.CommodityType, Source, quantity.Amount / ConversionFactor);
            }

            throw new NanoQLException("direct conversion not applicable");
        }

        if (quantity.UnitOfMeasure == first!.Source || quantity.UnitOfMeasure == first.Target)
        {
            return second!.Convert(first.Convert(quantity));
        }

        if (quantity.UnitOfMeasure == second!.Source || quantity.UnitOfMeasure == second.Target)
        {
            return first.Convert(second.Convert(quantity));
        }

        throw new NanoQLException("derived conversion factor not applicable");
    }

    /// <inheritdoc/>
    public bool Equals(UnitOfMeasureConversion? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null
            && CommodityType == other.CommodityType
            && Source == other.Source
            && Target == other.Target
            && ConversionFactor.Equals(other.ConversionFactor)
            && Type == other.Type
            && string.Equals(Code, other.Code, StringComparison.Ordinal)
            && Equals(first, other.first)
            && Equals(second, other.second);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is UnitOfMeasureConversion other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(CommodityType, Source, Target, ConversionFactor, Type, first, second);

    // A unit factor's conversion: the value of one unit of a quantity's unit in the other unit, through this
    // conversion's own convert, whichever way it is stored.
    internal double ValueOfOne(CommodityType? commodityType, UnitOfMeasure from) =>
        Convert(new Quantity(commodityType, from, 1.0)).Amount;
}
