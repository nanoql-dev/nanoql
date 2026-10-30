// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2021 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/inflation/genericindexes.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A quoted year-on-year generic consumer price index, not a ratio: family "YY_CPI", region
/// <see cref="GenericRegion"/>.</summary>
[Experimental("NQL0001")]
public sealed class YYGenericCPI : YoYInflationIndex
{
    /// <summary>Initializes a new instance of the <see cref="YYGenericCPI"/> class.</summary>
    /// <param name="frequency">The index's frequency.</param>
    /// <param name="revised">Whether its fixings are revised.</param>
    /// <param name="lag">Its availability lag.</param>
    /// <param name="ccy">Its currency.</param>
    /// <param name="ts">The year-on-year curve it forecasts through, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ccy"/> is null.</exception>
    public YYGenericCPI(Frequency frequency, bool revised, Period lag, Currency ccy, YoYInflationTermStructure? ts = null)
        : this(ConventionProvider.ReadInflationIndex("YYGenericCPI", () => Reference(frequency, revised, lag, ccy)), ts)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private YYGenericCPI((InflationIndexConventions Values, ConventionProvider Provider) read, YoYInflationTermStructure? ts)
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
    private static InflationIndexConventions Reference(Frequency frequency, bool revised, Period lag, Currency ccy) =>
        new(new GenericRegion(), revised, frequency, lag, ccy);
}
