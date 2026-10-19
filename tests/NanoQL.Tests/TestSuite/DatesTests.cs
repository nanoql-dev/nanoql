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
// Partial: ecbIsECBcode, ecbDates, ecbGetDateFromCode, ecbGetCodeFromDate, ecbNextCode, isoDates, parseDates, intraday
// Replaced: nullDate (Divergences/NullDateTests.cs)

using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class DatesTests
{
    // The evaluation date QuantLib's test runner sets globally (playbook section 9.2): the IMM and ASX functions take
    // the context, and read it only for a null date, which these cases never pass.
    private static readonly EvaluationContext RunnerContext = new() { EvaluationDate = new Date(16, Month.September, 2015) };

    [Fact]
    public void ImmDates()
    {
        // Testing IMM dates...
        string[] immCodes =
        [
            "F0", "G0", "H0", "J0", "K0", "M0", "N0", "Q0", "U0", "V0", "X0", "Z0",
            "F1", "G1", "H1", "J1", "K1", "M1", "N1", "Q1", "U1", "V1", "X1", "Z1",
            "F2", "G2", "H2", "J2", "K2", "M2", "N2", "Q2", "U2", "V2", "X2", "Z2",
            "F3", "G3", "H3", "J3", "K3", "M3", "N3", "Q3", "U3", "V3", "X3", "Z3",
            "F4", "G4", "H4", "J4", "K4", "M4", "N4", "Q4", "U4", "V4", "X4", "Z4",
            "F5", "G5", "H5", "J5", "K5", "M5", "N5", "Q5", "U5", "V5", "X5", "Z5",
            "F6", "G6", "H6", "J6", "K6", "M6", "N6", "Q6", "U6", "V6", "X6", "Z6",
            "F7", "G7", "H7", "J7", "K7", "M7", "N7", "Q7", "U7", "V7", "X7", "Z7",
            "F8", "G8", "H8", "J8", "K8", "M8", "N8", "Q8", "U8", "V8", "X8", "Z8",
            "F9", "G9", "H9", "J9", "K9", "M9", "N9", "Q9", "U9", "V9", "X9", "Z9",
        ];

        var counter = new Date(1, Month.January, 2000);
        var last = new Date(1, Month.January, 2040);
        Date imm;

        while (counter <= last)
        {
            imm = IMM.NextDate(RunnerContext, counter, false);

            // check that imm is greater than counter
            if (imm <= counter)
            {
                Assert.Fail($"{imm.Weekday} {imm} is not greater than {counter.Weekday} {counter}");
            }

            // check that imm is an IMM date
            if (!IMM.IsIMMdate(imm, false))
            {
                Assert.Fail($"{imm.Weekday} {imm} is not an IMM date (calculated from {counter.Weekday} {counter})");
            }

            // check that imm is <= to the next IMM date in the main cycle
            if (imm > IMM.NextDate(RunnerContext, counter, true))
            {
                Assert.Fail(
                    $"{imm.Weekday} {imm} is not less than or equal to the next future in the main cycle "
                    + $"{IMM.NextDate(RunnerContext, counter, true)}");
            }

            // check that for every date IMMdate is the inverse of IMMcode
            if (IMM.Date(IMM.Code(imm), RunnerContext, counter) != imm)
            {
                Assert.Fail($"{IMM.Code(imm)} at calendar day {counter} is not the IMM code matching {imm}");
            }

            // check that for every date the 120 IMM codes refer to future dates
            for (int i = 0; i < 40; ++i)
            {
                if (IMM.Date(immCodes[i], RunnerContext, counter) < counter)
                {
                    Assert.Fail(
                        $"{IMM.Date(immCodes[i], RunnerContext, counter)} is wrong for {immCodes[i]} at reference date "
                        + $"{counter}");
                }
            }

            counter = counter + 1;
        }
    }

    [Fact]
    public void AsxDates()
    {
        // Testing ASX dates...
        string[] asxCodes =
        [
            "F0", "G0", "H0", "J0", "K0", "M0", "N0", "Q0", "U0", "V0", "X0", "Z0",
            "F1", "G1", "H1", "J1", "K1", "M1", "N1", "Q1", "U1", "V1", "X1", "Z1",
            "F2", "G2", "H2", "J2", "K2", "M2", "N2", "Q2", "U2", "V2", "X2", "Z2",
            "F3", "G3", "H3", "J3", "K3", "M3", "N3", "Q3", "U3", "V3", "X3", "Z3",
            "F4", "G4", "H4", "J4", "K4", "M4", "N4", "Q4", "U4", "V4", "X4", "Z4",
            "F5", "G5", "H5", "J5", "K5", "M5", "N5", "Q5", "U5", "V5", "X5", "Z5",
            "F6", "G6", "H6", "J6", "K6", "M6", "N6", "Q6", "U6", "V6", "X6", "Z6",
            "F7", "G7", "H7", "J7", "K7", "M7", "N7", "Q7", "U7", "V7", "X7", "Z7",
            "F8", "G8", "H8", "J8", "K8", "M8", "N8", "Q8", "U8", "V8", "X8", "Z8",
            "F9", "G9", "H9", "J9", "K9", "M9", "N9", "Q9", "U9", "V9", "X9", "Z9",
        ];

        for (var counter = new Date(1, Month.January, 2000); counter <= new Date(1, Month.January, 2040); ++counter)
        {
            Date asx = ASX.NextDate(RunnerContext, counter, false);

            // check that asx is greater than counter
            if (asx <= counter)
            {
                Assert.Fail($"{asx.Weekday} {asx} is not greater than {counter.Weekday} {counter}");
            }

            // check that asx is an ASX date
            if (!ASX.IsASXdate(asx, false))
            {
                Assert.Fail($"{asx.Weekday} {asx} is not an ASX date (calculated from {counter.Weekday} {counter})");
            }

            // check that asx is <= to the next ASX date in the main cycle
            if (asx > ASX.NextDate(RunnerContext, counter, true))
            {
                Assert.Fail(
                    $"{asx.Weekday} {asx} is not less than or equal to the next future in the main cycle "
                    + $"{ASX.NextDate(RunnerContext, counter, true)}");
            }

            // check that for every date ASXdate is the inverse of ASXcode
            if (ASX.Date(ASX.Code(asx), RunnerContext, counter) != asx)
            {
                Assert.Fail($"{ASX.Code(asx)} at calendar day {counter} is not the ASX code matching {asx}");
            }

            // check that for every date the 120 ASX codes refer to future dates
            foreach (string asxCode in asxCodes)
            {
                if (ASX.Date(asxCode, RunnerContext, counter) < counter)
                {
                    Assert.Fail(
                        $"{ASX.Date(asxCode, RunnerContext, counter)} is wrong for {asxCode} at reference date {counter}");
                }
            }
        }
    }

    [Fact]
    public void AsxDatesSpecific()
    {
        // Testing ASX functionality with specific dates...

        // isASXdate
        {
            // date is ASX date depending on mainCycle
            var date = new Date(12, Month.January, 2024);
            Assert.True(date.Weekday == Weekday.Friday);

            // check mainCycle
            Assert.True(ASX.IsASXdate(date, mainCycle: false));
            Assert.True(!ASX.IsASXdate(date, mainCycle: true));
        }

        // nextDate from code + ref date
        Assert.True(
            new Date(8, Month.February, 2002)
            == ASX.NextDate("F2", RunnerContext, mainCycle: false, new Date(1, Month.January, 2000)));

        Assert.True(
            new Date(9, Month.June, 2023)
            == ASX.NextDate("K3", RunnerContext, mainCycle: true, new Date(1, Month.January, 2014)));

        // nextCode
        Assert.True("F4" == ASX.NextCode(RunnerContext, new Date(1, Month.January, 2024), mainCycle: false));
        Assert.True("G4" == ASX.NextCode(RunnerContext, new Date(15, Month.January, 2024), mainCycle: false));
        Assert.True("H4" == ASX.NextCode(RunnerContext, new Date(15, Month.January, 2024), mainCycle: true));

        Assert.True("G4" == ASX.NextCode("F4", RunnerContext, mainCycle: false, new Date(1, Month.January, 2020)));
        Assert.True("H5" == ASX.NextCode("Z4", RunnerContext, mainCycle: true, new Date(1, Month.January, 2020)));
    }

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
