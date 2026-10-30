// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/gbplibor.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;

namespace NanoQL.Indexes;

/// <summary>The overnight GBP Libor index.</summary>
public sealed class GBPLiborON : DailyTenorGBPLibor
{
    /// <summary>Initializes a new instance of the <see cref="GBPLiborON"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public GBPLiborON(YieldTermStructure? h = null)
        : base("GBPLiborON", 0, h)
    {
    }
}
