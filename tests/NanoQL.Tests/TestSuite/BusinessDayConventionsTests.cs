// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/businessdayconventions.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

/// <summary>QuantLib's business day convention tests, <c>test-suite/businessdayconventions.cpp</c>.</summary>
[Trait("Oracle", "QuantLib")]
public class BusinessDayConventionsTests
{
    [Fact]
    public void TestConventions()
    {
        // Testing business day conventions...

        SingleCase[] testCases =
        [
            // Following
            new(new SouthAfrica(), BusinessDayConvention.Following, new Date(3, February, 2015),
                new Period(1, TimeUnit.Months), false, new Date(3, March, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.Following, new Date(3, February, 2015),
                new Period(4, TimeUnit.Days), false, new Date(9, February, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.Following, new Date(31, January, 2015),
                new Period(1, TimeUnit.Months), true, new Date(27, February, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.Following, new Date(31, January, 2015),
                new Period(1, TimeUnit.Months), false, new Date(2, March, 2015)),

            // ModifiedFollowing
            new(new SouthAfrica(), BusinessDayConvention.ModifiedFollowing, new Date(3, February, 2015),
                new Period(1, TimeUnit.Months), false, new Date(3, March, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.ModifiedFollowing, new Date(3, February, 2015),
                new Period(4, TimeUnit.Days), false, new Date(9, February, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.ModifiedFollowing, new Date(31, January, 2015),
                new Period(1, TimeUnit.Months), true, new Date(27, February, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.ModifiedFollowing, new Date(31, January, 2015),
                new Period(1, TimeUnit.Months), false, new Date(27, February, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.ModifiedFollowing, new Date(25, March, 2015),
                new Period(1, TimeUnit.Months), false, new Date(28, April, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.ModifiedFollowing, new Date(7, February, 2015),
                new Period(1, TimeUnit.Months), false, new Date(9, March, 2015)),

            // Preceding
            new(new SouthAfrica(), BusinessDayConvention.Preceding, new Date(3, March, 2015),
                new Period(-1, TimeUnit.Months), false, new Date(3, February, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.Preceding, new Date(3, February, 2015),
                new Period(-2, TimeUnit.Days), false, new Date(30, January, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.Preceding, new Date(1, March, 2015),
                new Period(-1, TimeUnit.Months), true, new Date(30, January, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.Preceding, new Date(1, March, 2015),
                new Period(-1, TimeUnit.Months), false, new Date(30, January, 2015)),

            // ModifiedPreceding
            new(new SouthAfrica(), BusinessDayConvention.ModifiedPreceding, new Date(3, March, 2015),
                new Period(-1, TimeUnit.Months), false, new Date(3, February, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.ModifiedPreceding, new Date(3, February, 2015),
                new Period(-2, TimeUnit.Days), false, new Date(30, January, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.ModifiedPreceding, new Date(1, March, 2015),
                new Period(-1, TimeUnit.Months), true, new Date(2, February, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.ModifiedPreceding, new Date(1, March, 2015),
                new Period(-1, TimeUnit.Months), false, new Date(2, February, 2015)),

            // Unadjusted
            new(new SouthAfrica(), BusinessDayConvention.Unadjusted, new Date(3, February, 2015),
                new Period(1, TimeUnit.Months), false, new Date(3, March, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.Unadjusted, new Date(3, February, 2015),
                new Period(4, TimeUnit.Days), false, new Date(9, February, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.Unadjusted, new Date(31, January, 2015),
                new Period(1, TimeUnit.Months), true, new Date(28, February, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.Unadjusted, new Date(30, January, 2015),
                new Period(1, TimeUnit.Months), true, new Date(28, February, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.Unadjusted, new Date(27, February, 2015),
                new Period(1, TimeUnit.Months), true, new Date(27, March, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.Unadjusted, new Date(31, January, 2015),
                new Period(1, TimeUnit.Months), false, new Date(28, February, 2015)),

            // HalfMonthModifiedFollowing
            new(new SouthAfrica(), BusinessDayConvention.HalfMonthModifiedFollowing, new Date(3, February, 2015),
                new Period(1, TimeUnit.Months), false, new Date(3, March, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.HalfMonthModifiedFollowing, new Date(3, February, 2015),
                new Period(4, TimeUnit.Days), false, new Date(9, February, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.HalfMonthModifiedFollowing, new Date(31, January, 2015),
                new Period(1, TimeUnit.Months), true, new Date(27, February, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.HalfMonthModifiedFollowing, new Date(31, January, 2015),
                new Period(1, TimeUnit.Months), false, new Date(27, February, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.HalfMonthModifiedFollowing, new Date(3, January, 2015),
                new Period(1, TimeUnit.Weeks), false, new Date(12, January, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.HalfMonthModifiedFollowing, new Date(21, March, 2015),
                new Period(1, TimeUnit.Weeks), false, new Date(30, March, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.HalfMonthModifiedFollowing, new Date(7, February, 2015),
                new Period(1, TimeUnit.Months), false, new Date(9, March, 2015)),

            // Nearest
            new(new SouthAfrica(), BusinessDayConvention.Nearest, new Date(3, February, 2015),
                new Period(1, TimeUnit.Months), false, new Date(3, March, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.Nearest, new Date(3, February, 2015),
                new Period(4, TimeUnit.Days), false, new Date(9, February, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.Nearest, new Date(16, April, 2015),
                new Period(1, TimeUnit.Months), false, new Date(15, May, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.Nearest, new Date(17, April, 2015),
                new Period(1, TimeUnit.Months), false, new Date(18, May, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.Nearest, new Date(4, March, 2015),
                new Period(1, TimeUnit.Months), false, new Date(2, April, 2015)),
            new(new SouthAfrica(), BusinessDayConvention.Nearest, new Date(2, April, 2015),
                new Period(1, TimeUnit.Months), false, new Date(4, May, 2015)),
        ];

        for (int i = 0; i < testCases.Length; i++)
        {
            Calendar calendar = testCases[i].Calendar;
            Date result = calendar.Advance(
                testCases[i].Start,
                testCases[i].Period,
                testCases[i].Convention,
                testCases[i].EndOfMonth);

            Assert.True(
                result == testCases[i].Result,
                $"\ncase {i}:\n"
                + $"start date: {testCases[i].Start}\n"
                + $"calendar: {calendar.Name}\n"
                + $"period: {testCases[i].Period}, end of month: {testCases[i].EndOfMonth}\n"
                + $"convention: {testCases[i].Convention}\n"
                + $"expected: {testCases[i].Result} vs. actual: {result}");
        }
    }

    private sealed record SingleCase(
        Calendar Calendar,
        BusinessDayConvention Convention,
        Date Start,
        Period Period,
        bool EndOfMonth,
        Date Result);
}
