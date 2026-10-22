// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2021 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/inflation/zacpi.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// The South African consumer price inflation index (CPI): family "CPI", region South Africa, not revised, monthly,
/// available after one month, in ZAR.
/// </summary>
public sealed class ZACPI : ZeroInflationIndex
{
    /// <summary>Initializes a new instance of the <see cref="ZACPI"/> class.</summary>
    /// <param name="ts">The zero-inflation curve the index forecasts through, or <see langword="null"/> for
    /// none.</param>
    public ZACPI(ZeroInflationTermStructure? ts = null)
        : base("CPI", new ZARegion(), false, Frequency.Monthly, new Period(1, TimeUnit.Months), new ZARCurrency(), ts)
    {
    }
}
