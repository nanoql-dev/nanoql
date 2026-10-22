// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Utilities;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The data parsers' refusals, with QuantLib's texts (<c>dataparsers.cpp:41</c>, <c>:54</c>, <c>:64-69</c>, <c>:78</c>,
/// <c>:83-84</c>, <c>:107-108</c>): each is a refusal of the string parsed, <see cref="ArgumentException"/> naming
/// <c>str</c>. A month outside 1 to 12 is refused by <c>Date</c>, as QuantLib's is.
/// </summary>
public class DataParserErrorTests
{
    [Fact]
    public void TheParsersRefuseWithQuantLibsTexts()
    {
        (string Input, string Message)[] periods =
        [
            ("5", "period string length must be at least 2"),
            ("5X", "unknown '5X' unit"),
            ("3MD", "single period require a string of at least 2 characters"),
            ("xD", "no numbers of Days provided"),
            ("+-3M", "unable to parse the number of units of Months in '+-3M'"),
            ("99999999999D", "unable to parse the number of units of Days in '99999999999D'"),
        ];
        foreach ((string input, string message) in periods)
        {
            ArgumentException e = Assert.Throws<ArgumentException>(() => PeriodParser.Parse(input));
            Assert.Equal("str", e.ParamName);
            Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
        }

        foreach (string input in (string[])["2006/01/15", "2006-01-155"])
        {
            ArgumentException e = Assert.Throws<ArgumentException>(() => DateParser.ParseISO(input));
            Assert.Equal("str", e.ParamName);
            Assert.StartsWith("invalid format", e.Message, StringComparison.Ordinal);
        }

        ArgumentOutOfRangeException month = Assert.Throws<ArgumentOutOfRangeException>(() => DateParser.ParseISO("2006-13-15"));
        Assert.StartsWith("month 13 outside January-December range [1,12]", month.Message, StringComparison.Ordinal);

        Assert.Equal("str", Assert.Throws<ArgumentNullException>(() => PeriodParser.Parse(null!)).ParamName);
        Assert.Equal("str", Assert.Throws<ArgumentNullException>(() => DateParser.ParseISO(null!)).ParamName);
    }
}
