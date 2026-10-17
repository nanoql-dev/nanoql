// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;
using static NanoQL.Tests.Vectors.StochasticProcessVectorTests;
using MersenneTwisterGsg = NanoQL.Numerics.InverseCumulativeRsg<
    NanoQL.Numerics.RandomSequenceGenerator<NanoQL.Numerics.MersenneTwisterUniformRng>,
    NanoQL.Numerics.InverseCumulativeNormal>;
using SobolGsg = NanoQL.Numerics.InverseCumulativeRsg<NanoQL.Numerics.SobolRsg, NanoQL.Numerics.InverseCumulativeNormal>;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The PathGenerator vectors, <c>tests/vectors/methods/montecarlo/pathgenerator.json</c>: QuantLib-Python's
/// <c>GaussianPathGenerator</c> and <c>GaussianSobolPathGenerator</c> over Ornstein-Uhlenbeck processes, through
/// <see cref="InvCumulativeMersenneTwisterPathGenerator"/> (the same closed <see cref="PathGenerator{TGsg}"/>) and
/// <see cref="GaussianSobolPathGenerator"/>: each draw's path and weight and its antithetic's, the first path's
/// members (<see cref="MonteCarloPath.Length"/>, <see cref="MonteCarloPath.Front"/>, <see cref="MonteCarloPath.Back"/>,
/// <see cref="MonteCarloPath.Value"/>, <see cref="MonteCarloPath.Time"/>), the generator's size and time grid, and the
/// constructors' refusals. Each path is read before the next draw, which overwrites it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PathGeneratorVectorTests
{
    [Fact]
    public void PathGeneratorVector()
    {
        var file = VectorFile.Load("methods/montecarlo/pathgenerator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            StochasticProcess1D process = Process(c.Input("process"));
            long seed = c.Input("seed").GetInt64();
            bool bridge = c.Bool("brownianBridge");
            bool sobol = c.String("generator") switch
            {
                "GaussianPathGenerator" => false,
                "GaussianSobolPathGenerator" => true,
                string other => throw new InvalidOperationException($"{c.Name}: unknown generator {other}"),
            };
            // The constructor: over the mandatory times' grid where the case gives times, else by length and steps.
            TimeGrid? grid = c.Inputs.TryGetProperty("times", out _) ? new TimeGrid(Numbers(c.Input("times"))) : null;
            int dimension = c.Inputs.TryGetProperty("dimension", out _)
                ? c.Int("dimension")
                : grid is not null ? grid.Size - 1 : c.Int("timeSteps");
            Func<object> make = (sobol, grid) switch
            {
                (true, TimeGrid g) => () => new GaussianSobolPathGenerator(process, g, Sobol(dimension, seed), bridge),
                (true, null) => () => new GaussianSobolPathGenerator(
                    process, c.Double("length"), c.Int("timeSteps"), Sobol(dimension, seed), bridge),
                (false, TimeGrid g) => () => new InvCumulativeMersenneTwisterPathGenerator(
                    process, g, MersenneTwister(dimension, seed), bridge),
                (false, null) => () => new InvCumulativeMersenneTwisterPathGenerator(
                    process, c.Double("length"), c.Int("timeSteps"), MersenneTwister(dimension, seed), bridge),
            };
            if (!c.Expected.TryGetProperty("paths", out _))
            {
                check.Output(c, "size", () => make() switch
                {
                    GaussianSobolPathGenerator g => g.Size,
                    InvCumulativeMersenneTwisterPathGenerator g => g.Size,
                    _ => throw new InvalidOperationException(),
                });
                continue;
            }

            switch (make())
            {
                case GaussianSobolPathGenerator g:
                    Draws(check, c, g);
                    break;
                case InvCumulativeMersenneTwisterPathGenerator g:
                    Draws(check, c, g);
                    break;
            }
        }

        check.Complete();
    }

    // The wheel's GaussianRandomSequenceGenerator(UniformRandomSequenceGenerator(steps, UniformRandomGenerator(seed))).
    private static MersenneTwisterGsg MersenneTwister(int steps, long seed) =>
        new(new RandomSequenceGenerator<MersenneTwisterUniformRng>(steps, new MersenneTwisterUniformRng(seed)));

    // The wheel's GaussianLowDiscrepancySequenceGenerator(UniformLowDiscrepancySequenceGenerator(steps, seed)).
    private static SobolGsg Sobol(int steps, long seed) => new(new SobolRsg(steps, seed));

    private static void Draws<TGsg>(VectorAssert check, VectorCase c, PathGenerator<TGsg> g)
        where TGsg : class, IRsg
    {
        var paths = new List<object?>();
        var weights = new List<object?>();
        var antithetic = new List<object?>();
        var antitheticWeights = new List<object?>();
        for (int k = 0; k < 10; k++)
        {
            // Halfway, the draws go on from a clone, which continues QuantLib's stream (Clone()'s oracle, as the other
            // generators' vector tests give theirs).
            if (k == 5)
            {
                g = g.Clone();
            }

            Sample<MonteCarloPath> s = g.Next();
            MonteCarloPath path = s.Value;
            paths.Add(Values(path));
            weights.Add(s.Weight);
            if (k == 0)
            {
                int n = path.Length;
                check.Output(c, "length", n);
                check.Output(c, "front", path.Front());
                check.Output(c, "back", path.Back());
                check.Output(c, "value", Enumerable.Range(0, n).Select(i => (object?)path.Value(i)).ToArray());
                check.Output(c, "time", Enumerable.Range(0, n).Select(i => (object?)path.Time(i)).ToArray());
            }

            Sample<MonteCarloPath> a = g.Antithetic();
            antithetic.Add(Values(a.Value));
            antitheticWeights.Add(a.Weight);
        }

        check.Output(c, "paths", paths);
        check.Output(c, "weights", weights);
        check.Output(c, "antithetic", antithetic);
        check.Output(c, "antitheticWeights", antitheticWeights);
        check.Output(c, "size", g.Size);
        check.Output(c, "timeGrid", Enumerable.Range(0, g.TimeGrid.Size).Select(i => (object?)g.TimeGrid[i]).ToArray());
    }

    // A path's values through its indexer, copied before the next draw.
    private static object?[] Values(MonteCarloPath path) =>
        [.. Enumerable.Range(0, path.Length).Select(i => (object?)path[i])];
}
