// SPDX-FileCopyrightText: Copyright (C) 2013 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/expm.hpp ql/math/matrixutilities/expm.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>
/// The matrix exponential by the ordinary differential equations method (Moler and Van Loan 1978, "Nineteen Dubious
/// Ways to Compute the Exponential of a Matrix"): each column of exp(t·M) solves x' = M·x from a unit vector, by
/// adaptive Runge-Kutta.
/// </summary>
public static class ExpmFunctions
{
    /// <summary>The matrix exponential exp(t·M).</summary>
    /// <param name="m">The square matrix M.</param>
    /// <param name="t">The time t.</param>
    /// <param name="tol">The Runge-Kutta tolerance; QuantLib's default is <c>QL_EPSILON</c>.</param>
    /// <returns>A new matrix, exp(t·M).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="m"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="m"/> isn't square.</exception>
    /// <exception cref="NanoQLException">The Runge-Kutta integration fails.</exception>
    public static Matrix Expm(Matrix m, double t = 1.0, double tol = NumericLimits.QL_EPSILON)
    {
        ArgumentNullException.ThrowIfNull(m);
        int n = m.Rows;
        if (!(n == m.Columns))
        {
            throw new ArgumentException("Expm expects a square matrix", nameof(m));
        }

        var rk = new AdaptiveRungeKutta<double>(tol);
        Matrix copy = m.Clone();

        // implements x = M*y
        ImmutableArray<double> OdeFct(double time, ImmutableArray<double> y)
        {
            var result = new double[copy.Rows];
            for (int i = 0; i < result.Length; i++)
            {
                ReadOnlySpan<double> row = copy.Row(i);
                double sum = 0.0;
                for (int k = 0; k < y.Length; ++k)
                {
                    sum = sum + (y[k] * row[k]);
                }

                result[i] = sum;
            }

            return [.. result];
        }

        var expm = new Matrix(n, n);
        for (int i = 0; i < n; ++i)
        {
            var x0 = new double[n];
            x0[i] = 1.0;

            ImmutableArray<double> r = rk.Evaluate(OdeFct, x0, 0.0, t);
            for (int j = 0; j < n; ++j)
            {
                expm[j, i] = r[j];
            }
        }

        return expm;
    }
}
