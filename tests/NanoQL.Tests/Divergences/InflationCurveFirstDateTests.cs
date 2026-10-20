// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The interpolated inflation curves given no dates or no rates. QuantLib's constructors pass <c>dates.at(0)</c> to
/// their base before checking the count (<c>interpolatedzeroinflationcurve.hpp:98-102</c>,
/// <c>interpolatedyoyinflationcurve.hpp:103-108</c>), so with no date they throw <c>std::out_of_range</c> (the wheel's
/// <c>IndexError: invalid vector subscript</c>); the year-on-year curve also passes <c>rates[0]</c>, which reads past an
/// empty vector, undefined. nanoql refuses both first, with QuantLib's own texts: "too few dates: 0" naming
/// <c>dates</c>, and for empty rates QuantLib's checks of the count in its order, "too few dates" or the count mismatch
/// naming <c>rates</c>.
/// </summary>
public class InflationCurveFirstDateTests
{
    private static readonly Date Reference = new(15, Month.June, 2024);

    [Fact]
    public void NoDatesAreRefusedBeforeTheFirstIsRead()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new ZeroInflationCurve(Reference, [], [], Frequency.Monthly, new Actual365Fixed()));
        Assert.Equal("dates", e.ParamName);
        Assert.StartsWith("too few dates: 0", e.Message, StringComparison.Ordinal);
        e = Assert.Throws<ArgumentException>(
            () => new YoYInflationCurve(Reference, [], [0.03, 0.03], Frequency.Monthly, new Actual365Fixed()));
        Assert.Equal("dates", e.ParamName);
        Assert.StartsWith("too few dates: 0", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NoRatesAreRefusedBeforeTheFirstIsRead()
    {
        Date[] dates = [new(1, Month.April, 2024), new(1, Month.April, 2025)];
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new YoYInflationCurve(Reference, dates, [], Frequency.Monthly, new Actual365Fixed()));
        Assert.Equal("rates", e.ParamName);
        Assert.StartsWith("indices/dates count mismatch: 0 vs 2", e.Message, StringComparison.Ordinal);
        e = Assert.Throws<ArgumentException>(
            () => new YoYInflationCurve(Reference, [dates[0]], [], Frequency.Monthly, new Actual365Fixed()));
        Assert.Equal("dates", e.ParamName);
        Assert.StartsWith("too few dates: 1", e.Message, StringComparison.Ordinal);

        // the zero curve doesn't read its rates before its checks: QuantLib's own count check refuses them
        e = Assert.Throws<ArgumentException>(
            () => new ZeroInflationCurve(Reference, dates, [], Frequency.Monthly, new Actual365Fixed()));
        Assert.Equal("rates", e.ParamName);
    }
}
