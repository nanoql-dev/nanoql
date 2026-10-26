// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Numerics;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception type, message and parameter name of the refusals of the basket core's mathematics, each QuantLib's
/// text (the S-exo plan's ED14): <c>getCovariance</c> and <see cref="CovarianceDecomposition"/> (whose size message
/// prints the rows twice, as QuantLib's does, <c>getcovariance.cpp:31-32</c>), the Householder reflection of the zero
/// vector, Halley's solver out of evaluations, the multi-cubic spline's checks of its flat inputs (D1-5), and the
/// single-factor engine's root solver, internal as in QuantLib's <c>detail</c> namespace and read through reflection.
/// </summary>
public class BasketMathErrorTests
{
    [Fact]
    public void GetCovarianceRefusals()
    {
        Refused(
            () => GetCovarianceFunctions.GetCovariance([0.2, 0.3], Correlation(3)),
            "corr",
            "dimension mismatch between volatilities (2) and correlation rows (3)");
        Refused(
            () => GetCovarianceFunctions.GetCovariance([0.2, 0.3], new Matrix(2, 3, 0.0)),
            "corr",
            "correlation matrix is not square: 2 rows and 3 columns");
        Refused(
            () => GetCovarianceFunctions.GetCovariance([0.2, 0.3], new Matrix(new[,] { { 1.0, 0.5 }, { 0.4, 1.0 } })),
            "corr",
            "correlation matrix not symmetric:\nc[1,0] = 0.4\nc[0,1] = 0.5");
        Refused(
            () => GetCovarianceFunctions.GetCovariance([0.2, 0.3], new Matrix(new[,] { { 1.0, 0.5 }, { 0.5, 0.99 } })),
            "corr",
            "invalid correlation matrix, diagonal element of the 2nd row is 0.99 instead of 1.0");
    }

    [Fact]
    public void CovarianceDecompositionRefusals()
    {
        Refused(
            () => new CovarianceDecomposition(new Matrix(2, 3, 0.0)),
            "covarianceMatrix",
            "input covariance matrix must be square, it is [2x2]");
        Refused(
            () => new CovarianceDecomposition(new Matrix(new[,] { { 0.04, 0.01 }, { 0.02, 0.09 } })),
            "covarianceMatrix",
            "invalid covariance matrix:\nc[1, 0] = 0.02\nc[0, 1] = 0.01");
    }

    [Fact]
    public void HouseholderAndHalleyRefusals()
    {
        var reflection = new HouseholderReflection(new Vector([1.0, 0.0]));
        Refused(() => reflection.ReflectionVector(new Vector(2)), "a", "vector of length zero given");

        // x³ - 2 from far away, with too few evaluations to converge
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => new Halley().SetMaxEvaluations(3).Solve(
                x => (x * x * x) - 2.0, x => 3.0 * x * x, x => 6.0 * x, 1e-12, 50.0, 0.0, 100.0));
        Assert.Equal("maximum number of function evaluations (3) exceeded", e.Message);
    }

    [Fact]
    public void MultiCubicSplineInputChecks()
    {
        Refused(() => new MultiCubicSpline([], []), "grid", "the grid's dimension (0) must be 1 to 15");
        Refused(
            () => new MultiCubicSpline([[0.0, 1.0, 2.0, 3.0], [0.0, 1.0, 2.0, 3.0]], new double[16], [true]),
            "extrapolation",
            "1 extrapolation flags given for 2 dimensions");
        Refused(
            () => new MultiCubicSpline([[0.0, 1.0, 2.0, 3.0], [0.0, 1.0, 2.0, 3.0]], new double[5]),
            "values",
            "5 values given for a grid of 16 points");
        var spline = new MultiCubicSpline([[0.0, 1.0, 2.0, 3.0], [0.0, 1.0, 2.0, 3.0]], new double[16]);
        Refused(() => spline.Evaluate([0.5]), "x", "1 coordinates given for 2 dimensions");
    }

    [Fact]
    public void RootSolverRefusals()
    {
        Type solver = typeof(ChoiBasketEngine).Assembly.GetType("NanoQL.PricingEngines.SumExponentialsRootSolver", true)!;
        ConstructorInfo ctor = solver.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic, [typeof(double[]), typeof(double[]), typeof(double)])!;
        MethodInfo getRoot = solver.GetMethod("GetRoot", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object brent = Enum.ToObject(solver.GetNestedType("Strategy", BindingFlags.NonPublic)!, 2);

        ArgumentException size = Assert.IsType<ArgumentException>(
            Inner(() => ctor.Invoke([new[] { 1.0, 2.0 }, new[] { 0.2 }, 1.0])));
        Assert.Equal("sig", size.ParamName);
        Assert.StartsWith("Arrays must have the same size", size.Message, StringComparison.Ordinal);

        object negative = ctor.Invoke([new[] { 1.0, -1.0 }, new[] { 0.2, 0.3 }, 1.0]);
        Assert.Equal(
            "a*sig should not be negative",
            Assert.IsType<NanoQLException>(Inner(() => getRoot.Invoke(negative, [1e-10, brent]))).Message);

        object positive = ctor.Invoke([new[] { 1.0, 2.0 }, new[] { 0.2, 0.3 }, -1.0]);
        Assert.Equal(
            "non-positive strikes only allowed for spread options",
            Assert.IsType<NanoQLException>(Inner(() => getRoot.Invoke(positive, [1e-10, brent]))).Message);
    }

    private static Matrix Correlation(int n)
    {
        var rho = new Matrix(n, n, 0.0);
        for (int i = 0; i < n; ++i)
        {
            rho[i, i] = 1.0;
        }

        return rho;
    }

    private static Exception Inner(Action call) =>
        Assert.Throws<TargetInvocationException>(call).InnerException!;

    private static void Refused(Action call, string paramName, string message)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(call);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }
}
