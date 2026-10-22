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
/// The JPY Libor swap indexes fixed by ISDA in cooperation with Reuters and Intercapital Brokers at 3pm Tokyo:
/// semiannual Act/365 against 6M Libor. Reuters page ISDAFIX1 or JPYSFIXP=.
/// </summary>
/// <remarks>
/// Settled 2 TARGET days after fixing, against a fixed leg paying semiannually, modified following, Actual/Actual
/// (ISDA), as QuantLib's code has it; the floating leg is on JPY Libor 6M.
/// </remarks>
public sealed class JpyLiborSwapIsdaFixPm : SwapIndex
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JpyLiborSwapIsdaFixPm"/> class, discounting on the forwarding curve.
    /// </summary>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public JpyLiborSwapIsdaFixPm(Period tenor, YieldTermStructure? h = null)
        : base(
            "JpyLiborSwapIsdaFixPm", // familyName
            tenor,
            2, // settlementDays
            new JPYCurrency(),
            new TARGET(),
            new Period(6, TimeUnit.Months), // fixedLegTenor
            BusinessDayConvention.ModifiedFollowing, // fixedLegConvention
            new ActualActual(ActualActualConvention.ISDA), // fixedLegDaycounter
            new JPYLibor(new Period(6, TimeUnit.Months), h))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JpyLiborSwapIsdaFixPm"/> class with exogenous discounting, on the
    /// discounting curve even when it is <see langword="null"/>.
    /// </summary>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="forwarding">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <param name="discounting">The discounting curve, or <see langword="null"/>, QuantLib's empty handle.</param>
    public JpyLiborSwapIsdaFixPm(Period tenor, YieldTermStructure? forwarding, YieldTermStructure? discounting)
        : base(
            "JpyLiborSwapIsdaFixPm", // familyName
            tenor,
            2, // settlementDays
            new JPYCurrency(),
            new TARGET(),
            new Period(6, TimeUnit.Months), // fixedLegTenor
            BusinessDayConvention.ModifiedFollowing, // fixedLegConvention
            new ActualActual(ActualActualConvention.ISDA), // fixedLegDaycounter
            new JPYLibor(new Period(6, TimeUnit.Months), forwarding),
            discounting)
    {
    }
}
