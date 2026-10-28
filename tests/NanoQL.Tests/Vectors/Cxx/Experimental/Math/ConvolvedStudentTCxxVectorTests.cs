// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's convolved Student t vectors, <c>tests/vectors/cxx/experimental/math/convolvedstudentt.json</c>:
/// the cumulative distribution, the density and the inverse of combinations of odd-order t's, with the refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ConvolvedStudentTCxxVectorTests
{
    [Fact]
    public void ConvolvedStudentTCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/math/convolvedstudentt");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Name == "refused combinations")
            {
                check.Output(c, "inspect", Each(c.Input("refused").EnumerateArray(), p =>
                    Inspect(new CumulativeBehrensFisher(
                        [.. p[0].EnumerateArray().Select(x => x.GetInt32())], [.. p[1].EnumerateArray().Select(VectorCase.Real)]))));
                continue;
            }

            int[] dfs = c.Ints("degreesFreedom");
            double[] fs = c.Doubles("factors");
            var cumulative = new CumulativeBehrensFisher(dfs, fs);
            check.Output(c, "inspect", Inspect(cumulative));
            check.Output(c, "evaluate", c.Doubles("xs").Select(x => (object)new object[] { cumulative.Value(x), cumulative.Density(x) }).ToArray());
            check.Output(c, "inverse", c.Doubles("accuracies").Select(a =>
            {
                var inverse = new InverseCumulativeBehrensFisher(dfs, fs, a);
                return (object)Each(c.Doubles("qs"), q => inverse.Value(q));
            }).ToArray());
        }

        check.Complete();
    }

    private static object[] Inspect(CumulativeBehrensFisher x) =>
        [x.DegreeFreedom.Select(d => (object)d).ToArray(), x.Factors.Select(f => (object)f).ToArray()];

    // Each value lazily, so that a throw is checked element by element.
    private static Func<object?>[] Each<T>(IEnumerable<T> xs, Func<T, object?> f) =>
        [.. xs.Select(x => (Func<object?>)(() => f(x)))];
}
