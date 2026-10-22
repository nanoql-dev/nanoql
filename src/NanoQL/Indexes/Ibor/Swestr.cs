// SPDX-FileCopyrightText: Copyright (C) 2023 Skandinaviska Enskilda Banken AB (publ)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/swestr.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The Swestr (Swedish krona Short Term Rate) index.</summary>
public sealed class Swestr : OvernightIndex
{
    /// <summary>Initializes a new instance of the <see cref="Swestr"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public Swestr(YieldTermStructure? h = null)
        : base("SWESTR", 0, new SEKCurrency(), new Sweden(), new Actual360(), h)
    {
    }
}
