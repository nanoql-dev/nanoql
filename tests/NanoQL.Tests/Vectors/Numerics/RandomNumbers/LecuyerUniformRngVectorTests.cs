// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;


namespace NanoQL.Tests.Vectors;

/// <summary>
/// The L'Ecuyer generator's vectors, <c>tests/vectors/math/randomnumbers/lecuyeruniformrng.json</c>:
/// <see cref="LecuyerUniformRng"/> for three seeds, its first draws, and <see cref="LecuyerUniformRsg"/> from a
/// generator and from a seed. Halfway, the draws continue from a copy (<see cref="IRng.Clone"/>), whose stream is
/// QuantLib's, carried on.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LecuyerUniformRngVectorTests
{
    [Fact]
    public void LecuyerUniformRngVector()
    {
        var file = VectorFile.Load("math/randomnumbers/lecuyeruniformrng");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            long seed = c.Input("seed").GetInt64();
            if (c.String("generator") == "LecuyerUniformRng")
            {
                int draws = c.Int("draws");
                IRng rng = new LecuyerUniformRng(seed);
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
                LecuyerUniformRsg rsg = c.String("from") == "rng" ? new LecuyerUniformRsg(dimension, new LecuyerUniformRng(seed)) : new LecuyerUniformRsg(dimension, seed);
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
