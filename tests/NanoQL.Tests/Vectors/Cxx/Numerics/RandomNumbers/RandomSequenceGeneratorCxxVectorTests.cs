// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's sequence generator vectors, <c>tests/vectors/cxx/math/randomnumbers/randomsequencegenerator.json</c>:
/// <see cref="RandomSequenceGenerator{TRng}"/> over <see cref="MersenneTwisterUniformRng"/>, built through
/// <see cref="RandomSequenceGenerator.Create{TRng}(int, long)"/> (QuantLib's seed constructor) or from a generator;
/// <see cref="RandomSequenceGenerator.NextInt32Sequence{TRng}"/> and <see cref="RandomSequenceGenerator{TRng}.LastSequence"/>,
/// which the wheel doesn't expose, and the two constructions at dimension 0.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RandomSequenceGeneratorCxxVectorTests
{
    [Fact]
    public void RandomSequenceGeneratorCxxVector()
    {
        var file = VectorFile.Load("cxx/math/randomnumbers/randomsequencegenerator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            int dimension = c.Int("dimension");
            long seed = c.Input("seed").GetInt64();
            if (dimension == 0)
            {
                check.Output(c, "seedConstructor", (Func<object?>)(() =>
                    RandomSequenceGenerator.Create<MersenneTwisterUniformRng>(0, seed).Dimension));
                check.Output(c, "rngConstructor", (Func<object?>)(() =>
                    new RandomSequenceGenerator<MersenneTwisterUniformRng>(0, new MersenneTwisterUniformRng(seed)).Dimension));
                continue;
            }

            bool fromSeed = c.String("construction") == "seed";
            RandomSequenceGenerator<MersenneTwisterUniformRng> Make() => fromSeed
                ? RandomSequenceGenerator.Create<MersenneTwisterUniformRng>(dimension, seed)
                : new RandomSequenceGenerator<MersenneTwisterUniformRng>(dimension, new MersenneTwisterUniformRng(seed));

            RandomSequenceGenerator<MersenneTwisterUniformRng> rsg = Make();
            check.Output(c, "nextInt32Sequence", Enumerable.Range(0, c.Int("draws")).Select(_ => rsg.NextInt32Sequence()).ToArray());

            // Before the last draw, the draws continue from a copy, whose stream is QuantLib's, carried on.
            rsg = Make();
            for (int i = 0; i < c.Int("lastAfter"); i++)
            {
                if (i == c.Int("lastAfter") - 1)
                {
                    rsg = rsg.Clone();
                }

                rsg.NextSequence();
            }

            check.Output(c, "lastSequence", rsg.LastSequence().Value.ToArray());
            check.Output(c, "lastWeight", rsg.LastSequence().Weight);
        }

        check.Complete();
    }
}
