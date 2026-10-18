// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The index presets' refusals, by type, message and parameter, which a vector's <c>{"throws": true}</c> can't tell
/// apart. Euribor and Euribor365 refuse a daily tenor with QuantLib's <c>QL_REQUIRE</c> on the tenor
/// (<c>euribor.cpp:66-68, 78-80</c>), an <see cref="ArgumentException"/> with QuantLib's text; and an intraday unit,
/// which QuantLib's convention and end-of-month functions fail on (<c>euribor.cpp:31-55</c>), with
/// <see cref="ArgumentOutOfRangeException"/>, the enum row's type, and QuantLib's text.
/// </summary>
public class IndexPresetErrorTests
{
    [Fact]
    public void EuriborRefusesADailyOrAnIntradayTenor()
    {
        Func<Period, IborIndex>[] presets = [t => new Euribor(t), t => new Euribor365(t)];
        foreach (Func<Period, IborIndex> make in presets)
        {
            ArgumentException daily = Assert.Throws<ArgumentException>(() => make(new Period(1, TimeUnit.Days)));
            Assert.Equal("tenor", daily.ParamName);
            Assert.StartsWith(
                "for daily tenors (1D) dedicated DailyTenor constructor must be used",
                daily.Message,
                StringComparison.Ordinal);

            ArgumentException twoDays = Assert.Throws<ArgumentException>(() => make(new Period(2, TimeUnit.Days)));
            Assert.StartsWith("for daily tenors (2D) ", twoDays.Message, StringComparison.Ordinal);

            ArgumentOutOfRangeException intraday = Assert.Throws<ArgumentOutOfRangeException>(
                () => make(new Period(1, TimeUnit.Hours)));
            Assert.Equal("tenor", intraday.ParamName);
            Assert.StartsWith("invalid time units", intraday.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Every interest-rate index refuses an intraday tenor of non-zero length, which QuantLib's constructor fails on
    /// when it compares the tenor with one day (<c>interestrateindex.cpp:44</c>, through <c>daysMinMax</c>,
    /// <c>period.cpp:276</c>): <see cref="NanoQLException"/>, since a zero length passes; a Tibor and a plain Ibor
    /// index here. Euribor fails earlier, on its own conventions (above).
    /// </summary>
    [Fact]
    public void AnIntradayTenorOfNonZeroLengthIsRefused()
    {
        Assert.Equal(
            "unknown time unit (4)",
            Assert.Throws<NanoQLException>(() => new Tibor(new Period(1, TimeUnit.Hours))).Message);
        Assert.Equal(
            "unknown time unit (5)",
            Assert.Throws<NanoQLException>(() => new IborIndex(
                "Foo", new Period(-30, TimeUnit.Minutes), 2, null, new TARGET(), BusinessDayConvention.Following,
                false, new Actual360())).Message);
        Assert.Equal(
            "unknown time unit (8)",
            Assert.Throws<NanoQLException>(() => new OvernightIndexLike(new Period(1, TimeUnit.Microseconds))).Message);

        var zero = new IborIndex(
            "Foo", new Period(0, TimeUnit.Hours), 2, null, new TARGET(), BusinessDayConvention.Following, false,
            new Actual360());
        Assert.Equal(new Period(0, TimeUnit.Hours), zero.Tenor);
    }

    private sealed class OvernightIndexLike(Period tenor) : InterestRateIndex(
        "Bar", tenor, 0, null, new TARGET(), new Actual360())
    {
        public override Date MaturityDate(Date valueDate) => valueDate + 1;

        public override double ForecastFixing(Date fixingDate, EvaluationContext context) => 0.0;
    }
}
