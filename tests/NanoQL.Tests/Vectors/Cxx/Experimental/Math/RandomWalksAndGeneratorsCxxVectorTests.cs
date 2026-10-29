// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's walk and generator vectors,
/// <c>tests/vectors/cxx/experimental/math/randomwalksandgenerators.json</c>: the isotropic walk in one and two
/// dimensions on normal and Levy flight radii, the Levy flight distribution, the Ziggurat generator and its traits, and
/// the three copula generators.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RandomWalksAndGeneratorsCxxVectorTests
{
    [Fact]
    public void RandomWalksAndGeneratorsCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/math/randomwalksandgenerators");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement[] a = [.. c.Input("args").EnumerateArray()];
            if (c.Name.StartsWith("walk", StringComparison.Ordinal))
            {
                double[] p = Numbers(a[1]);
                int dim = a[2].GetInt32();
                double[] lower = Numbers(a[3]), upper = Numbers(a[4]);
                long seed = a[5].GetInt64();
                int n = a[6].GetInt32();
                double[] steps = a[0].GetString() == "normal"
                    ? Walk(new NormalRandomWalkDistribution(p[0], p[1]), dim, lower, upper, seed, n)
                    : Walk(new LevyFlightDistribution(p[0], p[1]), dim, lower, upper, seed, n);
                check.Output(c, "steps", steps);
            }
            else if (c.Name.StartsWith("levy", StringComparison.Ordinal))
            {
                var d = new LevyFlightDistribution(a[0].GetDouble(), a[1].GetDouble());
                check.Output(c, "density", Numbers(a[2]).Select(d.Density).ToArray());
                var engine = new MersenneTwisterUniformRng(a[3].GetInt64());
                check.Output(c, "draws", Enumerable.Range(0, a[4].GetInt32()).Select(_ => d.Next(engine)).ToArray());
                check.Output(c, "min", d.Min);
                check.Output(c, "max", d.Max);
            }
            else if (c.Name.StartsWith("ziggurat", StringComparison.Ordinal))
            {
                long seed = a[0].GetInt64();
                var rng = new ZigguratRng(seed);
                check.Output(c, "draws", Enumerable.Range(0, a[1].GetInt32()).Select(_ => rng.Next().Value).ToArray());

                // a copy continues the same stream
                ZigguratRng copy = rng.Clone();
                Assert.Equal(rng.Next().Value, copy.Next().Value);
                RandomSequenceGenerator<ZigguratRng> rsg = Ziggurat.MakeSequenceGenerator(a[2].GetInt32(), seed);
                var sequences = new List<double>();
                for (int i = 0; i < a[3].GetInt32(); i++)
                {
                    sequences.AddRange(rsg.NextSequence().Value.ToArray());
                }

                check.Output(c, "sequences", sequences.ToArray());
                check.Output(c, "allowsErrorEstimate", Ziggurat.AllowsErrorEstimate);
            }
            else
            {
                double theta = a[1].GetDouble();
                var mt = new MersenneTwisterUniformRng(a[2].GetInt64());
                Func<Sample<ImmutableArray<double>>> next = a[0].GetString() switch
                {
                    "clayton" => new ClaytonCopulaRng<MersenneTwisterUniformRng>(mt, theta).Next,
                    "frank" => new FrankCopulaRng<MersenneTwisterUniformRng>(mt, theta).Next,
                    _ => new FarlieGumbelMorgensternCopulaRng<MersenneTwisterUniformRng>(mt, theta).Next,
                };
                var u = new List<double>();
                var weights = new List<double>();
                for (int i = 0; i < a[3].GetInt32(); i++)
                {
                    Sample<ImmutableArray<double>> s = next();
                    u.AddRange(s.Value);
                    weights.Add(s.Weight);
                }

                check.Output(c, "u", u.ToArray());
                check.Output(c, "weights", weights.ToArray());
            }
        }

        check.Complete();
    }

    private static double[] Numbers(JsonElement e) => [.. e.EnumerateArray().Select(x => x.GetDouble())];

    // The harness's walk: the radius on a std::mt19937 and the direction on the walk's twister, both at the seed.
    private static double[] Walk<TDistribution>(
        TDistribution distribution, int dim, double[] lower, double[] upper, long seed, int n)
        where TDistribution : class, IRandomWalkDistribution
    {
        var walk = new IsotropicRandomWalk<TDistribution>(
            new MersenneTwisterUniformRng(seed), distribution, dim, new Vector(dim, 1.0), seed);
        if (lower.Length > 0)
        {
            walk.SetDimension(dim, Vector.Create(lower), Vector.Create(upper));
        }

        var steps = new List<double>();
        var step = new double[dim];
        for (int i = 0; i < n; i++)
        {
            walk.NextReal(step);
            steps.AddRange(step);
        }

        return [.. steps];
    }
}
