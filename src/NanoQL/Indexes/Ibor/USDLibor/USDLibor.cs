// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2017 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2017 Oleg Kulkov
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/usdlibor.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The USD LIBOR rate: US Dollar LIBOR fixed by ICE (https://www.theice.com/marketdata/reports/170).</summary>
public sealed class USDLibor : Libor
{
    /// <summary>Initializes a new instance of the <see cref="USDLibor"/> class.</summary>
    /// <param name="tenor">The tenor, not in days.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentException">The tenor is in days.</exception>
    public USDLibor(Period tenor, YieldTermStructure? h = null)
        : base("USDLibor", tenor, 2, new USDCurrency(), new UnitedStates(UnitedStatesMarket.LiborImpact), new Actual360(), h)
    {
    }
}
