// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/tqreigendecomposition.hpp ql/math/matrixutilities/tqreigendecomposition.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Runtime.InteropServices;

namespace NanoQL.Numerics;

/// <summary>
/// The eigenvalues and eigenvectors of a symmetric tridiagonal matrix by QR steps with an explicit shift (Wilkinson):
/// J.H. Wilkinson and C. Reinsch, Linear Algebra, vol. II of Handbook for Automatic Computation (Springer, 1971);
/// "Numerical Recipes in C", 2nd edition, Press, Teukolsky, Vetterling, Flannery.
/// </summary>
/// <remarks>
/// The eigenvalues are sorted in decreasing order, each eigenvector with its first component non-negative. The
/// decomposition is computed in the constructor; <see cref="Eigenvalues"/> and <see cref="Eigenvectors"/> return
/// copies.
/// <para>
/// QuantLib's iteration has no bound, and on some inputs it never returns: eigenvalues in pairs of opposite sign under
/// <see cref="TqrEigenDecompositionShiftStrategy.NoShift"/>, and, under any strategy, a NaN, an infinite off-diagonal
/// element or entries whose squares overflow. While one eigenvalue converges, the next step is a function of the
/// diagonal and off-diagonal alone, so a state repeated bit for bit proves the iteration endless: Brent's cycle test
/// on that state refuses there, and every input QuantLib answers keeps QuantLib's result
/// (<c>Divergences/TqrEigenDecompositionCycleTests.cs</c>).
/// </para>
/// <para>
/// The test catches the inputs whose state repeats, as a zero diagonal's does, but not every pair: under
/// <see cref="TqrEigenDecompositionShiftStrategy.NoShift"/> a pair whose magnitudes differ slightly converges at their
/// ratio without repeating a state, about 18.7/ε iterations for [[ε, 1], [1, ε]], and a pair equal only to rounding
/// may never repeat one either. Such an input runs as long as QuantLib's, without end once the ratio rounds to 1: a
/// QuantLib defect kept, since no bound on the iterations would separate these inputs from slow ones QuantLib answers.
/// The shifted strategies, the default among them, converge on them.
/// </para>
/// </remarks>
public sealed class TqrEigenDecomposition
{
    private readonly int iter;
    private readonly Vector d;
    private readonly Matrix ev;

