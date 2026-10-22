// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Chiara Fornarola
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/swap/eurliborswap.hpp ql/indexes/swap/eurliborswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// The EUR Libor swap indexes fixed by ISDA in cooperation with Reuters and Intercapital Brokers at 11am London: annual
/// 30/360 against 6M Libor, and 1Y against 3M Libor.
/// </summary>
/// <remarks>
/// Settled 2 TARGET days after fixing, against a fixed leg paying annually, modified following, 30/360 (bond basis);
/// the floating leg is on EUR Libor 6M for a tenor longer than a year and on EUR Libor 3M otherwise.
/// </remarks>
public sealed class EurLiborSwapIsdaFixB : SwapIndex
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EurLiborSwapIsdaFixB"/> class, discounting on the forwarding curve.
    /// </summary>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <exception cref="NanoQLException">The tenor can't be compared with one year, as 365 or 366 days can't.</exception>
    public EurLiborSwapIsdaFixB(Period tenor, YieldTermStructure? h = null)
        : base(
            "EurLiborSwapIsdaFixB", // familyName
            tenor,
            2, // settlementDays
            new EURCurrency(),
            new TARGET(),
            new Period(1, TimeUnit.Years), // fixedLegTenor
            BusinessDayConvention.ModifiedFollowing, // fixedLegConvention
            new Thirty360(Thirty360Convention.BondBasis), // fixedLegDaycounter
            tenor > new Period(1, TimeUnit.Years)
                ? new EURLibor(new Period(6, TimeUnit.Months), h)
                : new EURLibor(new Period(3, TimeUnit.Months), h))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EurLiborSwapIsdaFixB"/> class with exogenous discounting, on the
    /// discounting curve even when it is <see langword="null"/>.
    /// </summary>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="forwarding">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <param name="discounting">The discounting curve, or <see langword="null"/>, QuantLib's empty handle.</param>
    /// <exception cref="NanoQLException">The tenor can't be compared with one year, as 365 or 366 days can't.</exception>
    public EurLiborSwapIsdaFixB(Period tenor, YieldTermStructure? forwarding, YieldTermStructure? discounting)
        : base(
            "EurLiborSwapIsdaFixB", // familyName
            tenor,
            2, // settlementDays
            new EURCurrency(),
            new TARGET(),
            new Period(1, TimeUnit.Years), // fixedLegTenor
            BusinessDayConvention.ModifiedFollowing, // fixedLegConvention
            new Thirty360(Thirty360Convention.BondBasis), // fixedLegDaycounter
            tenor > new Period(1, TimeUnit.Years)
                ? new EURLibor(new Period(6, TimeUnit.Months), forwarding)
                : new EURLibor(new Period(3, TimeUnit.Months), forwarding),
            discounting)
    {
    }
}
