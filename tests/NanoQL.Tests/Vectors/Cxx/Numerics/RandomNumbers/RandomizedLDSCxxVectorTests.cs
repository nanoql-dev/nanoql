// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;


namespace NanoQL.Tests.Vectors;

/// <summary>
/// The randomized sequences' harness vectors, <c>tests/vectors/cxx/math/randomnumbers/randomizedlds.json</c>:
/// <see cref="RandomizedLDS{TLds, TPrng}"/> over <see cref="SobolRsg"/> and over <see cref="LatticeRsg"/>, with
/// QuantLib's default pseudo-random generator, the points and their weights, the randomizer renewed at intervals.
/// Halfway, the draws continue from a copy.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RandomizedLDSCxxVectorTests
{
    [Fact]
    public void RandomizedLDSCxxVector()
    {
        var file = VectorFile.Load("cxx/math/randomnumbers/randomizedlds");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            int dim = c.Int("dimensionality");
            long prsSeed = c.Input("prsSeed").GetInt64();
            int draws = c.Int("draws"), every = c.Int("every");
            if (c.String("sequence") == "Sobol")
            {
                var g = new RandomizedLDS<SobolRsg, RandomSequenceGenerator<MersenneTwisterUniformRng>>(
                    new SobolRsg(dim, c.Input("ldsSeed").GetInt64()),
                    RandomSequenceGenerator.Create<MersenneTwisterUniformRng>(dim, prsSeed));
                Check(check, c, g, draws, every);
            }
            else
            {
                int n = c.Int("n");
                var g = new RandomizedLDS<LatticeRsg, RandomSequenceGenerator<MersenneTwisterUniformRng>>(
                    new LatticeRsg(dim, LatticeRule.GetRule(Enum.Parse<LatticeRuleType>(c.String("rule")), n), n),
                    new RandomSequenceGenerator<MersenneTwisterUniformRng>(dim, new MersenneTwisterUniformRng(prsSeed)));
                Check(check, c, g, draws, every);
            }
        }

        check.Complete();
    }

    private static void Check<TLds>(
        VectorAssert check, VectorCase c, RandomizedLDS<TLds, RandomSequenceGenerator<MersenneTwisterUniformRng>> g, int draws, int every)
        where TLds : class, IRsg
    {
        var points = new double[draws][];
        var weights = new double[draws];
        for (int i = 0; i < draws; i++)
        {
            if (i == draws / 2)
            {
                g = g.Clone();
            }

            if (i > 0 && i % every == 0)
            {
                g.NextRandomizer();
            }

            Sample<ReadOnlyMemory<double>> x = g.NextSequence();
            points[i] = x.Value.ToArray();
            weights[i] = x.Weight;
        }

        check.Output(c, "nextSequence", points);
        check.Output(c, "weight", weights);
    }
}
