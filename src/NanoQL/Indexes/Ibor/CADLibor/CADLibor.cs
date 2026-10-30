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

/// <summary>The CAD LIBOR rate: Canadian Dollar LIBOR, discontinued as of 2013.</summary>
/// <remarks>
/// Conventions are taken from a number of sources including OpenGamma "Interest Rate Instruments and Market Conventions
/// Guide", BBG, IKON. This is the rate fixed in London by BBA. Use CDOR (<see cref="Cdor"/>) if you're interested in
/// the Canadian fixing by IDA.
/// </remarks>
public sealed class CADLibor : Libor
{
    /// <summary>Initializes a new instance of the <see cref="CADLibor"/> class.</summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public CADLibor(Period tenor, YieldTermStructure? h = null)
        : base("CADLibor", tenor, ConventionProvider.ReadIborFamily("CADLibor", tenor, () => Reference(tenor)), h)
    {
    }

    // QuantLib's conventions at the tenor, built in the provider's scope: London's fixing calendar, Libor's rules
    // by the tenor and the currency's financial centre.
    private static IborIndexConventions Reference(Period tenor) => new(
        0,
        new CADCurrency(),
        new UnitedKingdom(UnitedKingdomMarket.Exchange),
        LiborConventions.Convention(tenor),
        LiborConventions.EndOfMonth(tenor),
        new Actual365Fixed(),
        new Canada());
}
