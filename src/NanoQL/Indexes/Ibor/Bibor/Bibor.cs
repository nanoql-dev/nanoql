// SPDX-FileCopyrightText: Copyright (C) 2018 Matthias Groncki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/bibor.hpp ql/indexes/ibor/bibor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The Bibor index: the Bangkok Interbank Offered Rate fixed by the Bank of Thailand (BOT).</summary>
/// <remarks>An extension point: QuantLib's tenors, <see cref="BiborSW"/> to <see cref="Bibor1Y"/>, derive from
/// it.</remarks>
public class Bibor : IborIndex
{
    /// <summary>Initializes a new instance of the <see cref="Bibor"/> class.</summary>
    /// <param name="tenor">The tenor, in weeks, months or years.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentException">The tenor is in days.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The tenor's unit is shorter than a day.</exception>
    public Bibor(Period tenor, YieldTermStructure? h = null)
        : this(tenor, ConventionProvider.ReadIborFamily("Bibor", tenor, () => Reference(tenor)), h)
    {
        if (!(Tenor.Units != TimeUnit.Days))
        {
            throw new ArgumentException(
                $"for daily tenors ({Tenor}) dedicated DailyTenor constructor must be used", nameof(tenor));
        }
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private Bibor(Period tenor, (IborIndexConventions Values, ConventionProvider Provider) read, YieldTermStructure? h)
        : base(
            "Bibor",
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

    // QuantLib's conventions at the tenor, built in the provider's scope.
    private static IborIndexConventions Reference(Period tenor) => new(
        2,
        new THBCurrency(),
        new Thailand(),
        BiborConvention(tenor),
        BiborEOM(tenor),
        new Actual365Fixed());

    // Following for days and weeks, modified following for months and years.
    private static BusinessDayConvention BiborConvention(Period p) => p.Units switch
    {
        TimeUnit.Days or TimeUnit.Weeks => BusinessDayConvention.Following,
        TimeUnit.Months or TimeUnit.Years => BusinessDayConvention.ModifiedFollowing,
        _ => throw new ArgumentOutOfRangeException("tenor", p, "invalid time units"),
    };

    // No end-of-month rule for days and weeks; the rule for months and years.
    private static bool BiborEOM(Period p) => p.Units switch
    {
        TimeUnit.Days or TimeUnit.Weeks => false,
        TimeUnit.Months or TimeUnit.Years => true,
        _ => throw new ArgumentOutOfRangeException("tenor", p, "invalid time units"),
    };
}
