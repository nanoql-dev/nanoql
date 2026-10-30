// SPDX-FileCopyrightText: Copyright (C) 2023 Skandinaviska Enskilda Banken AB (publ)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/destr.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The Destr (Denmark Short-Term Rate) index.</summary>
public sealed class Destr : OvernightIndex
{
    /// <summary>Initializes a new instance of the <see cref="Destr"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Destr(YieldTermStructure? h = null)
        : this(ConventionProvider.ReadOvernightIndex("Destr", Reference), h)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private Destr((IborIndexConventions Values, ConventionProvider Provider) read, YieldTermStructure? h)
        : base("DESTR", read.Values.FixingDays, read.Values.Currency, read.Values.FixingCalendar, read.Values.DayCounter, h, read.Provider)
    {
    }

    // QuantLib's conventions, built in the provider's scope.
    private static IborIndexConventions Reference() => new(
        0,
        new DKKCurrency(),
        new Denmark(),
        BusinessDayConvention.Following,
        false,
        new Actual360());
}
