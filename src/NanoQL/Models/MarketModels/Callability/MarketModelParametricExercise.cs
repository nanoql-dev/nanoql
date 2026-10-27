// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/callability/marketmodelparametricexercise.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Methods;

namespace NanoQL.Models;

/// <summary>
/// A parametric exercise rule on a market model: a node-data provider whose data are the rule's variables, and the rule.
/// </summary>
/// <remarks>
/// A working type and an extension point (U13; the S-mmp plan's MMPR1 (i), (vi)). QuantLib derives it from
/// <c>MarketModelNodeDataProvider</c> and <c>ParametricExercise</c>; the second is the interface
/// <see cref="IParametricExercise"/> here.
/// </remarks>
public abstract class MarketModelParametricExercise : MarketModelNodeDataProvider, IParametricExercise
{
    /// <summary>The number of data at each exercise: the rule's variables.</summary>
    /// <returns>One count an exercise.</returns>
    public override ImmutableArray<int> NumberOfData() => NumberOfVariables();

    /// <inheritdoc/>
    public abstract ImmutableArray<int> NumberOfVariables();

    /// <inheritdoc/>
    public abstract ImmutableArray<int> NumberOfParameters();

    /// <inheritdoc/>
    public abstract bool Exercise(int exerciseNumber, ReadOnlySpan<double> parameters, ReadOnlySpan<double> variables);

    /// <inheritdoc/>
    public abstract void Guess(int exerciseNumber, Span<double> parameters);

    /// <summary>A deep copy of the exercise rule, its state included.</summary>
    /// <returns>The copy.</returns>
    public abstract MarketModelParametricExercise Clone();
}
