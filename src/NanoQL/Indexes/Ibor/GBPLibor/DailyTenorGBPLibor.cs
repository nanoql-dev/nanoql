// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/gbplibor.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The base class for the one day deposit ICE GBP LIBOR indexes.</summary>
public class DailyTenorGBPLibor : DailyTenorLibor
{
    /// <summary>Initializes a new instance of the <see cref="DailyTenorGBPLibor"/> class.</summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public DailyTenorGBPLibor(int settlementDays, YieldTermStructure? h = null)
        : this("DailyTenorGBPLibor", settlementDays, h)
    {
    }

    // A preset deriving this one reads its own name's conventions.
    private protected DailyTenorGBPLibor(string preset, int settlementDays, YieldTermStructure? h)
        : base("GBPLibor", ConventionProvider.ReadIborIndex(preset, () => Reference(settlementDays)), h)
    {
    }

    // QuantLib's conventions, built in the provider's scope: London's fixing calendar, Libor's rules at a day and
    // the currency's financial centre.
    private static IborIndexConventions Reference(int settlementDays) => new(
        settlementDays,
        new GBPCurrency(),
        new UnitedKingdom(UnitedKingdomMarket.Exchange),
        LiborConventions.Convention(new Period(1, TimeUnit.Days)),
        LiborConventions.EndOfMonth(new Period(1, TimeUnit.Days)),
        new Actual365Fixed(),
        new UnitedKingdom(UnitedKingdomMarket.Exchange));
}
