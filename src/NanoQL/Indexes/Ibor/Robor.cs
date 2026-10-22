// SPDX-FileCopyrightText: Copyright (C) 2018 Matthias Lungwitz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/robor.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The ROBOR rate: the Romanian Interbank Offered Rate fixed by BNR.</summary>
/// <remarks>
/// Conventions are taken from http://www.bnr.ro/files/d/Legislatie/En/RRR.pdf. Roll convention and end of month not yet
/// checked.
/// </remarks>
public sealed class Robor : IborIndex
{
    /// <summary>Initializes a new instance of the <see cref="Robor"/> class.</summary>
    /// <param name="tenor">The tenor.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Robor(Period tenor, YieldTermStructure? h = null)
        : base(
            "ROBOR",
            tenor,
            tenor == new Period(1, TimeUnit.Days) ? 0 : 2,
            new RONCurrency(),
            new Romania(),
            BusinessDayConvention.ModifiedFollowing,
            false,
            new Actual360(),
            h)
    {
    }
}
