// SPDX-FileCopyrightText: Copyright (C) 2014 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/fedfunds.hpp ql/indexes/ibor/fedfunds.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The Fed Funds rate fixed by the FED (for balances held at the Federal Reserve).</summary>
public sealed class FedFunds : OvernightIndex
{
    /// <summary>Initializes a new instance of the <see cref="FedFunds"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public FedFunds(YieldTermStructure? h = null)
        : this(ConventionProvider.ReadOvernightIndex("FedFunds", Reference), h)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private FedFunds((IborIndexConventions Values, ConventionProvider Provider) read, YieldTermStructure? h)
        : base("FedFunds", read.Values.FixingDays, read.Values.Currency, read.Values.FixingCalendar, read.Values.DayCounter, h, read.Provider)
    {
    }

    // QuantLib's conventions, built in the provider's scope.
    private static IborIndexConventions Reference() => new(
        0,
        new USDCurrency(),
        new UnitedStates(UnitedStatesMarket.FederalReserve),
        BusinessDayConvention.Following,
        false,
        new Actual360());
}
