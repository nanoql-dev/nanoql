// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's weekday vectors, <c>tests/vectors/cxx/time/weekday.json</c>: the weekday's four texts, which the
/// wheel doesn't expose, for every weekday and for a value on either side, where QuantLib throws.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class WeekdayCxxVectorTests
{
    [Fact]
    public void WeekdayCxxVector()
    {
        var file = VectorFile.Load("cxx/time/weekday");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Weekday[] weekdays = c.Ints("weekdays").Select(w => (Weekday)w).ToArray();
            check.Output(c, "longWeekday", Each(weekdays, w => WeekdayFunctions.LongWeekday(w)));
            check.Output(c, "shortWeekday", Each(weekdays, w => WeekdayFunctions.ShortWeekday(w)));
            check.Output(c, "shortestWeekday", Each(weekdays, w => WeekdayFunctions.ShortestWeekday(w)));
            check.Output(c, "toString", Each(weekdays, w => WeekdayFunctions.ToString(w)));
        }

        check.Complete();
    }

    private static Func<object?>[] Each(Weekday[] weekdays, Func<Weekday, string> f) =>
        weekdays.Select(w => (Func<object?>)(() => f(w))).ToArray();
}
