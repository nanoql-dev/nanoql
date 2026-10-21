// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/boundarycondition.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>The side of the grid a boundary condition applies to: QuantLib's <c>BoundaryCondition::Side</c>.</summary>
public enum BoundaryConditionSide
{
    /// <summary>No side.</summary>
    None,

    /// <summary>The upper side.</summary>
    Upper,

    /// <summary>The lower side.</summary>
    Lower,
}
