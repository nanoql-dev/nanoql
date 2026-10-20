// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The year-on-year inflation curve given no rates. QuantLib's constructor passes <c>rates[0]</c> to its base before
/// checking the count (<c>interpolatedyoyinflationcurve.hpp:103-108</c>), which reads past an empty vector, undefined.
/// nanoql refuses first, with QuantLib's checks of the count in its order: "too few dates" naming <c>dates</c>, or the
/// count mismatch naming <c>rates</c>. Given no dates, QuantLib's <c>dates.at(0)</c> throws <c>std::out_of_range</c>
/// first, a convention (<c>Conventions/InterpolatedInflationCurveCheckTests</c>).
/// </summary>
public class InflationCurveFirstDateTests
{
    private static readonly Date Reference = new(15, Month.June, 2024);

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
