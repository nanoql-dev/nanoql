// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/zibor.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The CHF ZIBOR rate: the Zurich Interbank Offered Rate.</summary>
/// <remarks>
/// This is the rate fixed in Zurich by BBA. Use CHFLibor (<see cref="CHFLibor"/>) if you're interested in the London
/// fixing by BBA.
/// </remarks>
public sealed class Zibor : IborIndex
{
    /// <summary>Initializes a new instance of the <see cref="Zibor"/> class.</summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Zibor(Period tenor, YieldTermStructure? h = null)
        : base(
            "Zibor",
            tenor,
            2,
            new CHFCurrency(),
            new Switzerland(),
            BusinessDayConvention.ModifiedFollowing,
            false,
            new Actual360(),
            h)
    {
    }
}
