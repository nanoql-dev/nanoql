// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/eonia.hpp ql/indexes/ibor/eonia.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The Eonia (Euro Overnight Index Average) rate fixed by the ECB.</summary>
public sealed class Eonia : OvernightIndex
{
    /// <summary>Initializes a new instance of the <see cref="Eonia"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Eonia(YieldTermStructure? h = null)
        : this(ConventionProvider.ReadOvernightIndex("Eonia", Reference), h)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private Eonia((IborIndexConventions Values, ConventionProvider Provider) read, YieldTermStructure? h)
        : base("Eonia", read.Values.FixingDays, read.Values.Currency, read.Values.FixingCalendar, read.Values.DayCounter, h, read.Provider)
    {
    }

    // QuantLib's conventions, built in the provider's scope.
    private static IborIndexConventions Reference() => new(
        0,
        new EURCurrency(),
        new TARGET(),
        BusinessDayConvention.Following,
        false,
        new Actual360());
}
