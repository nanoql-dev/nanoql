// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/tibor.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The JPY TIBOR index: the Tokyo Interbank Offered Rate.</summary>
/// <remarks>
/// <para>
/// Warning: this is the rate fixed in Tokio by JBA. Use JPYLibor if you're interested in the London fixing by BBA.
/// </para>
/// <para>
/// To do: check settlement days and end-of-month adjustment.
/// </para>
/// </remarks>
public sealed class Tibor : IborIndex
{
    /// <summary>Initializes a new instance of the <see cref="Tibor"/> class.</summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Tibor(Period tenor, YieldTermStructure? h = null)
        : base(
            "Tibor",
            tenor,
            2,
            new JPYCurrency(),
            new Japan(),
            BusinessDayConvention.ModifiedFollowing,
            false,
            new Actual365Fixed(),
            h)
    {
    }
}
