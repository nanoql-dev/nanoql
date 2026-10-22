// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// QuantLib's <c>DateParser::parseFormatted</c>, which runs Boost.Date_Time's format engine on a string, isn't ported:
/// <c>DateOnly.ParseExact</c> does its job, with .NET's format strings, and the
/// <see cref="DateOnly"/> converts to a <see cref="Date"/>. QuantLib's <c>parseDates</c> case
/// (<c>test-suite/dates.cpp:399-433</c>): its four inputs and formats, parsed so to QuantLib's dates.
/// </summary>
public class DateParserFormatTests
{
    [Fact]
    public void ParseDatesInputsParseExactlyToQuantLibsDates()
    {
        (string Input, string QuantLibFormat, string Format, Date Expected)[] cases =
        [
            ("2006-01-15", "%Y-%m-%d", "yyyy-MM-dd", new Date(15, Month.January, 2006)),
            ("12/02/2012", "%m/%d/%Y", "MM/dd/yyyy", new Date(2, Month.December, 2012)),
            ("12/02/2012", "%d/%m/%Y", "dd/MM/yyyy", new Date(12, Month.February, 2012)),
            ("20011002", "%Y%m%d", "yyyyMMdd", new Date(2, Month.October, 2001)),
        ];

        foreach ((string input, string quantLibFormat, string format, Date expected) in cases)
        {
            Date d = Date.FromDateOnly(DateOnly.ParseExact(input, format, CultureInfo.InvariantCulture));
            Assert.True(d == expected, $"{input} with {quantLibFormat} ({format}): {d}, expected {expected}");
        }
    }
}
