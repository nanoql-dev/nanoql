// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/latticerules.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The rank-1 lattice rules <see cref="LatticeRule"/> holds, QuantLib's <c>LatticeRule::type</c>.</summary>
public enum LatticeRuleType
{
    /// <summary>Rule A.</summary>
    A,

    /// <summary>Rule B.</summary>
    B,

    /// <summary>Rule C.</summary>
    C,

    /// <summary>Rule D.</summary>
    D,
}
