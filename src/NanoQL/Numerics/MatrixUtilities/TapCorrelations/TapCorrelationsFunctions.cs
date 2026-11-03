// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/tapcorrelations.hpp ql/math/matrixutilities/tapcorrelations.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The triangular angles parametrization of correlation matrices (Rapisarda, Brigo and Mercurio, "Parameterizing
/// correlations: a geometric interpretation"): a pseudo-root of the correlation matrix whose rows are unit vectors in
/// spherical coordinates.
/// </summary>
/// <remarks>
/// QuantLib's general parametrization checks its angles' count against the rank's formula, which holds for a rank
/// from 1 to the matrix's size, and at size + 1, whose count and loops are rank size's, so its matrix is rank size's;
/// beyond, and for a matrix of size 0, it writes or reads outside its arrays, and its LMM form reads its angles
/// unchecked. Those are refused here.
/// </remarks>
public static class TapCorrelationsFunctions
{
    /// <summary>
    /// The triangular angles parametrized pseudo-root (equation (24)): row i is cos(θ) times the sines of the row's
    /// earlier angles, to the rank, its last element the sines' product.
    /// </summary>
    /// <param name="angles">The angles, (rank − 1)·(2·size − rank)/2 of them.</param>
    /// <param name="matrixSize">The matrix's size, at least 1.</param>
    /// <param name="rank">The rank, from 1 to <paramref name="matrixSize"/> + 1, rank size + 1 giving rank size's matrix.</param>
    /// <returns>The pseudo-root, a new <paramref name="matrixSize"/> square matrix.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="angles"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The size is below 1, or the rank is outside [1, size + 1].
    /// </exception>
    /// <exception cref="ArgumentException">The angles' count doesn't match the size and rank.</exception>
    public static Matrix TriangularAnglesParametrization(Vector angles, int matrixSize, int rank)
    {
        ArgumentNullException.ThrowIfNull(angles);
        ArgumentOutOfRangeException.ThrowIfLessThan(matrixSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(rank, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rank, matrixSize + 1);

        // what if rank == 1?
        if (!((long)(rank - 1) * ((2L * matrixSize) - rank) == 2L * angles.Size))
        {
            throw new ArgumentException("rank-1) * (matrixSize - rank/2) == angles.size()", nameof(angles));
        }

        var m = new Matrix(matrixSize, matrixSize);

        // first row filling
        m[0, 0] = 1.0;
        for (int j = 1; j < matrixSize; ++j)
        {
            m[0, j] = 0.0;
        }

        // next ones...
        int k = 0; // angles index
        for (int i = 1; i < m.Rows; ++i)
        {
            double sinProduct = 1.0;
            int bound = Math.Min(i, rank - 1);
            for (int j = 0; j < bound; ++j)
            {
                m[i, j] = Math.Cos(angles[k]);
                m[i, j] *= sinProduct;
                sinProduct *= Math.Sin(angles[k]);
                ++k;
            }

            m[i, bound] = sinProduct;
            for (int j = bound + 1; j < m.Rows; ++j)
            {
                m[i, j] = 0;
            }
        }

        return m;
    }

    /// <summary>
    /// The LMM form of the triangular angles parametrization: row i is the previous row times sin(θᵢ₋₁), its diagonal
    /// cos(θᵢ₋₁), the first row (1, 0, …).
    /// </summary>
    /// <param name="angles">The angles, at least size − 1 of them.</param>
    /// <param name="matrixSize">The matrix's size, non-negative.</param>
    /// <param name="rank">The rank, which the form doesn't read.</param>
    /// <returns>The pseudo-root, a new <paramref name="matrixSize"/> square matrix.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="angles"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The size is negative.</exception>
    /// <exception cref="ArgumentException">Fewer than size − 1 angles.</exception>
    public static Matrix LmmTriangularAnglesParametrization(Vector angles, int matrixSize, int rank)
    {
        ArgumentNullException.ThrowIfNull(angles);
        ArgumentOutOfRangeException.ThrowIfNegative(matrixSize);
        _ = rank;
        if (!(angles.Size >= matrixSize - 1))
        {
            throw new ArgumentException(
                $"{angles.Size} angles for a {matrixSize} x {matrixSize} matrix, which reads {matrixSize - 1}",
                nameof(angles));
        }

        var m = new Matrix(matrixSize, matrixSize);
        for (int i = 0; i < m.Rows; ++i)
        {
            double cosPhi, sinPhi;
            if (i > 0)
            {
                cosPhi = Math.Cos(angles[i - 1]);
                sinPhi = Math.Sin(angles[i - 1]);
            }
            else
            {
                cosPhi = 1.0;
                sinPhi = 0.0;
            }

            for (int j = 0; j < i; ++j)
            {
                m[i, j] = sinPhi * m[i - 1, j];
            }

            m[i, i] = cosPhi;

            for (int j = i + 1; j < m.Rows; ++j)
            {
                m[i, j] = 0.0;
            }
        }

        return m;
    }

    /// <summary>
    /// <see cref="TriangularAnglesParametrization"/> on unconstrained parameters, each angle θᵢ = π/2 − arctan(xᵢ).
    /// </summary>
    /// <param name="x">The unconstrained parameters.</param>
    /// <param name="matrixSize">The matrix's size, at least 1.</param>
    /// <param name="rank">The rank, from 1 to <paramref name="matrixSize"/> + 1, rank size + 1 giving rank size's matrix.</param>
    /// <returns>The pseudo-root.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="x"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The size is below 1, or the rank is outside [1, size + 1].
    /// </exception>
    /// <exception cref="ArgumentException">The parameters' count doesn't match the size and rank.</exception>
    public static Matrix TriangularAnglesParametrizationUnconstrained(Vector x, int matrixSize, int rank) =>
        TriangularAnglesParametrization(ToAngles(x), matrixSize, rank);

    /// <summary>
    /// <see cref="LmmTriangularAnglesParametrization"/> on unconstrained parameters, each angle θᵢ = π/2 −
    /// arctan(xᵢ).
    /// </summary>
    /// <param name="x">The unconstrained parameters.</param>
    /// <param name="matrixSize">The matrix's size, non-negative.</param>
    /// <param name="rank">The rank, which the form doesn't read.</param>
    /// <returns>The pseudo-root.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="x"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The size is negative.</exception>
    /// <exception cref="ArgumentException">Fewer than size − 1 parameters.</exception>
    public static Matrix LmmTriangularAnglesParametrizationUnconstrained(Vector x, int matrixSize, int rank) =>
        LmmTriangularAnglesParametrization(ToAngles(x), matrixSize, rank);

    /// <summary>
    /// The rank-three triangular angles parametrization (equation (32)): the rows on the 3-D spherical spiral of
    /// parameters α, t₀ and ε.
    /// </summary>
    /// <param name="alpha">The spiral's α.</param>
    /// <param name="t0">The spiral's t₀.</param>
    /// <param name="epsilon">The spiral's ε.</param>
    /// <param name="matrixSize">The number of rows, non-negative.</param>
    /// <returns>The pseudo-root, a new <paramref name="matrixSize"/>-by-3 matrix.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The number of rows is negative.</exception>
    public static Matrix TriangularAnglesParametrizationRankThree(
        double alpha, double t0, double epsilon, int matrixSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(matrixSize);
        var m = new Matrix(matrixSize, 3);
        for (int i = 0; i < m.Rows; ++i)
        {
            double t = t0 * (1 - Math.Exp(epsilon * i));
            double phi = Math.Atan(alpha * t);
            m[i, 0] = Math.Cos(t) * Math.Cos(phi);
            m[i, 1] = Math.Sin(t) * Math.Cos(phi);
            m[i, 2] = -Math.Sin(phi);
        }

        return m;
    }

    /// <summary><see cref="TriangularAnglesParametrizationRankThree"/> with its parameters (α, t₀, ε) in a
    /// vector.</summary>
    /// <param name="parameters">The three parameters α, t₀, ε.</param>
    /// <param name="nbRows">The number of rows, non-negative.</param>
    /// <returns>The pseudo-root.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is null.</exception>
    /// <exception cref="ArgumentException">Not exactly three parameters.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The number of rows is negative.</exception>
    public static Matrix TriangularAnglesParametrizationRankThreeVectorial(Vector parameters, int nbRows)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (!(parameters.Size == 3))
        {
            throw new ArgumentException("the parameter array must contain exactly 3 values", nameof(parameters));
        }

        return TriangularAnglesParametrizationRankThree(parameters[0], parameters[1], parameters[2], nbRows);
    }

    // we convert the unconstrained parameters in angles
    private static Vector ToAngles(Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
        var angles = new Vector(x.Size);
        for (int i = 0; i < x.Size; ++i)
        {
            angles[i] = (MathConstants.M_PI * .5) - Math.Atan(x[i]);
        }

        return angles;
    }
}
