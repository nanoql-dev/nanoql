// SPDX-FileCopyrightText: Copyright (C) 2004, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2015 Maddalena Zanzi
// SPDX-FileCopyrightText: Copyright (c) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2020 Leonardo Arcari
// SPDX-FileCopyrightText: Copyright (C) 2020 Kline s.r.l.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/dates.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: ecbIsECBcode, ecbDates, ecbGetDateFromCode, ecbGetCodeFromDate, ecbNextCode, immDates, asxDates, asxDatesSpecific, isoDates, parseDates, intraday, nullDate

using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class DatesTests
{
    [Fact]
    public void TestConsistency()
    {
        // Testing dates...
        int minDate = Date.MinDate.SerialNumber + 1,
            maxDate = Date.MaxDate.SerialNumber;

        int dyold = new Date(minDate - 1).DayOfYear,
            dold = new Date(minDate - 1).DayOfMonth,
            mold = (int)new Date(minDate - 1).Month,
            yold = new Date(minDate - 1).Year,
            wdold = (int)new Date(minDate - 1).Weekday;

        for (int i = minDate; i <= maxDate; i++)
        {
            var t = new Date(i);
            int serial = t.SerialNumber;

            // check serial number consistency
            if (serial != i)
            {
                Assert.Fail($"inconsistent serial number:\n    original:      {i}\n    date:          {t}\n    serial number: {serial}");
            }

            Integer dy = t.DayOfYear,
                    d = t.DayOfMonth,
                    m = (int)t.Month,
                    y = t.Year,
                    wd = (int)t.Weekday;

            // check if skipping any date
            if (!((dy == dyold + 1) ||
                  (dy == 1 && dyold == 365 && !Date.IsLeap(yold)) ||
                  (dy == 1 && dyold == 366 && Date.IsLeap(yold))))
            {
                Assert.Fail($"wrong day of year increment: \n    date: {t}\n    day of year: {dy}\n    previous:    {dyold}");
            }

            dyold = dy;

            if (!((d == dold + 1 && m == mold && y == yold) ||
                  (d == 1 && m == mold + 1 && y == yold) ||
                  (d == 1 && m == 1 && y == yold + 1)))
            {
                Assert.Fail($"wrong day,month,year increment: \n    date: {t}\n    day,month,year: {d},{m},{y}\n    previous:       {dold},{mold},{yold}");
            }

            dold = d;
            mold = m;
            yold = y;

            // check month definition
            if (m < 1 || m > 12)
            {
                Assert.Fail($"invalid month: \n    date:  {t}\n    month: {m}");
            }

            // check day definition
            if (d < 1)
            {
                Assert.Fail($"invalid day of month: \n    date:  {t}\n    day: {d}");
            }

            if (!((m == 1 && d <= 31) ||
                  (m == 2 && d <= 28) ||
                  (m == 2 && d == 29 && Date.IsLeap(y)) ||
                  (m == 3 && d <= 31) ||
                  (m == 4 && d <= 30) ||
                  (m == 5 && d <= 31) ||
                  (m == 6 && d <= 30) ||
                  (m == 7 && d <= 31) ||
                  (m == 8 && d <= 31) ||
                  (m == 9 && d <= 30) ||
                  (m == 10 && d <= 31) ||
                  (m == 11 && d <= 30) ||
                  (m == 12 && d <= 31)))
            {
                Assert.Fail($"invalid day of month: \n    date:  {t}\n    day: {d}");
            }

            // check weekday definition
            if (!((wd == wdold + 1) || (wd == 1 && wdold == 7)))
            {
                Assert.Fail($"invalid weekday: \n    date:  {t}\n    weekday:  {wd}\n    previous: {wdold}");
            }

            wdold = wd;

            // create the same date with a different constructor
            var s = new Date(d, (Month)m, y);

            // check serial number consistency
            serial = s.SerialNumber;
            if (serial != i)
            {
                Assert.Fail($"inconsistent serial number:\n    date:          {t}\n    serial number: {i}\n    cloned date:   {s}\n    serial number: {serial}");
            }
        }
    }

    [Fact]
    public void CanHash()
    {
        // Testing hashing of dates...
        var startDate = new Date(1, Month.Jan, 2020);
        int nbTests = 500;

        // Check hash values
        for (int i = 0; i < nbTests; ++i)
        {
            for (int j = 0; j < nbTests; ++j)
            {
                Date lhs = startDate + i;
                Date rhs = startDate + j;

                if (lhs == rhs && lhs.GetHashCode() != rhs.GetHashCode())
                {
                    Assert.Fail($"Equal dates are expected to have same hash value\nrhs = {lhs}\nlhs = {rhs}\nhash(lhs) = {lhs.GetHashCode()}\nhash(rhs) = {rhs.GetHashCode()}\n");
                }

                if (lhs != rhs && lhs.GetHashCode() == rhs.GetHashCode())
                {
                    Assert.Fail($"Different dates are expected to have different hash value\nrhs = {lhs}\nlhs = {rhs}\nhash(lhs) = {lhs.GetHashCode()}\nhash(rhs) = {rhs.GetHashCode()}\n");
                }
            }
        }

        // Check if Date can be used as a hash-set key
        var set = new HashSet<Date> { startDate };

        if (!set.Contains(startDate))
        {
            Assert.Fail($"Expected to find date {startDate} in the hash set\n");
        }
    }

    [Fact]
    public void TodaysDate()
    {
        // Testing today's date...
        static Date LocalDay()
        {
            DateTime d = DateTime.Today;
            return new Date(d.Day, (Month)d.Month, d.Year);
        }

        Date today = Date.TodaysDate();

        Assert.NotEqual(0, today.SerialNumber);

        // cross-check against an independent implementation
        Date expected = LocalDay();
        if (today != expected)
        {
            // the two calls straddled midnight; it has passed now, so retry
            today = Date.TodaysDate();
            expected = LocalDay();
        }

        Assert.True(today == expected, $"Date.TodaysDate() returned {today}, expected {expected}");
    }
}
