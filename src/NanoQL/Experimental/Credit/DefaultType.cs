// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/defaulttype.hpp ql/experimental/credit/defaulttype.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>
/// An atomic credit-event type: one of ISDA's default contractual types and, for a restructuring, its restructuring
/// type.
/// </summary>
/// <remarks>
/// <para>
/// Immutable, and an extension point: a type that needs a finer match than its atomic type derives from it, as
/// <see cref="FailureToPay"/> does. The match methods are strict: an event implied by another isn't matched; the
/// event classes implement such hierarchies.
/// </para>
/// <para>
/// Equality is QuantLib's: two types are equal when their atomic types and restructuring types are, whatever a
/// derived type adds, since equality indexes the probability curves. It is sealed, so every subclass compares the
/// same way.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public class DefaultType : IEquatable<DefaultType>
{
    /// <summary>Initializes a new instance of the <see cref="DefaultType"/> class.</summary>
    /// <param name="defaultType">The atomic type; QuantLib's default is a bankruptcy.</param>
    /// <param name="restructuringType">The restructuring type; none by default.</param>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">
    /// A restructuring without a restructuring type, or a restructuring type on another event ("Incoherent credit event
    /// type definition.").
    /// </exception>
    public DefaultType(
        AtomicDefaultType defaultType = AtomicDefaultType.Bankruptcy,
        RestructuringType restructuringType = RestructuringType.XR)
    {
        EnumArguments.Check(defaultType, nameof(defaultType));
        EnumArguments.Check(restructuringType, nameof(restructuringType));

        // checks restruct and norestruct are never together.
        if ((defaultType == AtomicDefaultType.Restructuring) == (restructuringType == RestructuringType.NoRestructuring))
        {
            throw new ArgumentException("Incoherent credit event type definition.", nameof(restructuringType));
        }

        AtomicType = defaultType;
        RestructuringType = restructuringType;
    }

    /// <summary>Gets the atomic type: QuantLib's <c>defaultType()</c>.</summary>
    public AtomicDefaultType AtomicType { get; }

    /// <summary>Gets the restructuring type.</summary>
    public RestructuringType RestructuringType { get; }

    /// <summary>Gets a value indicating whether the type is a restructuring.</summary>
    public bool IsRestructuring => RestructuringType != RestructuringType.NoRestructuring;

    /// <summary>Whether two types are equal: their atomic and restructuring types are.</summary>
    /// <param name="left">The first type.</param>
    /// <param name="right">The second type.</param>
    /// <returns><see langword="true"/> when they are equal, or both <see langword="null"/>.</returns>
    public static bool operator ==(DefaultType? left, DefaultType? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Whether two types differ.</summary>
    /// <param name="left">The first type.</param>
    /// <param name="right">The second type.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    public static bool operator !=(DefaultType? left, DefaultType? right) => !(left == right);

    /// <summary>
    /// Whether an atomic type is this one, so that it would be recognised as a trigger; a strict match, not an
    /// implication.
    /// </summary>
    /// <param name="defaultType">The atomic type.</param>
    /// <returns><see langword="true"/> when it is this type's atomic type.</returns>
    public bool ContainsDefaultType(AtomicDefaultType defaultType) => AtomicType == defaultType;

    /// <summary>Whether a restructuring type is this one's, or is <see cref="RestructuringType.AnyRestructuring"/>.</summary>
    /// <param name="restructuringType">The restructuring type.</param>
    /// <returns><see langword="true"/> when it matches.</returns>
    public bool ContainsRestructuringType(RestructuringType restructuringType) =>
        RestructuringType == restructuringType || restructuringType == RestructuringType.AnyRestructuring;

    /// <inheritdoc/>
    public bool Equals(DefaultType? other) =>
        other is not null && AtomicType == other.AtomicType && RestructuringType == other.RestructuringType;

    /// <inheritdoc/>
    public sealed override bool Equals(object? obj) => obj is DefaultType other && Equals(other);

    /// <inheritdoc/>
    public sealed override int GetHashCode() => HashCode.Combine(AtomicType, RestructuringType);
}
