// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Goldstein line-search vectors, <c>tests/vectors/cxx/math/optimization/goldstein.json</c>:
/// <see cref="GoldsteinLineSearch"/> through the minimizations of <see cref="ConjugateGradient"/>,
/// <see cref="SteepestDescent"/> and <see cref="BFGS"/>, under QuantLib's default settings and two others.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GoldsteinLineSearchCxxVectorTests
{
    [Fact]
    public void GoldsteinLineSearchCxxVector()
    {
        var file = VectorFile.Load("cxx/math/optimization/goldstein");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var search = new GoldsteinLineSearch(c.Double("eps"), c.Double("alpha"), c.Double("beta"), c.Double("extrapolation"));
            OptimizationMethod method = c.String("method") switch
            {
                "ConjugateGradient" => new ConjugateGradient(search),
                "SteepestDescent" => new SteepestDescent(search),
                "BFGS" => new BFGS(search),
                string m => throw new InvalidOperationException($"unknown method {m}"),
            };
            var problem = new Problem(
                OptimizationSpecs.CostFunction(c.Input("costFunction")),
                OptimizationSpecs.Constraint(c.Input("constraint")),
                Numbers(c.Input("initialValue")));
            OptimizationSpecs.LineSearchResults(check, c, method.Minimize(problem, OptimizationSpecs.EndCriteria(c.Input("endCriteria"))));
        }

        check.Complete();
    }
}
