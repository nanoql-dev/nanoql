// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's trigonometric Gauss-Laguerre vectors,
/// <c>tests/vectors/cxx/math/integrals/gausslaguerrecosinepolynomial.json</c>:
/// <see cref="GaussLaguerreCosinePolynomial"/> and <see cref="GaussLaguerreSinePolynomial"/> at five frequencies, their
/// first 12 moments and recurrence coefficients, their weights, and the Gaussian quadratures on them at four orders.
/// The wheel exposes neither.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GaussLaguerreCosinePolynomialCxxVectorTests
{
    [Fact]
    public void GaussLaguerreCosinePolynomialCxxVector()
    {
        var file = VectorFile.Load("cxx/math/integrals/gausslaguerrecosinepolynomial");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double u = c.Double("u");
            MomentBasedGaussianPolynomial p = c.String("kind") == "Cosine"
                ? new GaussLaguerreCosinePolynomial(u)
                : new GaussLaguerreSinePolynomial(u);
            int[] moments = [.. Enumerable.Range(0, c.Int("moments"))];
            check.Output(c, "moment", moments.Select(p.Moment).ToArray());
            check.Output(c, "alpha", moments.Select(p.Alpha).ToArray());
            check.Output(c, "beta", moments.Select(p.Beta).ToArray());
            check.Output(c, "w", c.Doubles("x").Select(p.W).ToArray());
            var rule = new GaussianQuadrature(c.Int("order"), p);
            check.Output(c, "nodes", rule.X().ToArray());
            check.Output(c, "weights", rule.Weights().ToArray());
        }

        check.Complete();
    }
}
