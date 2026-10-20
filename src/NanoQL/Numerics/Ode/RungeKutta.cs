// SPDX-FileCopyrightText: Copyright (C) 2012 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/ode/adaptiverungekutta.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib-Python's <c>RungeKutta</c>: an <see cref="AdaptiveRungeKutta{T}"/> over real values.
/// </summary>
public sealed class RungeKutta : AdaptiveRungeKutta<double>
{
    /// <summary>Initializes a new instance of the <see cref="RungeKutta"/> class.</summary>
    /// <param name="eps">The error prescribed for the solution.</param>
    /// <param name="h1">The first step's size.</param>
    /// <param name="hmin">The smallest step size allowed.</param>
    public RungeKutta(double eps = 1.0e-6, double h1 = 1.0e-4, double hmin = 0.0)
        : base(eps, h1, hmin)
    {
    }
}
