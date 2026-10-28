// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;


namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Faure sequences' harness vectors, <c>tests/vectors/cxx/math/randomnumbers/faurersg.json</c>:
/// <see cref="FaureRsg"/> in 1 to 100 dimensions, each draw's sequence and integer sequence. Halfway, the draws
/// continue from a copy.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FaureRsgCxxVectorTests
{
    [Fact]
    public void FaureRsgCxxVector()
    {
        var file = VectorFile.Load("cxx/math/randomnumbers/faurersg");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var rsg = new FaureRsg(c.Int("dimensionality"));
            int draws = c.Int("draws");
            var sequences = new double[draws][];
            var integers = new int[draws][];
            for (int i = 0; i < draws; i++)
            {
                if (i == draws / 2)
                {
                    rsg = rsg.Clone();
                }

                sequences[i] = rsg.NextSequence().Value.ToArray();
                integers[i] = rsg.LastIntSequence().ToArray();
            }

            check.Output(c, "nextSequence", sequences);
            check.Output(c, "lastIntSequence", integers);
        }

        check.Complete();
    }
}
