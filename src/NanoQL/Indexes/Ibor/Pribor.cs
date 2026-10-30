// SPDX-FileCopyrightText: Copyright (C) 2018 Matthias Lungwitz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/pribor.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The PRIBOR rate: the Prague Interbank Offered Rate fixed by CFBF.</summary>
/// <remarks>
/// Conventions are taken from OpenGamma "Interest Rate Instruments and Market Conventions Guide" as well as
/// https://cfbf.cz/wp-content/uploads/2018/02/pribor-rules.pdf. Roll convention and end of month not yet checked.
/// </remarks>
public sealed class Pribor : IborIndex
{
    /// <summary>Initializes a new instance of the <see cref="Pribor"/> class.</summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Pribor(Period tenor, YieldTermStructure? h = null)
        : this(tenor, ConventionProvider.ReadIborFamily("Pribor", tenor, () => Reference(tenor)), h)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private Pribor(Period tenor, (IborIndexConventions Values, ConventionProvider Provider) read, YieldTermStructure? h)
        : base(
            "PRIBOR",
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
        tenor == new Period(1, TimeUnit.Days) ? 0 : 2,
        new CZKCurrency(),
        new CzechRepublic(),
        BusinessDayConvention.ModifiedFollowing,
        false,
        new Actual360());
}
