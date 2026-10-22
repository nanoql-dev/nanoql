// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/cdor.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The CDOR rate: the Canadian Dollar Offered Rate fixed by IDA.</summary>
/// <remarks>
/// Conventions are taken from a number of sources including OpenGamma "Interest Rate Instruments and Market Conventions
/// Guide", BBG, IKON. This is the rate fixed in Canada by IDA. Use CADLibor (<see cref="CADLibor"/>) if you're
/// interested in the London fixing by BBA.
/// </remarks>
public sealed class Cdor : IborIndex
{
    /// <summary>Initializes a new instance of the <see cref="Cdor"/> class.</summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Cdor(Period tenor, YieldTermStructure? h = null)
        : base(
            "CDOR",
            tenor,
            0,
            new CADCurrency(),
            new Canada(),
            BusinessDayConvention.ModifiedFollowing,
            false,
            new Actual365Fixed(),
            h)
    {
    }
}
