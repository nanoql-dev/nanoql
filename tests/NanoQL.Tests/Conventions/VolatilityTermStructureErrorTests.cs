// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of the volatility bases' refusals, which the vectors record only as throws: the strike check
/// (<c>voltermstructure.cpp:40-46</c>) and the range checks (<c>termstructure.cpp:54-72</c>) refuse a value against
/// the structure's domain with <see cref="NanoQLException"/>, as the pilot's <c>CheckRange</c> does, with QuantLib's
/// texts in C#'s number formatting; an unknown volatility type or convention throws
/// <see cref="ArgumentOutOfRangeException"/> on its parameter, with the text of QuantLib's stream operator for the
/// type; and a null argument <see cref="ArgumentNullException"/> on the caller's parameter.
/// </summary>
public class VolatilityTermStructureErrorTests
{
    private static readonly Date Reference = new(15, Month.January, 2025);

    [Fact]
    public void TheVolatilityBasesRefusals()
    {
        var surface = new ConstantOptionletVolatility(
            Reference, new TARGET(), BusinessDayConvention.Following, 0.2, new Actual365Fixed());
        Date before = Reference - 1;

        const string Domain = "[-1.7976931348623157E+308,1.7976931348623157E+308]";
        AssertRefused($"strike (NaN) is outside the curve domain {Domain}", () => surface.Volatility(Reference + 30, double.NaN));
        AssertRefused($"strike (Infinity) is outside the curve domain {Domain}", () => surface.Volatility(1.0, double.PositiveInfinity));
        AssertRefused($"strike (-Infinity) is outside the curve domain {Domain}", () => surface.BlackVariance(1.0, double.NegativeInfinity));
        AssertRefused(
            $"strike (NaN) is outside the curve domain {Domain}",
            () => surface.BlackVariance(new Period(1, TimeUnit.Years), double.NaN));
        Assert.Equal(0.2, surface.Volatility(1.0, double.NaN, true));
        Assert.Equal(0.2, surface.EnableExtrapolation().Volatility(1.0, double.NaN));

        AssertRefused(
            "date (January 14th, 2025) before reference date (January 15th, 2025)", () => surface.Volatility(before, 0.03, true));
        AssertRefused(
            "date (January 14th, 2025) before reference date (January 15th, 2025)",
            () => surface.Volatility(new Period(-1, TimeUnit.Days), 0.03));
        AssertRefused("negative time (-0.25) given", () => surface.BlackVariance(-0.25, 0.03));
        AssertRefused("negative time (NaN) given", () => surface.Volatility(double.NaN, 0.03, true));
        double pastMax = surface.MaxTime + 1.0;
        AssertRefused(
            $"time ({Show(pastMax)}) is past max curve time ({Show(surface.MaxTime)})", () => surface.Volatility(pastMax, 0.03));

        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => new ConstantOptionletVolatility(
            Reference, new TARGET(), BusinessDayConvention.Following, 0.2, new Actual365Fixed(), (VolatilityType)2));
        Assert.Equal("type", e.ParamName);
        Assert.StartsWith("Unknown volatility type (2)", e.Message, StringComparison.Ordinal);
        e = Assert.Throws<ArgumentOutOfRangeException>(() => ConstantOptionletVolatility.Create(
            0, new TARGET(), BusinessDayConvention.Following, 0.2, new Actual365Fixed(), Context(), (VolatilityType)(-1)));
        Assert.Equal("type", e.ParamName);
        Assert.StartsWith("Unknown volatility type (-1)", e.Message, StringComparison.Ordinal);
        e = Assert.Throws<ArgumentOutOfRangeException>(() => new ConstantOptionletVolatility(
            Reference, new TARGET(), (BusinessDayConvention)99, 0.2, new Actual365Fixed()));
        Assert.Equal("bdc", e.ParamName);

        // QuantLib's empty calendar is null: the structure is built and gives volatilities by date and time, and only
        // an option date from a tenor needs the calendar (calendar.hpp's "no calendar implementation provided").
        var noCalendar = new ConstantOptionletVolatility(Reference, null, BusinessDayConvention.Following, 0.2, new Actual365Fixed());
        Assert.Equal(0.2, noCalendar.Volatility(Reference + 30, 0.03));
        AssertRefused("no calendar implementation provided", () => noCalendar.OptionDateFromTenor(new Period(1, TimeUnit.Years)));
        AssertRefused("no calendar implementation provided", () => noCalendar.Volatility(new Period(1, TimeUnit.Years), 0.03));
        Assert.Equal("dc", Assert.Throws<ArgumentNullException>(() => new ConstantOptionletVolatility(
            Reference, new TARGET(), BusinessDayConvention.Following, 0.2, null!)).ParamName);
        Assert.Equal("cal", Assert.Throws<ArgumentNullException>(() => ConstantOptionletVolatility.Create(
            0, null!, BusinessDayConvention.Following, 0.2, new Actual365Fixed(), Context())).ParamName);
        Assert.Equal("dc", Assert.Throws<ArgumentNullException>(() => ConstantOptionletVolatility.Create(
            0, new TARGET(), BusinessDayConvention.Following, 0.2, null!, Context())).ParamName);
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => ConstantOptionletVolatility.Create(
            0, new TARGET(), BusinessDayConvention.Following, 0.2, new Actual365Fixed(), null!)).ParamName);
        Assert.Equal("optionDate", Assert.Throws<ArgumentException>(() => surface.Volatility(default(Date), 0.03)).ParamName);
    }

    private static EvaluationContext Context() => new() { EvaluationDate = Reference };

    private static string Show(double x) => x.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static void AssertRefused(string message, Func<object> action) =>
        Assert.Equal(message, Assert.Throws<NanoQLException>(action).Message);
}
