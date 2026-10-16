// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>The period vector, <c>tests/vectors/time/period.json</c>.</summary>
/// <remarks>
/// Where QuantLib's <c>==</c> throws on a pair whose order it can't decide, nanoql's returns <see langword="false"/>, a
/// documented divergence; those outputs are mapped to <see langword="false"/>.
/// </remarks>
[Trait("Oracle", "QuantLib")]
public class PeriodVectorTests
{
    private const string UndecidableEquality =
        "Period == returns false for a pair whose order can't be decided, where QuantLib's == throws";

    [Fact]
    public void PeriodVector()
    {
        var file = VectorFile.Load("time/period");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Period p = Read(c.Input("period"));
            if (c.Name.StartsWith("unary", StringComparison.Ordinal))
            {
                check.Output(c, "normalized", Pair(p.Normalized()));
                check.Output(c, "frequency", () => p.Frequency());
                check.Output(c, "negated", Pair(-p));
                check.Output(c, "toString", p.ToString());
            }
            else if (c.Name.StartsWith("compare", StringComparison.Ordinal))
            {
                Period[] others = c.Input("others").EnumerateArray().Select(Read).ToArray();
                check.Output(c, "less", others.Select(o => (Func<object?>)(() => p < o)).ToArray());
                check.Output(c, "lessOrEqual", others.Select(o => (Func<object?>)(() => p <= o)).ToArray());
                check.Mapped(c, "equal", others.Select(o => (Func<object?>)(() => p == o)).ToArray(), false, UndecidableEquality);
            }
            else
            {
                Period[] others = c.Input("others").EnumerateArray().Select(Read).ToArray();
                check.Output(c, "plus", others.Select(o => (Func<object?>)(() => Pair(p + o))).ToArray());
                check.Output(c, "minus", others.Select(o => (Func<object?>)(() => Pair(p - o))).ToArray());
                check.Output(c, "times", c.Ints("multipliers").Select(k => (Func<object?>)(() => Pair(p * k))).ToArray());
            }
        }

        check.Complete();
        Assert.Equal(28, check.MappedOutputs);
    }

    private static Period Read(JsonElement pair) => new(pair[0].GetInt32(), (TimeUnit)pair[1].GetInt32());

    private static object[] Pair(Period p) => [p.Length, p.Units];
}
