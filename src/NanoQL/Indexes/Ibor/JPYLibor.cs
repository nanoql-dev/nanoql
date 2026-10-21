// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/jpylibor.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The JPY LIBOR rate: Japanese Yen LIBOR fixed by ICE
/// (https://www.theice.com/marketdata/reports/170).</summary>
/// <remarks>This is the rate fixed in London by ICE. Use TIBOR (<see cref="Tibor"/>) if you're interested in the Tokio
/// fixing.</remarks>
public sealed class JPYLibor : Libor
{
    /// <summary>Initializes a new instance of the <see cref="JPYLibor"/> class.</summary>
    /// <param name="tenor">The tenor, not in days.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentException">The tenor is in days.</exception>
    public JPYLibor(Period tenor, YieldTermStructure? h = null)
        : base("JPYLibor", tenor, 2, new JPYCurrency(), new Japan(), new Actual360(), h)
    {
    }
}
