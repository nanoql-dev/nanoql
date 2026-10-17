// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's period vectors, <c>tests/vectors/cxx/time/period.json</c>: <see cref="PeriodFunctions.Days"/>,
/// <see cref="PeriodFunctions.LongPeriod"/> and <c>TimeUnit * int</c>, which the wheel doesn't expose, over every
/// time unit and a range of lengths; and a time unit outside the enum only where QuantLib throws too.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PeriodCxxVectorTests
{
    [Fact]
    public void PeriodCxxVector()
    {
        var file = VectorFile.Load("cxx/time/period");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var units = (TimeUnit)c.Int("units");
            int[] lengths = c.Ints("lengths");
            if (c.Expected.TryGetProperty("days", out _))
            {
                check.Output(c, "days", Each(lengths, n => PeriodFunctions.Days(new Period(n, units))));
            }

            if (c.Expected.TryGetProperty("longPeriod", out _))
            {
                check.Output(c, "longPeriod", Each(lengths, n => PeriodFunctions.LongPeriod(new Period(n, units))));
            }

            if (c.Expected.TryGetProperty("unitTimesLength", out _))
            {
                check.Output(c, "unitTimesLength", Each(lengths, n => Pair(units * n)));
            }
        }

        check.Complete();
    }

    private static object[] Pair(Period p) => [p.Length, p.Units];

    private static Func<object?>[] Each<TOut>(int[] lengths, Func<int, TOut> f) =>
        lengths.Select(n => (Func<object?>)(() => f(n))).ToArray();
}
