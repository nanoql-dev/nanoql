// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;


namespace NanoQL.Tests.Vectors;

/// <summary>
/// The xoshiro256** generator's harness vectors,
/// <c>tests/vectors/cxx/math/randomnumbers/xoshiro256starstaruniformrng.json</c>:
/// <see cref="Xoshiro256StarStarUniformRng"/> at 64-bit seeds and from four-word states, the draws of
/// <see cref="Xoshiro256StarStarUniformRng.NextInt64"/>, their bits read unsigned as QuantLib's, and of
/// <see cref="Xoshiro256StarStarUniformRng.NextReal"/>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Xoshiro256StarStarUniformRngCxxVectorTests
{
    [Fact]
    public void Xoshiro256StarStarUniformRngCxxVector()
    {
        var file = VectorFile.Load("cxx/math/randomnumbers/xoshiro256starstaruniformrng");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            int draws = c.Int("draws");
            JsonElement spec = c.Input("generator");
            Xoshiro256StarStarUniformRng rng = Make(spec);
            var ints = new string[draws];
            for (int i = 0; i < draws; i++)
            {
                ints[i] = unchecked((ulong)rng.NextInt64()).ToString(CultureInfo.InvariantCulture);
            }

            rng = Make(spec);
            var reals = new double[draws];
            for (int i = 0; i < draws; i++)
            {
                reals[i] = rng.NextReal();
            }

            check.Output(c, "nextInt64", ints);
            check.Output(c, "nextReal", reals);
        }

        check.Complete();
    }

    private static long Word(JsonElement e) => unchecked((long)ulong.Parse(e.GetString()!, CultureInfo.InvariantCulture));

    private static Xoshiro256StarStarUniformRng Make(JsonElement spec)
    {
        if (spec.TryGetProperty("seed", out JsonElement seed))
        {
            return new Xoshiro256StarStarUniformRng(Word(seed));
        }

        JsonElement[] s = [.. spec.GetProperty("state").EnumerateArray()];
        return new Xoshiro256StarStarUniformRng(Word(s[0]), Word(s[1]), Word(s[2]), Word(s[3]));
    }
}
