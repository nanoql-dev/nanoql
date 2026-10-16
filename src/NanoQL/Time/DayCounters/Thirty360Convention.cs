// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2018 Alexey Indiryakov
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/daycounters/thirty360.hpp ql/time/daycounters/thirty360.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The conventions of <see cref="Thirty360"/>, with QuantLib's values; some name the same rule.</summary>
public enum Thirty360Convention
{
    /// <summary>The US convention, also known as "30/360" or "360/360".</summary>
    USA = 0,

    /// <summary>The Bond Basis convention, also known as "US (ISMA)"; the same rule as <see cref="ISMA"/>.</summary>
    BondBasis = 1,

    /// <summary>
    /// The European convention, also known as "30E/360"; the same rule as <see cref="EurobondBasis"/>.
    /// </summary>
    European = 2,

    /// <summary>The Eurobond Basis convention; the same rule as <see cref="European"/>.</summary>
    EurobondBasis = 3,

    /// <summary>The Italian convention.</summary>
    Italian = 4,

    /// <summary>The German convention, also known as "30/360 German"; the same rule as <see cref="ISDA"/>.</summary>
    German = 5,

    /// <summary>The ISMA convention; the same rule as <see cref="BondBasis"/>.</summary>
    ISMA = 6,

    /// <summary>
    /// The ISDA convention, also known as "30E/360 ISDA" or "30/360 ISDA"; the same rule as <see cref="German"/>.
    /// </summary>
    ISDA = 7,

    /// <summary>The NASD convention.</summary>
    NASD = 8,
}
