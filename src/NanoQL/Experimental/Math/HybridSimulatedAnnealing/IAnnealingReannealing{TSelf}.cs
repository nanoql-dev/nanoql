// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/hybridsimulatedannealing.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The hybrid simulated annealing's reannealing, which rescales each dimension's steps to raise the temperature the
/// more sensitive dimensions see: QuantLib's <c>Reannealing</c> template parameter.
/// </summary>
/// <remarks>
/// An extension point, as in QuantLib's concept, <c>void operator()(Array&amp; steps, const Array&amp; currentPoint, Real
/// aCurrentValue, const Array&amp; currTemp)</c> with <c>setProblem(Problem&amp;)</c>; the annealing copies it
/// (<see cref="Clone"/>) for each minimization and gives the copy that minimization's state.
/// </remarks>
/// <typeparam name="TSelf">The reannealing's own type.</typeparam>
[Experimental("NQL0001")]
public interface IAnnealingReannealing<TSelf>
    where TSelf : IAnnealingReannealing<TSelf>
{
    /// <summary>Gives the reannealing the minimization's state, which it may evaluate (QuantLib's <c>setProblem</c>).</summary>
    /// <param name="P">The minimization's state.</param>
    void SetProblem(ProblemState P);

    /// <summary>Rescales the steps (QuantLib's <c>operator()</c>).</summary>
    /// <param name="steps">Each dimension's steps, which it sets.</param>
    /// <param name="currentPoint">The current point.</param>
    /// <param name="aCurrentValue">The current point's value.</param>
    /// <param name="currTemp">Each dimension's temperature.</param>
    void Reanneal(Vector steps, Vector currentPoint, double aCurrentValue, Vector currTemp);

    /// <summary>A copy, its state included, unbound from any minimization.</summary>
    /// <returns>The copy.</returns>
    TSelf Clone();
}
