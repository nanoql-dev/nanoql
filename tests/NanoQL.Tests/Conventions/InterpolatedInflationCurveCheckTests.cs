// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of the interpolated inflation curves' construction checks, which the vectors record only as
/// throws: each is an <see cref="ArgumentException"/> with QuantLib's text, naming <c>dates</c> for the count of dates
/// and their order and <c>rates</c> for the rates; an empty list of dates, whose first QuantLib reads with <c>at()</c>,
/// which throws <c>std::out_of_range</c>, is refused at that point with QuantLib's own "too few dates: 0" (the
/// playbook's convention for such an exception; <c>interpolatedzeroinflationcurve.hpp:98</c>,
/// <c>interpolatedyoyinflationcurve.hpp:103</c>); and the year-on-year curve's empty rates, which QuantLib reads past
/// (a documented divergence, <c>InflationCurveFirstDateTests</c>), with QuantLib's checks of the count, in QuantLib's
/// order.
/// </summary>
public class InterpolatedInflationCurveCheckTests
{
    private static readonly Date Reference = new(15, Month.June, 2024);
    private static readonly Date[] TwoDates = [new(1, Month.April, 2024), new(1, Month.April, 2025)];

    [Fact]
    public void TheZeroCurveChecksNameTheirArguments()
    {
        AssertArgument("dates", "too few dates: 0", () => Zero([], []));
        AssertArgument("dates", "too few dates: 1", () => Zero([TwoDates[0]], [0.03]));
        AssertArgument("rates", "indices/dates count mismatch: 1 vs 2", () => Zero(TwoDates, [0.03]));
        AssertArgument("rates", "zero inflation data < -100 %", () => Zero(TwoDates, [0.03, -1.0]));
        AssertArgument("dates", "dates not sorted", () => Zero([TwoDates[1], TwoDates[0]], [0.03, 0.03]));
        Assert.Equal(-1.0, Zero(TwoDates, [-1.0, 0.03]).Rates()[0]);
    }

    [Fact]
    public void TheYoYCurveChecksNameTheirArguments()
    {
        AssertArgument("dates", "too few dates: 0", () => YoY([], [0.03]));
        AssertArgument("dates", "too few dates: 1", () => YoY([TwoDates[0]], []));
        AssertArgument("rates", "indices/dates count mismatch: 0 vs 2", () => YoY(TwoDates, []));
        AssertArgument("rates", "indices/dates count mismatch: 3 vs 2", () => YoY(TwoDates, [0.03, 0.03, 0.03]));
        AssertArgument("rates", "year-on-year inflation data < -100 %", () => YoY(TwoDates, [0.03, -2.0]));
        Assert.Equal(0.025, YoY(TwoDates, [0.025, 0.03]).BaseRate());
    }

    private static ZeroInflationCurve Zero(Date[] dates, double[] rates) =>
        new(Reference, dates, rates, Frequency.Monthly, new Actual365Fixed());

    private static YoYInflationCurve YoY(Date[] dates, double[] rates) =>
        new(Reference, dates, rates, Frequency.Monthly, new Actual365Fixed());

    private static void AssertArgument(string paramName, string message, Action action)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(action);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }
}
