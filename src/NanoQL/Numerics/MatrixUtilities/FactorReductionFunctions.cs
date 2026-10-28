// SPDX-FileCopyrightText: Copyright (C) 2009 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/factorreduction.hpp ql/math/matrixutilities/factorreduction.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>
/// The reduction of a correlation matrix to a single factor's loadings by the principal factor iteration (Harman,
/// <i>Modern Factor Analysis</i>, 1976, chapter 9): the diagonal replaced by the communalities, the loadings the
/// principal eigenvector scaled by the root of its eigenvalue, until they settle.
/// </summary>
/// <remarks>
/// <para>
/// It assumes the reduction is possible: where the dependence isn't one factor's, loadings may exceed 1. QuantLib reads
/// the loadings from the matrix's row at the largest eigenvalue's index, the input's first row, where its own
/// commented-out eigenvectors belong: on a one-factor matrix with loadings (0.9, 0.7, 0.5, 0.3, 0.8) it returns
/// (0.546, 0.63, 0.45, 0.27, 0.72). Here the loadings are the principal eigenvector, its sign the decomposition's (the
/// first loading positive), times the root of its eigenvalue, on the diagonal patched with their squares, which
/// returns the one-factor matrix's loadings exactly; the relative change that stops the iteration divides by the
/// previous loading's magnitude, so a negative loading converges too, and a loading of exactly 0 counts its successor's
/// magnitude, where 0/0 would stop the iteration at its first pass whenever the first loading is 0. And QuantLib refuses a reduction that converges
/// on its last allowed pass; here only one that hasn't converged is refused (the S-core plan's CR7 (iv), a documented
/// divergence). A matrix below 2 × 2, where QuantLib's initial guess divides by 0, is refused (CR7 (xxix)).
/// </para>
/// </remarks>
public static class FactorReductionFunctions
{
    private const double Tolerance = 1.0e-6;

    /// <summary>The single factor's loadings of the correlation matrix <paramref name="mtrx"/>.</summary>
    /// <param name="mtrx">The correlation matrix, square, at least 2 × 2; it is copied.</param>
    /// <param name="maxIters">The most iterations after the first.</param>
    /// <returns>The loadings, one per variable.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mtrx"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="mtrx"/> isn't square, or is below 2 × 2.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxIters"/> is negative.</exception>
    /// <exception cref="NanoQLException">The loadings haven't converged after the iterations allowed.</exception>
    public static ImmutableArray<double> FactorReduction(Matrix mtrx, int maxIters = 25)
    {
        ArgumentNullException.ThrowIfNull(mtrx);
        ArgumentOutOfRangeException.ThrowIfNegative(maxIters);
        if (!(mtrx.Rows == mtrx.Columns))
        {
            throw new ArgumentException("Input matrix is not square", nameof(mtrx));
        }

        int n = mtrx.Columns;
        if (!(n >= 2))
        {
            throw new ArgumentException($"a {n} x {n} matrix has no factor to reduce to", nameof(mtrx));
        }

        Matrix m = mtrx.Clone();

        // Initial guess value
        var previousCorrels = new double[n];
        for (int iCol = 0; iCol < n; iCol++)
        {
            for (int iRow = 0; iRow < n; iRow++)
            {
                previousCorrels[iCol] += m[iRow, iCol] * m[iRow, iCol];
            }

            previousCorrels[iCol] = Math.Sqrt((previousCorrels[iCol] - 1.0) / (n - 1.0));
        }

        // iterative solution
        int iteration = 0;
        double distance;
        do
        {
            // patch Matrix diagonal with the communalities
            for (int iCol = 0; iCol < n; iCol++)
            {
                m[iCol, iCol] = previousCorrels[iCol] * previousCorrels[iCol];
            }

            // compute eigenvector decomposition
            var ssDec = new SymmetricSchurDecomposition(m);
            Matrix eigenVect = ssDec.Eigenvectors();
            Vector eigenVals = ssDec.Eigenvalues();
            int iMax = MaxElement(eigenVals.AsSpan());
            double scale = Math.Sqrt(eigenVals[iMax]);
            var newCorrels = new double[n];
            var distances = new double[n];
            for (int iCol = 0; iCol < n; iCol++)
            {
                double thisCorrel = scale * eigenVect[iCol, iMax];
                newCorrels[iCol] = thisCorrel;
                // a loading of exactly 0, a variable uncorrelated with the rest, by the new loading's magnitude: the
                // relative change's 0/0 would be NaN, which no comparison stops on
                distances[iCol] = previousCorrels[iCol] == 0.0
                    ? Math.Abs(thisCorrel)
                    : Math.Abs(thisCorrel - previousCorrels[iCol]) / Math.Abs(previousCorrels[iCol]);
            }

            previousCorrels = newCorrels;
            distance = distances[MaxElement(distances)];
        }
        while (distance > Tolerance && ++iteration <= maxIters);

        // the last allowed pass may converge
        if (distance > Tolerance)
        {
            throw new NanoQLException($"convergence not reached after {iteration} iterations");
        }

        return [.. previousCorrels];
    }

    // std::max_element: the first element no other exceeds
    private static int MaxElement(ReadOnlySpan<double> values)
    {
        int largest = 0;
        for (int i = 1; i < values.Length; ++i)
        {
            if (values[largest] < values[i])
            {
                largest = i;
            }
        }

        return largest;
    }
}
