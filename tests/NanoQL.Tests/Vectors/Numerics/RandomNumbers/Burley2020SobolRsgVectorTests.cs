// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Security.Cryptography;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Burley-scrambled Sobol vectors, <c>tests/vectors/math/randomnumbers/burley2020sobolrsg.json</c>:
/// <see cref="Burley2020SobolRsg"/> on nine generators, the scramble's groups of four dimensions ending inside and at a
/// group, past the direction integers' tables where the seed initializes the last dimensions, and on Unit: its
/// dimension, five integer draws (each one's last six integers and the SHA-256 of all of them), then five sequences
/// (each one's last six values and weight) and the last sequence.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Burley2020SobolRsgVectorTests
{
    [Fact]
    public void Burley2020SobolRsgVector()
    {
        var file = VectorFile.Load("math/randomnumbers/burley2020sobolrsg");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            int last = c.Int("last");
            var rsg = new Burley2020SobolRsg(
                c.Int("dimension"),
                c.Input("seed").GetInt64(),
                (SobolRsgDirectionIntegers)c.Int("directionIntegers"),
                c.Input("scrambleSeed").GetInt64());
            check.Output(c, "dimension", rsg.Dimension);

            var drawIntegers = new List<long[]>();
            var drawDigests = new List<string>();
            for (int i = 0; i < c.Int("draws"); i++)
            {
                ReadOnlySpan<long> integers = rsg.NextInt32Sequence().Span;
                drawIntegers.Add(Tail(integers, last).ToArray());
                drawDigests.Add(Digest(integers));
            }

            var sequences = new List<double[]>();
            var weights = new List<double>();
            for (int i = 0; i < c.Int("sequences"); i++)
            {
                Sample<ReadOnlyMemory<double>> s = rsg.NextSequence();
                sequences.Add(Tail(s.Value.Span, last).ToArray());
                weights.Add(s.Weight);
            }

            check.Output(c, "drawIntegers", drawIntegers);
            check.Output(c, "drawDigests", drawDigests);
            check.Output(c, "sequences", sequences);
            check.Output(c, "weights", weights.ToArray());
            check.Output(c, "lastSequence", Tail(rsg.LastSequence().Value.Span, last).ToArray());
        }

        check.Complete();
    }

    // The last n values, or all of them below n, as Python's [-n:] slice.
    private static ReadOnlySpan<T> Tail<T>(ReadOnlySpan<T> values, int n) => values[Math.Max(0, values.Length - n)..];

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
