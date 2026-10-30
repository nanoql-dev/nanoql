// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/chflibor.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// The CHF LIBOR rate: Swiss Franc LIBOR fixed by ICE (https://www.theice.com/marketdata/reports/170).
/// </summary>
/// <remarks>
/// This is the rate fixed in London by BBA. Use ZIBOR (<see cref="Zibor"/>) if you're interested in the Zurich fixing.
/// </remarks>
public sealed class CHFLibor : Libor
{
    /// <summary>Initializes a new instance of the <see cref="CHFLibor"/> class.</summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public CHFLibor(Period tenor, YieldTermStructure? h = null)
        : base("CHFLibor", tenor, ConventionProvider.ReadIborFamily("CHFLibor", tenor, () => Reference(tenor)), h)
    {
    }

    // QuantLib's conventions at the tenor, built in the provider's scope: London's fixing calendar, Libor's rules
    // by the tenor and the currency's financial centre.
    private static IborIndexConventions Reference(Period tenor) => new(
        2,
        new CHFCurrency(),
        new UnitedKingdom(UnitedKingdomMarket.Exchange),
        LiborConventions.Convention(tenor),
        LiborConventions.EndOfMonth(tenor),
        new Actual360(),
        new Switzerland());
}
