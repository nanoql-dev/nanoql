// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;


namespace NanoQL.Tests.Vectors;

/// <summary>
/// The ziggurat generator's vectors, <c>tests/vectors/math/randomnumbers/zigguratgaussianrng.json</c>:
/// <see cref="ZigguratXoshiro256StarStarGaussianRng"/> and <see cref="ZigguratXoshiro256StarStarGaussianRsg"/> for
/// three seeds below 2^31, the draws, the sequences, their last weight and dimension. Halfway, the draws continue from
/// a copy (<see cref="IRng.Clone"/>), whose stream is QuantLib's, carried on.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ZigguratGaussianRngVectorTests
{
    [Fact]
    public void ZigguratGaussianRngVector()
    {
        var file = VectorFile.Load("math/randomnumbers/zigguratgaussianrng");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            long seed = c.Input("seed").GetInt64();
            var rng = new ZigguratXoshiro256StarStarGaussianRng(new Xoshiro256StarStarUniformRng(seed));
            if (c.String("generator") == "Rng")
            {
                int draws = c.Int("draws");
                IRng g = rng;
                var next = new double[draws];
                for (int i = 0; i < draws; i++)
                {
                    if (i == draws / 2)
                    {
                        g = g.Clone();
                    }

                    next[i] = g.Next().Value;
                }

                check.Output(c, "next", next);
            }
            else
            {
                var rsg = new ZigguratXoshiro256StarStarGaussianRsg(c.Int("dimension"), rng);
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
