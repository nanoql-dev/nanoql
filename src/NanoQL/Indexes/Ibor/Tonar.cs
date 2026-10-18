// SPDX-FileCopyrightText: Copyright (C) 2022 Jonghee Lee
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/tonar.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The TONAR index: TONAR (Tokyo Overnight Average Rate) fixed by the BOJ.</summary>
/// <remarks>See https://www3.boj.or.jp/market/en/menu_m.htm.</remarks>
public sealed class Tonar : OvernightIndex
{
    /// <summary>Initializes a new instance of the <see cref="Tonar"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Tonar(YieldTermStructure? h = null)
        : base("Tonar", 0, new JPYCurrency(), new Japan(), new Actual365Fixed(), h)
    {
    }
}
