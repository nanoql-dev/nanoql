// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's polar Student t generator vectors, <c>tests/vectors/cxx/experimental/math/polarstudenttrng.json</c>:
/// draws on QuantLib's Mersenne Twister at fixed seeds, with the refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PolarStudentTRngCxxVectorTests
{
    [Fact]
    public void PolarStudentTRngCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/math/polarstudenttrng");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            int count = c.Int("count");
            check.Output(c, "draws", Each(
                c.Doubles("degrees").SelectMany(df => c.Input("seeds").EnumerateArray().Select(s => (df, s.GetInt64()))),
                p => Draws(p.df, p.Item2, count)));
            check.Output(c, "refused", Each(c.Doubles("refused"), df => Draws(df, 42, 1)));
        }

        check.Complete();
    }

    private static object[] Draws(double df, long seed, int count)
    {
        PolarStudentTRng<MersenneTwisterUniformRng> g = PolarStudentTRng.Create<MersenneTwisterUniformRng>(df, seed);
        return [.. Enumerable.Range(0, count).Select(_ =>
        {
            var s = g.Next();
            return (object)new object[] { s.Value, s.Weight };
        })];
    }

    // Each value lazily, so that a throw is checked element by element.
    private static Func<object?>[] Each<T>(IEnumerable<T> xs, Func<T, object?> f) =>
        [.. xs.Select(x => (Func<object?>)(() => f(x)))];
}
