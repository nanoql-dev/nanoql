// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="YearFractionToDateFunctions.YearFractionToDate"/> refuses a time whose day count overflows an integer,
/// an infinite time among them, where QuantLib's <c>boost::numeric_cast&lt;Integer&gt;</c> throws
/// <c>boost::numeric::bad_numeric_cast</c>, "bad numeric conversion: positive overflow" or "negative overflow"
/// (<c>yearfractiontodate.cpp:40-41</c>): <see cref="ArgumentException"/> naming <c>t</c>, with a text naming the
/// cause, since Boost's text is the library's.
/// </summary>
public class YearFractionToDateRefusalTests
{
    [Fact]
    public void AnInfiniteOrOverflowingTimeIsRefused()
    {
        var reference = new Date(15, Month.June, 2020);
        foreach (double t in (double[])[double.PositiveInfinity, double.NegativeInfinity, 1e300, -1e300, 6e6])
        {
            ArgumentException e = Assert.Throws<ArgumentException>(
                () => YearFractionToDateFunctions.YearFractionToDate(new Actual365Fixed(), reference, t));
            Assert.Equal("t", e.ParamName);
            Assert.Contains("overflows an integer", e.Message, StringComparison.Ordinal);
        }

        // a day count within an integer's range but past Date's is refused by Date, as QuantLib's
        ArgumentOutOfRangeException range = Assert.Throws<ArgumentOutOfRangeException>(
            () => YearFractionToDateFunctions.YearFractionToDate(new Actual365Fixed(), reference, 1e6));
        Assert.StartsWith("Date's serial number", range.Message, StringComparison.Ordinal);
    }
}
