// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/parametricexercise.hpp ql/methods/montecarlo/parametricexercise.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Methods;

/// <summary>
/// An exercise rule with parameters: at each exercise, whether to exercise given the parameters and the path's
/// variables, and a first guess of the parameters. QuantLib's <c>ParametricExercise</c>.
/// </summary>
/// <remarks>
/// An extension point (U13): QuantLib's mixin base, an interface here (the playbook's renames), which
/// <c>MarketModelParametricExercise</c> implements beside its node-data provider base.
/// </remarks>
public interface IParametricExercise
{
    /// <summary>The number of variables at each exercise.</summary>
    /// <returns>One count an exercise.</returns>
    ImmutableArray<int> NumberOfVariables();

    /// <summary>The number of parameters at each exercise.</summary>
    /// <returns>One count an exercise.</returns>
    ImmutableArray<int> NumberOfParameters();

    /// <summary>Whether to exercise at an exercise, given its parameters and the path's variables.</summary>
    /// <param name="exerciseNumber">The exercise's index.</param>
    /// <param name="parameters">The exercise's parameters.</param>
    /// <param name="variables">The path's variables at the exercise.</param>
    /// <returns><see langword="true"/> to exercise.</returns>
    bool Exercise(int exerciseNumber, ReadOnlySpan<double> parameters, ReadOnlySpan<double> variables);

    /// <summary>Writes a first guess of an exercise's parameters.</summary>
    /// <param name="exerciseNumber">The exercise's index.</param>
    /// <param name="parameters">The parameters written, the exercise's count of them.</param>
    void Guess(int exerciseNumber, Span<double> parameters);
}
