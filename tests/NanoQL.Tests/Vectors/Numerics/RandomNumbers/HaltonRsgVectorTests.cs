// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Halton generator's vectors, <c>tests/vectors/math/randomnumbers/haltonrsg.json</c>: 7-dimensional
/// <see cref="HaltonRsg"/>s with and without a random start and shift, for seeds 1, 42 and 1472677 (whose random start
/// wraps at 2^32 at draw 74), 100 draws each, the last sequence and the dimension. Halfway, the draws continue from a
/// <see cref="HaltonRsg.Clone"/>, whose stream is QuantLib's, carried on.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HaltonRsgVectorTests
{
    [Fact]
    public void HaltonRsgVector()
    {
        var file = VectorFile.Load("math/randomnumbers/haltonrsg");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            int draws = c.Int("draws");
            var rsg = new HaltonRsg(c.Int("dimension"), c.Input("seed").GetInt64(), c.Bool("randomStart"), c.Bool("randomShift"));
            var sequences = new double[draws][];
            for (int i = 0; i < draws; i++)
            {
                if (i == draws / 2)
                {
                    rsg = rsg.Clone();
                }

                sequences[i] = rsg.NextSequence().Value.ToArray();
            }

            check.Output(c, "nextSequence", sequences);
            check.Output(c, "lastSequence", rsg.LastSequence().Value.ToArray());
            check.Output(c, "dimension", rsg.Dimension);
        }

        check.Complete();
    }
}
