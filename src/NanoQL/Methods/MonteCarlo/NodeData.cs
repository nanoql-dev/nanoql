// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/nodedata.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>
/// The data a path gives at an exercise node: the exercise value, the cash flows up to the next exercise, the basis or
/// exercise variables, the control's value, and whether the path reached the node, each in numeraire units.
/// </summary>
/// <remarks>
/// A working type: the node-data collection writes it and the regression and the parametric optimisation add into its
/// <see cref="CumulatedCashFlows"/>.
/// </remarks>
[WorkingType]
public sealed class NodeData
{
    /// <summary>Gets or sets the exercise value.</summary>
    public double ExerciseValue { get; set; }

    /// <summary>Gets or sets the cash flows from this node to the next.</summary>
    public double CumulatedCashFlows { get; set; }

    /// <summary>Gets or sets the basis functions' values, or the parametric exercise's variables.</summary>
    public double[] Values { get; set; } = [];

    /// <summary>Gets or sets the control's value.</summary>
    public double ControlValue { get; set; }

    /// <summary>Gets or sets a value indicating whether the path reached the node.</summary>
    public bool IsValid { get; set; }
}
