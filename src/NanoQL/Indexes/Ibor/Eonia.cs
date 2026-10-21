// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/eonia.hpp ql/indexes/ibor/eonia.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The Eonia (Euro Overnight Index Average) rate fixed by the ECB.</summary>
public sealed class Eonia : OvernightIndex
{
    /// <summary>Initializes a new instance of the <see cref="Eonia"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Eonia(YieldTermStructure? h = null)
        : base("Eonia", 0, new EURCurrency(), new TARGET(), new Actual360(), h)
    {
    }
}
