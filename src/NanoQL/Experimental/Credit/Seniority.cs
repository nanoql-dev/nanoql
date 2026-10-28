// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/defaulttype.hpp ql/experimental/credit/defaulttype.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>The seniority of a bond; also the ISDA tiers and seniorities of CDS conventional spreads.</summary>
/// <remarks>Markit's names are members of equal value, as QuantLib's enum aliases are.</remarks>
[Experimental("NQL0001")]
public enum Seniority
{
    /// <summary>Senior secured domestic.</summary>
    SecDom = 0,

    /// <summary>Senior unsecured foreign.</summary>
    SnrFor,

    /// <summary>Subordinated lower tier 2.</summary>
    SubLT2,

    /// <summary>Junior subordinated upper tier 2.</summary>
    JrSubT2,

    /// <summary>Preference shares, tier 1.</summary>
    PrefT1,

    /// <summary>No seniority assigned, which allows the default recovery quote.</summary>
    NoSeniority,

    /// <summary>Markit's name for <see cref="SecDom"/>.</summary>
    SeniorSec = SecDom,

    /// <summary>Markit's name for <see cref="SnrFor"/>.</summary>
    SeniorUnSec = SnrFor,

    /// <summary>Markit's name for <see cref="PrefT1"/>.</summary>
    SubTier1 = PrefT1,

    /// <summary>Markit's name for <see cref="JrSubT2"/>.</summary>
    SubUpperTier2 = JrSubT2,

    /// <summary>Markit's name for <see cref="SubLT2"/>.</summary>
    SubLoweTier2 = SubLT2,
}
