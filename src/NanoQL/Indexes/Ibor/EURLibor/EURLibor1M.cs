// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Chiara Fornarola
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/eurlibor.hpp ql/indexes/ibor/eurlibor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The 1-month EUR Libor index.</summary>
public sealed class EURLibor1M : EURLibor
{
    /// <summary>Initializes a new instance of the <see cref="EURLibor1M"/> class.</summary>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    public EURLibor1M(YieldTermStructure? h = null)
        : base(new Period(1, TimeUnit.Months), h)
    {
    }
}
