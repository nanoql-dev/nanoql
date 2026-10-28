// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's CMA-ES vectors, <c>tests/vectors/cxx/math/optimization/cmaes.json</c>: <see cref="Cmaes"/>'s results
/// on QuantLib's testCMAES problems, and with the configuration's bounds, an initial mean, a population of 2, three seeds
/// and a limit of 3 generations. Each minimization draws from its seed afresh, as each new QuantLib optimizer does.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CmaesCxxVectorTests
{
    [Fact]
    public void CmaesCxxVector()
    {
        var file = VectorFile.Load("cxx/math/optimization/cmaes");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement s = c.Input("configuration");
            CmaesConfiguration configuration = new CmaesConfiguration()
                .WithPopulationSize(s.GetProperty("populationSize").GetInt32())
                .WithSigma(s.GetProperty("sigma").GetDouble());
            if (s.TryGetProperty("lowerBound", out JsonElement lower))
            {
                configuration = configuration.WithBounds(Numbers(lower), Numbers(s.GetProperty("upperBound")));
            }

            if (s.TryGetProperty("initialMean", out JsonElement mean))
            {
                configuration = configuration.WithInitialMean(Numbers(mean));
            }

            var method = new Cmaes(configuration, s.GetProperty("seed").GetInt64());
            var problem = new Problem(
                OptimizationSpecs.CostFunction(c.Input("costFunction")),
                OptimizationSpecs.Constraint(c.Input("constraint")),
                Numbers(c.Input("initialValue")));
            OptimizationSpecs.Results(check, c, method.Minimize(problem, OptimizationSpecs.EndCriteria(c.Input("endCriteria"))));
        }

        check.Complete();
    }
}
