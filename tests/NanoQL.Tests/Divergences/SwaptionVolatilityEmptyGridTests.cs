// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A discrete swaption volatility structure on no option tenors, no option dates or no swap tenors is refused with
/// <see cref="ArgumentException"/> naming the parameter: "no option tenors given", "no option dates given", "no swap
/// tenors given". QuantLib's <c>SwaptionVolatilityDiscrete</c> reads the first element of each in its checks
/// (<c>optionTenors_[0]</c>, <c>optionDates_[0]</c>, <c>swapTenors_[0]</c>, <c>swaptionvoldiscrete.cpp:137, 125,
/// 148</c>), before any check of its subclass could refuse the grid: a read past an empty vector's end, undefined, which
/// crashes the wheel. nanoql refuses each where QuantLib reads it, in QuantLib's order.
/// </summary>
public class SwaptionVolatilityEmptyGridTests
{
    [Fact]
    public void AnEmptyGridIsRefused()
    {
        var today = new Date(15, Month.January, 2025);
        Period[] options = [new(1, TimeUnit.Years), new(2, TimeUnit.Years)];
        Period[] swaps = [new(2, TimeUnit.Years), new(5, TimeUnit.Years)];
        Date[] dates = [new(15, Month.January, 2026), new(15, Month.January, 2027)];
        var vols = new Matrix(2, 2, 0.2);
        var empty = new Matrix(0, 2);

        ArgumentException e = Assert.Throws<ArgumentException>(() => new SwaptionVolatilityMatrix(
            today, new TARGET(), BusinessDayConvention.Following, Array.Empty<Period>(), swaps, empty, new Actual365Fixed()));
        Assert.Equal("optionTenors", e.ParamName);
        Assert.StartsWith("no option tenors given", e.Message, StringComparison.Ordinal);

        e = Assert.Throws<ArgumentException>(() => new SwaptionVolatilityMatrix(
            today, new TARGET(), BusinessDayConvention.Following, Array.Empty<Date>(), swaps, empty, new Actual365Fixed()));
        Assert.Equal("optionDates", e.ParamName);
        Assert.StartsWith("no option dates given", e.Message, StringComparison.Ordinal);

        e = Assert.Throws<ArgumentException>(() => new SwaptionVolatilityMatrix(
            today, new TARGET(), BusinessDayConvention.Following, options, [], new Matrix(2, 0), new Actual365Fixed()));
        Assert.Equal("swapTenors", e.ParamName);
        Assert.StartsWith("no swap tenors given", e.Message, StringComparison.Ordinal);

        e = Assert.Throws<ArgumentException>(() => new SwaptionVolatilityMatrix(
            today, new TARGET(), BusinessDayConvention.Following, dates, Array.Empty<Period>(), new Matrix(2, 0), new Actual365Fixed()));
        Assert.Equal("swapTenors", e.ParamName);

        // With the grid given, the same matrices build.
        Assert.Equal(2, new SwaptionVolatilityMatrix(
            today, new TARGET(), BusinessDayConvention.Following, options, swaps, vols, new Actual365Fixed()).OptionDates.Length);
        Assert.Equal(2, new SwaptionVolatilityMatrix(
            today, new TARGET(), BusinessDayConvention.Following, dates, swaps, vols, new Actual365Fixed()).OptionDates.Length);
    }
}
