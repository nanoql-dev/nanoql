// SPDX-FileCopyrightText: Copyright (C) 2009 Dirk Eddelbuettel
// SPDX-FileCopyrightText: Copyright (C) 2006, 2009, 2010 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2010 Kakhkhor Abdijalilov
// SPDX-FileCopyrightText: Copyright (C) 2010 Slava Mazur
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/generallinearleastsquares.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// General linear least-squares regression: the coefficients of a linear combination of basis functions that best fits
/// samples, through the singular value decomposition ("Numerical Recipes in C", 2nd edition, Press, Teukolsky,
/// Vetterling, Flannery).
/// </summary>
/// <remarks>
/// <para>
/// The fit is computed by <see cref="Create{TX}"/>, QuantLib's templated constructor, and the object is immutable: its
/// members return copies. Singular values below n·ε times the largest are dropped, as QuantLib drops them.
/// </para>
/// <para>
/// The standard errors are the modelling uncertainty times the residuals' standard deviation over n − p degrees of
/// freedom, p the coefficients the fit estimates (the singular values it keeps: the basis' size at full rank), as R's
/// <c>lm</c> computes them. QuantLib divides by n − 2 whatever the basis (<c>generallinearleastsquares.hpp:144</c>),
/// which is R's only for a straight line, infinite at n = 2 and undefined at n = 1, where <c>n − 2</c> wraps: a
/// documented divergence (the S-van plan's VR4 (vi), <c>Divergences/LeastSquaresStandardErrorTests.cs</c>); at
/// p = 2 the errors are QuantLib's.
/// </para>
/// <para>
/// Samples and values of different sizes are refused, with QuantLib's text, where QuantLib checks the values' size
/// against itself and writes the samples past the matrix's column, or leaves it partly unset.
/// </para>
/// </remarks>
public sealed class GeneralLinearLeastSquares
{
    private readonly Vector a;
    private readonly Vector err;
    private readonly Vector residuals;
    private readonly Vector standardErrors;

    private GeneralLinearLeastSquares(Vector a, Vector err, Vector residuals, Vector standardErrors)
    {
        this.a = a;
        this.err = err;
        this.residuals = residuals;
        this.standardErrors = standardErrors;
    }

    /// <summary>Gets the number of samples.</summary>
    public int Size => residuals.Size;

    /// <summary>Gets the number of basis functions.</summary>
    public int Dim => a.Size;

    /// <summary>Fits a linear combination of basis functions to samples: QuantLib's constructor.</summary>
    /// <typeparam name="TX">The samples' type, QuantLib's <c>xContainer</c>'s value type.</typeparam>
    /// <param name="x">The samples' arguments.</param>
    /// <param name="y">The samples' values.</param>
    /// <param name="v">The basis functions.</param>
    /// <returns>The fit.</returns>
    /// <exception cref="ArgumentNullException">An argument or a basis function is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The arguments and values have different sizes, or there are fewer samples than basis functions.
    /// </exception>
    public static GeneralLinearLeastSquares Create<TX>(
        IReadOnlyList<TX> x, IReadOnlyList<double> y, IReadOnlyList<Func<TX, double>> v)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        ArgumentNullException.ThrowIfNull(v);
        var a = new Vector(v.Count, 0.0);
        var err = new Vector(v.Count, 0.0);
        var residuals = new Vector(y.Count);
        var standardErrors = new Vector(v.Count);

        int n = residuals.Size;
        int m = err.Size;

        // QuantLib compares the values' size with itself; the arguments are the ones that can differ
        if (!(n == x.Count))
        {
            throw new ArgumentException("sample set need to be of the same size", nameof(x));
        }

        if (!(n >= m))
        {
            throw new ArgumentException("sample set is too small", nameof(y));
        }

        int i;

        var matrixA = new Matrix(n, m);
        for (i = 0; i < m; ++i)
        {
            Func<TX, double> f = v[i] ?? throw new ArgumentNullException(nameof(v), "null basis function");
            for (int r = 0; r < n; r++)
            {
                matrixA[r, i] = f(x[r]);
            }
        }

        var svd = new SVD(matrixA);
        Matrix matrixV = svd.V();
        Matrix matrixU = svd.U();
        Vector w = svd.SingularValues();
        double threshold = n * NumericLimits.QL_EPSILON * w[0];

        int estimated = 0;
        for (i = 0; i < m; ++i)
        {
            if (w[i] > threshold)
            {
                double inner = 0.0;
                for (int r = 0; r < n; r++)
                {
                    inner += matrixU[r, i] * y[r];
                }

                double u = inner / w[i];

                for (int j = 0; j < m; ++j)
                {
                    a[j] += u * matrixV[j, i];
                    err[j] += matrixV[j, i] * matrixV[j, i] / (w[i] * w[i]);
                }

                ++estimated;
            }
        }

        err = ArrayFunctions.Sqrt(err);
        Vector tmp = matrixA * a;
        for (int r = 0; r < n; r++)
        {
            residuals[r] = tmp[r] - y[r];
        }

        double chiSq = 0.0;
        for (int r = 0; r < n; r++)
        {
            chiSq += residuals[r] * residuals[r];
        }

        // R's n − p degrees of freedom, p the coefficients estimated, where QuantLib's n − 2 is R's for a line alone
        // (the S-van plan's VR4 (vi))
        double multiplier = Math.Sqrt(chiSq / (n - estimated));
        for (int j = 0; j < m; j++)
        {
            standardErrors[j] = err[j] * multiplier;
        }

        return new GeneralLinearLeastSquares(a, err, residuals, standardErrors);
    }

    /// <summary>The fit's coefficients, one per basis function.</summary>
    /// <returns>The coefficients.</returns>
    public Vector Coefficients() => new(a.AsSpan());

    /// <summary>The residuals, the fit at each sample less its value.</summary>
    /// <returns>The residuals.</returns>
    public Vector Residuals() => new(residuals.AsSpan());

    /// <summary>The coefficients' standard errors, as Excel and R give them.</summary>
    /// <returns>The standard errors.</returns>
    public Vector StandardErrors() => new(standardErrors.AsSpan());

    /// <summary>The modelling uncertainty, as "Numerical Recipes" defines it.</summary>
    /// <returns>The uncertainty.</returns>
    public Vector Error() => new(err.AsSpan());
}
