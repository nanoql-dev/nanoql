// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2021 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/inflation/frhicp.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// The quoted year-on-year French HICP (not a ratio): family "YY_HICP", region France, not revised, monthly, available
/// after one month, in EUR.
/// </summary>
public sealed class YYFRHICP : YoYInflationIndex
{
    /// <summary>Initializes a new instance of the <see cref="YYFRHICP"/> class.</summary>
    /// <param name="ts">The year-on-year inflation curve the index forecasts through, or <see langword="null"/> for
    /// none.</param>
    public YYFRHICP(YoYInflationTermStructure? ts = null)
        : base(
            "YY_HICP",
            new FranceRegion(),
            false,
            Frequency.Monthly,
            new Period(1, TimeUnit.Months),
            new EURCurrency(),
            ts)
    {
    }
}
