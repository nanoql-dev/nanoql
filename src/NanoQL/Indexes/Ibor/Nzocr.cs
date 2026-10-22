// SPDX-FileCopyrightText: Copyright (C) 2016 Fabrice Lecuyer
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/nzocr.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// The Nzocr index: the New Zealand official cash rate fixed by the RBNZ
/// (http://www.rbnz.govt.nz/monetary-policy/official-cash-rate-decisions).
/// </summary>
public sealed class Nzocr : OvernightIndex
{
    /// <summary>Initializes a new instance of the <see cref="Nzocr"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Nzocr(YieldTermStructure? h = null)
        : base("Nzocr", 0, new NZDCurrency(), new NewZealand(), new Actual365Fixed(), h)
    {
    }
}
