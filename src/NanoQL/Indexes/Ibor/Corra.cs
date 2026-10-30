// SPDX-FileCopyrightText: Copyright (C) 2023 Aditya Narayan Das
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/corra.hpp ql/indexes/ibor/corra.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The CORRA index (the Canadian Overnight Repo Rate Average), fixed on the Canadian calendar.</summary>
public sealed class Corra : OvernightIndex
{
    /// <summary>Initializes a new instance of the <see cref="Corra"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Corra(YieldTermStructure? h = null)
        : this(ConventionProvider.ReadOvernightIndex("Corra", Reference), h)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private Corra((IborIndexConventions Values, ConventionProvider Provider) read, YieldTermStructure? h)
        : base("CORRA", read.Values.FixingDays, read.Values.Currency, read.Values.FixingCalendar, read.Values.DayCounter, h, read.Provider)
    {
    }

    // QuantLib's conventions, built in the provider's scope.
    private static IborIndexConventions Reference() => new(
        0,
        new CADCurrency(),
        new Canada(),
        BusinessDayConvention.Following,
        false,
        new Actual365Fixed());
}
