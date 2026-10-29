// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The experimental maths against a second route to the same values (the S-xrt plan's §2.5): the Moore-Penrose inverse
/// of an invertible matrix is its inverse; Laplace interpolation of complete data is the data; the piecewise integral of
/// a smooth function is the integrator's over the whole interval; the Clayton generator at a negative θ samples
/// Clayton's copula, which QuantLib's test leaves out; and the Ziggurat generator's moments and tail are the standard
/// normal's.
/// </summary>
public class MathTwoRouteTests
{
    [Fact]
    public void TheInverseOfAnInvertibleMatrixIsItsInverse()
    {
        var A = new Matrix(new double[,] { { 4.0, 1.0, 0.5 }, { 1.0, 3.0, -1.0 }, { 0.5, -1.0, 2.0 } });
        Matrix P = MoorePenroseInverseFunctions.MoorePenroseInverse(A);
        Matrix I = MatrixFunctions.Inverse(A);
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                Assert.True(Math.Abs(P[i, j] - I[i, j]) <= 1e-14, $"({i}, {j}): {P[i, j]} for {I[i, j]}");
            }
        }
    }

    [Fact]
    public void LaplaceInterpolationOfCompleteDataIsTheData()
    {
        var A = new Matrix(new double[,] { { 1.0, 2.0, 4.0 }, { 6.0, 6.5, 7.0 }, { 5.0, 3.0, 2.0 } });
        Matrix filled = LaplaceInterpolationFunctions.LaplaceInterpolation(A, [1.0, 2.0, 4.0], [1.0, 3.0, 7.0]);
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                Assert.Equal(A[i, j], filled[i, j]);
            }
        }
    }

    [Fact]
    public void ThePiecewiseIntegralOfASmoothFunctionIsTheWholeIntegral()
    {
        var simpson = new SimpsonIntegral(1e-12, 25);
        var piecewise = new PiecewiseIntegral(simpson, [0.5, 1.5, 3.0]);
        double whole = simpson.Integrate(Math.Exp, 0.2, 4.0);
        double pieces = piecewise.Integrate(Math.Exp, 0.2, 4.0);
        Assert.True(Math.Abs(pieces - whole) <= 1e-10, $"{pieces} for {whole}");
        Assert.True(Math.Abs(pieces - (Math.Exp(4.0) - Math.Exp(0.2))) <= 1e-10, $"{pieces}");
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(-0.9)]
    public void TheClaytonGeneratorSamplesClaytonsCopulaBelowZero(double theta)
    {
        // QuantLib's test's check: the empirical copula on a 4 × 4 grid within 0.01, the marginals uniform
        const int samples = 50000;
        var rng = new ClaytonCopulaRng<MersenneTwisterUniformRng>(new MersenneTwisterUniformRng(42), theta);
        var copula = new ClaytonCopula(theta);
        var u = new double[samples];
        var v = new double[samples];
        for (int i = 0; i < samples; i++)
        {
            var s = rng.Next().Value;
            u[i] = s[0];
            v[i] = s[1];
        }

        Assert.True(Math.Abs(u.Average() - 0.5) < 0.01 && Math.Abs(v.Average() - 0.5) < 0.01);
        for (int i = 1; i <= 4; i++)
        {
            for (int j = 1; j <= 4; j++)
            {
                double x = 0.2 * i, y = 0.2 * j;
                double empirical = Enumerable.Range(0, samples).Count(k => u[k] <= x && v[k] <= y) / (double)samples;
                Assert.True(Math.Abs(empirical - copula.Evaluate(x, y)) < 0.01, $"({x}, {y}): {empirical} for {copula.Evaluate(x, y)}");
            }
        }
    }

    [Fact]
    public void TheZigguratsMomentsAreTheNormals()
    {
        const int n = 400000;
        var rng = new ZigguratRng(42);
        double s1 = 0.0, s2 = 0.0, s3 = 0.0, s4 = 0.0;
        int tail = 0;
        for (int i = 0; i < n; i++)
        {
            double x = rng.Next().Value;
            s1 += x;
            s2 += x * x;
            s3 += x * x * x;
            s4 += x * x * x * x;
            if (Math.Abs(x) > 3.0)
            {
                tail++;
            }
        }

        // each within four standard errors: the mean's 1/√n, the variance's √(2/n), the third moment's √(15/n), the
        // fourth's √(96/n), the tail's √(p(1 − p)/n) about P(|x| > 3) = 2.6998e-3
        Assert.True(Math.Abs(s1 / n) < 4.0 * Math.Sqrt(1.0 / n));
        Assert.True(Math.Abs((s2 / n) - 1.0) < 4.0 * Math.Sqrt(2.0 / n));
        Assert.True(Math.Abs(s3 / n) < 4.0 * Math.Sqrt(15.0 / n));
        Assert.True(Math.Abs((s4 / n) - 3.0) < 4.0 * Math.Sqrt(96.0 / n));
        const double p = 2.699796063260207e-3;
        Assert.True(Math.Abs(((double)tail / n) - p) < 4.0 * Math.Sqrt(p * (1 - p) / n), $"{(double)tail / n}");
    }
}
