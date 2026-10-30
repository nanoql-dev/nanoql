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
        : this(ConventionProvider.ReadInflationIndex("YYAUCPI", () => Reference(frequency, revised)), ts)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private YYAUCPI((InflationIndexConventions Values, ConventionProvider Provider) read, YoYInflationTermStructure? ts)
        : base(
            "YY_CPI",
            read.Values.Region,
            read.Values.Revised,
            read.Values.Frequency,
            read.Values.AvailabilityLag,
            read.Values.Currency,
            ts,
            read.Provider)
    {
    }

    // QuantLib's conventions, with the preset's arguments, built in the provider's scope.
    private static InflationIndexConventions Reference(Frequency frequency, bool revised) => new(
        new AustraliaRegion(),
        revised,
        frequency,
        new Period(2, TimeUnit.Months),
        new AUDCurrency());
}
