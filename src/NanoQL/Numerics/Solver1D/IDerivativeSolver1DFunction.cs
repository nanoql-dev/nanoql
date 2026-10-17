// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/solver1d.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A function with its first derivative, as the derivative-based solvers need it (<see cref="NewtonSafe"/>): the
/// concept QuantLib's solvers state as "the passed function object implement a method <c>Real derivative(Real)</c>".
/// </summary>
public interface IDerivativeSolver1DFunction : ISolver1DFunction
{
    /// <summary>The function's first derivative at a point.</summary>
    /// <param name="x">The point.</param>
    /// <returns>f′(x).</returns>
    double Derivative(double x);
}
