// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/getcovariance.hpp ql/math/matrixutilities/getcovariance.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using static NanoQL.Utilities.DataFormattersFunctions;

namespace NanoQL.Numerics;

/// <summary>The free functions of QuantLib's <c>getcovariance.hpp</c>.</summary>
public static class GetCovarianceFunctions
{
    /// <summary>
    /// The covariance matrix from a correlation matrix and standard deviations. Only the correlation's symmetric part
    /// is used, and its diagonal is taken to be 1.
    /// </summary>
    /// <param name="stdDev">The standard deviations.</param>
    /// <param name="corr">The correlation matrix, symmetric with a unit diagonal within the tolerance.</param>
    /// <param name="tolerance">The tolerance of the symmetry and diagonal checks.</param>
    /// <returns>A new matrix.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="corr"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The correlation's size isn't the deviations' number, or it isn't square, symmetric or of unit diagonal within
    /// the tolerance.
    /// </exception>
    public static Matrix GetCovariance(ReadOnlySpan<double> stdDev, Matrix corr, double tolerance = 1.0e-12)
    {
        ArgumentNullException.ThrowIfNull(corr);
        int size = stdDev.Length;
        if (!(corr.Rows == size))
        {
            throw new ArgumentException(
                $"dimension mismatch between volatilities ({size}) and correlation rows ({corr.Rows})", nameof(corr));
        }

        if (!(corr.Columns == size))
        {
            throw new ArgumentException(
                $"correlation matrix is not square: {size} rows and {corr.Columns} columns", nameof(corr));
        }

        var covariance = new Matrix(size, size);
        for (int i = 0; i < size; ++i)
        {
            for (int j = 0; j < i; ++j)
            {
                if (!(Math.Abs(corr[i, j] - corr[j, i]) <= tolerance))
                {
                    throw new ArgumentException(
                        $"correlation matrix not symmetric:\nc[{i},{j}] = {General(corr[i, j])}\nc[{j},{i}] = {General(corr[j, i])}",
                        nameof(corr));
                }

                covariance[i, i] = stdDev[i] * stdDev[i];
                covariance[i, j] = stdDev[i] * stdDev[j] * 0.5 * (corr[i, j] + corr[j, i]);
                covariance[j, i] = covariance[i, j];
            }

            if (!(Math.Abs(corr[i, i] - 1.0) <= tolerance))
            {
                throw new ArgumentException(
                    $"invalid correlation matrix, diagonal element of the {Ordinal(i + 1)} row is {General(corr[i, i])} instead of 1.0",
                    nameof(corr));
            }

            covariance[i, i] = stdDev[i] * stdDev[i];
        }

        return covariance;
    }
}
