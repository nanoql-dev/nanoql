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
/// The quoted year-on-year South African CPI (not a ratio of ZA CPI): family "YY_CPI", region South Africa, not
/// revised, monthly, available after one month, in ZAR.
/// </summary>
public sealed class YYZACPI : YoYInflationIndex
{
    /// <summary>Initializes a new instance of the <see cref="YYZACPI"/> class.</summary>
    /// <param name="ts">The year-on-year inflation curve the index forecasts through, or <see langword="null"/> for
    /// none.</param>
    public YYZACPI(YoYInflationTermStructure? ts = null)
        : base(
            "YY_CPI",
            new ZARegion(),
            false,
            Frequency.Monthly,
            new Period(1, TimeUnit.Months),
            new ZARCurrency(),
            ts)
    {
    }
}
