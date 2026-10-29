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

/// <summary>A generic consumer price index: family "CPI", region <see cref="GenericRegion"/>.</summary>
[Experimental("NQL0001")]
public sealed class GenericCPI : ZeroInflationIndex
{
    /// <summary>Initializes a new instance of the <see cref="GenericCPI"/> class.</summary>
    /// <param name="frequency">The index's frequency.</param>
    /// <param name="revised">Whether its fixings are revised.</param>
    /// <param name="lag">Its availability lag.</param>
    /// <param name="ccy">Its currency.</param>
    /// <param name="ts">The zero-inflation curve it forecasts through, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ccy"/> is null.</exception>
    public GenericCPI(Frequency frequency, bool revised, Period lag, Currency ccy, ZeroInflationTermStructure? ts = null)
        : base("CPI", new GenericRegion(), revised, frequency, lag, ccy, ts)
    {
    }
}
