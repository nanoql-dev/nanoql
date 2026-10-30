// SPDX-FileCopyrightText: Copyright (C) 2024 Francois Botha
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/zaronia.hpp ql/indexes/ibor/zaronia.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// The Zaronia (South African Rand Overnight Index Average) rate fixed by the South African Reserve Bank.
/// </summary>
public sealed class Zaronia : OvernightIndex
{
    /// <summary>Initializes a new instance of the <see cref="Zaronia"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Zaronia(YieldTermStructure? h = null)
        : this(ConventionProvider.ReadOvernightIndex("Zaronia", Reference), h)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private Zaronia((IborIndexConventions Values, ConventionProvider Provider) read, YieldTermStructure? h)
        : base("ZARONIA", read.Values.FixingDays, read.Values.Currency, read.Values.FixingCalendar, read.Values.DayCounter, h, read.Provider)
    {
    }

    // QuantLib's conventions, built in the provider's scope.
    private static IborIndexConventions Reference() => new(
        0,
        new ZARCurrency(),
        new SouthAfrica(),
        BusinessDayConvention.Following,
        false,
        new Actual365Fixed());
}
