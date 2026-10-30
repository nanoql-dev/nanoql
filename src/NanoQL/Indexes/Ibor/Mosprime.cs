// SPDX-FileCopyrightText: Copyright (C) 2018 Matthias Lungwitz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/mosprime.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The MOSPRIME rate: the Moscow Prime Offered Rate fixed by NFEA.</summary>
/// <remarks>
/// Conventions are taken from http://www.mosprime.com/uploads/files/MosPrime20Rate20Rules20since2030.01.2014.docx. Roll
/// convention and end of month not yet checked.
/// </remarks>
public sealed class Mosprime : IborIndex
{
    /// <summary>Initializes a new instance of the <see cref="Mosprime"/> class.</summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Mosprime(Period tenor, YieldTermStructure? h = null)
        : this(tenor, ConventionProvider.ReadIborFamily("Mosprime", tenor, () => Reference(tenor)), h)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private Mosprime(Period tenor, (IborIndexConventions Values, ConventionProvider Provider) read, YieldTermStructure? h)
        : base(
            "MOSPRIME",
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
        tenor == new Period(1, TimeUnit.Days) ? 0 : 1,
        new RUBCurrency(),
        new Russia(),
        BusinessDayConvention.ModifiedFollowing,
        false,
        new ActualActual(ActualActualConvention.ISDA));
}
