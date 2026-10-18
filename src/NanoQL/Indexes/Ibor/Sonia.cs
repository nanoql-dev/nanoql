// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/sonia.hpp ql/indexes/ibor/sonia.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The Sonia (Sterling Overnight Index Average) rate.</summary>
public sealed class Sonia : OvernightIndex
{
    /// <summary>Initializes a new instance of the <see cref="Sonia"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Sonia(YieldTermStructure? h = null)
        : base("Sonia", 0, new GBPCurrency(), new UnitedKingdom(UnitedKingdomMarket.Exchange), new Actual365Fixed(), h)
    {
    }
}
