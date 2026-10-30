// SPDX-FileCopyrightText: Copyright (C) 2016 Fabrice Lecuyer
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/bkbm.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The Bkbm index: the Bkbm rate fixed by NZFMA (http://www.nzfma.org/Site/data/default.aspx).</summary>
/// <remarks>An extension point: QuantLib's tenors, <see cref="Bkbm1M"/> to <see cref="Bkbm6M"/>, derive from
/// it.</remarks>
public class Bkbm : IborIndex
{
    /// <summary>Initializes a new instance of the <see cref="Bkbm"/> class.</summary>
    /// <param name="tenor">The tenor, not in days.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentException">The tenor is in days.</exception>
    public Bkbm(Period tenor, YieldTermStructure? h = null)
        : this(tenor, ConventionProvider.ReadIborFamily("Bkbm", tenor, Reference), h)
    {
        if (!(Tenor.Units != TimeUnit.Days))
        {
            throw new ArgumentException(
                $"for daily tenors ({Tenor}) dedicated DailyTenor constructor must be used", nameof(tenor));
        }
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private Bkbm(Period tenor, (IborIndexConventions Values, ConventionProvider Provider) read, YieldTermStructure? h)
        : base(
            "Bkbm",
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
        new NZDCurrency(),
        new NewZealand(),
        BusinessDayConvention.ModifiedFollowing,
        true,
        new Actual365Fixed());
}
