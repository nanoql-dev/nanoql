// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The MultiPathGenerator vectors, <c>tests/vectors/methods/montecarlo/multipathgenerator.json</c>: 12 draws of
/// <see cref="MultiPathGenerator{TGsg}"/>, each with its antithetic, over stochastic process arrays on uniform and
/// non-uniform grids: the pseudo-random generator built through <see cref="MultiVariate{TRng}"/>'s traits, the Sobol
/// one as <see cref="GaussianSobolMultiPathGenerator"/>; and the constructor's refusals, through
/// <see cref="GaussianMultiPathGenerator"/>, the Brownian bridge's among them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MultiPathGeneratorVectorTests
{
    [Fact]
    public void MultiPathGeneratorVector()
    {
        var file = VectorFile.Load("methods/montecarlo/multipathgenerator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var process = new StochasticProcessArray(
                StochasticProcessArrayVectorTests.Processes(c), MatrixVectorTests.FromRows(c.Input("correlation")));
            var times = new TimeGrid(Numbers(c.Input("times")));
            long seed = c.Input("seed").GetInt64();
            if (c.Inputs.TryGetProperty("brownianBridge", out _))
            {
                int dimension = c.Int("dimension");
                bool bridge = c.Bool("brownianBridge");
                check.Output(c, "first", () =>
                {
                    var generator = new GaussianMultiPathGenerator(
                        process, times, PseudoRandom.MakeSequenceGenerator(dimension, seed), bridge);
                    return Paths(generator.Next().Value);
                });
                continue;
            }

            int dim = process.Factors * (times.Size - 1);
            IPathGenerator<MultiPath> g = c.String("generator") == "pseudorandom"
                ? MultiVariate<PseudoRandom>.MakePathGenerator(
                    process, times, MultiVariate<PseudoRandom>.MakeSequenceGenerator(dim, seed), false)
                : new GaussianSobolMultiPathGenerator(process, times, LowDiscrepancy.MakeSequenceGenerator(dim, seed));
            var paths = new List<object?>();
            var antithetic = new List<object?>();
            var weights = new List<object?>();
            int assetNumber = 0;
            for (int k = 0; k < c.Int("draws"); k++)
            {
                Sample<MultiPath> s = g.Next();
                paths.Add(Paths(s.Value));
                weights.Add(s.Weight);
                assetNumber = s.Value.AssetNumber;
                antithetic.Add(Paths(g.Antithetic().Value));
            }

            check.Output(c, "paths", paths);
            check.Output(c, "antithetic", antithetic);
            check.Output(c, "weights", weights);
            check.Output(c, "assetNumber", assetNumber);
        }

        check.Complete();
    }

    // Each asset's path, its values by index, as the wheel's MultiPath reads them.
    private static object?[] Paths(MultiPath mp) =>
        [.. Enumerable.Range(0, mp.AssetNumber).Select(j =>
            (object?)Enumerable.Range(0, mp.PathSize()).Select(i => mp.At(j)[i]).ToArray())];
}
