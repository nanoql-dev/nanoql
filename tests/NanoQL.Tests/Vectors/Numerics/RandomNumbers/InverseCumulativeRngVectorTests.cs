// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The inverse cumulative generators' vectors, <c>tests/vectors/math/randomnumbers/inversecumulativerng.json</c>:
/// <see cref="InverseCumulativeRng{TRng, TIc}"/> through its two named classes over
/// <see cref="MersenneTwisterUniformRng"/>, for three seeds, 500 draws each and the last weight. Halfway, the draws
/// continue from a copy (<see cref="IRng.Clone"/>), whose stream is QuantLib's, carried on.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InverseCumulativeRngVectorTests
{
    [Fact]
    public void InverseCumulativeRngVector()
    {
        var file = VectorFile.Load("math/randomnumbers/inversecumulativerng");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            int draws = c.Int("draws");
            var uniform = new MersenneTwisterUniformRng(c.Input("seed").GetInt64());
            IRng rng = c.String("composition") switch
            {
                "InvCumulativeMersenneTwisterGaussianRng" => new InvCumulativeMersenneTwisterGaussianRng(uniform),
                "MoroInvCumulativeMersenneTwisterGaussianRng" => new MoroInvCumulativeMersenneTwisterGaussianRng(uniform),
                string other => throw new InvalidOperationException($"no composition {other}"),
            };
            var next = new double[draws];
            Sample<double> sample = default;
            for (int i = 0; i < draws; i++)
            {
                if (i == draws / 2)
                {
                    rng = rng.Clone();
                }

                sample = rng.Next();
                next[i] = sample.Value;
            }

            check.Output(c, "next", next);
            check.Output(c, "lastWeight", sample.Weight);
        }

        check.Complete();
    }
}
