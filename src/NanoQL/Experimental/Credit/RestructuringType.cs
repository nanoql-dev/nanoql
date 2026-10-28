// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/defaulttype.hpp ql/experimental/credit/defaulttype.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>A restructuring type: QuantLib's <c>Restructuring::Type</c>.</summary>
/// <remarks>Markit's notation is members of equal value, as QuantLib's enum aliases are.</remarks>
[Experimental("NQL0001")]
public enum RestructuringType
{
    /// <summary>No restructuring.</summary>
    NoRestructuring = 0,

    /// <summary>Modified restructuring.</summary>
    ModifiedRestructuring,

    /// <summary>Modified-modified restructuring.</summary>
    ModifiedModifiedRestructuring,

    /// <summary>Full restructuring.</summary>
    FullRestructuring,

    /// <summary>Any restructuring.</summary>
    AnyRestructuring,

    /// <summary>Markit's name for <see cref="NoRestructuring"/>.</summary>
    XR = NoRestructuring,

    /// <summary>Markit's name for <see cref="ModifiedRestructuring"/>.</summary>
    MR = ModifiedRestructuring,

    /// <summary>Markit's name for <see cref="ModifiedModifiedRestructuring"/>.</summary>
    MM = ModifiedModifiedRestructuring,

    /// <summary>Markit's name for <see cref="FullRestructuring"/>.</summary>
    CR = FullRestructuring,
}
