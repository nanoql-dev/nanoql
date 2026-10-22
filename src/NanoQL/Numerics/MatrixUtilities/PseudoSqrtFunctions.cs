// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Yiping Chen
// SPDX-FileCopyrightText: Copyright (C) 2007 Neil Firth
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/pseudosqrt.hpp ql/math/matrixutilities/pseudosqrt.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using static NanoQL.Numerics.CholeskyDecompositionFunctions;
using static NanoQL.Numerics.MatrixFunctions;
using static NanoQL.Utilities.DataFormattersFunctions;

namespace NanoQL.Numerics;

/// <summary>The pseudo square root of a real symmetric matrix, and its rank-reduced form.</summary>
/// <remarks>
/// <para>
/// For more information see: R. Rebonato and P. Jäckel, The most general methodology to create a valid correlation
/// matrix for risk management and option pricing purposes, The Journal of Risk, 2(2), Winter 1999/2000
/// (http://www.rebonato.com/correlationmatrix.pdf); revised and extended in "Monte Carlo Methods in Finance", by Peter
/// Jäckel, Chapter 6.
/// </para>
/// <para>
/// From QuantLib's master after the pin (#2846, the S-sr plan's SSR5): an empty matrix is refused with "matrix must not
/// be empty" before the decomposition reads it, where 1.44 refuses it in the decomposition with "null matrix given"
/// (<c>Divergences/PseudoSqrtEmptyMatrixTests.cs</c>). The hypersphere and lower-diagonal salvaging read the conjugate
/// gradient's result, whose current value is the last point its line search accepted
/// (<c>Divergences/LineSearchReturnTests.cs</c>).
/// </para>
/// </remarks>
public static class PseudoSqrtFunctions
{
    /// <summary>
    /// The pseudo square root of a real symmetric matrix: the matrix S such that S·Sᵀ = M. If the matrix isn't positive
    /// semi-definite, it can return an approximation of the pseudo square root using a salvaging algorithm.
    /// </summary>
    /// <param name="matrix">The matrix, symmetric.</param>
    /// <param name="sa">The salvaging algorithm. Higham's only works for correlation matrices.</param>
    /// <returns>A new matrix.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sa"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="matrix"/> isn't square, is empty, or has a negative eigenvalue where <paramref name="sa"/> is
    /// <see cref="SalvagingAlgorithmType.None"/> or <see cref="SalvagingAlgorithmType.Principal"/>.
    /// </exception>
    public static Matrix PseudoSqrt(Matrix matrix, SalvagingAlgorithmType sa = SalvagingAlgorithmType.None)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        if (sa is < SalvagingAlgorithmType.None or > SalvagingAlgorithmType.Principal)
        {
            throw new ArgumentOutOfRangeException(nameof(sa), sa, "unknown salvaging algorithm");
        }

        int size = matrix.Rows;

        if (!(size == matrix.Columns))
        {
            throw new ArgumentException(
                $"non square matrix: {size} rows, {matrix.Columns} columns", nameof(matrix));
        }

        if (!(size > 0))
        {
            throw new ArgumentException("matrix must not be empty", nameof(matrix));
        }

        // spectral (a.k.a Principal Component) analysis
        var jd = new SymmetricSchurDecomposition(matrix);
        Vector eigenvalues = jd.Eigenvalues();
        Matrix eigenvectors = jd.Eigenvectors();
        var diagonal = new Matrix(size, size, 0.0);

