// SPDX-FileCopyrightText: Copyright (C) 2005, 2016 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/gaussianquadratures.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testMomentBasedGaussianPolynomial, testGaussLaguerreCosinePolynomial (S-core); testNonCentralChiSquared, testNonCentralChiSquaredSumOfNodes (S-xrt)

using NanoQL.Numerics;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class GaussianQuadraturesTests
{
    // qldefines.hpp's QL_EPSILON and mathconstants.hpp's M_PI, M_TWOPI and M_SQRT2, which nanoql keeps internal.
    private const double QL_EPSILON = 2.220446049250313E-16;
    private const double M_PI = 3.141592653589793238462643383280;
    private const double M_TWOPI = M_PI * 2.0;
    private const double M_SQRT2 = 1.41421356237309504880;

    [Fact]
    public void TestJacobi()
    {
        // Testing Gauss-Jacobi integration...

        TestSingleJacobi(new GaussLegendreIntegration(16));
        TestSingleJacobi(new GaussChebyshevIntegration(130));
        TestSingleJacobi(new GaussChebyshev2ndIntegration(130));
        TestSingleJacobi(new GaussGegenbauerIntegration(50, 0.55));
    }

    [Fact]
    public void TestLaguerre()
    {
        // Testing Gauss-Laguerre integration...

        TestSingleLaguerre(new GaussLaguerreIntegration(16));
        TestSingleLaguerre(new GaussLaguerreIntegration(150, 0.01));

        TestSingle(new GaussLaguerreIntegration(16, 1.0), "f(x) = x*exp(-x)", XInvExp, 1.0);
        TestSingle(new GaussLaguerreIntegration(32, 0.9), "f(x) = x*exp(-x)", XInvExp, 1.0);
    }

    [Fact]
    public void TestLaguerreLargeOrder()
    {
        // Testing Gauss-Laguerre integration for large orders...

        // Beyond n ~ 200 the Laguerre weight function w(x) = x^s*exp(-x) underflows
        // to zero at the largest quadrature node, which used to turn the weight
        // computation into an inf*0 -> NaN (see issue #2776). AnalyticHestonEngine
        // relies on GaussLaguerreIntegration(192) (test-suite/hestonmodel.cpp), just
        // below that threshold, so this also covers orders spanning across it.
        // testSingle's tolerance check does not fire on NaN (any comparison with
        // NaN is false), so finiteness is asserted explicitly here first.
        for (Size n = 184; n <= 400; n += 8)
        {
            var quad = new GaussLaguerreIntegration(n);
            Real calculated = quad.Evaluate(InvExp);
            if (!double.IsFinite(calculated))
            {
                Assert.Fail(
                    $"GaussLaguerreIntegration({n}) returned a non-finite result integrating f(x) = exp(-x): {calculated}");
            }
            else
            {
                TestSingle(quad, "f(x) = exp(-x)", InvExp, 1.0);
            }
        }
    }

    [Fact]
    public void TestHermite()
    {
        // Testing Gauss-Hermite integration...

        TestSingle(new GaussHermiteIntegration(16), "f(x) = Gaussian(x)", new NormalDistribution().Evaluate, 1.0);
        TestSingle(new GaussHermiteIntegration(16, 0.5), "f(x) = x*Gaussian(x)", XNormalDistribution, 0.0);
        TestSingle(new GaussHermiteIntegration(64, 0.9), "f(x) = x*x*Gaussian(x)", XXNormalDistribution, 1.0);
    }

    [Fact]
    public void TestHyperbolic()
    {
        // Testing Gauss hyperbolic integration...

        TestSingle(new GaussHyperbolicIntegration(16), "f(x) = 1/cosh(x)", InvCosh, M_PI);
        TestSingle(new GaussHyperbolicIntegration(16), "f(x) = x/cosh(x)", XInvCosh, 0.0);
    }

    [Fact]
    public void TestTabulated()
    {
        // Testing tabulated Gauss-Laguerre integration...

        TestSingleTabulated(x => x, "f(x) = x", 0.0, 1.0e-13);
        TestSingleTabulated(x => x * x, "f(x) = x^2", 2.0 / 3.0, 1.0e-13);
        TestSingleTabulated(x => x * x * x, "f(x) = x^3", 0.0, 1.0e-13);
        TestSingleTabulated(x => x * x * x * x, "f(x) = x^4", 2.0 / 5.0, 1.0e-13);
    }

    [Fact]
    public void TestMultiDimensionalGaussIntegration()
    {
        // Testing multi-dimensional Gaussian quadrature...

        static Real Normal(Vector x) => Math.Exp(-ArrayFunctions.DotProduct(x, x));

        for (Size n = 1; n < 5; ++n)
        {
            int[] ns = [.. Enumerable.Range(1, n)];

            var quad = new MultiDimGaussianIntegration(ns, order => new GaussHermiteIntegration(order));

            const double tol = 1e4 * QL_EPSILON;

            Real calculated = quad.Evaluate(Normal);
            Real expected = Math.Sqrt(Math.Pow(M_PI, n));
            Real diff = Math.Abs(expected - calculated);
            if (diff > tol)
            {
                Assert.Fail(
                    "failed to reproduce multi dimensional Gaussian quadrature"
                    + $"\n    calculated: {calculated:G12}"
                    + $"\n    expected:   {expected:G12}"
                    + $"\n    diff:       {diff:G12}");
            }
        }

        // testing some Gaussian Integrals
        // https://en.wikipedia.org/wiki/Gaussian_integral
        var rng = new MersenneTwisterUniformRng(1234);
        int[] orders = [20, 28, 16, 22];
        Real[] tols = [1e-8, 1e-6, 1e-2, 5e-2];
        for (Size n = 1; n < 5; ++n)
        {
            // create symmetric positive-definite matrix
            var a0 = new Matrix(n, n);
            for (Size i = 0; i < n; ++i)
            {
                for (Size j = 0; j < n; ++j)
                {
                    a0[i, j] = (i == j) ? i + 1 : rng.NextReal();
                }
            }

            Matrix A0 = a0 * MatrixFunctions.Transpose(a0);
            Matrix invA0 = MatrixFunctions.Inverse(A0);
            Real det_2piA = Math.Sqrt(MatrixFunctions.Determinant(M_TWOPI * invA0));

            var quad = new MultiDimGaussianIntegration(orders[..n], order => new GaussHermiteIntegration(order));

            Real calculated = quad.Evaluate(x => Math.Exp(-0.5 * ArrayFunctions.DotProduct(x, A0 * x)));

            Real expected = det_2piA;
            Real diff = Math.Abs(calculated - expected);
            if (diff > tols[n - 1])
            {
                Assert.Fail(
                    "failed to reproduce multi dimensional Gaussian quadrature"
                    + $"\n    dimensions: {n}"
                    + $"\n    calculated: {calculated:G12}"
                    + $"\n    expected:   {expected:G12}"
                    + $"\n    diff:       {diff:G12}"
                    + $"\n    tolerance:  {tols[n - 1]}");
            }
        }

        var a = new Matrix(3, 3);
        for (Size i = 0; i < 3; ++i)
        {
            for (Size j = 0; j < 3; ++j)
            {
                a[i, j] = (i == j) ? i + 1 : rng.NextReal();
            }
        }

        Matrix A = a * MatrixFunctions.Transpose(a);
        Matrix invA = MatrixFunctions.Inverse(A);
        Real sqrt_det_2piA = Math.Sqrt(MatrixFunctions.Determinant(M_TWOPI * invA));

        var quadHigh = new MultiDimGaussianIntegration([22, 18, 26], order => new GaussHermiteIntegration(order));
        var quad2 = new MultiDimGaussianIntegration([2, 2, 2], order => new GaussHermiteIntegration(order));

        for (Size i = 0; i < 3; ++i)
        {
            for (Size j = 0; j < 3; ++j)
            {
                Real expected = sqrt_det_2piA * invA[i, j];

                Size ii = i, jj = j;
                Real calculated = quadHigh.Evaluate(x => x[ii] * x[jj] * Math.Exp(-0.5 * ArrayFunctions.DotProduct(x, A * x)));

                Real diff = Math.Abs(calculated - expected);
                Real tol = 1e-4;
                if (diff > tol)
                {
                    Assert.Fail(
                        "failed to reproduce multi dimensional Gaussian quadrature"
                        + $"\n    calculated: {calculated:G12}"
                        + $"\n    expected:   {expected:G12}"
                        + $"\n    diff:       {diff:G12}"
                        + $"\n    tolerance:  {tol}");
                }

                Matrix inva = MatrixFunctions.Inverse(MatrixFunctions.Transpose(a));
                calculated = quad2.Evaluate(
                    x =>
                    {
                        Vector f = M_SQRT2 * inva * x;
                        return f[ii] * f[jj] * Math.Exp(-ArrayFunctions.DotProduct(x, x));
                    });

                calculated *= MatrixFunctions.Determinant(M_SQRT2 * inva);
                diff = Math.Abs(calculated - expected);
                tol = QL_EPSILON * 1e4;
                if (diff > tol)
                {
                    Assert.Fail(
                        "failed to reproduce multi dimensional Gaussian quadrature"
                        + $"\n    calculated: {calculated:G12}"
                        + $"\n    expected:   {expected:G12}"
                        + $"\n    diff:       {diff:G12}"
                        + $"\n    tolerance:  {tol}");
                }
            }
        }
    }

    [Fact]
    public void TestHighOrderWeightsStayFinite()
    {
        // Testing quadrature weights at high orders...

        // At high orders the outermost nodes move far enough into the tail
        // that the weight function underflows, and a single non-finite weight
        // turns the whole integral into NaN.  #2776 covered Gauss-Laguerre;
        // Gauss-Hermite and the hyperbolic quadrature fail the same way.

        // Note the explicit isfinite check: abs(NaN - expected) > tol is false,
        // so a tolerance test on its own would let a NaN through.
        foreach (Size n in new[] { 200, 300, 400, 500 })
        {
            var laguerre = new GaussLaguerreIntegration(n);
            Real l = laguerre.Evaluate(x => Math.Exp(-x));
            if (!double.IsFinite(l) || Math.Abs(l - 1.0) > 1e-12)
            {
                Assert.Fail(
                    "failed to reproduce Gauss-Laguerre integral"
                    + $"\n    order:      {n}"
                    + $"\n    calculated: {l:G16}"
                    + $"\n    expected:   {1.0:G16}");
            }

            var hermite = new GaussHermiteIntegration(n);
            Real h = hermite.Evaluate(x => Math.Exp(-x * x));
            if (!double.IsFinite(h) || Math.Abs(h - Math.Sqrt(M_PI)) > 1e-12)
            {
                Assert.Fail(
                    "failed to reproduce Gauss-Hermite integral"
                    + $"\n    order:      {n}"
                    + $"\n    calculated: {h:G16}"
                    + $"\n    expected:   {Math.Sqrt(M_PI):G16}");
            }

            var hyperbolic = new GaussHyperbolicIntegration(n);
            Real y = hyperbolic.Evaluate(x => 1.0 / Math.Cosh(x));
            if (!double.IsFinite(y) || Math.Abs(y - M_PI) > 1e-12)
            {
                Assert.Fail(
                    "failed to reproduce Gauss-hyperbolic integral"
                    + $"\n    order:      {n}"
                    + $"\n    calculated: {y:G16}"
                    + $"\n    expected:   {M_PI:G16}");
            }
        }
    }

    private static void TestSingle(GaussianQuadrature I, string tag, Func<Real, Real> f, Real expected)
    {
        Real calculated = I.Evaluate(f);
        if (Math.Abs(calculated - expected) > 1.0e-4)
        {
            Assert.Fail(
                $"integrating{tag}\n"
                + $"    calculated: {calculated}\n"
                + $"    expected:   {expected}");
        }
    }

    // test functions
    private static Real InvExp(Real x) => Math.Exp(-x);

    private static Real XInvExp(Real x) => x * Math.Exp(-x);

    private static Real XNormalDistribution(Real x) => x * new NormalDistribution().Evaluate(x);

    private static Real XXNormalDistribution(Real x) => x * x * new NormalDistribution().Evaluate(x);

    private static Real InvCosh(Real x) => 1 / Math.Cosh(x);

    private static Real XInvCosh(Real x) => x / Math.Cosh(x);

    private static void TestSingleJacobi(GaussianQuadrature I)
    {
        TestSingle(I, "f(x) = 1", x => 1.0, 2.0);
        TestSingle(I, "f(x) = x", x => x, 0.0);
        TestSingle(I, "f(x) = x^2", x => x * x, 2 / 3.0);
        TestSingle(I, "f(x) = sin(x)", Math.Sin, 0.0);
        TestSingle(I, "f(x) = cos(x)", Math.Cos, Math.Sin(1.0) - Math.Sin(-1.0));
        TestSingle(
            I,
            "f(x) = Gaussian(x)",
            new NormalDistribution().Evaluate,
            new CumulativeNormalDistribution().Evaluate(1.0) - new CumulativeNormalDistribution().Evaluate(-1.0));
    }

    private static void TestSingleLaguerre(GaussianQuadrature I)
    {
        TestSingle(I, "f(x) = exp(-x)", InvExp, 1.0);
        TestSingle(I, "f(x) = x*exp(-x)", XInvExp, 1.0);
        TestSingle(I, "f(x) = Gaussian(x)", new NormalDistribution().Evaluate, 0.5);
    }

    private static void TestSingleTabulated(Func<Real, Real> f, string tag, Real expected, Real tolerance)
    {
        Size[] order = [6, 7, 12, 20];
        var quad = new TabulatedGaussLegendre();
        foreach (Size i in order)
        {
            quad = quad.Order(i);
            Real realised = quad.Evaluate(f);
            if (Math.Abs(realised - expected) > tolerance)
            {
                Assert.Fail(
                    $" integrating {tag}\n"
                    + $"    order {i}\n"
                    + $"    realised: {realised}\n"
                    + $"    expected: {expected}");
            }
        }
    }
}
