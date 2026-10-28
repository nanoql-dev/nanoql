// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's differential evolution vectors, <c>tests/vectors/cxx/math/optimization/differentialevolution.json</c>:
/// <see cref="DifferentialEvolution"/>'s results under each strategy and crossover type, with adaptive crossover,
/// without bounds applied, with the configuration's bounds and an initial population, at three seeds, and under
/// QuantLib's test's configuration. Each minimization draws from its seed afresh, as each new QuantLib optimizer does.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DifferentialEvolutionCxxVectorTests
{
    [Fact]
    public void DifferentialEvolutionCxxVector()
    {
        var file = VectorFile.Load("cxx/math/optimization/differentialevolution");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement s = c.Input("configuration");
            DifferentialEvolutionConfiguration configuration = new DifferentialEvolutionConfiguration()
                .WithStrategy((DifferentialEvolutionStrategy)s.GetProperty("strategy").GetInt32())
                .WithCrossoverType((DifferentialEvolutionCrossoverType)s.GetProperty("crossoverType").GetInt32())
                .WithPopulationMembers(s.GetProperty("populationMembers").GetInt32())
                .WithStepsizeWeight(s.GetProperty("stepsizeWeight").GetDouble())
                .WithCrossoverProbability(s.GetProperty("crossoverProbability").GetDouble())
                .WithBounds(s.GetProperty("applyBounds").GetBoolean())
                .WithAdaptiveCrossover(s.GetProperty("adaptiveCrossover").GetBoolean());
            if (s.TryGetProperty("lowerBound", out JsonElement lower))
            {
                configuration = configuration.WithLowerBound(Numbers(lower));
            }

            if (s.TryGetProperty("upperBound", out JsonElement upper))
            {
                configuration = configuration.WithUpperBound(Numbers(upper));
            }

            if (s.TryGetProperty("initialPopulation", out JsonElement population))
            {
                configuration = configuration.WithInitialPopulation(
                    population.EnumerateArray().Select(m => new Vector(Numbers(m))));
            }

            var method = new DifferentialEvolution(configuration, s.GetProperty("seed").GetInt64());
            var problem = new Problem(
                OptimizationSpecs.CostFunction(c.Input("costFunction")),
                OptimizationSpecs.Constraint(c.Input("constraint")),
                Numbers(c.Input("initialValue")));
            OptimizationSpecs.Results(check, c, method.Minimize(problem, OptimizationSpecs.EndCriteria(c.Input("endCriteria"))));
        }

        check.Complete();
    }
}
