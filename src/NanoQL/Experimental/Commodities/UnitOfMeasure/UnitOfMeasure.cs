// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/unitofmeasure.hpp ql/experimental/commodities/unitofmeasure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>A unit of measure specification: its name, code and type.</summary>
/// <remarks>
/// <para>
/// Immutable, and an extension point: each of QuantLib's units derives from it (<see cref="BarrelUnitOfMeasure"/>,
/// …), and a user may build one. QuantLib's empty <c>UnitOfMeasure()</c>, a placeholder whose members throw, is a
/// <c>UnitOfMeasure?</c> null.
/// </para>
/// <para>
/// QuantLib keeps a process-wide registry keyed by name, so a second construction with a known name returns the first
/// construction's data whatever it was given: <c>UnitOfMeasure("Barrels", "B2", Mass)</c> after
/// <c>("Barrels", "BBL", Volume)</c> has code BBL and type Volume (<c>unitofmeasure.cpp:37-49</c>). There is no
/// registry here, so every unit holds the data it is given, a documented divergence
/// (<c>Divergences/CommodityRegistryTests.cs</c>; the S-xrt plan's XRR1).
/// </para>
/// <para>
/// Two units are equal when their codes are, compared ordinally, whatever their other data, as QuantLib's
/// <c>operator==</c> compares them (<c>unitofmeasure.hpp:130-132</c>); the hash is the code's. The equality is sealed,
/// so every subclass compares the same way.
/// </para>
/// <para>
/// Every unit's rounding is QuantLib's default <c>Rounding()</c>, which leaves an amount as it is, where QuantLib's is
/// <c>Rounding(0)</c>, to the closest whole unit, which its public constructor can't change
/// (<c>unitofmeasure.hpp:82-86</c>), so its automated conversions round a thousand-barrel quantity to thousands of
/// barrels: 2 MB plus 1,500 BBL is 4 MB there, 3.5 MB here (the S-xrt plan's XRR10;
/// <c>Divergences/QuantityRoundingTests.cs</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public class UnitOfMeasure : IEquatable<UnitOfMeasure>
{
    /// <summary>Initializes a new instance of the <see cref="UnitOfMeasure"/> class, with no triangulation unit.</summary>
    /// <param name="name">The unit's name, such as "Barrels".</param>
    /// <param name="code">The unit's code, such as "BBL".</param>
    /// <param name="unitType">What the unit measures.</param>
    /// <exception cref="ArgumentNullException">A string is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="unitType"/> isn't a member of its enum.</exception>
    public UnitOfMeasure(string name, string code, UnitOfMeasureType unitType)
        : this(name, code, unitType, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="UnitOfMeasure"/> class.</summary>
    /// <param name="name">The unit's name.</param>
    /// <param name="code">The unit's code.</param>
    /// <param name="unitType">What the unit measures.</param>
    /// <param name="triangulationUnitOfMeasure">The unit conversions go through when no direct one is known; none when
    /// <see langword="null"/>.</param>
    private protected UnitOfMeasure(
        string name,
        string code,
        UnitOfMeasureType unitType,
        UnitOfMeasure? triangulationUnitOfMeasure)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(code);
        EnumArguments.Check(unitType, nameof(unitType));
        Name = name;
        Code = code;
        UnitType = unitType;
        TriangulationUnitOfMeasure = triangulationUnitOfMeasure;
    }

    /// <summary>Gets the unit's name, such as "Barrels".</summary>
    public string Name { get; }

    /// <summary>Gets the unit's code, such as "BBL" or "MT".</summary>
    public string Code { get; }

    /// <summary>Gets what the unit measures (mass, volume, …).</summary>
    public UnitOfMeasureType UnitType { get; }

    /// <summary>Gets the rounding of amounts in the unit: QuantLib's default <c>Rounding()</c>, none.</summary>
    public Rounding Rounding { get; } = new();

    /// <summary>Gets the unit conversions go through when no direct one is known; <see langword="null"/> for none,
    /// QuantLib's empty unit.</summary>
    public UnitOfMeasure? TriangulationUnitOfMeasure { get; }

    /// <summary>Whether two units are equal: their codes are.</summary>
    /// <param name="left">A unit, or <see langword="null"/>.</param>
    /// <param name="right">Another unit, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both are null or their codes are equal.</returns>
    public static bool operator ==(UnitOfMeasure? left, UnitOfMeasure? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Whether two units differ.</summary>
    /// <param name="left">A unit, or <see langword="null"/>.</param>
    /// <param name="right">Another unit, or <see langword="null"/>.</param>
    /// <returns>The negation of <c>==</c>.</returns>
    public static bool operator !=(UnitOfMeasure? left, UnitOfMeasure? right) => !(left == right);

    /// <inheritdoc/>
    public bool Equals(UnitOfMeasure? other) =>
        other is not null && string.Equals(Code, other.Code, StringComparison.Ordinal);

    /// <inheritdoc/>
    public sealed override bool Equals(object? obj) => obj is UnitOfMeasure other && Equals(other);

    /// <inheritdoc/>
    public sealed override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Code);

    /// <summary>The unit's code, as QuantLib's <c>operator&lt;&lt;</c> writes it.</summary>
    /// <returns>The code.</returns>
    public override string ToString() => Code;

    /// <summary>
    /// A unit as QuantLib's <c>operator&lt;&lt;</c> writes it into a message: its code, or "null unit of measure" for
    /// the empty unit.
    /// </summary>
    /// <param name="unit">The unit, or <see langword="null"/>.</param>
    /// <returns>The text.</returns>
    internal static string Describe(UnitOfMeasure? unit) => unit?.Code ?? "null unit of measure";
}
