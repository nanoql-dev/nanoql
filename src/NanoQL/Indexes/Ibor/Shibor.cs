// SPDX-FileCopyrightText: Copyright (C) 2014 Cheng Li
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/shibor.hpp ql/indexes/ibor/shibor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The Shibor index: the Shanghai interbank offered rate, on China's interbank calendar.</summary>
public sealed class Shibor : IborIndex
{
    /// <summary>Initializes a new instance of the <see cref="Shibor"/> class.</summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentOutOfRangeException">The tenor's unit is shorter than a day.</exception>
    public Shibor(Period tenor, YieldTermStructure? h = null)
        : base(
            "Shibor",
            tenor,
            tenor == new Period(1, TimeUnit.Days) ? 0 : 1,
            new CNYCurrency(),
            new China(ChinaMarket.IB),
            ShiborConvention(tenor),
            false,
            new Actual360(),
            h)
    {
    }

    /// <summary>A copy of the index on another forwarding curve: a <see cref="Shibor"/> of the same tenor, as
    /// QuantLib's.</summary>
    /// <param name="forwarding">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <returns>The copy.</returns>
    public override IborIndex Clone(YieldTermStructure? forwarding) => new Shibor(Tenor, forwarding);

    // Following for days and weeks, modified following for months and years.
    private static BusinessDayConvention ShiborConvention(Period p) => p.Units switch
    {
        TimeUnit.Days or TimeUnit.Weeks => BusinessDayConvention.Following,
        TimeUnit.Months or TimeUnit.Years => BusinessDayConvention.ModifiedFollowing,
        _ => throw new ArgumentOutOfRangeException("tenor", p, "invalid time units"),
    };
}
