// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Nicolas Di Césaré
// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/leastsquare.hpp ql/math/optimization/leastsquare.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The cost function of a least-squares problem: r(x) = |f(x)|², f(x) = b − φ(x), its gradient −2 f′(x)ᵀ f(x).
/// </summary>
/// <remarks>Immutable over its problem.</remarks>
public sealed class LeastSquareFunction : CostFunction
{
    private readonly LeastSquareProblem lsp;

    /// <summary>Initializes a new instance of the <see cref="LeastSquareFunction"/> class.</summary>
    /// <param name="lsp">The problem.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lsp"/> is <see langword="null"/>.</exception>
    public LeastSquareFunction(LeastSquareProblem lsp)
    {
        ArgumentNullException.ThrowIfNull(lsp);
        this.lsp = lsp;
    }

    /// <inheritdoc/>
    /// <returns>The square of the residuals' norm.</returns>
    public override double Value(Vector x)
    {
        // size of target and function to fit vectors
        Vector target = new(lsp.Size), fct2fit = new(lsp.Size);

        // compute its values
        lsp.TargetAndValue(x, target, fct2fit);

        // do the difference
        Vector diff = target - fct2fit;

        // and compute the scalar product (square of the norm)
        return ArrayFunctions.DotProduct(diff, diff);
    }

    /// <inheritdoc/>
    /// <returns>The squared residuals.</returns>
    public override Vector Values(Vector x)
    {
        // size of target and function to fit vectors
        Vector target = new(lsp.Size), fct2fit = new(lsp.Size);

        // compute its values
        lsp.TargetAndValue(x, target, fct2fit);

        // do the difference
        Vector diff = target - fct2fit;
        return diff * diff;
    }

    /// <inheritdoc/>
    public override void Gradient(Vector grad, Vector x)
    {
        ArgumentNullException.ThrowIfNull(grad);
        ArgumentNullException.ThrowIfNull(x);

        // size of target and function to fit vectors
        Vector target = new(lsp.Size), fct2fit = new(lsp.Size);

        // size of gradient matrix
        var gradFct2fit = new Matrix(lsp.Size, x.Size);

        // compute its values
        lsp.TargetValueAndGradient(x, gradFct2fit, target, fct2fit);

        // do the difference
        Vector diff = target - fct2fit;

        // compute derivative
        Write(grad, -2.0 * (MatrixFunctions.Transpose(gradFct2fit) * diff));
    }

    /// <inheritdoc/>
    public override double ValueAndGradient(Vector grad, Vector x)
    {
        ArgumentNullException.ThrowIfNull(grad);
        ArgumentNullException.ThrowIfNull(x);

        // size of target and function to fit vectors
        Vector target = new(lsp.Size), fct2fit = new(lsp.Size);

        // size of gradient matrix
        var gradFct2fit = new Matrix(lsp.Size, x.Size);

        // compute its values
        lsp.TargetValueAndGradient(x, gradFct2fit, target, fct2fit);

        // do the difference
        Vector diff = target - fct2fit;

        // compute derivative
        Write(grad, -2.0 * (MatrixFunctions.Transpose(gradFct2fit) * diff));

        // and compute the scalar product (square of the norm)
        return ArrayFunctions.DotProduct(diff, diff);
    }

    private static void Write(Vector grad, Vector value)
    {
        for (int i = 0; i < value.Size; ++i)
        {
            grad[i] = value[i];
        }
    }
}
