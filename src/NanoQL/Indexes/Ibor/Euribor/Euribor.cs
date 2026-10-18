// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2006 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2009 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chiara Fornarola
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/euribor.hpp ql/indexes/ibor/euribor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The Euribor index: the rate fixed by the European Central Bank, not the London fixing.</summary>
/// <remarks>Unsealed, as in QuantLib, where the tenor classes (<see cref="Euribor6M"/>, ...) derive from it.</remarks>
public class Euribor : IborIndex
{
    /// <summary>Initializes a new instance of the <see cref="Euribor"/> class.</summary>
    /// <param name="tenor">The tenor, in weeks, months or years.</param>
    /// <param name="h">The forwarding curve, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentException">The tenor is in days.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The tenor's unit is shorter than a day.</exception>
    public Euribor(Period tenor, YieldTermStructure? h = null)
        : base(
            "Euribor",
            tenor,
            2, // settlement days
            new EURCurrency(),
            new TARGET(),
            EuriborConventions.Convention(tenor),
            EuriborConventions.EndOfMonth(tenor),
            new Actual360(),
            h)
    {
        EuriborConventions.CheckTenor(this);
    }
}
