// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;


namespace NanoQL.Tests.Vectors;

/// <summary>
/// The xoshiro256** generator's vectors, <c>tests/vectors/math/randomnumbers/xoshiro256starstaruniformrng.json</c>:
/// <see cref="Xoshiro256StarStarUniformRng"/> for three seeds, its first draws, and
/// <see cref="Xoshiro256StarStarUniformRsg"/> from a generator and from a seed. Halfway, the draws continue from a copy
/// (<see cref="IRng.Clone"/>), whose stream is QuantLib's, carried on.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Xoshiro256StarStarUniformRngVectorTests
{
    [Fact]
    public void Xoshiro256StarStarUniformRngVector()
    {
        var file = VectorFile.Load("math/randomnumbers/xoshiro256starstaruniformrng");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            long seed = c.Input("seed").GetInt64();
            if (c.String("generator") == "Xoshiro256StarStarUniformRng")
            {
                int draws = c.Int("draws");
                IRng rng = new Xoshiro256StarStarUniformRng(seed);
                var next = new double[draws];
                for (int i = 0; i < draws; i++)
                {
                    if (i == draws / 2)
                    {
                        rng = rng.Clone();
                    }

                    next[i] = rng.Next().Value;
                }

                check.Output(c, "next", next);
            }
            else
            {
                int dimension = c.Int("dimension");
                Xoshiro256StarStarUniformRsg rsg = c.String("from") == "rng" ? new Xoshiro256StarStarUniformRsg(dimension, new Xoshiro256StarStarUniformRng(seed)) : new Xoshiro256StarStarUniformRsg(dimension, seed);
                int sequences = c.Int("sequences");
                var drawn = new double[sequences][];
                Sample<ReadOnlyMemory<double>> sample = default;
                for (int i = 0; i < sequences; i++)
                {
                    sample = rsg.NextSequence();
                    drawn[i] = sample.Value.ToArray();
                }

                check.Output(c, "nextSequence", drawn);
                check.Output(c, "lastWeight", sample.Weight);
                check.Output(c, "dimension", rsg.Dimension);
            }
        }

        check.Complete();
    }
}