        // salvaging algorithm
        Matrix result;
        bool negative;
        switch (sa)
        {
            case SalvagingAlgorithmType.None:
                // eigenvalues are sorted in decreasing order
                if (!(eigenvalues[size - 1] >= -1e-16))
                {
                    throw new ArgumentException(
                        $"negative eigenvalue(s) ({Scientific(eigenvalues[size - 1])})", nameof(matrix));
                }

                result = CholeskyDecomposition(matrix, true);
                break;
            case SalvagingAlgorithmType.Spectral:
                // negative eigenvalues set to zero
                for (int i = 0; i < size; i++)
                {
                    diagonal[i, i] = Math.Sqrt(Floor0(eigenvalues[i]));
                }

                result = eigenvectors * diagonal;
                NormalizePseudoRoot(matrix, result);
                break;
            case SalvagingAlgorithmType.Hypersphere:
                // negative eigenvalues set to zero
                negative = false;
                for (int i = 0; i < size; ++i)
                {
                    diagonal[i, i] = Math.Sqrt(Floor0(eigenvalues[i]));
                    if (eigenvalues[i] < 0.0)
                    {
                        negative = true;
                    }
                }

                result = eigenvectors * diagonal;
                NormalizePseudoRoot(matrix, result);

                if (negative)
                {
                    result = HypersphereOptimize(matrix, result, false);
                }

                break;
            case SalvagingAlgorithmType.LowerDiagonal:
                // negative eigenvalues set to zero
                negative = false;
                for (int i = 0; i < size; ++i)
                {
                    diagonal[i, i] = Math.Sqrt(Floor0(eigenvalues[i]));
                    if (eigenvalues[i] < 0.0)
                    {
                        negative = true;
                    }
                }

                result = eigenvectors * diagonal;

                NormalizePseudoRoot(matrix, result);

                if (negative)
                {
                    result = HypersphereOptimize(matrix, result, true);
                }

                break;
            case SalvagingAlgorithmType.Higham:
            {
                int maxIterations = 40;
                double tol = 1e-6;
                result = HighamImplementation(matrix, maxIterations, tol);
                result = CholeskyDecomposition(result, true);
                break;
            }

            default:
            {
                // SalvagingAlgorithmType.Principal
                if (!(eigenvalues[size - 1] >= -10 * NumericLimits.QL_EPSILON))
                {
                    throw new ArgumentException(
                        $"negative eigenvalue(s) ({Scientific(eigenvalues[size - 1])})", nameof(matrix));
                }

                var sqrtEigenvalues = new double[size];
                for (int i = 0; i < size; ++i)
                {
                    sqrtEigenvalues[i] = Math.Sqrt(Floor0(eigenvalues[i]));
                }

                for (int i = 0; i < size; ++i)
                {
                    for (int k = 0; k < size; ++k)
                    {
                        diagonal[k, i] = sqrtEigenvalues[k] * eigenvectors[i, k];
                    }
                }

                result = eigenvectors * diagonal;
                result = 0.5 * (result + Transpose(result));
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// The rank-reduced pseudo square root of a real symmetric matrix: the result has rank ≤ <paramref name="maxRank"/>.
    /// If <paramref name="maxRank"/> is at least the size, the given share of the eigenvalues out of the eigenvalues' sum
    /// is retained. If the matrix isn't positive semi-definite, it can return an approximation using a salvaging
    /// algorithm.
    /// </summary>
    /// <param name="matrix">The matrix, symmetric.</param>
    /// <param name="maxRank">The largest rank, at least 1.</param>
    /// <param name="componentRetainedPercentage">The share of the eigenvalues' sum to retain, in (0, 1].</param>
    /// <param name="sa">
    /// The salvaging algorithm: <see cref="SalvagingAlgorithmType.None"/>, <see cref="SalvagingAlgorithmType.Spectral"/>
    /// or <see cref="SalvagingAlgorithmType.Higham"/>.
    /// </param>
    /// <returns>A new matrix of the matrix's rows and as many columns as the factors retained.</returns>
    /// <remarks>
    /// A retained eigenvalue below 0, which <see cref="SalvagingAlgorithmType.None"/> accepts down to −1e-16 and
    /// <see cref="SalvagingAlgorithmType.Higham"/>'s adjusted matrix keeps to its projections' tolerance, contributes a
    /// zero column, its root floored at 0 as <see cref="SalvagingAlgorithmType.Spectral"/>'s is: a divergence, QuantLib
    /// taking its square root, a NaN column that leaves the others unnormalized. The result equals QuantLib's root at the
    /// rank that keeps the non-negative eigenvalues, with zero columns appended.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sa"/> isn't one of the three, <paramref name="componentRetainedPercentage"/> is outside (0, 1] or
    /// <paramref name="maxRank"/> is below 1.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="matrix"/> isn't square, is empty, or has a negative eigenvalue where <paramref name="sa"/> is
    /// <see cref="SalvagingAlgorithmType.None"/>.
    /// </exception>
    public static Matrix RankReducedSqrt(
        Matrix matrix, int maxRank, double componentRetainedPercentage, SalvagingAlgorithmType sa)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        if (sa is not (SalvagingAlgorithmType.None or SalvagingAlgorithmType.Spectral or SalvagingAlgorithmType.Higham))
        {
            throw new ArgumentOutOfRangeException(nameof(sa), sa, "unknown or invalid salvaging algorithm");
        }

        int size = matrix.Rows;

        if (!(size == matrix.Columns))
        {
            throw new ArgumentException(
                $"non square matrix: {size} rows, {matrix.Columns} columns", nameof(matrix));
        }

        if (!(size > 0))
        {
            throw new ArgumentException("matrix must not be empty", nameof(matrix));
        }

        if (!(componentRetainedPercentage > 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(componentRetainedPercentage), "no eigenvalues retained");
        }

        if (!(componentRetainedPercentage <= 1.0))
        {
            throw new ArgumentOutOfRangeException(nameof(componentRetainedPercentage), "percentage to be retained > 100%");
        }

        if (!(maxRank >= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(maxRank), "max rank required < 1");
        }

        // spectral (a.k.a Principal Component) analysis
        var jd = new SymmetricSchurDecomposition(matrix);
        Vector eigenValues = jd.Eigenvalues();

        // salvaging algorithm
        switch (sa)
        {
            case SalvagingAlgorithmType.None:
                // eigenvalues are sorted in decreasing order
                if (!(eigenValues[size - 1] >= -1e-16))
                {
                    throw new ArgumentException(
                        $"negative eigenvalue(s) ({Scientific(eigenValues[size - 1])})", nameof(matrix));
                }

                break;
            case SalvagingAlgorithmType.Spectral:
                // negative eigenvalues set to zero
                for (int i = 0; i < size; ++i)
                {
                    eigenValues[i] = Floor0(eigenValues[i]);
                }

                break;
            default:
            {
                // SalvagingAlgorithmType.Higham
                int maxIterations = 40;
                double tolerance = 1e-6;
                Matrix adjustedMatrix = HighamImplementation(matrix, maxIterations, tolerance);
                jd = new SymmetricSchurDecomposition(adjustedMatrix);
                eigenValues = jd.Eigenvalues();
                break;
            }
        }

        // factor reduction
        double total = 0.0;
        foreach (double e in eigenValues)
        {
            total += e;
        }

        double enough = componentRetainedPercentage * total;
        if (componentRetainedPercentage == 1.0)
        {
            // numerical glitches might cause some factors to be discarded
            enough *= 1.1;
        }

        // retain at least one factor
        double components = eigenValues[0];
        int retainedFactors = 1;
        for (int i = 1; components < enough && i < size; ++i)
        {
            components += eigenValues[i];
            retainedFactors++;
        }

        // output is granted to have a rank<=maxRank
        retainedFactors = Math.Min(retainedFactors, maxRank);

        var diagonal = new Matrix(size, retainedFactors, 0.0);
        for (int i = 0; i < retainedFactors; ++i)
        {
            // QuantLib takes std::sqrt(eigenValues[i]) of a retained negative eigenvalue, which None (down to -1e-16)
            // and Higham (to its projections' tolerance) keep, giving a NaN column; floored at 0, as Spectral and every
            // pseudoSqrt branch do (Divergences/RankReducedSqrtNegativeEigenvalueTests.cs).
            diagonal[i, i] = Math.Sqrt(Floor0(eigenValues[i]));
        }

        Matrix result = jd.Eigenvectors() * diagonal;

        NormalizePseudoRoot(matrix, result);
        return result;
    }

    // std::max<Real>(x, 0.0): x < 0.0 ? 0.0 : x, a NaN kept.
    private static double Floor0(double x) => x < 0.0 ? 0.0 : x;

    private static void NormalizePseudoRoot(Matrix matrix, Matrix pseudo)
    {
        int size = matrix.Rows;
        if (!(size == pseudo.Rows))
        {
            // #2846's corrected message; 1.44 names the pseudo root's columns.
            throw new NanoQLException(
                $"matrix/pseudo mismatch: matrix rows are {size} while pseudo rows are {pseudo.Rows}");
        }

        int pseudoCols = pseudo.Columns;

        // row normalization
        for (int i = 0; i < size; ++i)
        {
            double norm = 0.0;
            for (int j = 0; j < pseudoCols; ++j)
            {
                norm += pseudo[i, j] * pseudo[i, j];
            }

            if (norm > 0.0)
            {
                double normAdj = Math.Sqrt(matrix[i, i] / norm);
                for (int j = 0; j < pseudoCols; ++j)
                {
                    pseudo[i, j] *= normAdj;
                }
            }
        }
    }

    // Optimization function for hypersphere and lower-diagonal algorithm
    private static Matrix HypersphereOptimize(Matrix targetMatrix, Matrix currentRoot, bool lowerDiagonal)
    {
        int i, j, k, size = targetMatrix.Rows;
        Matrix result = currentRoot.Clone();
        var variance = new Vector(size, 0);
        for (i = 0; i < size; i++)
        {
            variance[i] = Math.Sqrt(targetMatrix[i, i]);
        }

        if (lowerDiagonal)
        {
            Matrix approxMatrix = result * Transpose(result);
            result = CholeskyDecomposition(approxMatrix, true);
            for (i = 0; i < size; i++)
            {
                for (j = 0; j < size; j++)
                {
                    result[i, j] /= Math.Sqrt(approxMatrix[i, i]);
                }
            }
        }
        else
        {
            for (i = 0; i < size; i++)
            {
                for (j = 0; j < size; j++)
                {
                    result[i, j] /= variance[i];
                }
            }
        }

        var optimize = new ConjugateGradient();
        var endCriteria = new EndCriteria(100, 10, 1e-8, 1e-8, 1e-8);
        var costFunction = new HypersphereCostFunction(targetMatrix, variance, lowerDiagonal);
        var constraint = new NoConstraint();

        // hypersphere vector optimization

        if (lowerDiagonal)
        {
            var theta = new Vector(size * (size - 1) / 2);
            const double eps = 1e-16;
            for (i = 1; i < size; i++)
            {
                for (j = 0; j < i; j++)
                {
                    int ij = (i * (i - 1) / 2) + j;
                    theta[ij] = result[i, j];
                    if (theta[ij] > 1 - eps)
                    {
                        theta[ij] = 1 - eps;
                    }

                    if (theta[ij] < -1 + eps)
                    {
                        theta[ij] = -1 + eps;
                    }

                    for (k = 0; k < j; k++)
                    {
                        theta[ij] /= Math.Sin(theta[(i * (i - 1) / 2) + k]);
                        if (theta[ij] > 1 - eps)
                        {
                            theta[ij] = 1 - eps;
                        }

                        if (theta[ij] < -1 + eps)
                        {
                            theta[ij] = -1 + eps;
                        }
                    }

                    theta[ij] = Math.Acos(theta[ij]);
                    if (j == i - 1)
                    {
                        if (result[i, i] < 0)
                        {
                            theta[ij] = -theta[ij];
                        }
                    }
                }
            }

            var p = new Problem(costFunction, constraint, theta.AsSpan());
            ProblemResults minimized = optimize.Minimize(p, endCriteria);
            theta = new Vector(minimized.CurrentValue.AsSpan());
            result.AsSpan().Fill(1.0);
            for (i = 0; i < size; i++)
            {
                for (k = 0; k < size; k++)
                {
                    if (k > i)
                    {
                        result[i, k] = 0;
                    }
                    else
                    {
                        for (j = 0; j <= k; j++)
                        {
                            if (j == k && k != i)
                            {
                                result[i, k] *= Math.Cos(theta[(i * (i - 1) / 2) + j]);
                            }
                            else if (j != i)
                            {
                                result[i, k] *= Math.Sin(theta[(i * (i - 1) / 2) + j]);
                            }
                        }
                    }
                }
            }
        }
        else
        {
            var theta = new Vector(size * (size - 1));
            const double eps = 1e-16;
            for (i = 0; i < size; i++)
            {
                for (j = 0; j < size - 1; j++)
                {
                    int ji = (j * size) + i;
                    theta[ji] = result[i, j];
                    if (theta[ji] > 1 - eps)
                    {
                        theta[ji] = 1 - eps;
                    }

                    if (theta[ji] < -1 + eps)
                    {
                        theta[ji] = -1 + eps;
                    }

                    for (k = 0; k < j; k++)
                    {
                        theta[ji] /= Math.Sin(theta[(k * size) + i]);
                        if (theta[ji] > 1 - eps)
                        {
                            theta[ji] = 1 - eps;
                        }

                        if (theta[ji] < -1 + eps)
                        {
                            theta[ji] = -1 + eps;
                        }
                    }

                    theta[ji] = Math.Acos(theta[ji]);
                    if (j == size - 2)
                    {
                        if (result[i, j + 1] < 0)
                        {
                            theta[ji] = -theta[ji];
                        }
                    }
                }
            }

            var p = new Problem(costFunction, constraint, theta.AsSpan());
            ProblemResults minimized = optimize.Minimize(p, endCriteria);
            theta = new Vector(minimized.CurrentValue.AsSpan());
            result.AsSpan().Fill(1.0);
            for (i = 0; i < size; i++)
            {
                for (k = 0; k < size; k++)
                {
                    for (j = 0; j <= k; j++)
                    {
                        if (j == k && k != size - 1)
                        {
                            result[i, k] *= Math.Cos(theta[(j * size) + i]);
                        }
                        else if (j != size - 1)
                        {
                            result[i, k] *= Math.Sin(theta[(j * size) + i]);
                        }
                    }
                }
            }
        }

        for (i = 0; i < size; i++)
        {
            for (j = 0; j < size; j++)
            {
                result[i, j] *= variance[i];
            }
        }

        return result;
    }

    // Matrix infinity norm. See Golub and van Loan (2.3.10) or
    // <http://en.wikipedia.org/wiki/Matrix_norm>
    private static double NormInf(Matrix M)
    {
        int rows = M.Rows;
        int cols = M.Columns;
        double norm = 0.0;
        for (int i = 0; i < rows; ++i)
        {
            double colSum = 0.0;
            for (int j = 0; j < cols; ++j)
            {
                colSum += Math.Abs(M[i, j]);
            }

            // std::max(norm, colSum)
            norm = norm < colSum ? colSum : norm;
        }

        return norm;
    }

    // Take a matrix and make all the diagonal entries 1.
    private static Matrix ProjectToUnitDiagonalMatrix(Matrix M)
    {
        int size = M.Rows;
        if (!(size == M.Columns))
        {
            throw new NanoQLException("matrix not square");
        }

        Matrix result = M.Clone();
        for (int i = 0; i < size; ++i)
        {
            result[i, i] = 1.0;
        }

        return result;
    }

    // Take a matrix and make all the eigenvalues non-negative
    private static Matrix ProjectToPositiveSemidefiniteMatrix(Matrix M)
    {
        int size = M.Rows;
        if (!(size == M.Columns))
        {
            throw new NanoQLException("matrix not square");
        }

        var diagonal = new Matrix(size, size, 0.0);
        var jd = new SymmetricSchurDecomposition(M);
        Vector eigenvalues = jd.Eigenvalues();
        for (int i = 0; i < size; ++i)
        {
            diagonal[i, i] = Floor0(eigenvalues[i]);
        }

        Matrix eigenvectors = jd.Eigenvectors();
        Matrix result = eigenvectors * diagonal * Transpose(eigenvectors);
        return result;
    }

    // implementation of the Higham algorithm to find the nearest
    // correlation matrix.
    private static Matrix HighamImplementation(Matrix A, int maxIterations, double tolerance)
    {
        int size = A.Rows;
        Matrix R, Y = A.Clone(), X = A.Clone(), deltaS = new(size, size, 0.0);

        Matrix lastX = X.Clone();
        Matrix lastY = Y.Clone();

        for (int i = 0; i < maxIterations; ++i)
        {
            R = Y - deltaS;
            X = ProjectToPositiveSemidefiniteMatrix(R);
            deltaS = X - R;
            Y = ProjectToUnitDiagonalMatrix(X);

            // convergence test: std::max({...}), the first largest by operator<
            double change = NormInf(X - lastX) / NormInf(X);
            double next = NormInf(Y - lastY) / NormInf(Y);
            if (change < next)
            {
                change = next;
            }

            next = NormInf(Y - X) / NormInf(Y);
            if (change < next)
            {
                change = next;
            }

            if (change <= tolerance)
            {
                break;
            }

            lastX = X;
            lastY = Y;
        }

        // ensure we return a symmetric matrix
        for (int i = 0; i < size; ++i)
        {
            for (int j = 0; j < i; ++j)
            {
                Y[i, j] = Y[j, i];
            }
        }

        return Y;
    }
}

//cost function for hypersphere and lower-diagonal algorithm
file sealed class HypersphereCostFunction : CostFunction
{
    private readonly int size;
    private readonly bool lowerDiagonal;
    private readonly Matrix targetMatrix;
    private readonly Vector targetVariance;

    public HypersphereCostFunction(Matrix targetMatrix, Vector targetVariance, bool lowerDiagonal)
    {
        size = targetMatrix.Rows;
        this.lowerDiagonal = lowerDiagonal;
        this.targetMatrix = targetMatrix.Clone();
        this.targetVariance = targetVariance.Clone();
    }

    public override Vector Values(Vector x) => throw new NanoQLException("values method not implemented");

    // QuantLib keeps its working matrices in mutable members; here they are the call's own.
    public override double Value(Vector x)
    {
        int i, j, k;
        var currentRoot = new Matrix(size, size, 1.0);
        if (lowerDiagonal)
        {
            for (i = 0; i < size; i++)
            {
                for (k = 0; k < size; k++)
                {
                    if (k > i)
                    {
                        currentRoot[i, k] = 0;
                    }
                    else
                    {
                        for (j = 0; j <= k; j++)
                        {
                            if (j == k && k != i)
                            {
                                currentRoot[i, k] *= Math.Cos(x[(i * (i - 1) / 2) + j]);
                            }
                            else if (j != i)
                            {
                                currentRoot[i, k] *= Math.Sin(x[(i * (i - 1) / 2) + j]);
                            }
                        }
                    }
                }
            }
        }
        else
        {
            for (i = 0; i < size; i++)
            {
                for (k = 0; k < size; k++)
                {
                    for (j = 0; j <= k; j++)
                    {
                        if (j == k && k != size - 1)
                        {
                            currentRoot[i, k] *= Math.Cos(x[(j * size) + i]);
                        }
                        else if (j != size - 1)
                        {
                            currentRoot[i, k] *= Math.Sin(x[(j * size) + i]);
                        }
                    }
                }
            }
        }

        double temp, error = 0;
        Matrix tempMatrix = MatrixFunctions.Transpose(currentRoot);
        Matrix currentMatrix = currentRoot * tempMatrix;
        for (i = 0; i < size; i++)
        {
            for (j = 0; j < size; j++)
            {
                temp = (currentMatrix[i, j] * targetVariance[i] * targetVariance[j]) - targetMatrix[i, j];
                error += temp * temp;
            }
        }

        return error;
    }
}
