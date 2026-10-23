// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Theo Boafo
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bonds/convertiblebonds.hpp ql/instruments/bonds/convertiblebonds.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A call leaving the holder the possibility to convert: exercisable only where the underlying is at or above the
/// trigger times the conversion value (the redemption over the conversion ratio).
/// </summary>
public sealed class SoftCallability : Callability
{
    /// <summary>Initializes a new instance of the <see cref="SoftCallability"/> class.</summary>
    /// <param name="price">The price the call is exercised at, clean or dirty, per 100 of notional.</param>
    /// <param name="date">The date the call can be exercised on.</param>
    /// <param name="trigger">The trigger, a multiple of the conversion value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="price"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="date"/> is <c>default(Date)</c>.</exception>
    public SoftCallability(BondPrice price, Date date, double trigger)
        : base(price, CallabilityType.Call, date)
    {
        Trigger = trigger;
    }

    /// <summary>Gets the trigger, a multiple of the conversion value.</summary>
    public double Trigger { get; }
}
