// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;


namespace NanoQL.Tests.Vectors;

/// <summary>
/// The ziggurat generator's harness vectors, <c>tests/vectors/cxx/math/randomnumbers/zigguratgaussianrng.json</c>:
/// <see cref="ZigguratGaussianRng{TRng}"/> over <see cref="Xoshiro256StarStarUniformRng"/> at 64-bit seeds, its first
/// draws, past the tail's draw by hand and the wedges' acceptance. Halfway, the draws continue from a copy
/// (<see cref="IRng.Clone"/>), whose stream is QuantLib's, carried on.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ZigguratGaussianRngCxxVectorTests
{
    [Fact]
    public void ZigguratGaussianRngCxxVector()
    {
        var file = VectorFile.Load("cxx/math/randomnumbers/zigguratgaussianrng");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            long seed = unchecked((long)ulong.Parse(c.String("seed"), CultureInfo.InvariantCulture));
            IRng rng = new ZigguratGaussianRng<Xoshiro256StarStarUniformRng>(new Xoshiro256StarStarUniformRng(seed));
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
