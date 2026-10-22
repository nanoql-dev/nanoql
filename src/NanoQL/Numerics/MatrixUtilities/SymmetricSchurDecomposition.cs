// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/symmetricschurdecomposition.hpp ql/math/matrixutilities/symmetricschurdecomposition.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The eigenvalues and eigenvectors of a real symmetric matrix, by the symmetric threshold Jacobi algorithm.</summary>
/// <remarks>
/// <para>
/// Given a real symmetric matrix S, the Schur decomposition finds the eigenvalues and eigenvectors of S. If D is the
/// diagonal matrix formed by the eigenvalues and U the unitarian matrix of the eigenvectors, S = U·D·Uᵀ. For details on
/// the different Jacobi transformations see "Matrix computation", second edition, by Golub and Van Loan, The Johns
/// Hopkins University Press.
/// </para>
/// <para>
/// The decomposition is computed in the constructor; <see cref="Eigenvalues"/> and <see cref="Eigenvectors"/> return
/// copies. The eigenvalues are sorted in decreasing order, each eigenvector with its first component non-negative. A
/// NaN eigenvalue, or a NaN in two equal eigenvalues' eigenvectors where their comparison reads it, is refused: QuantLib
/// sorts the pairs with a comparison that NaN leaves undefined (a divergence).
/// </para>
/// </remarks>
public sealed class SymmetricSchurDecomposition
{
    private readonly Vector diagonal;
    private readonly Matrix eigenVectors;

    /// <summary>Initializes a new instance of the <see cref="SymmetricSchurDecomposition"/> class.</summary>
    /// <param name="s">The matrix, symmetric.</param>
    /// <exception cref="ArgumentException"><paramref name="s"/> is empty or not square.</exception>
    /// <exception cref="NanoQLException">
    /// The algorithm doesn't converge in 100 iterations, or the sort would read a NaN (a divergence).
    /// </exception>
    public SymmetricSchurDecomposition(Matrix s)
    {
        ArgumentNullException.ThrowIfNull(s);
        diagonal = new Vector(s.Rows);
        eigenVectors = new Matrix(s.Rows, s.Columns, 0.0);

        if (!(s.Rows > 0 && s.Columns > 0))
        {
            throw new ArgumentException("null matrix given", nameof(s));
        }

        if (!(s.Rows == s.Columns))
        {
            throw new ArgumentException("input matrix must be square", nameof(s));
        }

        int size = s.Rows;
        for (int q = 0; q < size; q++)
        {
            diagonal[q] = s[q, q];
            eigenVectors[q, q] = 1.0;
        }

        Matrix ss = s.Clone();

        double[] tmpDiag = [.. diagonal.AsSpan()];
        double[] tmpAccumulate = new double[size];
        double threshold, epsPrec = 1e-15;
        bool keeplooping = true;
        int maxIterations = 100, ite = 1;
        do
        {
            //main loop
            double sum = 0;
            for (int a = 0; a < size - 1; a++)
            {
                for (int b = a + 1; b < size; b++)
                {
                    sum += Math.Abs(ss[a, b]);
                }
            }

            if (sum == 0)
            {
                keeplooping = false;
            }
            else
            {
                /* To speed up computation a threshold is introduced to
                   make sure it is worthy to perform the Jacobi rotation
                */
                if (ite < 5)
                {
                    threshold = 0.2 * sum / ((long)size * size);
                }
                else
                {
                    threshold = 0.0;
                }

                int j, k, l;
                for (j = 0; j < size - 1; j++)
                {
                    for (k = j + 1; k < size; k++)
                    {
                        double sine, rho, cosin, heig, tang, beta;
                        double smll = Math.Abs(ss[j, k]);
                        if (ite > 5
                            && smll < epsPrec * Math.Abs(diagonal[j])
                            && smll < epsPrec * Math.Abs(diagonal[k]))
                        {
                            ss[j, k] = 0;
                        }
                        else if (Math.Abs(ss[j, k]) > threshold)
                        {
                            heig = diagonal[k] - diagonal[j];
                            if (smll < epsPrec * Math.Abs(heig))
                            {
                                tang = ss[j, k] / heig;
                            }
                            else
                            {
                                beta = 0.5 * heig / ss[j, k];
                                tang = 1.0 / (Math.Abs(beta) + Math.Sqrt(1 + (beta * beta)));
                                if (beta < 0)
                                {
                                    tang = -tang;
                                }
                            }

                            cosin = 1 / Math.Sqrt(1 + (tang * tang));
                            sine = tang * cosin;
                            rho = sine / (1 + cosin);
                            heig = tang * ss[j, k];
                            tmpAccumulate[j] -= heig;
                            tmpAccumulate[k] += heig;
                            diagonal[j] -= heig;
                            diagonal[k] += heig;
                            ss[j, k] = 0.0;
                            for (l = 0; l + 1 <= j; l++)
                            {
                                JacobiRotate(ss, rho, sine, l, j, l, k);
                            }

                            for (l = j + 1; l <= k - 1; l++)
                            {
                                JacobiRotate(ss, rho, sine, j, l, l, k);
                            }

                            for (l = k + 1; l < size; l++)
                            {
                                JacobiRotate(ss, rho, sine, j, l, k, l);
                            }

                            for (l = 0; l < size; l++)
                            {
                                JacobiRotate(eigenVectors, rho, sine, l, j, l, k);
                            }
                        }
                    }
                }

                for (k = 0; k < size; k++)
                {
                    tmpDiag[k] += tmpAccumulate[k];
                    diagonal[k] = tmpDiag[k];
                    tmpAccumulate[k] = 0.0;
                }
            }
        }
        while (++ite <= maxIterations && keeplooping);

        if (!(ite <= maxIterations))
        {
            throw new NanoQLException($"Too many iterations ({maxIterations}) reached");
        }

        // sort (eigenvalues, eigenvectors)
        var temp = new (double Value, double[] Vector)[size];
        for (int col = 0; col < size; col++)
        {
            var eigenVector = new double[size];
            for (int row = 0; row < size; row++)
            {
                eigenVector[row] = eigenVectors[row, col];
            }

            temp[col] = (diagonal[col], eigenVector);
        }

        SortDecreasing(temp);
        double maxEv = temp[0].Value;
        for (int col = 0; col < size; col++)
        {
            // check for round-off errors
            diagonal[col] = Math.Abs(temp[col].Value / maxEv) < 1e-16 ? 0.0 : temp[col].Value;
            double sign = 1.0;
            if (temp[col].Vector[0] < 0.0)
            {
                sign = -1.0;
            }

            for (int row = 0; row < size; row++)
            {
                eigenVectors[row, col] = sign * temp[col].Vector[row];
            }
        }
    }

