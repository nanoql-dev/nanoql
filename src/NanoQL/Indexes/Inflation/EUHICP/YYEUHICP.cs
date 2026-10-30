// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2010 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2021 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/inflation/euhicp.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// The quoted year-on-year EU harmonized index of consumer prices: family "YY_HICP", region EU, not revised, monthly,
/// available after one month, in EUR.
/// </summary>
public sealed class YYEUHICP : YoYInflationIndex
{
    /// <summary>Initializes a new instance of the <see cref="YYEUHICP"/> class.</summary>
    /// <param name="ts">The year-on-year inflation curve the index forecasts through, or <see langword="null"/> for
    /// none.</param>
    public YYEUHICP(YoYInflationTermStructure? ts = null)
        : this(ConventionProvider.ReadInflationIndex("YYEUHICP", Reference), ts)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private YYEUHICP((InflationIndexConventions Values, ConventionProvider Provider) read, YoYInflationTermStructure? ts)
        : base(
            "YY_HICP",
            read.Values.Region,
            read.Values.Revised,
            read.Values.Frequency,
            read.Values.AvailabilityLag,
            read.Values.Currency,
            ts,
            read.Provider)
    {
    }

    // QuantLib's conventions, built in the provider's scope.
    private static InflationIndexConventions Reference() => new(
        new EURegion(),
        false,
        Frequency.Monthly,
        new Period(1, TimeUnit.Months),
        new EURCurrency());
}
