// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Sobol vectors, <c>tests/vectors/cxx/math/randomnumbers/sobolrsg.json</c>: <see cref="SobolRsg"/>
/// without the Gray code, which the wheel can't build, for the ten sets of direction integers: integer draws (a fresh
/// generator's first two the same point), <see cref="SobolRsg.SkipTo"/>'s results, a sequence after a skip and the
/// last sequence; the constructor's refusals; and the period's end, QuantLib's defined behaviour without the Gray
/// code. Halfway through the integer draws, they continue from a <see cref="SobolRsg.Clone"/>, whose stream is
/// QuantLib's, carried on.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SobolRsgCxxVectorTests
{
    [Fact]
    public void SobolRsgCxxVector()
    {
        var file = VectorFile.Load("cxx/math/randomnumbers/sobolrsg");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            long seed = c.Input("seed").GetInt64();
            var directionIntegers = (SobolRsgDirectionIntegers)c.Int("directionIntegers");
            if (c.Name == "refused dimensions")
            {
                int[] dimensions = c.Ints("dimensions");
                bool[] grayCodes = [.. c.Input("useGrayCode").EnumerateArray().Select(g => g.GetBoolean())];
                check.Output(c, "dimension", dimensions.Select((d, i) => (object?)(Func<object?>)(() =>
                    new SobolRsg(d, seed, directionIntegers, grayCodes[i]).Dimension)).ToArray());
                continue;
            }

            int dimension = c.Int("dimension");
            SobolRsg Make() => new(dimension, seed, directionIntegers, c.Bool("useGrayCode"));
            long[] skips = [.. c.Input("skips").EnumerateArray().Select(e => e.GetInt64())];
            if (c.Name == "period end without the Gray code")
            {
                SobolRsg skipped = Make();
                check.Output(c, "skipTo", skips.Select(n => skipped.SkipTo(n).ToArray()).ToArray());
                check.Output(c, "drawsAfterSkip", DrawsAfter(Make(), skips[0], c.Expected.GetProperty("drawsAfterSkip").GetArrayLength()));
                check.Output(c, "drawsAfterEnd", DrawsAfter(Make(), skips[1], c.Expected.GetProperty("drawsAfterEnd").GetArrayLength()));
                continue;
            }

            SobolRsg rsg = Make();
            check.Output(c, "dimension", rsg.Dimension);
            int draws = c.Int("draws");
            var integers = new long[draws][];
            for (int i = 0; i < draws; i++)
            {
                if (i == draws / 2)
                {
                    rsg = rsg.Clone();
                }

                integers[i] = rsg.NextInt32Sequence().ToArray();
            }

            check.Output(c, "nextInt32Sequence", integers);

            rsg = Make();
            check.Output(c, "skipTo", skips.Select(n => rsg.SkipTo(n).ToArray()).ToArray());

            rsg = Make();
            rsg.SkipTo(c.Int("afterSkip"));
            check.Output(c, "nextSequenceAfterSkip", rsg.NextSequence().Value.ToArray());

            rsg = Make();
            for (int i = 0; i < c.Int("lastAfter"); i++)
            {
                rsg.NextSequence();
            }

            check.Output(c, "lastSequence", rsg.LastSequence().Value.ToArray());
        }

        check.Complete();
    }

    // The draws after a skip, in turn on one generator, each computed when compared, so that the last one's throw is
    // checked.
    private static object?[] DrawsAfter(SobolRsg rsg, long n, int draws)
    {
        rsg.SkipTo(n);
        return [.. Enumerable.Range(0, draws).Select(_ => (object?)(Func<object?>)(() => rsg.NextInt32Sequence().ToArray()))];
    }
}
