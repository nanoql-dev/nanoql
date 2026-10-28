// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;


namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Ranlux generators' harness vectors, <c>tests/vectors/cxx/math/randomnumbers/ranluxuniformrng.json</c>:
/// <see cref="Ranlux3UniformRng"/> and <see cref="Ranlux4UniformRng"/> for seeds across [1, 4294967295], their first
/// draws. Halfway, the draws continue from a copy (<see cref="IRng.Clone"/>), whose stream is QuantLib's, carried on.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RanluxUniformRngCxxVectorTests
{
    [Fact]
    public void RanluxUniformRngCxxVector()
    {
        var file = VectorFile.Load("cxx/math/randomnumbers/ranluxuniformrng");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            long seed = c.Input("seed").GetInt64();
            IRng rng = c.String("generator") == "Ranlux3UniformRng" ? new Ranlux3UniformRng(seed) : new Ranlux4UniformRng(seed);
            int draws = c.Int("draws");
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

        check.Complete();
    }
}
