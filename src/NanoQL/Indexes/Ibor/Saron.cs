// SPDX-FileCopyrightText: Copyright (C) 2025 Paolo D'Elia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/saron.hpp ql/indexes/ibor/saron.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The SARON (Swiss Average Rate Overnight) index.</summary>
public sealed class Saron : OvernightIndex
{
    /// <summary>Initializes a new instance of the <see cref="Saron"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Saron(YieldTermStructure? h = null)
        : base("SARON", 0, new CHFCurrency(), new Switzerland(), new Actual360(), h)
    {
    }
}
