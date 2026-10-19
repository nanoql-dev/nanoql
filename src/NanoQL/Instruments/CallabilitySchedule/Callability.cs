// SPDX-FileCopyrightText: Copyright (C) 2005 Joseph Wang
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Theo Boafo
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/callabilityschedule.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>An instrument's callability: a call or a put at a price, on a date.</summary>
/// <remarks>
/// <para>
/// QuantLib's <c>CallabilitySchedule</c> is a typedef of a vector of callabilities, not a type: a schedule is an
/// <see cref="System.Collections.Immutable.ImmutableArray{T}"/> of them, and a member that takes one copies the
/// sequence it is given. Whether a callability has occurred is <see cref="Event"/>'s <see cref="Event.HasOccurred"/>
/// on its date.
/// </para>
/// <para>
/// Unsealed, an extension point, as QuantLib's class is: the convertible bonds' soft callability derives from it.
/// </para>
/// </remarks>
public class Callability : Event
{
    private readonly Date date;

    /// <summary>Initializes a new instance of the <see cref="Callability"/> class.</summary>
    /// <param name="price">The price the callability is exercised at, clean or dirty, per 100 of notional.</param>
    /// <param name="type">Whether the callability is a call or a put.</param>
    /// <param name="date">The date the callability can be exercised on.</param>
    /// <exception cref="ArgumentNullException"><paramref name="price"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException"><paramref name="date"/> is <c>default(Date)</c>.</exception>
    /// <remarks>
    /// QuantLib stores any type and refuses one outside its members where the callable bond's tree applies it, with
    /// "unknown callability type": the type is checked here, with that text.
    /// </remarks>
    public Callability(BondPrice price, CallabilityType type, Date date)
    {
        ArgumentNullException.ThrowIfNull(price);
        EnumArguments.Check(type, nameof(type));
        Date.ThrowIfDefault(date, nameof(date));
        Price = price;
        Type = type;
        this.date = date;
    }

    /// <summary>Gets the price the callability is exercised at, clean or dirty, per 100 of notional.</summary>
    /// <remarks>
    /// QuantLib's <c>price()</c> refuses "no price given" for a missing price, which its constructor never leaves.
    /// </remarks>
    public BondPrice Price { get; }

    /// <summary>Gets whether the callability is a call or a put.</summary>
    public CallabilityType Type { get; }

    /// <summary>Gets the date the callability can be exercised on.</summary>
    public override Date Date => date;
}
