// SPDX-FileCopyrightText: Copyright (C) 2026 Pratyush Patel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/shir.hpp ql/indexes/ibor/shir.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The SHIR index: the Shekel Overnight Interest Rate, published by the Bank of Israel.</summary>
/// <remarks>
/// SHIR replaces the Telbor rate in interest rate derivative transactions, and serves as the overnight interest rate
/// for that day (same-day fixing). See https://www.boi.org.il/en/economic-roles/financial-markets/shir/.
/// </remarks>
public sealed class Shir : OvernightIndex
{
    /// <summary>Initializes a new instance of the <see cref="Shir"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Shir(YieldTermStructure? h = null)
        : base("Shir", 0, new ILSCurrency(), new Israel(IsraelMarket.SHIR), new Actual365Fixed(), h)
    {
    }
}
