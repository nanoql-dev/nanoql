// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/jibar.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The JIBAR rate: the Johannesburg Interbank Agreed Rate.</summary>
/// <remarks>
/// To do: check settlement days and day-count convention.
/// </remarks>
public sealed class Jibar : IborIndex
{
    /// <summary>Initializes a new instance of the <see cref="Jibar"/> class.</summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Jibar(Period tenor, YieldTermStructure? h = null)
        : this(tenor, ConventionProvider.ReadIborFamily("Jibar", tenor, Reference), h)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private Jibar(Period tenor, (IborIndexConventions Values, ConventionProvider Provider) read, YieldTermStructure? h)
        : base(
            "Jibar",
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
        new ZARCurrency(),
        new SouthAfrica(),
        BusinessDayConvention.ModifiedFollowing,
        false,
        new Actual365Fixed());
}
