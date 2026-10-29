// SPDX-FileCopyrightText: Copyright (C) 2011, 2012 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdmvppstepcondition.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>A virtual power plant's operating parameters, as its step conditions read them.</summary>
/// <param name="HeatRate">The heat rate: fuel burned per unit of power.</param>
/// <param name="PMin">The minimum output while running.</param>
/// <param name="PMax">The maximum output.</param>
/// <param name="TMinUp">The minimum up time, in steps.</param>
/// <param name="TMinDown">The minimum down time, in steps.</param>
/// <param name="StartUpFuel">The fuel a start burns.</param>
/// <param name="StartUpFixCost">A start's fixed cost.</param>
/// <param name="FuelCostAddon">The cost added to each unit of fuel.</param>
[Experimental("NQL0001")]
public readonly record struct FdmVPPStepConditionParams(
    double HeatRate,
    double PMin,
    double PMax,
    int TMinUp,
    int TMinDown,
    double StartUpFuel,
    double StartUpFixCost,
    double FuelCostAddon);