    /// <summary>Initializes a new instance of the <see cref="TqrEigenDecomposition"/> class.</summary>
    /// <param name="diag">The diagonal.</param>
    /// <param name="sub">The sub-diagonal, one element shorter.</param>
    /// <param name="calc">Which eigenvectors to compute.</param>
    /// <param name="strategy">The shift strategy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="diag"/> or <paramref name="sub"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="sub"/> isn't one element shorter than the
    /// diagonal.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The iteration repeats a state, where QuantLib's never returns.</exception>
    public TqrEigenDecomposition(
        Vector diag,
        Vector sub,
        TqrEigenDecompositionEigenVectorCalculation calc = TqrEigenDecompositionEigenVectorCalculation.WithEigenVector,
        TqrEigenDecompositionShiftStrategy strategy = TqrEigenDecompositionShiftStrategy.CloseEigenValue)
    {
        ArgumentNullException.ThrowIfNull(diag);
        ArgumentNullException.ThrowIfNull(sub);
        EnumArguments.Check(calc, nameof(calc));
        EnumArguments.Check(strategy, nameof(strategy));

        d = diag.Clone();
        ev = new Matrix(
            (calc == TqrEigenDecompositionEigenVectorCalculation.WithEigenVector) ? d.Size :
            (calc == TqrEigenDecompositionEigenVectorCalculation.WithoutEigenVector) ? 0 :
                                                                                      1,
            d.Size,
            0);
        int n = diag.Size;

        if (!(n == sub.Size + 1))
        {
            throw new ArgumentException("Wrong dimensions", nameof(sub));
        }

        var e = new Vector(n, 0.0);
        sub.AsSpan().CopyTo(e.AsSpan()[1..]);
        for (int i = 0; i < ev.Rows; ++i)
        {
            ev[i, i] = 1.0;
        }

        // Brent's cycle test on (d, e): the state saved at each power of two of the steps for this k
        double[] savedD = new double[n], savedE = new double[n];
        for (int k = n - 1; k >= 1; --k)
        {
            d.AsSpan().CopyTo(savedD);
            e.AsSpan().CopyTo(savedE);
            int power = 1, steps = 1;
            while (!OffDiagIsZero(k, e))
            {
                int l = k;
                while (--l > 0 && !OffDiagIsZero(l, e))
                {
                    // down to the first zero off-diagonal element above row k
                }

                iter++;

                double q = d[l];
                if (strategy != TqrEigenDecompositionShiftStrategy.NoShift)
                {
                    // calculated eigenvalue of 2x2 sub matrix of
                    // [ d_[k-1] e_[k] ]
                    // [  e_[k]  d_[k] ]
                    // which is closer to d_[k+1].
                    double t1 = Math.Sqrt(
                        (0.25 * ((d[k] * d[k]) + (d[k - 1] * d[k - 1])))
                        - (0.5 * d[k - 1] * d[k]) + (e[k] * e[k]));
                    double t2 = 0.5 * (d[k] + d[k - 1]);

                    double lambda =
                        (Math.Abs(t2 + t1 - d[k]) < Math.Abs(t2 - t1 - d[k])) ?
                        t2 + t1 : t2 - t1;

                    if (strategy == TqrEigenDecompositionShiftStrategy.CloseEigenValue)
                    {
                        q -= lambda;
                    }
                    else
                    {
                        q -= ((k == n - 1) ? 1.25 : 1.0) * lambda;
                    }
                }

                // the QR transformation
                double sine = 1.0;
                double cosine = 1.0;
                double u = 0.0;

                bool recoverUnderflow = false;
                for (int i = l + 1; i <= k && !recoverUnderflow; ++i)
                {
                    double h = cosine * e[i];
                    double p = sine * e[i];

                    e[i - 1] = Math.Sqrt((p * p) + (q * q));
                    if (e[i - 1] != 0.0)
                    {
                        sine = p / e[i - 1];
                        cosine = q / e[i - 1];

                        double g = d[i - 1] - u;
                        double t = ((d[i] - g) * sine) + (2 * cosine * h);

                        u = sine * t;
                        d[i - 1] = g + u;
                        q = (cosine * t) - h;

                        for (int j = 0; j < ev.Rows; ++j)
                        {
                            double tmp = ev[j, i - 1];
                            ev[j, i - 1] = (sine * ev[j, i]) + (cosine * tmp);
                            ev[j, i] = (cosine * ev[j, i]) - (sine * tmp);
                        }
                    }
                    else
                    {
                        // recover from underflow
                        d[i - 1] -= u;
                        e[l] = 0.0;
                        recoverUnderflow = true;
                    }
                }

                if (!recoverUnderflow)
                {
                    d[k] -= u;
                    e[k] = q;
                    e[l] = 0.0;
                }

                // the next step depends on (d, e) alone, so a repeat is a cycle QuantLib never leaves
                if (SameBits(d.AsSpan(), savedD) && SameBits(e.AsSpan(), savedE))
                {
                    throw new NanoQLException("the QR iteration repeats a state, so it never converges");
                }

                if (steps == power)
                {
                    d.AsSpan().CopyTo(savedD);
                    e.AsSpan().CopyTo(savedE);
                    power *= 2;
                    steps = 0;
                }

                steps++;
            }
        }

        // sort (eigenvalues, eigenvectors),
        // code taken from symmetricSchureDecomposition.cpp
        var temp = new (double Value, double[] Vector)[n];
        for (int i = 0; i < n; i++)
        {
            double[] eigenVector = new double[ev.Rows];
            for (int j = 0; j < ev.Rows; j++)
            {
                eigenVector[j] = ev[j, i];
            }

            temp[i] = (d[i], eigenVector);
        }

        // std::greater<> on (eigenvalue, eigenvector) pairs: decreasing, ties broken by the vectors' lexicographic
        // order
        Array.Sort(temp, (x, y) => PairLess(y, x) ? -1 : PairLess(x, y) ? 1 : 0);

        // first element is positive
        for (int i = 0; i < n; i++)
        {
            d[i] = temp[i].Value;
            double sign = 1.0;
            if (ev.Rows > 0 && temp[i].Vector[0] < 0.0)
            {
                sign = -1.0;
            }

            for (int j = 0; j < ev.Rows; ++j)
            {
                ev[j, i] = sign * temp[i].Vector[j];
            }
        }
    }

    /// <summary>Gets the number of QR iterations the decomposition took.</summary>
    public int Iterations => iter;

    /// <summary>The eigenvalues, in decreasing order.</summary>
    /// <returns>A copy of the eigenvalues.</returns>
    public Vector Eigenvalues() => d.Clone();

    /// <summary>
    /// The eigenvectors, one per column in the eigenvalues' order: every row, none or the first row, as the
    /// constructor's calculation asked.
    /// </summary>
    /// <returns>A copy of the eigenvectors.</returns>
    public Matrix Eigenvectors() => ev.Clone();

    // see NR for abort assumption as it is
    // not part of the original Wilkinson algorithm
    private bool OffDiagIsZero(int k, Vector e) =>
        Math.Abs(d[k - 1]) + Math.Abs(d[k]) == Math.Abs(d[k - 1]) + Math.Abs(d[k]) + Math.Abs(e[k]);

    private static bool SameBits(ReadOnlySpan<double> x, ReadOnlySpan<double> y) =>
        MemoryMarshal.AsBytes(x).SequenceEqual(MemoryMarshal.AsBytes(y));

    // std::pair's operator<, with std::vector's lexicographic operator< on the second members.
    private static bool PairLess((double Value, double[] Vector) x, (double Value, double[] Vector) y)
    {
        if (x.Value < y.Value)
        {
            return true;
        }

        if (y.Value < x.Value)
        {
            return false;
        }

        for (int i = 0; i < x.Vector.Length && i < y.Vector.Length; i++)
        {
            if (x.Vector[i] < y.Vector[i])
            {
                return true;
            }

            if (y.Vector[i] < x.Vector[i])
            {
                return false;
            }
        }

        return x.Vector.Length < y.Vector.Length;
    }
}
