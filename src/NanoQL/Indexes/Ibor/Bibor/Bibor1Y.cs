// SPDX-FileCopyrightText: Copyright (C) 2018 Matthias Groncki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/bibor.hpp ql/indexes/ibor/bibor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The 1-year Bibor index.</summary>
public sealed class Bibor1Y : Bibor
{
    /// <summary>Initializes a new instance of the <see cref="Bibor1Y"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Bibor1Y(YieldTermStructure? h = null)
        : base(new Period(1, TimeUnit.Years), h)
    {
    }
}
