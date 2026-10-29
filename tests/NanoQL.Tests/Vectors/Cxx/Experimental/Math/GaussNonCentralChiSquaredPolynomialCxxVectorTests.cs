// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's non-central chi-squared polynomial vectors,
/// <c>tests/vectors/cxx/experimental/math/gaussiannoncentralchisquaredpolynomial.json</c>: from its multi-precision
/// oracle, the recurrence coefficients and the Gauss rule's nodes and weights.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GaussNonCentralChiSquaredPolynomialCxxVectorTests
{
    [Fact]
    public void GaussNonCentralChiSquaredPolynomialCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/math/gaussiannoncentralchisquaredpolynomial");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var p = new GaussNonCentralChiSquaredPolynomial(c.Double("nu"), c.Double("lambda"));
            int n = c.Int("n");
            check.Output(c, "alpha", Enumerable.Range(0, n).Select(p.Alpha).ToArray());
            check.Output(c, "beta", Enumerable.Range(0, n).Select(p.Beta).ToArray());
            var rule = new GaussianQuadrature(n, p);
            check.Output(c, "x", rule.X().ToArray());
            check.Output(c, "weights", rule.Weights().ToArray());
        }

        check.Complete();
    }
}
