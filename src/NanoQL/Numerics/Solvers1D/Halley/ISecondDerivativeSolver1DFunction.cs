// SPDX-FileCopyrightText: Copyright (C) 2024 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/solvers1d/halley.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A function with its first and second derivatives, as <see cref="Halley"/>'s solver needs it: the concept QuantLib's
/// solver states as "the passed function object implement a method <c>Real derivative(Real)</c> and
/// <c>Real secondDerivative(Real)</c>".
/// </summary>
public interface ISecondDerivativeSolver1DFunction : IDerivativeSolver1DFunction
{
    /// <summary>The function's second derivative at a point.</summary>
    /// <param name="x">The point.</param>
    /// <returns>f″(x).</returns>
    double SecondDerivative(double x);
}
