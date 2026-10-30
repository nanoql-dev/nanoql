// SPDX-FileCopyrightText: Copyright (C) 2018 Matthias Groncki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/thbfix.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The THB THBFIX rate: the THB interest rate implied by USD/THB foreign exchange swaps.</summary>
/// <remarks>
/// The Swap Offer Rate represents the cost of borrowing a currency synthetically by borrowing USD for the same tenor
/// and using the foreign exchange swap offer rate on the offer side to swap out the USD in return for the foreign
/// currency. Fixing is based on average FX Forward rates from 21 banks and the USD Libor Fixing. Fixing is published at
/// 11:00 am BKK time.
/// </remarks>
public sealed class THBFIX : IborIndex
{
    /// <summary>Initializes a new instance of the <see cref="THBFIX"/> class.</summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public THBFIX(Period tenor, YieldTermStructure? h = null)
        : this(tenor, ConventionProvider.ReadIborFamily("THBFIX", tenor, Reference), h)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private THBFIX(Period tenor, (IborIndexConventions Values, ConventionProvider Provider) read, YieldTermStructure? h)
        : base(
            "THBFIX",
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
        2,
        new THBCurrency(),
        new Thailand(),
        BusinessDayConvention.ModifiedFollowing,
        true,
        new Actual365Fixed());
}
