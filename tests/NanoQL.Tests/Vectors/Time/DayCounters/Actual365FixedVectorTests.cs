// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Actual/365 (Fixed) vectors, <c>tests/vectors/time/daycounters/actual365fixed.json</c>: the name, the day count
/// and the year fraction under the Standard and NoLeap conventions, and the name and the year fraction inside a
/// reference period under the Canadian one.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Actual365FixedVectorTests
{
    [Fact]
    public void Actual365Fixed()
    {
        var file = VectorFile.Load("time/daycounters/actual365fixed");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var dc = new Actual365Fixed((Actual365FixedConvention)c.Int("convention"));
            Date d1 = c.DateInput("d1");
            Date[] d2 = c.DateInputs("d2");
            check.Output(c, "name", dc.Name);
            if (c.Inputs.TryGetProperty("refPeriodStart", out _))
            {
                Date refPeriodStart = c.DateInput("refPeriodStart"), refPeriodEnd = c.DateInput("refPeriodEnd");
                check.Output(c, "yearFraction", Each(d2, x => dc.YearFraction(d1, x, refPeriodStart, refPeriodEnd)));
            }
            else
            {
                check.Output(c, "dayCount", Each(d2, x => dc.DayCount(d1, x)));
                check.Output(c, "yearFraction", Each(d2, x => dc.YearFraction(d1, x)));
            }
        }

        check.Complete();
    }

    private static Func<object?>[] Each<T>(Date[] dates, Func<Date, T> f) =>
        dates.Select(x => (Func<object?>)(() => f(x))).ToArray();
}
