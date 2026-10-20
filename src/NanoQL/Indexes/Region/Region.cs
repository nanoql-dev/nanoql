// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2014 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/region.hpp ql/indexes/region.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Indexes;

/// <summary>A region, a geographical area an inflation index is published for: its name and code.</summary>
/// <remarks>
/// <para>
/// Immutable, and an extension point: QuantLib's regions derive from it, sealed, and <see cref="CustomRegion"/> builds
/// one without a class of its own.
/// </para>
/// <para>
/// Two regions are equal when their names are, compared ordinally, as QuantLib's <c>operator==</c> compares them,
/// whatever their codes. The equality is sealed, so every subclass compares the same way.
/// </para>
/// </remarks>
public abstract class Region : IEquatable<Region>
{
    /// <summary>Initializes a new instance of the <see cref="Region"/> class.</summary>
    /// <param name="name">The region's name, such as "UK".</param>
    /// <param name="code">The region's code, such as "UK".</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="code"/> is null.</exception>
    protected Region(string name, string code)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(code);
        Name = name;
        Code = code;
    }

    /// <summary>Gets the region's name.</summary>
    public string Name { get; }

    /// <summary>Gets the region's code.</summary>
    public string Code { get; }

    /// <summary>Compares two regions by name, as QuantLib's <c>operator==</c> does; two nulls are equal.</summary>
    /// <param name="left">The first region.</param>
    /// <param name="right">The second region.</param>
    /// <returns><see langword="true"/> when both are null or both have the same name.</returns>
    public static bool operator ==(Region? left, Region? right) => left is null ? right is null : left.Equals(right);

    /// <summary>Compares two regions by name, as QuantLib's <c>operator!=</c> does.</summary>
    /// <param name="left">The first region.</param>
    /// <param name="right">The second region.</param>
    /// <returns><see langword="true"/> unless both are null or both have the same name.</returns>
    public static bool operator !=(Region? left, Region? right) => !(left == right);

    /// <summary>Compares this region with another by name, ordinally.</summary>
    /// <param name="other">The other region.</param>
    /// <returns><see langword="true"/> when the other region isn't null and has this region's name.</returns>
    public bool Equals(Region? other) => other is not null && string.Equals(Name, other.Name, StringComparison.Ordinal);

    /// <inheritdoc/>
    public sealed override bool Equals(object? obj) => obj is Region other && Equals(other);

    /// <summary>The hash of the region's name.</summary>
    /// <returns>The hash.</returns>
    public sealed override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Name);
}
