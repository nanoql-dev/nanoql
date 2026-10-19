// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007, 2008, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/swap/euriborswap.hpp ql/indexes/swap/euriborswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// The Euribor swap indexes published by IFR Markets and distributed by Reuters page TGM42281 and by Telerate:
/// annual 30/360 against 6M Euribor, and 1Y against 3M Euribor.
/// </summary>
/// <remarks>
/// Settled 2 TARGET days after fixing, against a fixed leg paying annually, modified following, 30/360 (bond basis); the
/// floating leg is on Euribor 6M for a tenor longer than a year and on Euribor 3M otherwise.
/// </remarks>
public sealed class EuriborSwapIfrFix : SwapIndex
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EuriborSwapIfrFix"/> class, discounting on the forwarding curve.
    /// </summary>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <exception cref="NanoQLException">The tenor can't be compared with one year, as 365 or 366 days can't.</exception>
    public EuriborSwapIfrFix(Period tenor, YieldTermStructure? h = null)
        : base(
            "EuriborSwapIfrFix", // familyName
            tenor,
            2, // settlementDays
            new EURCurrency(),
            new TARGET(),
            new Period(1, TimeUnit.Years), // fixedLegTenor
            BusinessDayConvention.ModifiedFollowing, // fixedLegConvention
            new Thirty360(Thirty360Convention.BondBasis), // fixedLegDaycounter
            tenor > new Period(1, TimeUnit.Years)
                ? new Euribor(new Period(6, TimeUnit.Months), h)
                : new Euribor(new Period(3, TimeUnit.Months), h))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EuriborSwapIfrFix"/> class with exogenous discounting, on the
    /// discounting curve even when it is <see langword="null"/>.
    /// </summary>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="forwarding">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <param name="discounting">The discounting curve, or <see langword="null"/>, QuantLib's empty handle.</param>
    /// <exception cref="NanoQLException">The tenor can't be compared with one year, as 365 or 366 days can't.</exception>
    public EuriborSwapIfrFix(Period tenor, YieldTermStructure? forwarding, YieldTermStructure? discounting)
        : base(
            "EuriborSwapIfrFix", // familyName
            tenor,
            2, // settlementDays
            new EURCurrency(),
            new TARGET(),
            new Period(1, TimeUnit.Years), // fixedLegTenor
            BusinessDayConvention.ModifiedFollowing, // fixedLegConvention
            new Thirty360(Thirty360Convention.BondBasis), // fixedLegDaycounter
            tenor > new Period(1, TimeUnit.Years)
                ? new Euribor(new Period(6, TimeUnit.Months), forwarding)
                : new Euribor(new Period(3, TimeUnit.Months), forwarding),
            discounting)
    {
    }
}
