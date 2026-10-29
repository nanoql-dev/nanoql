// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/hybridsimulatedannealing.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The hybrid simulated annealing's probability, which decides whether a new point is accepted: QuantLib's
/// <c>Probability</c> template parameter.
/// </summary>
/// <remarks>
/// An extension point, as in QuantLib's concept, <c>bool operator()(Real currentValue, Real newValue, const Array&amp;
/// temp)</c>. A probability may keep state between calls, its generators'; the annealing copies it
/// (<see cref="Clone"/>) for each minimization.
/// </remarks>
/// <typeparam name="TSelf">The probability's own type.</typeparam>
[Experimental("NQL0001")]
public interface IAnnealingProbability<TSelf>
    where TSelf : IAnnealingProbability<TSelf>
{
    /// <summary>Whether the new point is accepted (QuantLib's <c>operator()</c>).</summary>
    /// <param name="currentValue">The current point's value.</param>
    /// <param name="newValue">The new point's value.</param>
    /// <param name="temp">Each dimension's temperature.</param>
    /// <returns><see langword="true"/> to accept it.</returns>
    bool Accept(double currentValue, double newValue, Vector temp);

    /// <summary>A copy, its state included: the copy's next call equals this probability's.</summary>
    /// <returns>The copy.</returns>
    TSelf Clone();
}
