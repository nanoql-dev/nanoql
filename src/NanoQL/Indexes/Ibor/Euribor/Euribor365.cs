// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2006 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2009 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chiara Fornarola
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/euribor.hpp ql/indexes/ibor/euribor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// The Actual/365 Euribor index: Euribor adjusted for the mismatch between Euribor's Actual/360 and the Actual/365 a few
/// pre-euro currencies used.
/// </summary>
public sealed class Euribor365 : IborIndex
{
    /// <summary>Initializes a new instance of the <see cref="Euribor365"/> class.</summary>
    /// <param name="tenor">The tenor, in weeks, months or years.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentException">The tenor is in days.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The tenor's unit is shorter than a day.</exception>
    public Euribor365(Period tenor, YieldTermStructure? h = null)
        : this(tenor, ConventionProvider.ReadIborFamily("Euribor365", tenor, () => Reference(tenor)), h)
    {
        EuriborConventions.CheckTenor(this);
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private Euribor365(Period tenor, (IborIndexConventions Values, ConventionProvider Provider) read, YieldTermStructure? h)
        : base(
            "Euribor365",
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
        new EURCurrency(),
        new TARGET(),
        EuriborConventions.Convention(tenor),
        EuriborConventions.EndOfMonth(tenor),
        new Actual365Fixed());
}
