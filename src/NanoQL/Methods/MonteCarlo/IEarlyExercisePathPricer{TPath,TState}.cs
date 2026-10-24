// SPDX-FileCopyrightText: Copyright (C) 2006 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/earlyexercisepathpricer.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Methods;

/// <summary>
/// An early-exercise path pricer: the value of exercising an option on a given path at a given time step, the state the
/// regression reads there, and the regression's basis functions of that state.
/// </summary>
/// <typeparam name="TPath">The path type, <see cref="MonteCarloPath"/> or <see cref="MultiPath"/>.</typeparam>
/// <typeparam name="TState">
/// The state type, QuantLib's <c>EarlyExerciseTraits&lt;PathType&gt;::StateType</c>: a <see cref="double"/> on a
/// single path, a <see cref="NanoQL.Numerics.Vector"/> on a multi-path.
/// </typeparam>
/// <remarks>
/// QuantLib's base class, an interface here, as its only members are abstract. Its time and value types are
/// QuantLib's defaults, a time-step index and a real; its traits, which name the state type and a path's length, are the
/// type parameter.
/// </remarks>
public interface IEarlyExercisePathPricer<TPath, TState>
{
    /// <summary>The value of exercising on a path at a time step: QuantLib's <c>operator()</c>.</summary>
    /// <param name="path">The path.</param>
    /// <param name="t">The time step's index on the path's grid.</param>
    /// <returns>The exercise value.</returns>
    double Evaluate(TPath path, int t);

    /// <summary>The state the regression reads on a path at a time step.</summary>
    /// <param name="path">The path.</param>
    /// <param name="t">The time step's index on the path's grid.</param>
    /// <returns>The state.</returns>
    TState State(TPath path, int t);

    /// <summary>The regression's basis functions of the state.</summary>
    /// <returns>The basis functions.</returns>
    ImmutableArray<Func<TState, double>> BasisSystem();
}
