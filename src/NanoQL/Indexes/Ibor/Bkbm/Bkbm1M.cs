// SPDX-FileCopyrightText: Copyright (C) 2016 Fabrice Lecuyer
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/bkbm.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The 1-month Bkbm index.</summary>
public sealed class Bkbm1M : Bkbm
{
    /// <summary>Initializes a new instance of the <see cref="Bkbm1M"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Bkbm1M(YieldTermStructure? h = null)
        : base(new Period(1, TimeUnit.Months), h)
    {
    }
}
