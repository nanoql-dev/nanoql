// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Gaussian quadratures' vectors, <c>tests/vectors/math/integrals/gaussianquadratures.json</c>: the order, nodes,
/// weights and weighted sums of three named integrands of the eight named integrations over orders from 1 to 256, and
/// the refusals of their polynomials' parameters and of order 0.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GaussianQuadraturesVectorTests
{
    private static readonly Dictionary<string, Func<double, double>> Integrands = new()
    {
        ["exp(-x)"] = x => Math.Exp(-x),
        ["x exp(-x)"] = x => x * Math.Exp(-x),
        ["1/((1+x)(1+x))"] = x => 1.0 / ((1.0 + x) * (1.0 + x)),
        ["exp(-x x)"] = x => Math.Exp(-x * x),
        ["x x exp(-x x)"] = x => x * x * Math.Exp(-x * x),
        ["1/(1+x x)"] = x => 1.0 / (1.0 + (x * x)),
        ["1/cosh(x)"] = x => 1.0 / Math.Cosh(x),
        ["x x/cosh(x)"] = x => x * x / Math.Cosh(x),
        ["1"] = x => 1.0,
        ["x x"] = x => x * x,
        ["cos(x)"] = Math.Cos,
    };

    [Fact]
    public void GaussianQuadraturesVector()
    {
        var file = VectorFile.Load("math/integrals/gaussianquadratures");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            string kind = c.Input("kind").GetString()!;
            int n = c.Input("n").GetInt32();
            double[] p = [.. c.Input("params").EnumerateArray().Select(e => e.GetDouble())];
            if (!c.Inputs.TryGetProperty("integrands", out JsonElement names))
            {
                check.Output(c, "order", (Func<object?>)(() => Quadrature(kind, n, p).Order));
                continue;
            }

            GaussianQuadrature q = Quadrature(kind, n, p);
            check.Output(c, "order", q.Order);
            check.Output(c, "x", q.X().ToArray());
            check.Output(c, "weights", q.Weights().ToArray());
            check.Output(c, "sum", names.EnumerateArray().Select(name => q.Evaluate(Integrands[name.GetString()!])).ToArray());
        }

        check.Complete();
    }

    private static GaussianQuadrature Quadrature(string kind, int n, double[] p) => kind switch
    {
        "Laguerre" => new GaussLaguerreIntegration(n, p[0]),
        "Hermite" => new GaussHermiteIntegration(n, p[0]),
        "Jacobi" => new GaussJacobiIntegration(n, p[0], p[1]),
        "Hyperbolic" => new GaussHyperbolicIntegration(n),
        "Legendre" => new GaussLegendreIntegration(n),
        "Chebyshev" => new GaussChebyshevIntegration(n),
        "Chebyshev2nd" => new GaussChebyshev2ndIntegration(n),
        "Gegenbauer" => new GaussGegenbauerIntegration(n, p[0]),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown quadrature"),
    };
}
