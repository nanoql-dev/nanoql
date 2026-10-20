// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2021 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/inflation/ukrpi.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// The quoted year-on-year UK retail price index: family "YY_RPI", region UK, not revised, monthly, available after one
/// month, in GBP.
/// </summary>
public sealed class YYUKRPI : YoYInflationIndex
{
    /// <summary>Initializes a new instance of the <see cref="YYUKRPI"/> class.</summary>
    /// <param name="ts">The year-on-year inflation curve the index forecasts through, or <see langword="null"/> for
    /// none.</param>
    public YYUKRPI(YoYInflationTermStructure? ts = null)
        : base(
            "YY_RPI",
            new UKRegion(),
            false,
            Frequency.Monthly,
            new Period(1, TimeUnit.Months),
            new GBPCurrency(),
            ts)
    {
    }
}
