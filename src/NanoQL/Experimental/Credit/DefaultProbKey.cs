// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/defaultprobabilitykey.hpp ql/experimental/credit/defaultprobabilitykey.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>
/// The key that indexes market-implied credit curves: a proxy for the defaultable bond, or class of bonds, that sets a
/// credit contract's conditions, grouping the atomic default types the contract is sensitive to with the obligation's
/// currency and seniority.
/// </summary>
/// <remarks>
/// <para>
/// Immutable, and an extension point (<see cref="NorthAmericaCorpDefaultKey"/> derives from it). QuantLib's empty
/// currency is <see langword="null"/>.
/// </para>
/// <para>
/// Equality is QuantLib's: the same seniority and currency, and the same number of event types, each of one key's
/// equal to one of the other's (<see cref="DefaultType"/>'s equality). It is sealed, so every subclass compares the
/// same way.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public class DefaultProbKey : IEquatable<DefaultProbKey>
{
    /// <summary>Initializes a new instance of the <see cref="DefaultProbKey"/> class: no event type, QuantLib's empty
    /// currency and no seniority.</summary>
    public DefaultProbKey()
        : this([], null, Seniority.NoSeniority)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DefaultProbKey"/> class.</summary>
    /// <param name="eventTypes">The event types the contract is sensitive to, one per atomic type.</param>
    /// <param name="currency">The currency of the bond and of the protection leg's payment; <see langword="null"/> for
    /// QuantLib's empty currency.</param>
    /// <param name="seniority">The reference bonds' seniority.</param>
    /// <exception cref="ArgumentNullException"><paramref name="eventTypes"/> or one of its elements is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seniority"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">Two event types have the same atomic type ("Duplicated event type in
    /// contract definition").</exception>
    public DefaultProbKey(IEnumerable<DefaultType> eventTypes, Currency? currency, Seniority seniority)
    {
        ArgumentNullException.ThrowIfNull(eventTypes);
        EnumArguments.Check(seniority, nameof(seniority));
        ImmutableArray<DefaultType> types = [.. eventTypes];
        var buffer = new HashSet<AtomicDefaultType>();
        foreach (DefaultType t in types)
        {
            ArgumentNullException.ThrowIfNull(t, nameof(eventTypes));
            buffer.Add(t.AtomicType);
        }

        if (buffer.Count != types.Length)
        {
            throw new ArgumentException("Duplicated event type in contract definition", nameof(eventTypes));
        }

        EventTypes = types;
        Currency = currency;
        Seniority = seniority;
    }

    /// <summary>Gets the event types the contract is sensitive to.</summary>
    public ImmutableArray<DefaultType> EventTypes { get; }

    /// <summary>Gets the currency of the bond and of the protection leg's payment; <see langword="null"/> for
    /// QuantLib's empty currency.</summary>
    public Currency? Currency { get; }

    /// <summary>Gets the reference bonds' seniority.</summary>
    public Seniority Seniority { get; }

    /// <summary>Gets the number of event types.</summary>
    public int Size => EventTypes.Length;

    /// <summary>Whether two keys are equal.</summary>
    /// <param name="left">The first key.</param>
    /// <param name="right">The second key.</param>
    /// <returns><see langword="true"/> when they are equal, or both <see langword="null"/>.</returns>
    public static bool operator ==(DefaultProbKey? left, DefaultProbKey? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Whether two keys differ.</summary>
    /// <param name="left">The first key.</param>
    /// <param name="right">The second key.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    public static bool operator !=(DefaultProbKey? left, DefaultProbKey? right) => !(left == right);

    /// <inheritdoc/>
    public bool Equals(DefaultProbKey? other)
    {
        if (other is null || Seniority != other.Seniority || Currency != other.Currency
            || EventTypes.Length != other.EventTypes.Length)
        {
            return false;
        }

        // the all types must be equal in the weak sense.
        foreach (DefaultType t in other.EventTypes)
        {
            if (!EventTypes.Contains(t))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public sealed override bool Equals(object? obj) => obj is DefaultProbKey other && Equals(other);

    /// <inheritdoc/>
    public sealed override int GetHashCode()
    {
        int types = 0;
        foreach (DefaultType t in EventTypes)
        {
            types ^= t.GetHashCode();
        }

        return HashCode.Combine(Seniority, Currency, EventTypes.Length, types);
    }
}
