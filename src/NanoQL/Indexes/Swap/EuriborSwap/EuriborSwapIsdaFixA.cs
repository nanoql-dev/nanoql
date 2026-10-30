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
/// The Euribor swap indexes fixed by ISDA in cooperation with Reuters and Intercapital Brokers at 11am Frankfurt:
/// annual 30/360 against 6M Euribor, and 1Y against 3M Euribor. Reuters page ISDAFIX2 or EURSFIXA=.
/// </summary>
/// <remarks>
/// Settled 2 TARGET days after fixing, against a fixed leg paying annually, modified following, 30/360 (bond basis); the
/// floating leg is on Euribor 6M for a tenor longer than a year and on Euribor 3M otherwise.
/// </remarks>
public sealed class EuriborSwapIsdaFixA : SwapIndex
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EuriborSwapIsdaFixA"/> class, discounting on the forwarding curve.
    /// </summary>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <exception cref="NanoQLException">The tenor can't be compared with one year, as 365 or 366 days can't.</exception>
    public EuriborSwapIsdaFixA(Period tenor, YieldTermStructure? h = null)
        : this(tenor, ConventionProvider.ReadSwapIndex("EuriborSwapIsdaFixA", tenor, () => Reference(tenor, h)), false, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EuriborSwapIsdaFixA"/> class with exogenous discounting, on the
    /// discounting curve even when it is <see langword="null"/>.
    /// </summary>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="forwarding">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <param name="discounting">The discounting curve, or <see langword="null"/>, QuantLib's empty handle.</param>
    /// <exception cref="NanoQLException">The tenor can't be compared with one year, as 365 or 366 days can't.</exception>
    public EuriborSwapIsdaFixA(Period tenor, YieldTermStructure? forwarding, YieldTermStructure? discounting)
        : this(tenor, ConventionProvider.ReadSwapIndex("EuriborSwapIsdaFixA", tenor, () => Reference(tenor, forwarding)), true, discounting)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private EuriborSwapIsdaFixA(
        Period tenor,
        (SwapIndexConventions Values, ConventionProvider Provider) read,
        bool exogenousDiscount,
        YieldTermStructure? discounting)
        : base(
            "EuriborSwapIsdaFixA",
            tenor,
            read.Values.SettlementDays,
            read.Values.Currency,
            read.Values.FixingCalendar,
            read.Values.FixedLegTenor,
            read.Values.FixedLegConvention,
            read.Values.FixedLegDayCounter,
            read.Values.IborIndex,
            exogenousDiscount,
            discounting,
            read.Provider)
    {
    }

    // QuantLib's conventions at the tenor, built in the provider's scope, the Ibor index on the forwarding curve.
    private static SwapIndexConventions Reference(Period tenor, YieldTermStructure? forwarding) => new(
        2,
        new EURCurrency(),
        new TARGET(),
        new Period(1, TimeUnit.Years),
        BusinessDayConvention.ModifiedFollowing,
        new Thirty360(Thirty360Convention.BondBasis),
        tenor > new Period(1, TimeUnit.Years) ? new Euribor(new Period(6, TimeUnit.Months), forwarding) : new Euribor(new Period(3, TimeUnit.Months), forwarding));
}
