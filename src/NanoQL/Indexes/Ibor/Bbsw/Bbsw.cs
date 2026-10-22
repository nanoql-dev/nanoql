// SPDX-FileCopyrightText: Copyright (C) 2016 Fabrice Lecuyer
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/bbsw.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The Bbsw index: the Bbsw rate fixed by AFMA (http://www.afma.com.au/data/BBSW).</summary>
/// <remarks>An extension point: QuantLib's tenors, <see cref="Bbsw1M"/> to <see cref="Bbsw6M"/>, derive from
/// it.</remarks>
public class Bbsw : IborIndex
{
    /// <summary>Initializes a new instance of the <see cref="Bbsw"/> class.</summary>
    /// <param name="tenor">The tenor, not in days.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentException">The tenor is in days.</exception>
    public Bbsw(Period tenor, YieldTermStructure? h = null)
        : base(
            "Bbsw",
            tenor,
            0, // settlement days
            new AUDCurrency(),
            new Australia(),
            BusinessDayConvention.HalfMonthModifiedFollowing,
            true,
            new Actual365Fixed(),
            h)
    {
        if (!(Tenor.Units != TimeUnit.Days))
        {
            throw new ArgumentException(
                $"for daily tenors ({Tenor}) dedicated DailyTenor constructor must be used", nameof(tenor));
        }
    }
}
