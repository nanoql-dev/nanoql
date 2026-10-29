// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/commoditytype.hpp ql/experimental/commodities/commoditytype.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>A commodity type: its name and code, such as "Heating Oil" and "HO".</summary>
/// <remarks>
/// <para>
/// Immutable, and an extension point: <see cref="NullCommodityType"/> derives from it, and a user may build one.
/// QuantLib's empty <c>CommodityType()</c>, a placeholder whose members throw, is a <c>CommodityType?</c> null.
/// </para>
/// <para>
/// QuantLib keeps a process-wide registry keyed by code, so a second construction with a known code returns the first
/// construction's name whatever it was given (<c>commoditytype.cpp:28-38</c>). There is no registry here, so every
/// type holds the data it is given, a documented divergence (<c>Divergences/CommodityRegistryTests.cs</c>; the S-xrt
/// plan's XRR1).
/// </para>
/// <para>
/// Two types are equal when their codes are, compared ordinally, as QuantLib's <c>operator==</c> compares them
/// (<c>commoditytype.hpp:107-109</c>); the hash is the code's. The equality is sealed.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public class CommodityType : IEquatable<CommodityType>
{
    /// <summary>Initializes a new instance of the <see cref="CommodityType"/> class.</summary>
    /// <param name="name">The type's name, such as "Heating Oil".</param>
    /// <param name="code">The type's code, such as "HO".</param>
    /// <exception cref="ArgumentNullException">A string is null.</exception>
    public CommodityType(string name, string code)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(code);
        Name = name;
        Code = code;
    }

    /// <summary>Gets the type's code, such as "HO".</summary>
    public string Code { get; }

    /// <summary>Gets the type's name, such as "Heating Oil".</summary>
    public string Name { get; }

    /// <summary>Whether two types are equal: their codes are.</summary>
    /// <param name="left">A type, or <see langword="null"/>.</param>
    /// <param name="right">Another type, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both are null or their codes are equal.</returns>
    public static bool operator ==(CommodityType? left, CommodityType? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Whether two types differ.</summary>
    /// <param name="left">A type, or <see langword="null"/>.</param>
    /// <param name="right">Another type, or <see langword="null"/>.</param>
    /// <returns>The negation of <c>==</c>.</returns>
    public static bool operator !=(CommodityType? left, CommodityType? right) => !(left == right);

    /// <inheritdoc/>
    public bool Equals(CommodityType? other) =>
        other is not null && string.Equals(Code, other.Code, StringComparison.Ordinal);

    /// <inheritdoc/>
    public sealed override bool Equals(object? obj) => obj is CommodityType other && Equals(other);

    /// <inheritdoc/>
    public sealed override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Code);

    /// <summary>The type's code, as QuantLib's <c>operator&lt;&lt;</c> writes it.</summary>
    /// <returns>The code.</returns>
    public override string ToString() => Code;
}
