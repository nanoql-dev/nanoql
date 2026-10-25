// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The MTBrownianGenerator vectors,
/// <c>tests/vectors/models/marketmodels/browniangenerators/mtbrowniangenerator.json</c>:
/// <see cref="MTBrownianGenerator"/>'s paths on five (factors, steps, seed) sets, each path's weight and its steps'
/// weights and variates, and the same paths from <see cref="MTBrownianGeneratorFactory"/>'s generator.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MTBrownianGeneratorVectorTests
{
    [Fact]
    public void MTBrownianGeneratorVector()
    {
        var file = VectorFile.Load("models/marketmodels/browniangenerators/mtbrowniangenerator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("generator");
            int factors = spec.GetProperty("factors").GetInt32(), steps = spec.GetProperty("steps").GetInt32();
            long seed = spec.GetProperty("seed").GetInt64();
            CheckPaths(check, c, new MTBrownianGenerator(factors, steps, seed), new MTBrownianGeneratorFactory(seed).Create(factors, steps));
        }

        check.Complete();
    }

    // The case's paths from a generator and from its factory's: each path's weight, its steps' weights and variates.
    internal static void CheckPaths(VectorAssert check, VectorCase c, BrownianGenerator generator, BrownianGenerator fromFactory)
    {
        check.Output(c, "numberOfFactors", generator.NumberOfFactors);
        check.Output(c, "numberOfSteps", generator.NumberOfSteps);
        int paths = c.Expected.GetProperty("generatorPathWeights").GetArrayLength();
        foreach ((string prefix, BrownianGenerator g) in new[] { ("generator", generator), ("factory", fromFactory) })
        {
            var pathWeights = new List<object?>();
            var stepWeights = new List<object?>();
            var variates = new List<object?>();
            double[] output = new double[g.NumberOfFactors];
            for (int p = 0; p < paths; ++p)
            {
                pathWeights.Add(g.NextPath());
                var weights = new List<object?>();
                var values = new List<object?>();
                for (int s = 0; s < g.NumberOfSteps; ++s)
                {
                    weights.Add(g.NextStep(output));
                    values.Add(output.ToArray());
                }

                stepWeights.Add(weights);
                variates.Add(values);
            }

            check.Output(c, prefix + "PathWeights", pathWeights);
            check.Output(c, prefix + "StepWeights", stepWeights);
            check.Output(c, prefix + "Variates", variates);
        }
    }
}
