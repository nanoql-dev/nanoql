// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Nicolas Di Césaré
// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/leastsquare.hpp ql/math/optimization/leastsquare.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A least-squares problem: a target vector b and a function φ(x) to fit to it, f(x) = b − φ(x).
/// </summary>
/// <remarks>
/// An extension point, as in QuantLib: a problem derives from it, and its members write into the caller's vectors and
/// matrix.
/// </remarks>
public abstract class LeastSquareProblem
{
    /// <summary>Initializes a new instance of the <see cref="LeastSquareProblem"/> class.</summary>
    protected LeastSquareProblem()
    {
    }

    /// <summary>Gets the size of the target and of the function to fit.</summary>
    public abstract int Size { get; }

    /// <summary>The target vector and the values of the function to fit at a point.</summary>
    /// <param name="x">The point.</param>
    /// <param name="target">The target, written; of <see cref="Size"/>.</param>
    /// <param name="fct2fit">The values of the function to fit, written; of <see cref="Size"/>.</param>
    public abstract void TargetAndValue(Vector x, Vector target, Vector fct2fit);

    /// <summary>The target vector, the values of the function to fit and the matrix of its derivatives at a point.</summary>
    /// <param name="x">The point.</param>
    /// <param name="gradFct2fit">The derivatives, written; a row per value and a column per element of x.</param>
    /// <param name="target">The target, written; of <see cref="Size"/>.</param>
    /// <param name="fct2fit">The values of the function to fit, written; of <see cref="Size"/>.</param>
    public abstract void TargetValueAndGradient(Vector x, Matrix gradFct2fit, Vector target, Vector fct2fit);
}
