// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/solver1d.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The function a 1-D solver finds a zero of: the concept of QuantLib's <c>F</c> template parameter, whose
/// <c>operator()</c> is <see cref="Evaluate"/>.
/// </summary>
/// <remarks>
/// A solver takes the function as a struct type argument (<c>where TF : struct, ISolver1DFunction</c>), so its calls
/// are direct; each solver also has overloads taking delegates.
/// </remarks>
public interface ISolver1DFunction
{
    /// <summary>The function's value at a point.</summary>
    /// <param name="x">The point.</param>
    /// <returns>f(x).</returns>
    double Evaluate(double x);
}
