// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Malformed input to the data parsers. QuantLib reads the numbers with <c>std::stoi</c>, which skips leading blanks
/// and stops at the first non-digit: <c>DateParser::parseISO</c> checks only the length and the dashes
/// (<c>dataparsers.cpp:106-114</c>), so "2006-1a-15" parses as 15 January 2006 and "2006-ab-15" throws
/// <c>std::invalid_argument</c> past QuantLib's checks; <c>PeriodParser</c> reads a period's number from its first sign
/// or digit (<c>:77-81</c>), so "x5D" and "5xD" parse as 5D. ISO 8601's YYYY-MM-DD is four, two and two digits, and
/// QuantLib's own texts state the grammar: nanoql refuses a field or a number whose text isn't digits (a period's with
/// an optional sign), with QuantLib's texts, "invalid format" and "unable to parse the number of units of ... in
/// '...'". Every well-formed input parses as QuantLib's.
/// </summary>
public class DataParserStrictnessTests
{
    [Fact]
    public void AnIsoDateFieldMustBeDigits()
    {
        // QuantLib: January 15th, 2006; January 15th, 2006; January 1st, 2006; January 5th, 2006; and "invalid stoi
        // argument", a std::invalid_argument
        foreach (string s in (string[])["2006-1a-15", "2006-+1-15", "2006-01-1x", "2006-01- 5", "2006-ab-15"])
        {
            ArgumentException e = Assert.Throws<ArgumentException>(() => DateParser.ParseISO(s));
            Assert.Equal("str", e.ParamName);
            Assert.StartsWith("invalid format", e.Message, StringComparison.Ordinal);
        }

        // QuantLib's own
        Assert.Equal(new Date(15, Month.January, 2006), DateParser.ParseISO("2006-01-15"));
        Assert.Equal(new Date(29, Month.February, 2024), DateParser.ParseISO("2024-02-29"));
    }

    [Fact]
    public void APeriodsNumberMustBeASignAndDigits()
    {
        // QuantLib: 5D, 5D, 3M, 3M and 18M
        (string Input, string Unit)[] lenient = [("x5D", "Days"), ("5xD", "Days"), (" 3M", "Months"), ("3 M", "Months"), ("1Y x6M", "Months")];
        foreach ((string s, string unit) in lenient)
        {
            ArgumentException e = Assert.Throws<ArgumentException>(() => PeriodParser.Parse(s));
            Assert.Equal("str", e.ParamName);
            Assert.StartsWith($"unable to parse the number of units of {unit} in '", e.Message, StringComparison.Ordinal);
        }

        // QuantLib's own
        Assert.Equal(new Period(3, TimeUnit.Months), PeriodParser.Parse("3M"));
        Assert.Equal(new Period(18, TimeUnit.Months), PeriodParser.Parse("1Y6M"));
        Assert.Equal(new Period(-3, TimeUnit.Months), PeriodParser.Parse("-3M"));
        Assert.Equal(new Period(3, TimeUnit.Months), PeriodParser.Parse("+3m"));
        Assert.Equal(new Period(10, TimeUnit.Days), PeriodParser.Parse("10d"));
    }
}
