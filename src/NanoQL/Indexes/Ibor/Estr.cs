// SPDX-FileCopyrightText: Copyright (C) 2021 Magnus Mencke
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/estr.hpp ql/indexes/ibor/estr.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The euro short-term rate (€STR).</summary>
public sealed class Estr : OvernightIndex
{
    /// <summary>Initializes a new instance of the <see cref="Estr"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Estr(YieldTermStructure? h = null)
        : this(ConventionProvider.ReadOvernightIndex("Estr", Reference), h)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private Estr((IborIndexConventions Values, ConventionProvider Provider) read, YieldTermStructure? h)
        : base("ESTR", read.Values.FixingDays, read.Values.Currency, read.Values.FixingCalendar, read.Values.DayCounter, h, read.Provider)
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
