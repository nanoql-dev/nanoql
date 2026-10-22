// SPDX-FileCopyrightText: Copyright (C) 2016 Fabrice Lecuyer
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/bbsw.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The 1-month Bbsw index.</summary>
public sealed class Bbsw1M : Bbsw
{
    /// <summary>Initializes a new instance of the <see cref="Bbsw1M"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Bbsw1M(YieldTermStructure? h = null)
        : base(new Period(1, TimeUnit.Months), h)
    {
    }
}
