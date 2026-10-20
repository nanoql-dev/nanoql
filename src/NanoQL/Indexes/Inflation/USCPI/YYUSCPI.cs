// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2021 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/inflation/uscpi.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// The quoted year-on-year US consumer price index: family "YY_CPI", region USA, not revised, monthly, available after
/// one month, in USD.
/// </summary>
public sealed class YYUSCPI : YoYInflationIndex
{
    /// <summary>Initializes a new instance of the <see cref="YYUSCPI"/> class.</summary>
    /// <param name="ts">The year-on-year inflation curve the index forecasts through, or <see langword="null"/> for
    /// none.</param>
    public YYUSCPI(YoYInflationTermStructure? ts = null)
        : base(
            "YY_CPI",
            new USRegion(),
            false,
            Frequency.Monthly,
            new Period(1, TimeUnit.Months),
            new USDCurrency(),
            ts)
    {
    }
}
