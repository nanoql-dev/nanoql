// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/chflibor.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The base class for the one day deposit BBA CHF LIBOR indexes.</summary>
public sealed class DailyTenorCHFLibor : DailyTenorLibor
{
    /// <summary>Initializes a new instance of the <see cref="DailyTenorCHFLibor"/> class.</summary>
    /// <param name="settlementDays">The settlementDays.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public DailyTenorCHFLibor(int settlementDays, YieldTermStructure? h = null)
        : base("CHFLibor", settlementDays, new CHFCurrency(), new Switzerland(), new Actual360(), h)
    {
    }
}
