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
/// The UK retail price index (RPI): family "RPI", region UK, not revised, monthly, available after one month, in GBP.
/// </summary>
public sealed class UKRPI : ZeroInflationIndex
{
    /// <summary>Initializes a new instance of the <see cref="UKRPI"/> class.</summary>
    /// <param name="ts">The zero-inflation curve the index forecasts through, or <see langword="null"/> for
    /// none.</param>
    public UKRPI(ZeroInflationTermStructure? ts = null)
        : this(ConventionProvider.ReadInflationIndex("UKRPI", Reference), ts)
    {
    }

    // The conventions read, with the provider they came from, which the index keeps.
    private UKRPI((InflationIndexConventions Values, ConventionProvider Provider) read, ZeroInflationTermStructure? ts)
        : base(
            "RPI",
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
        new UKRegion(),
        false,
        Frequency.Monthly,
        new Period(1, TimeUnit.Months),
        new GBPCurrency());
}
