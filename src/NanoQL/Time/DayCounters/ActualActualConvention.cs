// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/daycounters/actualactual.hpp ql/time/daycounters/actualactual.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The conventions of <see cref="ActualActual"/>, with QuantLib's values; some name the same rule.</summary>
public enum ActualActualConvention
{
    /// <summary>The ISMA and US Treasury convention; the same rule as <see cref="Bond"/>.</summary>
    ISMA = 0,

    /// <summary>"Actual/Actual (Bond)"; the same rule as <see cref="ISMA"/>.</summary>
    Bond = 1,

    /// <summary>
    /// The ISDA convention, also known as "Actual/Actual", "Act/Act", and according to ISDA also "Actual/365",
    /// "Act/365", and "A/365"; the same rule as <see cref="Historical"/> and <see cref="Actual365"/>.
    /// </summary>
    ISDA = 2,

    /// <summary>"Actual/Actual (Historical)"; the same rule as <see cref="ISDA"/>.</summary>
    Historical = 3,

    /// <summary>"Actual/365" according to ISDA; the same rule as <see cref="ISDA"/>.</summary>
    Actual365 = 4,

    /// <summary>The AFB convention; the same rule as <see cref="Euro"/>.</summary>
    AFB = 5,

    /// <summary>"Actual/Actual (Euro)"; the same rule as <see cref="AFB"/>.</summary>
    Euro = 6,
}
