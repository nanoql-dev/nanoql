// SPDX-FileCopyrightText: Copyright (C) 2008, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/swap/jpyliborswap.hpp ql/indexes/swap/jpyliborswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// The JPY Libor swap indexes fixed by ISDA in cooperation with Reuters and Intercapital Brokers at 10am Tokyo:
/// semiannual Act/365 against 6M Libor. Reuters page ISDAFIX1 or JPYSFIXA=.
/// </summary>
/// <remarks>
/// Settled 2 TARGET days after fixing, against a fixed leg paying semiannually, modified following, Actual/Actual
/// (ISDA), as QuantLib's code has it; the floating leg is on JPY Libor 6M.
/// </remarks>
public sealed class JpyLiborSwapIsdaFixAm : SwapIndex
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JpyLiborSwapIsdaFixAm"/> class, discounting on the forwarding curve.
    /// </summary>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public JpyLiborSwapIsdaFixAm(Period tenor, YieldTermStructure? h = null)
        : this(tenor, ConventionProvider.ReadSwapIndex("JpyLiborSwapIsdaFixAm", tenor, () => Reference(h)), false, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JpyLiborSwapIsdaFixAm"/> class with exogenous discounting, on the
    /// discounting curve even when it is <see langword="null"/>.
    /// </summary>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="forwarding">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <param name="discounting">The discounting curve, or <see langword="null"/>, QuantLib's empty handle.</param>
    public JpyLiborSwapIsdaFixAm(Period tenor, YieldTermStructure? forwarding, YieldTermStructure? discounting)
        : this(tenor, ConventionProvider.ReadSwapIndex("JpyLiborSwapIsdaFixAm", tenor, () => Reference(forwarding)), true, discounting)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private JpyLiborSwapIsdaFixAm(
        Period tenor,
        (SwapIndexConventions Values, ConventionProvider Provider) read,
        bool exogenousDiscount,
        YieldTermStructure? discounting)
        : base(
            "JpyLiborSwapIsdaFixAm",
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
        new JPYCurrency(),
        new TARGET(),
        new Period(6, TimeUnit.Months),
        BusinessDayConvention.ModifiedFollowing,
        new ActualActual(ActualActualConvention.ISDA),
        new JPYLibor(new Period(6, TimeUnit.Months), forwarding));
}
