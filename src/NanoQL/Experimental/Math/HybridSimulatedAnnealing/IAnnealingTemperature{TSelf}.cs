// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/hybridsimulatedannealing.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The hybrid simulated annealing's temperature, a schedule T(k) over each dimension's steps: QuantLib's
/// <c>Temperature</c> template parameter.
/// </summary>
/// <remarks>
/// An extension point, as in QuantLib's concept, <c>void operator()(Array&amp; newTemp, const Array&amp; currTemp,
/// const Array&amp; steps)</c>; the annealing passes the same vector as the new and the current temperature, and copies
/// the temperature (<see cref="Clone"/>) for each minimization.
/// </remarks>
/// <typeparam name="TSelf">The temperature's own type.</typeparam>
[Experimental("NQL0001")]
public interface IAnnealingTemperature<TSelf>
    where TSelf : IAnnealingTemperature<TSelf>
{
    /// <summary>Sets the new temperatures (QuantLib's <c>operator()</c>).</summary>
    /// <param name="newTemp">The new temperatures, which it sets.</param>
    /// <param name="currTemp">The current temperatures.</param>
    /// <param name="steps">Each dimension's steps.</param>
    void Update(Vector newTemp, Vector currTemp, Vector steps);

    /// <summary>A copy, its state included.</summary>
    /// <returns>The copy.</returns>
    TSelf Clone();
}
