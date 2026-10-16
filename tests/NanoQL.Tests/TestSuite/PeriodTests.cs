// SPDX-FileCopyrightText: Copyright (C) 2007, 2014 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/period.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;
using static NanoQL.Time.PeriodFunctions;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class PeriodTests
{
    [Fact]
    public void TestYearsMonthsAlgebra()
    {
        // Testing period algebra on years/months...
        var oneYear = new Period(1, TimeUnit.Years);
        var sixMonths = new Period(6, TimeUnit.Months);
        var threeMonths = new Period(3, TimeUnit.Months);

        Integer n = 4;
        Assert.True(oneYear / n == threeMonths, $"division error: {oneYear}/{n} not equal to {threeMonths}");
        n = 2;
        Assert.True(oneYear / n == sixMonths, $"division error: {oneYear}/{n} not equal to {sixMonths}");

        Period sum = threeMonths;
        sum += sixMonths;
        Assert.True(sum == new Period(9, TimeUnit.Months), $"sum error: {threeMonths} + {sixMonths} != {new Period(9, TimeUnit.Months)}");

        sum += oneYear;
        Assert.True(sum == new Period(21, TimeUnit.Months), $"sum error: {threeMonths} + {sixMonths} + {oneYear} != {new Period(21, TimeUnit.Months)}");

        var twelveMonths = new Period(12, TimeUnit.Months);
        Assert.True(twelveMonths.Length == 12, $"normalization error: TwelveMonths.length() is {twelveMonths.Length} instead of 12");
        Assert.True(twelveMonths.Units == TimeUnit.Months, $"normalization error: TwelveMonths.units() is {twelveMonths.Units} instead of {TimeUnit.Months}");

        // QuantLib's normalize() mutates; nanoql's Normalized() returns the normal form.
        var normalizedTwelveMonths = new Period(12, TimeUnit.Months);
        normalizedTwelveMonths = normalizedTwelveMonths.Normalized();
        Assert.True(normalizedTwelveMonths.Length == 1, $"normalization error: NormalizedTwelveMonths.length() is {normalizedTwelveMonths.Length} instead of 1");
        Assert.True(normalizedTwelveMonths.Units == TimeUnit.Years, $"normalization error: NormalizedTwelveMonths.units() is {normalizedTwelveMonths.Units} instead of {TimeUnit.Years}");
    }

    [Fact]
    public void TestWeeksDaysAlgebra()
    {
        // Testing period algebra on weeks/days...
        var twoWeeks = new Period(2, TimeUnit.Weeks);
        var oneWeek = new Period(1, TimeUnit.Weeks);
        var threeDays = new Period(3, TimeUnit.Days);
        var oneDay = new Period(1, TimeUnit.Days);
        var zeroDays = new Period(0, TimeUnit.Days);

        Integer n = 2;
        Assert.True(twoWeeks / n == oneWeek, $"division error: {twoWeeks}/{n} not equal to {oneWeek}");
        n = 7;
        Assert.True(oneWeek / n == oneDay, $"division error: {oneWeek}/{n} not equal to {oneDay}");

        Period sum = threeDays;
        sum += oneDay;
        Assert.True(sum == new Period(4, TimeUnit.Days), $"sum error: {threeDays} + {oneDay} != {new Period(4, TimeUnit.Days)}");

        sum += oneWeek;
        Assert.True(sum == new Period(11, TimeUnit.Days), $"sum error: {threeDays} + {oneDay} + {oneWeek} != {new Period(11, TimeUnit.Days)}");

        Assert.True((oneWeek + zeroDays) == oneWeek);
        Assert.True((oneWeek + (3 * oneDay)) == new Period(10, TimeUnit.Days));
        Assert.True((oneWeek + (7 * oneDay)) == twoWeeks);

        var sevenDays = new Period(7, TimeUnit.Days);
        Assert.True(sevenDays.Length == 7, $"normalization error: SevenDays.length() is {sevenDays.Length} instead of 7");
        Assert.True(sevenDays.Units == TimeUnit.Days, $"normalization error: SevenDays.units() is {sevenDays.Units} instead of {TimeUnit.Days}");
    }

    [Fact]
    public void TestOperators()
    {
        // Testing period operators...
        var p = new Period(3, TimeUnit.Days);
        p *= 2;
        Assert.True(p == new Period(6, TimeUnit.Days));

        p -= new Period(2, TimeUnit.Days);
        Assert.True(p == new Period(4, TimeUnit.Days));
    }

    [Fact]
    public void TestConvertToYears()
    {
        // Testing conversion of periods to years...
        Assert.True(Years(new Period(0, TimeUnit.Years)) == 0);
        Assert.True(Years(new Period(1, TimeUnit.Years)) == 1);
        Assert.True(Years(new Period(5, TimeUnit.Years)) == 5);

        const Real tol = 1e-15;
        Close(1.0 / 12.0, Years(new Period(1, TimeUnit.Months)), tol);
        Close(8.0 / 12.0, Years(new Period(8, TimeUnit.Months)), tol);
        Assert.True(Years(new Period(12, TimeUnit.Months)) == 1);
        Close(1.5, Years(new Period(18, TimeUnit.Months)), tol);
    }

    [Fact]
    public void TestConvertToMonths()
    {
        // Testing conversion of periods to months...
        Assert.True(Months(new Period(0, TimeUnit.Months)) == 0);
        Assert.True(Months(new Period(1, TimeUnit.Months)) == 1);
        Assert.True(Months(new Period(5, TimeUnit.Months)) == 5);
        Assert.True(Months(new Period(1, TimeUnit.Years)) == 12);
        Assert.True(Months(new Period(3, TimeUnit.Years)) == 36);
    }

    [Fact]
    public void TestConvertToWeeks()
    {
        // Testing conversion of periods to weeks...
        Assert.True(Weeks(new Period(0, TimeUnit.Weeks)) == 0);
        Assert.True(Weeks(new Period(1, TimeUnit.Weeks)) == 1);
        Assert.True(Weeks(new Period(5, TimeUnit.Weeks)) == 5);

        const Real tol = 1e-15;
        Close(1.0 / 7.0, Weeks(new Period(1, TimeUnit.Days)), tol);
        Close(3.0 / 7.0, Weeks(new Period(3, TimeUnit.Days)), tol);
        Close(11.0 / 7.0, Weeks(new Period(11, TimeUnit.Days)), tol);
    }

    [Fact]
    public void TestNormalization()
    {
        // Testing period normalization...
        Period[] testValues =
        [
            0 * TimeUnit.Days,
            0 * TimeUnit.Weeks,
            0 * TimeUnit.Months,
            0 * TimeUnit.Years,
            3 * TimeUnit.Days,
            7 * TimeUnit.Days,
            14 * TimeUnit.Days,
            30 * TimeUnit.Days,
            60 * TimeUnit.Days,
            365 * TimeUnit.Days,
            1 * TimeUnit.Weeks,
            2 * TimeUnit.Weeks,
            4 * TimeUnit.Weeks,
            8 * TimeUnit.Weeks,
            52 * TimeUnit.Weeks,
            1 * TimeUnit.Months,
            2 * TimeUnit.Months,
            6 * TimeUnit.Months,
            12 * TimeUnit.Months,
            18 * TimeUnit.Months,
            24 * TimeUnit.Months,
            1 * TimeUnit.Years,
            2 * TimeUnit.Years,
        ];

        foreach (Period p1 in testValues)
        {
            Period n1 = p1.Normalized();
            Assert.True(n1 == p1, $"Normalizing {p1} yields {n1}, which compares different");

            foreach (Period p2 in testValues)
            {
                Period n2 = p2.Normalized();

                // QuantLib's == throws where nanoql's returns false; either way the pair isn't asserted equal.
                bool comparison = p1 == p2;
                if (comparison)
                {
                    // periods which compare equal must normalize to exactly the same period
                    Assert.True(
                        n1.Units == n2.Units && n1.Length == n2.Length,
                        $"{p1} and {p2} compare equal, but normalize to {n1} and {n2} respectively");
                }

                if (n1.Units == n2.Units && n1.Length == n2.Length)
                {
                    // periods normalizing to exactly the same period must compare equal
                    Assert.False(p1 != p2, $"{p1} and {p2} compare different, but normalize to {n1} and {n2} respectively");
                }
            }
        }
    }

    [Fact]
    public void TestNegativePeriodComparison()
    {
        // Testing comparison of negative periods...
        Period[] testValues =
        [
            1 * TimeUnit.Days, 27 * TimeUnit.Days, 28 * TimeUnit.Days, 30 * TimeUnit.Days, 31 * TimeUnit.Days,
            32 * TimeUnit.Days, 365 * TimeUnit.Days, 366 * TimeUnit.Days, 367 * TimeUnit.Days, 1 * TimeUnit.Weeks,
            4 * TimeUnit.Weeks, 5 * TimeUnit.Weeks, 52 * TimeUnit.Weeks, 53 * TimeUnit.Weeks, 1 * TimeUnit.Months,
            2 * TimeUnit.Months, 12 * TimeUnit.Months, 1 * TimeUnit.Years, 2 * TimeUnit.Years,
        ];

        // negating both periods must reverse their ordering, and must keep undecidable comparisons undecidable
        foreach (Period p1 in testValues)
        {
            foreach (Period p2 in testValues)
            {
                bool? positive = null, negative = null;
                try
                {
                    positive = p1 < p2;
                }
                catch (NanoQLException)
                {
                }

                try
                {
                    negative = -p2 < -p1;
                }
                catch (NanoQLException)
                {
                }

                Assert.True(
                    positive == negative,
                    $"comparing {p1} < {p2} gives {Show(positive)} but comparing {-p2} < {-p1} gives {Show(negative)}");
            }
        }
    }

    [Fact]
    public void TestFrequencyComputation()
    {
        // Testing computation of frequency from period...

        // frequency -> period -> frequency == initial frequency?
        foreach (Frequency f in new[]
        {
            Frequency.NoFrequency, Frequency.Once, Frequency.Annual, Frequency.Semiannual, Frequency.EveryFourthMonth,
            Frequency.Quarterly, Frequency.Bimonthly, Frequency.Monthly, Frequency.EveryFourthWeek, Frequency.Biweekly,
            Frequency.Weekly, Frequency.Daily,
        })
        {
            Assert.True(new Period(f).Frequency() == f);
        }

        Assert.Throws<NanoQLException>(() => new Period(Frequency.OtherFrequency).Frequency());

        // test Period(count, timeUnit).frequency()
        Assert.True(new Period(1, TimeUnit.Years).Frequency() == Frequency.Annual);
        Assert.True(new Period(6, TimeUnit.Months).Frequency() == Frequency.Semiannual);
        Assert.True(new Period(4, TimeUnit.Months).Frequency() == Frequency.EveryFourthMonth);
        Assert.True(new Period(3, TimeUnit.Months).Frequency() == Frequency.Quarterly);
        Assert.True(new Period(2, TimeUnit.Months).Frequency() == Frequency.Bimonthly);
        Assert.True(new Period(1, TimeUnit.Months).Frequency() == Frequency.Monthly);
        Assert.True(new Period(4, TimeUnit.Weeks).Frequency() == Frequency.EveryFourthWeek);
        Assert.True(new Period(2, TimeUnit.Weeks).Frequency() == Frequency.Biweekly);
        Assert.True(new Period(1, TimeUnit.Weeks).Frequency() == Frequency.Weekly);
        Assert.True(new Period(1, TimeUnit.Days).Frequency() == Frequency.Daily);
    }

    // BOOST_TEST's tolerance check: the relative difference against each operand within the tolerance.
    private static void Close(Real expected, Real actual, Real tolerance)
    {
        Real diff = Math.Abs(actual - expected);
        Assert.True(
            diff <= tolerance * Math.Abs(expected) && diff <= tolerance * Math.Abs(actual),
            $"{actual:R} != {expected:R} within relative tolerance {tolerance}");
    }

    private static string Show(bool? comparison) => comparison is bool b ? (b ? "true" : "false") : "undecidable";
}
