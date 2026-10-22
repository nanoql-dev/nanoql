// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/audlibor.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The AUD LIBOR rate: Australian Dollar LIBOR, discontinued as of 2013.</summary>
public sealed class AUDLibor : Libor
{
    /// <summary>Initializes a new instance of the <see cref="AUDLibor"/> class.</summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public AUDLibor(Period tenor, YieldTermStructure? h = null)
        : base("AUDLibor", tenor, 2, new AUDCurrency(), new Australia(), new Actual360(), h)
    {
    }
}
