// SPDX-FileCopyrightText: Copyright (C) 2005 Sercan Atalik
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/trlibor.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The TRY LIBOR rate: TRY LIBOR fixed by TBA (http://www.trlibor.org/trlibor/english/).</summary>
public sealed class TRLibor : IborIndex
{
    /// <summary>Initializes a new instance of the <see cref="TRLibor"/> class.</summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public TRLibor(Period tenor, YieldTermStructure? h = null)
        : this(tenor, ConventionProvider.ReadIborFamily("TRLibor", tenor, Reference), h)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private TRLibor(Period tenor, (IborIndexConventions Values, ConventionProvider Provider) read, YieldTermStructure? h)
        : base(
            "TRLibor",
            tenor,
            read.Values.FixingDays,
            read.Values.Currency,
            read.Values.FixingCalendar,
            read.Values.Convention,
            read.Values.EndOfMonth,
            read.Values.DayCounter,
            h,
            read.Provider)
    {
    }

    // QuantLib's conventions, the same at every tenor, built in the provider's scope.
    private static IborIndexConventions Reference() => new(
        0,
        new TRYCurrency(),
        new Turkey(),
        BusinessDayConvention.ModifiedFollowing,
        false,
        new Actual360());
}
