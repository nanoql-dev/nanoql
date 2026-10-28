// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/defaulttype.hpp ql/experimental/credit/defaulttype.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>An atomic default event, a single contractual event: QuantLib's <c>AtomicDefault::Type</c>.</summary>
/// <remarks>QuantLib's synonyms are members of equal value.</remarks>
[Experimental("NQL0001")]
public enum AtomicDefaultType
{
    /// <summary>A restructuring, of one of the restructuring types.</summary>
    Restructuring = 0,

    /// <summary>A bankruptcy.</summary>
    Bankruptcy,

    /// <summary>A failure to pay.</summary>
    FailureToPay,

    /// <summary>A repudiation or moratorium.</summary>
    RepudiationMoratorium,

    /// <summary>An obligation acceleration.</summary>
    Acceleration,

    /// <summary>An obligation default.</summary>
    Default,

    /// <summary>A downgrade; not ISDA's, not in FpML.</summary>
    Downgrade,

    /// <summary>A merger; not ISDA's, not in FpML.</summary>
    MergerEvent,

    /// <summary>A synonym of <see cref="Acceleration"/>.</summary>
    ObligationAcceleration = Acceleration,

    /// <summary>A synonym of <see cref="Default"/>.</summary>
    ObligationDefault = Default,

    /// <summary>A synonym of <see cref="Default"/>.</summary>
    CrossDefault = Default,
}
