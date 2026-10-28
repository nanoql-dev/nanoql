// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's simulated annealing vectors, <c>tests/vectors/cxx/math/optimization/simulatedannealing.json</c>:
/// <see cref="SimulatedAnnealing{TRng}"/>'s results over QuantLib's Mersenne Twister, under both temperature schemes,
/// at two seeds, under end criteria the simplex meets and an iteration limit.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SimulatedAnnealingCxxVectorTests
{
    [Fact]
    public void SimulatedAnnealingCxxVector()
    {
        var file = VectorFile.Load("cxx/math/optimization/simulatedannealing");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement s = c.Input("scheme");
            var rng = new MersenneTwisterUniformRng(c.Input("seed").GetInt64());
            double lambda = s.GetProperty("lambda").GetDouble(), t0 = s.GetProperty("T0").GetDouble();
            SimulatedAnnealing<MersenneTwisterUniformRng> method = s.GetProperty("type").GetString() == "ConstantFactor"
                ? new(lambda, t0, s.GetProperty("epsilon").GetDouble(), s.GetProperty("m").GetInt32(), rng)
                : new(lambda, t0, s.GetProperty("K").GetInt32(), s.GetProperty("alpha").GetDouble(), rng);
            var problem = new Problem(
                OptimizationSpecs.CostFunction(c.Input("costFunction")),
                OptimizationSpecs.Constraint(c.Input("constraint")),
                Numbers(c.Input("initialValue")));
            OptimizationSpecs.Results(check, c, method.Minimize(problem, OptimizationSpecs.EndCriteria(c.Input("endCriteria"))));
        }

        check.Complete();
    }
}
