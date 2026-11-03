// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using static NanoQL.Numerics.MatrixFunctions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The least squares' standard errors. QuantLib's <c>GeneralLinearLeastSquares</c> scales the modelling uncertainty by
/// the residuals' standard deviation over n − 2 degrees of freedom whatever the basis' size
/// (<c>generallinearleastsquares.hpp:142-147</c>): for a quadratic fitted to seven points, a multiplier of 0.20401
/// where R's <c>lm</c>, over n − p, gives 0.22809 (QuantLib 1.44). nanoql's errors are R's: σ̂ √[(AᵀA)⁻¹]ⱼⱼ with σ̂²
/// the residual sum of squares over n − p, p the coefficients estimated. QuantLib's checks: that closed form, its own
/// modelling uncertainty times σ̂; and at p = 2, where n − 2 is R's, QuantLib's own errors.
/// </summary>
public class LeastSquaresStandardErrorTests
{
    private static readonly double[] X = [0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0];
    private static readonly double[] Y = [1.1, 1.9, 5.2, 9.8, 17.1, 26.2, 36.7];

    [Fact]
    public void TheStandardErrorsTakeNMinusPDegreesOfFreedom()
    {
        Func<double, double>[] basis = [_ => 1.0, x => x, x => x * x];
        AssertClosedForm(basis);

        // QuantLib's multiplier over n − 2, 0.20401, against R's over n − 3
        var fit = GeneralLinearLeastSquares.Create(X, Y, basis);
        double rss = fit.Residuals().Sum(r => r * r);
        Assert.Equal(0.22809, Math.Sqrt(rss / 4.0), 5);
        Assert.Equal(0.20401, Math.Sqrt(rss / 5.0), 5);
    }

    [Fact]
    public void ForAStraightLineTheErrorsAreQuantLibs()
    {
        // p = 2, where QuantLib's n − 2 is R's
        AssertClosedForm([_ => 1.0, x => x]);
    }

    // The standard errors against σ̂ √[(AᵀA)⁻¹]ⱼⱼ, σ̂² = RSS / (n − p).
    private static void AssertClosedForm(Func<double, double>[] basis)
    {
        var fit = GeneralLinearLeastSquares.Create(X, Y, basis);
        int n = X.Length, p = basis.Length;
        var a = new Matrix(n, p);
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < p; j++)
            {
                a[i, j] = basis[j](X[i]);
            }
        }

        Matrix inverse = Inverse(Transpose(a) * a);
        double sigma = Math.Sqrt(fit.Residuals().Sum(r => r * r) / (n - p));
        Vector standardErrors = fit.StandardErrors();
        Vector error = fit.Error();
        for (int j = 0; j < p; j++)
        {
            double expected = sigma * Math.Sqrt(inverse[j, j]);
            Assert.Equal(expected, standardErrors[j], Math.Abs(expected) * 1e-12);
            Assert.Equal(error[j] * sigma, standardErrors[j], Math.Abs(expected) * 1e-15);
        }
    }
}
