// SPDX-FileCopyrightText: Copyright (C) 2008, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007, 2008, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Chiara Fornarola
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/swap/gbpliborswap.hpp ql/indexes/swap/gbpliborswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// The GBP Libor swap indexes fixed by ISDA in cooperation with Reuters and Intercapital Brokers at 11am London:
/// semiannual Actual/365F against 6M Libor, and 1Y annual against 3M Libor.
/// </summary>
/// <remarks>
/// Settled 0 days after fixing on the London stock exchange's calendar, against a fixed leg paying semiannually for a
/// tenor longer than a year and annually otherwise, modified following, Actual/365 (Fixed); the floating leg is on GBP
/// Libor 6M for a tenor longer than a year and on GBP Libor 3M otherwise.
/// </remarks>
public sealed class GbpLiborSwapIsdaFix : SwapIndex
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GbpLiborSwapIsdaFix"/> class, discounting on the forwarding curve.
    /// </summary>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <exception cref="NanoQLException">The tenor can't be compared with one year, as 365 or 366 days can't.</exception>
    public GbpLiborSwapIsdaFix(Period tenor, YieldTermStructure? h = null)
        : this(tenor, ConventionProvider.ReadSwapIndex("GbpLiborSwapIsdaFix", tenor, () => Reference(tenor, h)), false, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GbpLiborSwapIsdaFix"/> class with exogenous discounting, on the
    /// discounting curve even when it is <see langword="null"/>.
    /// </summary>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="forwarding">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <param name="discounting">The discounting curve, or <see langword="null"/>, QuantLib's empty handle.</param>
    /// <exception cref="NanoQLException">The tenor can't be compared with one year, as 365 or 366 days can't.</exception>
    public GbpLiborSwapIsdaFix(Period tenor, YieldTermStructure? forwarding, YieldTermStructure? discounting)
        : this(tenor, ConventionProvider.ReadSwapIndex("GbpLiborSwapIsdaFix", tenor, () => Reference(tenor, forwarding)), true, discounting)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private GbpLiborSwapIsdaFix(
        Period tenor,
        (SwapIndexConventions Values, ConventionProvider Provider) read,
        bool exogenousDiscount,
        YieldTermStructure? discounting)
        : base(
            "GbpLiborSwapIsdaFix",
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
        0,
        new GBPCurrency(),
        new UnitedKingdom(UnitedKingdomMarket.Exchange),
        tenor > new Period(1, TimeUnit.Years) ? new Period(6, TimeUnit.Months) : new Period(1, TimeUnit.Years),
        BusinessDayConvention.ModifiedFollowing,
        new Actual365Fixed(),
        tenor > new Period(1, TimeUnit.Years) ? new GBPLibor(new Period(6, TimeUnit.Months), forwarding) : new GBPLibor(new Period(3, TimeUnit.Months), forwarding));
}
