// SPDX-FileCopyrightText: Copyright (C) 2016 Fabrice Lecuyer
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/aonia.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// The Aonia (Australia Overnight Index Average) rate fixed by the RBA
/// (http://www.isda.org/publications/pdf/Supplement-13-to-2000DefinitionsAnnex.pdf).
/// </summary>
public sealed class Aonia : OvernightIndex
{
    /// <summary>Initializes a new instance of the <see cref="Aonia"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Aonia(YieldTermStructure? h = null)
        : this(ConventionProvider.ReadOvernightIndex("Aonia", Reference), h)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private Aonia((IborIndexConventions Values, ConventionProvider Provider) read, YieldTermStructure? h)
        : base("Aonia", read.Values.FixingDays, read.Values.Currency, read.Values.FixingCalendar, read.Values.DayCounter, h, read.Provider)
    {
    }

    // QuantLib's conventions, built in the provider's scope.
    private static IborIndexConventions Reference() => new(
        0,
        new AUDCurrency(),
        new Australia(),
        BusinessDayConvention.Following,
        false,
        new Actual365Fixed());
}