    /// <summary>The eigenvalues, in decreasing order.</summary>
    /// <returns>A new vector.</returns>
    public Vector Eigenvalues() => diagonal.Clone();

    /// <summary>The eigenvectors, one per column in the eigenvalues' order, each with its first component non-negative.</summary>
    /// <returns>A new matrix.</returns>
    public Matrix Eigenvectors() => eigenVectors.Clone();

    //! This routines implements the Jacobi, a.k.a. Givens, rotation
    private static void JacobiRotate(Matrix m, double rot, double dil, int j1, int k1, int j2, int k2)
    {
        double x1, x2;
        x1 = m[j1, k1];
        x2 = m[j2, k2];
        m[j1, k1] = x1 - (dil * (x2 + (x1 * rot)));
        m[j2, k2] = x2 + (dil * (x1 - (x2 * rot)));
    }

    // std::sort(temp.begin(), temp.end(), std::greater<>()): the pairs in decreasing order of eigenvalue, then of
    // eigenvector lexicographically. Pairs equal under the comparison are equal in every bit but a zero's sign, so a
    // stable sort gives MSVC's order. A comparison that reads a NaN breaks the strict weak ordering std::sort needs, so
    // QuantLib's sort is undefined there: refused before it.
    private static void SortDecreasing((double Value, double[] Vector)[] temp)
    {
        int size = temp.Length;
        if (size < 2)
        {
            return;
        }

        foreach ((double value, _) in temp)
        {
            if (double.IsNaN(value))
            {
                throw new NanoQLException("NaN eigenvalue not allowed in a sort");
            }
        }

        for (int a = 0; a < size; a++)
        {
            for (int b = a + 1; b < size; b++)
            {
                if (temp[a].Value == temp[b].Value)
                {
                    _ = CompareVectors(temp[a].Vector, temp[b].Vector);
                }
            }
        }

        var order = new int[size];
        for (int i = 0; i < size; i++)
        {
            order[i] = i;
        }

        Array.Sort(order, (x, y) =>
        {
            int c = Greater(temp[x], temp[y]) ? -1 : Greater(temp[y], temp[x]) ? 1 : 0;
            return c != 0 ? c : x.CompareTo(y);
        });
        var sorted = new (double Value, double[] Vector)[size];
        for (int i = 0; i < size; i++)
        {
            sorted[i] = temp[order[i]];
        }

        sorted.CopyTo(temp, 0);
    }

    // std::greater<> on the pairs: b < a, the pair's operator< reading the vector only where the values are equal.
    private static bool Greater((double Value, double[] Vector) a, (double Value, double[] Vector) b) =>
        b.Value < a.Value || (!(a.Value < b.Value) && CompareVectors(b.Vector, a.Vector) < 0);

    // std::lexicographical_compare's order of two vectors of one size: negative, zero or positive.
    private static int CompareVectors(double[] a, double[] b)
    {
        for (int i = 0; i < a.Length; i++)
        {
            if (double.IsNaN(a[i]) || double.IsNaN(b[i]))
            {
                throw new NanoQLException("NaN eigenvector component not allowed in a sort");
            }

            if (a[i] < b[i])
            {
                return -1;
            }

            if (b[i] < a[i])
            {
                return 1;
            }
        }

        return 0;
    }
}
