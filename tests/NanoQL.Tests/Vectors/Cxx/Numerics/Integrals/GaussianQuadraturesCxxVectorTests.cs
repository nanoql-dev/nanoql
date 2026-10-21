// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Gaussian quadrature vectors, <c>tests/vectors/cxx/math/integrals/gaussianquadratures.json</c>:
/// <see cref="GaussLegendreIntegrator"/>, <see cref="GaussChebyshevIntegrator"/> and
/// <see cref="GaussChebyshev2ndIntegrator"/> over the named integrands, each the integral, the number of evaluations
/// and the quadrature's order; the orthogonal polynomials' members, each polynomial built for each value, so that a
/// constructor's refusal is each value's; and <see cref="TabulatedGaussLegendre.Order()"/> and
/// <see cref="MultiDimGaussianIntegration"/>'s weights and abscissas.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GaussianQuadraturesCxxVectorTests
{
    [Fact]
    public void GaussianQuadraturesCxxVector()
    {
        var file = VectorFile.Load("cxx/math/integrals/gaussianquadratures");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            string kind = c.String("kind");
            if (kind == "TabulatedGaussLegendre")
            {
                int n = c.Int("n");
                int? other = c.Input("other").ValueKind == System.Text.Json.JsonValueKind.Null ? null : c.Int("other");
                check.Output(
                    c, "order", () => other is int m ? new TabulatedGaussLegendre(n).Order(m).Order() : new TabulatedGaussLegendre(n).Order());
                continue;
            }

            if (kind.StartsWith("MultiDim", StringComparison.Ordinal))
            {
                Func<int, GaussianQuadrature> quadrature = kind == "MultiDimHermite"
                    ? n => new GaussHermiteIntegration(n)
                    : n => new GaussLegendreIntegration(n);
                var integration = new MultiDimGaussianIntegration(c.Ints("ns"), quadrature);
                check.Output(c, "weights", integration.Weights());
                check.Output(c, "x", integration.X().Select(x => (object?)x).ToArray());
                continue;
            }

            if (c.Inputs.TryGetProperty("integrals", out _))
            {
                int n = c.Int("n");
                object?[] results = [.. c.Input("integrals").EnumerateArray().Select(e =>
                {
                    Func<double, double> f = IntegralInputs.Integrands[e.GetProperty("f").GetString()!];
                    double a = e.GetProperty("a").GetDouble(), b = e.GetProperty("b").GetDouble();
                    return (object?)(Func<object?>)(() => Integrate(kind, n, f, a, b));
                })];
                check.Output(c, "result", results);
                continue;
            }

            double[] p = c.Doubles("parameters");
            int[] degrees = c.Ints("n");
            double[] x = c.Doubles("x");

            object?[] Values(Func<GaussianOrthogonalPolynomial, int, double, double> member) =>
                [.. Enumerable.Range(0, x.Length).Select(i =>
                    (object?)(Func<object?>)(() => member(Polynomial(kind, p), degrees[i], x[i])))];

            check.Output(c, "mu_0", Values((P, _, _) => P.Mu_0()));
            check.Output(c, "alpha", Values((P, n, _) => P.Alpha(n)));
            check.Output(c, "beta", Values((P, n, _) => P.Beta(n)));
            check.Output(c, "w", Values((P, _, t) => P.W(t)));
            check.Output(c, "logW", Values((P, _, t) => P.LogW(t)));
            check.Output(c, "value", Values((P, n, t) => P.Value(n, t)));
            check.Output(c, "weightedValue", Values((P, n, t) => P.WeightedValue(n, t)));
        }

        check.Complete();
    }

    private static object[] Integrate(string kind, int n, Func<double, double> f, double a, double b)
    {
        switch (kind)
        {
            case "Legendre":
            {
                var I = new GaussLegendreIntegrator(n);
                IntegrationResult r = I.IntegrateWithStatistics(f, a, b);
                return [r.Value, r.NumberOfEvaluations, I.GetIntegration().Order];
            }

            case "Chebyshev":
            {
                var I = new GaussChebyshevIntegrator(n);
                IntegrationResult r = I.IntegrateWithStatistics(f, a, b);
                return [r.Value, r.NumberOfEvaluations, I.GetIntegration().Order];
            }

            default:
            {
                var I = new GaussChebyshev2ndIntegrator(n);
                IntegrationResult r = I.IntegrateWithStatistics(f, a, b);
                return [r.Value, r.NumberOfEvaluations, I.GetIntegration().Order];
            }
        }
    }

    private static GaussianOrthogonalPolynomial Polynomial(string kind, double[] p) => kind switch
    {
        "Laguerre" => new GaussLaguerrePolynomial(p[0]),
        "Hermite" => new GaussHermitePolynomial(p[0]),
        "Jacobi" => new GaussJacobiPolynomial(p[0], p[1]),
        "Legendre" => new GaussLegendrePolynomial(),
        "Chebyshev" => new GaussChebyshevPolynomial(),
        "Chebyshev2nd" => new GaussChebyshev2ndPolynomial(),
        "Gegenbauer" => new GaussGegenbauerPolynomial(p[0]),
        "Hyperbolic" => new GaussHyperbolicPolynomial(),
        _ => throw new InvalidOperationException($"unknown polynomial {kind}"),
    };
}
