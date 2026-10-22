// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2021 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/inflation/aucpi.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// The quoted year-on-year Australian CPI (not a ratio), quarterly or annual: family "YY_CPI", region Australia,
/// available after two months, in AUD.
/// </summary>
public sealed class YYAUCPI : YoYInflationIndex
{
    /// <summary>Initializes a new instance of the <see cref="YYAUCPI"/> class.</summary>
    /// <param name="frequency">The frequency of the fixings, quarterly or annual.</param>
    /// <param name="revised">Whether the index is revised.</param>
    /// <param name="ts">The year-on-year inflation curve the index forecasts through, or <see langword="null"/> for
    /// none.</param>
    public YYAUCPI(Frequency frequency, bool revised, YoYInflationTermStructure? ts = null)
        : base(
            "YY_CPI",
            new AustraliaRegion(),
            revised,
            frequency,
            new Period(2, TimeUnit.Months),
            new AUDCurrency(),
            ts)
    {
    }
}
