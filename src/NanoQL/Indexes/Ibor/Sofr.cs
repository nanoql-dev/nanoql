// SPDX-FileCopyrightText: Copyright (C) 2018 Roy Zywina
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/sofr.hpp ql/indexes/ibor/sofr.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The Secured Overnight Financing Rate, fixed on the SOFR calendar.</summary>
public sealed class Sofr : OvernightIndex
{
    /// <summary>Initializes a new instance of the <see cref="Sofr"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Sofr(YieldTermStructure? h = null)
        : base("SOFR", 0, new USDCurrency(), new UnitedStates(UnitedStatesMarket.SOFR), new Actual360(), h)
    {
    }
}
