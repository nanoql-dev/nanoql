// SPDX-FileCopyrightText: Copyright (C) 2008, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/swap/usdliborswap.hpp ql/indexes/swap/usdliborswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// The USD Libor swap indexes fixed by ISDA in cooperation with Reuters and Intercapital Brokers at 11am New York:
/// semiannual 30/360 against 3M Libor. Reuters page ISDAFIX1 or USDSFIX=.
/// </summary>
/// <remarks>
/// Settled 2 US government bond days after fixing, against a fixed leg paying semiannually, modified following, 30/360
/// (bond basis); the floating leg is on USD Libor 3M.
/// </remarks>
public sealed class UsdLiborSwapIsdaFixAm : SwapIndex
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UsdLiborSwapIsdaFixAm"/> class, discounting on the forwarding curve.
    /// </summary>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public UsdLiborSwapIsdaFixAm(Period tenor, YieldTermStructure? h = null)
        : this(tenor, ConventionProvider.ReadSwapIndex("UsdLiborSwapIsdaFixAm", tenor, () => Reference(h)), false, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UsdLiborSwapIsdaFixAm"/> class with exogenous discounting, on the
    /// discounting curve even when it is <see langword="null"/>.
    /// </summary>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="forwarding">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <param name="discounting">The discounting curve, or <see langword="null"/>, QuantLib's empty handle.</param>
    public UsdLiborSwapIsdaFixAm(Period tenor, YieldTermStructure? forwarding, YieldTermStructure? discounting)
        : this(tenor, ConventionProvider.ReadSwapIndex("UsdLiborSwapIsdaFixAm", tenor, () => Reference(forwarding)), true, discounting)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private UsdLiborSwapIsdaFixAm(
        Period tenor,
        (SwapIndexConventions Values, ConventionProvider Provider) read,
        bool exogenousDiscount,
        YieldTermStructure? discounting)
        : base(
            "UsdLiborSwapIsdaFixAm",
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
    private static SwapIndexConventions Reference(YieldTermStructure? forwarding) => new(
        2,
        new USDCurrency(),
        new UnitedStates(UnitedStatesMarket.GovernmentBond),
        new Period(6, TimeUnit.Months),
        BusinessDayConvention.ModifiedFollowing,
        new Thirty360(Thirty360Convention.BondBasis),
        new USDLibor(new Period(3, TimeUnit.Months), forwarding));
}
