// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Mersenne Twister's vectors, <c>tests/vectors/math/randomnumbers/mt19937uniformrng.json</c>:
/// <see cref="MersenneTwisterUniformRng"/>'s first 1,000 draws for ten seeds across the 32-bit range, and the last
/// draw's weight. Halfway, the draws continue from a <see cref="MersenneTwisterUniformRng.Clone"/>, whose stream is
/// QuantLib's, carried on.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MT19937UniformRngVectorTests
{
    [Fact]
    public void MT19937UniformRngVector()
    {
        var file = VectorFile.Load("math/randomnumbers/mt19937uniformrng");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            int draws = c.Int("draws");
            var rng = new MersenneTwisterUniformRng(c.Input("seed").GetInt64());
            var next = new double[draws];
            Sample<double> last = default;
            for (int i = 0; i < draws; i++)
            {
                if (i == draws / 2)
                {
                    rng = rng.Clone();
                }

                last = rng.Next();
                next[i] = last.Value;
            }

            check.Output(c, "next", next);
            check.Output(c, "lastWeight", last.Weight);
        }

        check.Complete();
    }
}
