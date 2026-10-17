// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Security.Cryptography;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Sobol generator's vectors, <c>tests/vectors/math/randomnumbers/sobolrsg.json</c>: twenty
/// <see cref="SobolRsg"/>s with the Gray code, each set of direction integers among them, at dimensions up to
/// 21,200; their first four integer draws and a draw of each kind after each of four <see cref="SobolRsg.SkipTo"/>
/// calls, each integer draw's last six integers and the SHA-256 of all of them, each sequence's last six values.
/// Before the skips, the draws continue from a <see cref="SobolRsg.Clone"/>, whose stream is QuantLib's, carried on.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SobolRsgVectorTests
{
    [Fact]
    public void SobolRsgVector()
    {
        var file = VectorFile.Load("math/randomnumbers/sobolrsg");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            int last = c.Int("last");
            var rsg = new SobolRsg(
                c.Int("dimension"), c.Input("seed").GetInt64(), (SobolRsgDirectionIntegers)c.Int("directionIntegers"));
            check.Output(c, "dimension", rsg.Dimension);

            var drawIntegers = new List<long[]>();
            var drawDigests = new List<string>();
            for (int i = 0; i < c.Int("draws"); i++)
            {
                ReadOnlySpan<long> integers = rsg.NextInt32Sequence().Span;
                drawIntegers.Add(integers[^last..].ToArray());
                drawDigests.Add(Digest(integers));
            }

            rsg = rsg.Clone();
            var skipIntegers = new List<long[]>();
            var skipDigests = new List<string>();
            var skipSequences = new List<double[]>();
            foreach (long n in c.Input("skips").EnumerateArray().Select(e => e.GetInt64()))
            {
                rsg.SkipTo(n);
                ReadOnlySpan<long> integers = rsg.NextInt32Sequence().Span;
                skipIntegers.Add(integers[^last..].ToArray());
                skipDigests.Add(Digest(integers));
                skipSequences.Add(rsg.NextSequence().Value.Span[^last..].ToArray());
            }

            check.Output(c, "drawIntegers", drawIntegers);
            check.Output(c, "drawDigests", drawDigests);
            check.Output(c, "skipIntegers", skipIntegers);
            check.Output(c, "skipDigests", skipDigests);
            check.Output(c, "skipSequences", skipSequences);
        }

        check.Complete();
    }

    // The SHA-256 of the integers as little-endian unsigned 32-bit integers, as the generator writes it.
    private static string Digest(ReadOnlySpan<long> integers)
    {
        var bytes = new byte[integers.Length * sizeof(uint)];
        for (int i = 0; i < integers.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * sizeof(uint)), checked((uint)integers[i]));
        }

        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
