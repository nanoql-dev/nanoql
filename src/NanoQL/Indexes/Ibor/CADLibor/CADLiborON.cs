// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/cadlibor.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The overnight CAD Libor index.</summary>
public sealed class CADLiborON : DailyTenorLibor
{
    /// <summary>Initializes a new instance of the <see cref="CADLiborON"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public CADLiborON(YieldTermStructure? h = null)
        : base("CADLibor", ConventionProvider.ReadIborIndex("CADLiborON", () => Reference()), h)
    {
    }

    // QuantLib's conventions, built in the provider's scope: London's fixing calendar, Libor's rules at a day and
    // the currency's financial centre.
    private static IborIndexConventions Reference() => new(
        0,
        new CADCurrency(),
        new UnitedKingdom(UnitedKingdomMarket.Exchange),
        LiborConventions.Convention(new Period(1, TimeUnit.Days)),
        LiborConventions.EndOfMonth(new Period(1, TimeUnit.Days)),
        new Actual365Fixed(),
        new Canada());
}
