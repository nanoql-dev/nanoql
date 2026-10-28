// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/tapcorrelations.hpp ql/math/matrixutilities/tapcorrelations.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The cost of a parametrized pseudo-root against a target correlation matrix in the Frobenius norm: the strict lower
/// triangle of P·Pᵀ − target as the values, their squares' sum as the value.
/// </summary>
public sealed class FrobeniusCostFunction : CostFunction
{
    private readonly Matrix target;
    private readonly Func<Vector, int, int, Matrix> f;
    private readonly int matrixSize;
    private readonly int rank;

    /// <summary>Initializes a new instance of the <see cref="FrobeniusCostFunction"/> class.</summary>
    /// <param name="target">The target correlation matrix; it is copied.</param>
    /// <param name="f">The parametrization: the pseudo-root of the parameters, the size and the rank.</param>
    /// <param name="matrixSize">The size passed to the parametrization.</param>
    /// <param name="rank">The rank passed to the parametrization.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="f"/> is null.</exception>
    public FrobeniusCostFunction(Matrix target, Func<Vector, int, int, Matrix> f, int matrixSize, int rank)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(f);
        this.target = target.Clone();
        this.f = f;
        this.matrixSize = matrixSize;
        this.rank = rank;
    }

    /// <summary>The squared Frobenius distance of the strict lower triangles.</summary>
    /// <param name="x">The parameters.</param>
    /// <returns>The sum of the values' squares.</returns>
    public override double Value(Vector x)
    {
        Vector temp = Values(x);
        return ArrayFunctions.DotProduct(temp, temp);
    }

    /// <summary>The strict lower triangle of P·Pᵀ − target, by rows.</summary>
    /// <param name="x">The parameters.</param>
    /// <returns>A new vector of the differences.</returns>
    public override Vector Values(Vector x)
    {
        var result = new Vector(target.Rows * (target.Columns - 1) / 2);
        Matrix pseudoRoot = f(x, matrixSize, rank);
        Matrix differences = (pseudoRoot * MatrixFunctions.Transpose(pseudoRoot)) - target;
        int k = 0;

        // then we store the elementwise differences in a vector.
        for (int i = 0; i < target.Rows; ++i)
        {
            for (int j = 0; j < i; ++j)
            {
                result[k] = differences[i, j];
                ++k;
            }
        }

        return result;
    }
}
