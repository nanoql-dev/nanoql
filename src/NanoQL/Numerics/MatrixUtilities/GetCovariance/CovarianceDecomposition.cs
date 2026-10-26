// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/getcovariance.hpp ql/math/matrixutilities/getcovariance.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using static NanoQL.Utilities.DataFormattersFunctions;

namespace NanoQL.Numerics;

/// <summary>
/// A covariance matrix decomposed into its variances, standard deviations and correlation matrix. Only the
/// covariance's lower part is used.
/// </summary>
/// <remarks>Immutable and safe to share across threads: each member returns a copy.</remarks>
public sealed class CovarianceDecomposition
{
    private readonly Vector variances;
    private readonly Vector stdDevs;
    private readonly Matrix correlationMatrix;

    /// <summary>Initializes a new instance of the <see cref="CovarianceDecomposition"/> class.</summary>
    /// <param name="covarianceMatrix">The covariance matrix, square and symmetric within the tolerance.</param>
    /// <param name="tolerance">The tolerance of the symmetry check.</param>
    /// <exception cref="ArgumentNullException"><paramref name="covarianceMatrix"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The matrix isn't square, or isn't symmetric within the
    /// tolerance.</exception>
    public CovarianceDecomposition(Matrix covarianceMatrix, double tolerance = 1.0e-12)
    {
        ArgumentNullException.ThrowIfNull(covarianceMatrix);
        Matrix cov = covarianceMatrix;
        int size = cov.Rows;
        int diagonal = Math.Min(cov.Rows, cov.Columns);
        variances = new Vector(diagonal);
        for (int i = 0; i < diagonal; ++i)
        {
            variances[i] = cov[i, i];
        }

        stdDevs = new Vector(size);
        correlationMatrix = new Matrix(size, size);
        if (!(size == cov.Columns))
        {
            throw new ArgumentException(
                $"input covariance matrix must be square, it is [{size}x{cov.Rows}]", nameof(covarianceMatrix));
        }

        for (int i = 0; i < size; ++i)
        {
            stdDevs[i] = Math.Sqrt(variances[i]);
            correlationMatrix[i, i] = 1.0;
            for (int j = 0; j < i; ++j)
            {
                if (!(Math.Abs(cov[i, j] - cov[j, i]) <= tolerance))
                {
                    throw new ArgumentException(
                        $"invalid covariance matrix:\nc[{i}, {j}] = {General(cov[i, j])}\nc[{j}, {i}] = {General(cov[j, i])}",
                        nameof(covarianceMatrix));
                }

                correlationMatrix[i, j] = correlationMatrix[j, i] = cov[i, j] / (stdDevs[i] * stdDevs[j]);
            }
        }
    }

    /// <summary>The variances.</summary>
    /// <returns>A new vector.</returns>
    public Vector Variances() => variances.Clone();

    /// <summary>The standard deviations.</summary>
    /// <returns>A new vector.</returns>
    public Vector StandardDeviations() => stdDevs.Clone();

    /// <summary>The correlation matrix.</summary>
    /// <returns>A new matrix.</returns>
    public Matrix CorrelationMatrix() => correlationMatrix.Clone();
}
