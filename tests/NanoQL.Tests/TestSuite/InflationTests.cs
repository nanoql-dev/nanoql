// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2008 Piero Del Boca
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/inflation.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testZeroIndex's UK HICP and quarterly AU CPI checks, which wait for batch 2's UKHICP and AUCPI; testZeroTermStructure, testZeroTermStructureLazyBaseDate, testYYTermStructure, testZeroBpsYoYInflationSwapFairRateAndSpread, testExpiredYoYInflationSwapFairRateAndSpread, testNotifications, testExtrapolationRegression, testUsCpiLinearBootstrapAtMonthStart, testEuHicpFlatBootstrapAtMonthStart, testUkRpiFlatBootstrapAtMonthStart, testUsCpiLinearGlobalBootstrapAtMonthStart and testPillarCollisionWithDifferentMonthLengths, which wait for the inflation coupons, swaps and bootstrap
// Not ported: testCpiAsIndexInterpolation (CPI::AsIndex, deprecated in QuantLib 1.43, D5)

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.TermStructures.InflationTermStructureFunctions;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

/// <summary>QuantLib's inflation tests, <c>test-suite/inflation.cpp</c>: indexes, fixings, periods and curves.</summary>
[Trait("Oracle", "QuantLib")]
public class InflationTests
{
    private static readonly Period Months1 = new(1, TimeUnit.Months);

    // Adapted: the UK HICP check (lines 248-257) and the quarterly AU CPI check (lines 309-317) wait for batch 2's
    // UKHICP and AUCPI; the fixings go into the case's evaluation context.
    [Fact]
    public void TestZeroIndex()
    {
        // Testing zero inflation indices...
        var euhicp = new EUHICP();
        if (euhicp.Name != "EU HICP"
            || euhicp.Frequency != Frequency.Monthly
            || euhicp.Revised
            || !(euhicp.AvailabilityLag == Months1))
        {
            Assert.Fail(
                $"wrong EU HICP data ({euhicp.Name}, {euhicp.Frequency}, {euhicp.Revised}, {euhicp.AvailabilityLag})");
        }

        var ukrpi = new UKRPI();
        if (ukrpi.Name != "UK RPI"
            || ukrpi.Frequency != Frequency.Monthly
            || ukrpi.Revised
            || !(ukrpi.AvailabilityLag == Months1))
        {
            Assert.Fail($"wrong UK RPI data ({ukrpi.Name}, {ukrpi.Frequency}, {ukrpi.Revised}, {ukrpi.AvailabilityLag})");
        }

        // Retrieval test.
        //----------------
        // make sure of the evaluation date
        Date evaluationDate = new UnitedKingdom().Adjust(new Date(13, August, 2007));
        var context = new EvaluationContext { EvaluationDate = evaluationDate };

        // fixing data
        var from = new Date(1, January, 2005);
        var to = new Date(1, August, 2007);
        Schedule rpiSchedule = new MakeSchedule().From(from).To(to).WithFrequency(Frequency.Monthly).ToSchedule();

        double[] fixData =
        [
            189.9, 189.9, 189.6, 190.5, 191.6, 192.0,
            192.2, 192.2, 192.6, 193.1, 193.3, 193.6,
            194.1, 193.4, 194.2, 195.0, 196.5, 197.7,
            198.5, 198.5, 199.2, 200.1, 200.4, 201.1,
            202.7, 201.6, 203.1, 204.4, 205.4, 206.2,
            207.3, 206.1,
        ];

        var iir = new UKRPI();
        for (int i = 0; i < fixData.Length; i++)
        {
            context = AddFixing(context, iir, rpiSchedule[i], fixData[i]);
        }

        if (iir.LastFixingDate(context) != to)
        {
            Assert.Fail(
                $"Wrong last fixing date for UK RPI\n    calculated: {iir.LastFixingDate(context)}\n    expected:   {to}");
        }

        Date todayMinusLag = evaluationDate - iir.AvailabilityLag;
        todayMinusLag = InflationPeriod(todayMinusLag, iir.Frequency).Start;

        double eps = 1.0e-8;

        // -1 because last value not yet available,
        // (no TS so can't forecast).
        for (int i = 0; i < rpiSchedule.Size - 1; i++)
        {
            (Date start, Date end) = InflationPeriod(rpiSchedule[i], iir.Frequency);
            for (Date d = start; d <= end; d++)
            {
                if (d < InflationPeriod(todayMinusLag, iir.Frequency).Start)
                {
                    if (Math.Abs(iir.Fixing(d, context) - fixData[i]) > eps)
                    {
                        Assert.Fail($"Fixings not constant within a period: {iir.Fixing(d, context)}, should be {fixData[i]}");
                    }
                }
            }
        }
    }

    // Adapted: QuantLib's setSeasonality on the curve the index holds becomes a copy of the curve with the
    // seasonality, which a clone of the index forecasts through (checkSeasonality, lines 135-201).
    [Fact]
    public void TestSeasonalityCorrection()
    {
        // Testing seasonality correction on zero inflation term structure...

        // try the Zero UK
        Calendar calendar = new UnitedKingdom();
        Date evaluationDate = calendar.Adjust(new Date(13, August, 2007));
        var context = new EvaluationContext { EvaluationDate = evaluationDate };

        // fixing data
        var from = new Date(1, January, 2005);
        var to = new Date(13, August, 2007);
        Schedule rpiSchedule = new MakeSchedule()
            .From(from)
            .To(to)
            .WithTenor(Months1)
            .WithCalendar(new UnitedKingdom())
            .WithConvention(BusinessDayConvention.ModifiedFollowing)
            .ToSchedule();

        double[] fixData =
        [
            189.9, 189.9, 189.6, 190.5, 191.6, 192.0,
            192.2, 192.2, 192.6, 193.1, 193.3, 193.6,
            194.1, 193.4, 194.2, 195.0, 196.5, 197.7,
            198.5, 198.5, 199.2, 200.1, 200.4, 201.1,
            202.7, 201.6, 203.1, 204.4, 205.4, 206.2,
            207.3,
        ];

        var ii = new UKRPI();
        for (int i = 0; i < fixData.Length; i++)
        {
            context = AddFixing(context, ii, rpiSchedule[i], fixData[i]);
        }

        Date[] nodes =
        [
            new(1, June, 2007), new(1, June, 2008), new(1, June, 2009), new(1, June, 2010), new(1, June, 2011),
            new(1, June, 2012), new(1, June, 2014), new(1, June, 2017), new(1, June, 2019), new(1, June, 2022),
            new(1, June, 2027), new(1, June, 2032), new(1, June, 2037), new(1, June, 2047), new(1, June, 2057),
        ];

        double[] rates =
        [
            0.0293, 0.0293, 0.0295, 0.02965, 0.0298, 0.03, 0.0306, 0.03175, 0.03243, 0.03293, 0.03338, 0.03348,
            0.03348, 0.03308, 0.03228,
        ];

        DayCounter dc = new Thirty360(Thirty360Convention.BondBasis);
        Frequency frequency = Frequency.Monthly;

        var zeroCurve = new InterpolatedZeroInflationCurve<Linear>(evaluationDate, nodes, rates, frequency, dc);
        ZeroInflationIndex index = new UKRPI(zeroCurve);

        CheckSeasonality(zeroCurve, index, context);
    }

    // Adapted: the fixings go into the case's evaluation context.
    [Fact]
    public void TestZeroIndexFutureFixing()
    {
        // Testing that zero inflation indices forecast future fixings...

        // we create an index without a term structure, so
        // it won't be able to forecast fixings
        var euhicp = new EUHICP();

        // let's say we're at some point in April 2024...
        var context = new EvaluationContext { EvaluationDate = new Date(10, April, 2024) };

        // ..and the last available fixing is February 2024, we don't have March yet
        context = AddFixing(context, euhicp, new Date(1, December, 2023), 100.0);
        context = AddFixing(context, euhicp, new Date(1, January, 2024), 100.1);
        context = AddFixing(context, euhicp, new Date(1, February, 2024), 100.2);

        // Asking for the February fixing works, it's stored
        double fixing = euhicp.Fixing(new Date(1, February, 2024), context);
        double expected = 100.2;
        Assert.True(
            Math.Abs(fixing - expected) <= 1e-12,
            $"Failed to retrieve correct fixing: \n    returned: {fixing}\n    expected: {expected}");

        // Asking for the March fixing doesn't (because we can't forecast)
        CheckEmptyHandle(() => euhicp.Fixing(new Date(1, March, 2024), context));

        // but it works once it gets published:
        context = AddFixing(context, euhicp, new Date(1, March, 2024), 100.3);
        fixing = euhicp.Fixing(new Date(1, March, 2024), context);
        expected = 100.3;
        Assert.True(
            Math.Abs(fixing - expected) <= 1e-12,
            $"Failed to retrieve correct fixing: \n    returned: {fixing}\n    expected: {expected}");

        // On the other hand, April would be forecast...
        CheckEmptyHandle(() => euhicp.Fixing(new Date(1, April, 2024), context));

        // ...even if it's stored:
        context = AddFixing(context, euhicp, new Date(1, April, 2024), 100.4);
        CheckEmptyHandle(() => euhicp.Fixing(new Date(1, April, 2024), context));
    }

    [Fact]
    public void TestInterpolatedZeroTermStructure()
    {
        // Testing interpolated zero-rate inflation curve...
        var today = new Date(27, January, 2022);
        var baseDate = new Date(1, December, 2021);

        Date[] dates =
        [
            baseDate,
            today + new Period(7, TimeUnit.Days),
            today + new Period(14, TimeUnit.Days),
            today + new Period(1, TimeUnit.Months),
            today + new Period(2, TimeUnit.Months),
            today + new Period(3, TimeUnit.Months),
            today + new Period(6, TimeUnit.Months),
            today + new Period(1, TimeUnit.Years),
            today + new Period(2, TimeUnit.Years),
            today + new Period(5, TimeUnit.Years),
            today + new Period(10, TimeUnit.Years),
        ];
        double[] rates = [0.01, 0.01, 0.011, 0.012, 0.013, 0.015, 0.018, 0.02, 0.025, 0.03, 0.03];

        var curve = new InterpolatedZeroInflationCurve<Linear>(today, dates, rates, Frequency.Monthly, new Actual360());

        var nodes = curve.Nodes();

        Assert.True(nodes.Length == dates.Length, "different number of nodes and input dates");

        for (int i = 0; i < dates.Length; ++i)
        {
            Assert.True(dates[i] == nodes[i].Key, $"node {i} at {nodes[i].Key}; {dates[i]} expected");
        }
    }

    // Adapted: QuantLib's deprecated interpolated() check (line 769) isn't ported (D5): the index never interpolates.
    [Fact]
    public void TestQuotedYYIndex()
    {
        // Testing quoted year-on-year inflation indices...
        var yyukrpi = new YYUKRPI();
        if (yyukrpi.Name != "UK YY_RPI"
            || yyukrpi.Frequency != Frequency.Monthly
            || yyukrpi.Revised
            || yyukrpi.Ratio
            || !(yyukrpi.AvailabilityLag == Months1))
        {
            Assert.Fail(
                $"wrong year-on-year UK RPI data ({yyukrpi.Name}, {yyukrpi.Frequency}, {yyukrpi.Revised}, "
                + $"{yyukrpi.Ratio}, {yyukrpi.AvailabilityLag})");
        }
    }

    // Adapted: the fixings go into the case's evaluation context.
    [Fact]
    public void TestQuotedYYIndexFutureFixing()
    {
        // Testing that quoted year-on-year inflation indices forecast future fixings...

        // we create indexes without a term structure, so
        // they won't be able to forecast fixings
        var quoted = new YYEUHICP();

        // let's say we're at some point in April 2024...
        var context = new EvaluationContext { EvaluationDate = new Date(10, April, 2024) };

        // ..and the last available fixing is February 2024, we don't have March yet
        context = AddFixing(context, quoted, new Date(1, December, 2023), 100.0);
        context = AddFixing(context, quoted, new Date(1, January, 2024), 100.1);
        context = AddFixing(context, quoted, new Date(1, February, 2024), 100.2);

        Assert.Equal(new Date(1, February, 2024), quoted.LastFixingDate(context));

        // mid-January fixing: ok
        quoted.Fixing(new Date(15, January, 2024), context);

        // mid-February fixing: ok too
        quoted.Fixing(new Date(15, February, 2024), context);

        // still ok after March is published:
        context = AddFixing(context, quoted, new Date(1, March, 2024), 100.3);

        Assert.Equal(new Date(1, March, 2024), quoted.LastFixingDate(context));
        quoted.Fixing(new Date(15, February, 2024), context);

        // April can't be available now, fail even if it's stored:
        context = AddFixing(context, quoted, new Date(1, April, 2024), 100.4);
        CheckEmptyHandle(() => quoted.Fixing(new Date(1, April, 2024), context));
    }

    // Adapted: QuantLib's deprecated interpolated() check (line 826) isn't ported (D5); the fixings go into the case's
    // evaluation context.
    [Fact]
    public void TestRatioYYIndex()
    {
        // Testing ratio year-on-year inflation indices...
        var ukrpi = new UKRPI();

        var yyukrpir = new YoYInflationIndex(ukrpi);
        if (yyukrpir.Name != "UK YYR_RPI"
            || yyukrpir.Frequency != Frequency.Monthly
            || yyukrpir.Revised
            || !yyukrpir.Ratio
            || !(yyukrpir.AvailabilityLag == Months1))
        {
            Assert.Fail(
                $"wrong year-on-year UK RPIr data ({yyukrpir.Name}, {yyukrpir.Frequency}, {yyukrpir.Revised}, "
                + $"{yyukrpir.Ratio}, {yyukrpir.AvailabilityLag})");
        }

        // Retrieval test.
        //----------------
        // make sure of the evaluation date
        Date evaluationDate = new UnitedKingdom().Adjust(new Date(13, August, 2007));
        var context = new EvaluationContext { EvaluationDate = evaluationDate };

        // fixing data
        var from = new Date(1, January, 2005);
        var to = new Date(13, August, 2007);
        Schedule rpiSchedule = new MakeSchedule()
            .From(from)
            .To(to)
            .WithTenor(Months1)
            .WithCalendar(new UnitedKingdom())
            .WithConvention(BusinessDayConvention.ModifiedFollowing)
            .ToSchedule();

        double[] fixData =
        [
            189.9, 189.9, 189.6, 190.5, 191.6, 192.0,
            192.2, 192.2, 192.6, 193.1, 193.3, 193.6,
            194.1, 193.4, 194.2, 195.0, 196.5, 197.7,
            198.5, 198.5, 199.2, 200.1, 200.4, 201.1,
            202.7, 201.6, 203.1, 204.4, 205.4, 206.2,
            207.3,
        ];

        for (int i = 0; i < fixData.Length; i++)
        {
            context = AddFixing(context, ukrpi, rpiSchedule[i], fixData[i]);
        }

        var iir = new YoYInflationIndex(ukrpi);

        Date todayMinusLag = evaluationDate - iir.AvailabilityLag;
        (_, Date limEnd) = InflationPeriod(todayMinusLag, iir.Frequency);
        todayMinusLag = limEnd + 1 - (2 * new Period(iir.Frequency));

        double eps = 1.0e-8;

        // Interpolation tests
        //--------------------
        // (no TS so can't forecast).
        for (int i = 13; i < rpiSchedule.Size; i++)
        {
            (Date start, Date end) = InflationPeriod(rpiSchedule[i], iir.Frequency);
            for (Date d = start; d <= end; d++)
            {
                if (d < todayMinusLag)
                {
                    double expected = (fixData[i] / fixData[i - 12]) - 1.0;
                    double calculated = iir.Fixing(d, context);
                    Assert.True(
                        Math.Abs(calculated - expected) < eps,
                        $"Non-interpolated fixings not constant within a period: {calculated}, should be {expected}");
                }
            }
        }
    }

    // Adapted: the fixings go into the case's evaluation context.
    [Fact]
    public void TestRatioYYIndexFutureFixing()
    {
        // Testing that ratio year-on-year inflation indices forecast future fixings...

        // we create indexes without a term structure, so
        // they won't be able to forecast fixings
        var euhicp = new EUHICP();
        var ratio = new YoYInflationIndex(euhicp);

        // let's say we're at some point in April 2024...
        var context = new EvaluationContext { EvaluationDate = new Date(10, April, 2024) };

        // ..and the last available fixing is February 2024, we don't have March yet
        context = AddFixing(context, euhicp, new Date(1, December, 2022), 98.0);
        context = AddFixing(context, euhicp, new Date(1, January, 2023), 98.1);
        context = AddFixing(context, euhicp, new Date(1, February, 2023), 98.2);
        context = AddFixing(context, euhicp, new Date(1, March, 2023), 98.3);

        // ...
        context = AddFixing(context, euhicp, new Date(1, December, 2023), 100.0);
        context = AddFixing(context, euhicp, new Date(1, January, 2024), 100.1);
        context = AddFixing(context, euhicp, new Date(1, February, 2024), 100.2);

        Assert.Equal(new Date(1, February, 2024), ratio.LastFixingDate(context));

        // mid-January fixing: ok
        ratio.Fixing(new Date(15, January, 2024), context);

        // mid-February fixing: also ok
        ratio.Fixing(new Date(15, February, 2024), context);

        // ok after March is published:
        context = AddFixing(context, euhicp, new Date(1, March, 2024), 100.3);

        Assert.Equal(new Date(1, March, 2024), ratio.LastFixingDate(context));
        ratio.Fixing(new Date(15, February, 2024), context);

        // April can't be available now, fail even if it's stored:
        context = AddFixing(context, euhicp, new Date(1, April, 2024), 100.4);
        CheckEmptyHandle(() => ratio.Fixing(new Date(1, April, 2024), context));
    }

    [Fact]
    public void TestPeriod()
    {
        // Testing inflation period...
        int[] days = [0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];

        for (int year = 1950; year < 2051; ++year)
        {
            days[2] = Date.IsLeap(year) ? 29 : 28;

            for (int i = 1; i <= 12; ++i)
            {
                var d = new Date(1, (Month)i, year);

                (Date First, Date Second) res = InflationPeriod(d, Frequency.Monthly);
                if (res.First != new Date(1, (Month)i, year) || res.Second != new Date(days[i], (Month)i, year))
                {
                    ReportFailure(d, res, "Monthly");
                }

                res = InflationPeriod(d, Frequency.Quarterly);
                if (i is 1 or 2 or 3
                    && (res.First != new Date(1, (Month)1, year) || res.Second != new Date(31, (Month)3, year)))
                {
                    ReportFailure(d, res, "Quarterly");
                }
                else if (i is 4 or 5 or 6
                    && (res.First != new Date(1, (Month)4, year) || res.Second != new Date(30, (Month)6, year)))
                {
                    ReportFailure(d, res, "Quarterly");
                }
                else if (i is 7 or 8 or 9
                    && (res.First != new Date(1, (Month)7, year) || res.Second != new Date(30, (Month)9, year)))
                {
                    ReportFailure(d, res, "Quarterly");
                }
                else if (i is 10 or 11 or 12
                    && (res.First != new Date(1, (Month)10, year) || res.Second != new Date(31, (Month)12, year)))
                {
                    ReportFailure(d, res, "Quarterly");
                }

                res = InflationPeriod(d, Frequency.Semiannual);
                if (i is > 0 and < 7
                    && (res.First != new Date(1, (Month)1, year) || res.Second != new Date(30, (Month)6, year)))
                {
                    ReportFailure(d, res, "Semiannual");
                }
                else if (i is > 6 and < 13
                    && (res.First != new Date(1, (Month)7, year) || res.Second != new Date(31, (Month)12, year)))
                {
                    ReportFailure(d, res, "Semiannual");
                }

                res = InflationPeriod(d, Frequency.Annual);
                if (res.First != new Date(1, (Month)1, year) || res.Second != new Date(31, (Month)12, year))
                {
                    ReportFailure(d, res, "Annual");
                }
            }
        }
    }

    // Adapted: the fixings go into the case's evaluation context.
    [Fact]
    public void TestCpiFlatInterpolation()
    {
        // Testing CPI flat interpolation for inflation fixings...
        var context = new EvaluationContext { EvaluationDate = new Date(10, February, 2022) };

        var testIndex = new UKRPI();

        context = AddFixing(context, testIndex, new Date(1, November, 2020), 293.5);
        context = AddFixing(context, testIndex, new Date(1, December, 2020), 295.4);
        context = AddFixing(context, testIndex, new Date(1, January, 2021), 294.6);
        context = AddFixing(context, testIndex, new Date(1, February, 2021), 296.0);
        context = AddFixing(context, testIndex, new Date(1, March, 2021), 296.9);

        double calculated = CPI.LaggedFixing(testIndex, new Date(10, February, 2021), Months3, CPIInterpolationType.Flat, context);
        double expected = 293.5;

        CheckClose(calculated, expected, 1e-8);

        calculated = CPI.LaggedFixing(testIndex, new Date(12, May, 2021), Months3, CPIInterpolationType.Flat, context);
        expected = 296.0;

        CheckClose(calculated, expected, 1e-8);

        calculated = CPI.LaggedFixing(testIndex, new Date(25, June, 2021), Months3, CPIInterpolationType.Flat, context);
        expected = 296.9;

        CheckClose(calculated, expected, 1e-8);
    }

    // Adapted: the fixings go into the case's evaluation context.
    [Fact]
    public void TestCpiLinearInterpolation()
    {
        // Testing CPI linear interpolation for inflation fixings...
        var context = new EvaluationContext { EvaluationDate = new Date(10, February, 2022) };

        var testIndex = new UKRPI();

        context = AddFixing(context, testIndex, new Date(1, November, 2020), 293.5);
        context = AddFixing(context, testIndex, new Date(1, December, 2020), 295.4);
        context = AddFixing(context, testIndex, new Date(1, January, 2021), 294.6);
        context = AddFixing(context, testIndex, new Date(1, February, 2021), 296.0);
        context = AddFixing(context, testIndex, new Date(1, March, 2021), 296.9);

        double calculated = CPI.LaggedFixing(testIndex, new Date(10, February, 2021), Months3, CPIInterpolationType.Linear, context);
        double expected = (293.5 * (19 / 28.0)) + (295.4 * (9 / 28.0));

        CheckClose(calculated, expected, 1e-8);

        calculated = CPI.LaggedFixing(testIndex, new Date(12, May, 2021), Months3, CPIInterpolationType.Linear, context);
        expected = (296.0 * (20 / 31.0)) + (296.9 * (11 / 31.0));

        CheckClose(calculated, expected, 1e-8);

        // this would require April's fixing
        Assert.ThrowsAny<NanoQLException>(
            () => CPI.LaggedFixing(testIndex, new Date(25, June, 2021), Months3, CPIInterpolationType.Linear, context));

        // however, this is a special case
        calculated = CPI.LaggedFixing(testIndex, new Date(1, June, 2021), Months3, CPIInterpolationType.Linear, context);
        expected = 296.9;

        CheckClose(calculated, expected, 1e-8);
    }

    // Adapted: the fixings go into the case's evaluation context.
    [Fact]
    public void TestCpiYoYQuotedFlatInterpolation()
    {
        // Testing CPI flat interpolation for year-on-year quoted rates...
        var context = new EvaluationContext { EvaluationDate = new Date(10, February, 2022) };

        var testIndex = new YYUKRPI();

        context = AddFixing(context, testIndex, new Date(1, November, 2020), 0.02935);
        context = AddFixing(context, testIndex, new Date(1, December, 2020), 0.02954);
        context = AddFixing(context, testIndex, new Date(1, January, 2021), 0.02946);
        context = AddFixing(context, testIndex, new Date(1, February, 2021), 0.02960);
        context = AddFixing(context, testIndex, new Date(1, March, 2021), 0.02969);

        double calculated = CPI.LaggedYoYRate(testIndex, new Date(10, February, 2021), Months3, CPIInterpolationType.Flat, context);
        double expected = 0.02935;

        CheckClose(calculated, expected, 1e-8);

        calculated = CPI.LaggedYoYRate(testIndex, new Date(25, June, 2021), Months3, CPIInterpolationType.Flat, context);
        expected = 0.02969;

        CheckClose(calculated, expected, 1e-8);
    }

    // Adapted: the fixings go into the case's evaluation context.
    [Fact]
    public void TestCpiYoYQuotedLinearInterpolation()
    {
        // Testing CPI linear interpolation for year-on-year quoted rates...
        var context = new EvaluationContext { EvaluationDate = new Date(10, February, 2022) };

        var testIndex = new YYUKRPI();

        context = AddFixing(context, testIndex, new Date(1, November, 2020), 0.02935);
        context = AddFixing(context, testIndex, new Date(1, December, 2020), 0.02954);
        context = AddFixing(context, testIndex, new Date(1, January, 2021), 0.02946);
        context = AddFixing(context, testIndex, new Date(1, February, 2021), 0.02960);
        context = AddFixing(context, testIndex, new Date(1, March, 2021), 0.02969);

        double calculated = CPI.LaggedYoYRate(testIndex, new Date(10, February, 2021), Months3, CPIInterpolationType.Linear, context);
        double expected = (0.02935 * (19 / 28.0)) + (0.02954 * (9 / 28.0));

        CheckClose(calculated, expected, 1e-8);

        calculated = CPI.LaggedYoYRate(testIndex, new Date(12, May, 2021), Months3, CPIInterpolationType.Linear, context);
        expected = (0.02960 * (20 / 31.0)) + (0.02969 * (11 / 31.0));

        CheckClose(calculated, expected, 1e-8);

        // this would require April's fixing
        var e = Assert.ThrowsAny<NanoQLException>(
            () => CPI.LaggedYoYRate(testIndex, new Date(25, June, 2021), Months3, CPIInterpolationType.Linear, context));
        Assert.Contains("Missing UK YY_RPI fixing", e.Message, StringComparison.Ordinal);

        // however, this is a special case
        calculated = CPI.LaggedYoYRate(testIndex, new Date(1, June, 2021), Months3, CPIInterpolationType.Linear, context);
        expected = 0.02969;

        CheckClose(calculated, expected, 1e-8);
    }

    // Adapted: the fixings go into the case's evaluation context.
    [Fact]
    public void TestCpiYoYRatioFlatInterpolation()
    {
        // Testing CPI flat interpolation for year-on-year ratios...
        var context = new EvaluationContext { EvaluationDate = new Date(10, February, 2022) };

        var underlying = new UKRPI();

        var testIndex1 = new YoYInflationIndex(underlying);

        context = AddRatioFixings(context, underlying);

        double calculated = CPI.LaggedYoYRate(testIndex1, new Date(10, February, 2021), Months3, CPIInterpolationType.Flat, context);
        double expected = (293.5 / 291.0) - 1;

        CheckClose(calculated, expected, 1e-8);

        calculated = CPI.LaggedYoYRate(testIndex1, new Date(25, June, 2021), Months3, CPIInterpolationType.Flat, context);
        expected = (296.9 / 292.6) - 1;

        CheckClose(calculated, expected, 1e-8);
    }

    // Adapted: the fixings go into the case's evaluation context.
    [Fact]
    public void TestCpiYoYRatioLinearInterpolation()
    {
        // Testing CPI linear interpolation for year-on-year ratios...
        var context = new EvaluationContext { EvaluationDate = new Date(10, February, 2022) };

        var underlying = new UKRPI();

        var testIndex1 = new YoYInflationIndex(underlying);

        context = AddRatioFixings(context, underlying);

        double calculated = CPI.LaggedYoYRate(testIndex1, new Date(10, February, 2021), Months3, CPIInterpolationType.Linear, context);

        // interpolate, then take ratio
        double expected =
            (((293.5 * (19 / 28.0)) + (295.4 * (9 / 28.0))) / ((291.0 * (20 / 29.0)) + (291.9 * (9 / 29.0)))) - 1;

        CheckClose(calculated, expected, 1e-8);

        calculated = CPI.LaggedYoYRate(testIndex1, new Date(12, May, 2021), Months3, CPIInterpolationType.Linear, context);
        expected = (((296.0 * (20 / 31.0)) + (296.9 * (11 / 31.0))) / ((292.0 * (20 / 31.0)) + (292.6 * (11 / 31.0)))) - 1;

        CheckClose(calculated, expected, 1e-8);

        // this would require April's fixing
        var e = Assert.ThrowsAny<NanoQLException>(
            () => CPI.LaggedYoYRate(testIndex1, new Date(25, June, 2021), Months3, CPIInterpolationType.Linear, context));
        Assert.Contains("Missing UK RPI fixing", e.Message, StringComparison.Ordinal);

        // however, this is a special case
        calculated = CPI.LaggedYoYRate(testIndex1, new Date(1, June, 2021), Months3, CPIInterpolationType.Linear, context);
        expected = (296.9 / 292.6) - 1;

        CheckClose(calculated, expected, 1e-8);
    }

    private static Period Months3 => new(3, TimeUnit.Months);

    private static EvaluationContext AddFixing(EvaluationContext context, FinancialIndex index, Date date, double value) =>
        context with { Fixings = context.Fixings.AddFixing(index, date, value) };

    // The underlying's fixings of the two ratio cases (lines 1484-1494 and 1516-1526).
    private static EvaluationContext AddRatioFixings(EvaluationContext context, UKRPI underlying)
    {
        context = AddFixing(context, underlying, new Date(1, November, 2019), 291.0);
        context = AddFixing(context, underlying, new Date(1, December, 2019), 291.9);
        context = AddFixing(context, underlying, new Date(1, January, 2020), 290.6);
        context = AddFixing(context, underlying, new Date(1, February, 2020), 292.0);
        context = AddFixing(context, underlying, new Date(1, March, 2020), 292.6);

        context = AddFixing(context, underlying, new Date(1, November, 2020), 293.5);
        context = AddFixing(context, underlying, new Date(1, December, 2020), 295.4);
        context = AddFixing(context, underlying, new Date(1, January, 2021), 294.6);
        context = AddFixing(context, underlying, new Date(1, February, 2021), 296.0);
        context = AddFixing(context, underlying, new Date(1, March, 2021), 296.9);
        return context;
    }

    // QuantLib's checkSeasonality (lines 98-201), with each setSeasonality a copy of the curve and a clone of the index
    // forecasting through it.
    private static void CheckSeasonality(ZeroInflationTermStructure hz, ZeroInflationIndex ii, EvaluationContext context)
    {
        if (hz.HasSeasonality)
        {
            throw new NanoQLException(
                "We require that the initially passed in term structure does not have seasonality");
        }

        // Tolerance that we will use below when comparing projected index fixings
        double tolerance = 1e-12;

        (_, Date trueBaseDate) = InflationPeriod(hz.BaseDate, ii.Frequency);
        var seasonalityBaseDate = new Date(31, January, trueBaseDate.Year);

        // Create two different seasonality objects

        // 1) Monthly seasonality with all elements equal to 1 <=> no seasonality
        double[] seasonalityFactors = [.. Enumerable.Repeat(1.0, 12)];
        var unitSeasonality = new MultiplicativePriceSeasonality(seasonalityBaseDate, Frequency.Monthly, seasonalityFactors);

        // 2) Seasonality with factors != 1.0
        seasonalityFactors[0] = 1.003245;
        seasonalityFactors[1] = 1.000000;
        seasonalityFactors[2] = 0.999715;
        seasonalityFactors[3] = 1.000495;
        seasonalityFactors[4] = 1.000929;
        seasonalityFactors[5] = 0.998687;
        seasonalityFactors[6] = 0.995949;
        seasonalityFactors[7] = 0.994682;
        seasonalityFactors[8] = 0.995949;
        seasonalityFactors[9] = 1.000519;
        seasonalityFactors[10] = 1.003705;
        seasonalityFactors[11] = 1.004186;

        var nonUnitSeasonality =
            new MultiplicativePriceSeasonality(seasonalityBaseDate, Frequency.Monthly, seasonalityFactors);

        // Create dates on which we will check fixings
        var fixingDates = new Date[12];
        var anchorDate = new Date(14, January, 2013);
        for (int i = 0; i < fixingDates.Length; ++i)
        {
            fixingDates[i] = anchorDate + (i * Months1);
        }

        // Projected inflation index fixings when there is no seasonality
        var noSeasonalityFixings = new double[12];
        for (int i = 0; i < fixingDates.Length; ++i)
        {
            noSeasonalityFixings[i] = ii.Fixing(fixingDates[i], context, true);
        }

        // Set seasonality of all 1's and get the projected index fixings
        ZeroInflationIndex unitIndex = ii.Clone(hz.SetSeasonality(unitSeasonality, context));
        var unitSeasonalityFixings = new double[12];
        for (int i = 0; i < fixingDates.Length; ++i)
        {
            unitSeasonalityFixings[i] = unitIndex.Fixing(fixingDates[i], context, true);
        }

        // Check that the unit seasonality fixings agree with the no seasonality fixings
        for (int i = 0; i < fixingDates.Length; i++)
        {
            if (Math.Abs(noSeasonalityFixings[i] - unitSeasonalityFixings[i]) > tolerance)
            {
                Assert.Fail(
                    "Seasonality doesn't work correctly when seasonality factors are set = 1No seasonality fixing is: "
                    + $"{noSeasonalityFixings[i]} but unit seasonality fixing is: {unitSeasonalityFixings[i]} for "
                    + $"fixing date {fixingDates[i]}");
            }
        }

        // Testing seasonality correction when seasonality factors are different from 1
        // We expect to see that I_{SA}(t) = I_{NSA}(t) * S(t) / S(t_b)
        Month baseCpiMonth = hz.BaseDate.Month;
        int baseCpiIndex = (int)baseCpiMonth - 1;
        double baseSeasonality = seasonalityFactors[baseCpiIndex];

        // These are the expected fixings
        var expectedSeasonalityFixings = new double[12];
        for (int i = 0; i < expectedSeasonalityFixings.Length; ++i)
        {
            expectedSeasonalityFixings[i] =
                unitIndex.Fixing(fixingDates[i], context, true) * seasonalityFactors[i] / baseSeasonality;
        }

        // Set the seasonality and calculate the actual seasonally adjusted fixings
        ZeroInflationIndex nonUnitIndex = ii.Clone(hz.SetSeasonality(nonUnitSeasonality, context));
        var nonUnitSeasonalityFixings = new double[12];
        for (int i = 0; i < fixingDates.Length; ++i)
        {
            nonUnitSeasonalityFixings[i] = nonUnitIndex.Fixing(fixingDates[i], context, true);
        }

        // Check that the calculated fixings agree with the expected fixings
        for (int i = 0; i < fixingDates.Length; i++)
        {
            if (Math.Abs(expectedSeasonalityFixings[i] - nonUnitSeasonalityFixings[i]) > tolerance)
            {
                Assert.Fail(
                    "Seasonality doesn't work correctly for non-unit seasonality factors. Expected fixing is: "
                    + $"{expectedSeasonalityFixings[i]} but calculated fixing is: {nonUnitSeasonalityFixings[i]} for "
                    + $"fixing date {fixingDates[i]}");
            }
        }

        // Testing that unsetting seasonality works also
        ZeroInflationIndex unsetIndex = ii.Clone(hz.SetSeasonality(null, context));
        var unsetSeasonalityFixings = new double[12];
        for (int i = 0; i < fixingDates.Length; ++i)
        {
            unsetSeasonalityFixings[i] = unsetIndex.Fixing(fixingDates[i], context, true);
        }

        // Check that seasonality has been unset by comparing with the no seasonality fixings
        for (int i = 0; i < fixingDates.Length; i++)
        {
            if (Math.Abs(noSeasonalityFixings[i] - unsetSeasonalityFixings[i]) > tolerance)
            {
                Assert.Fail(
                    $"Unsetting seasonality doesn't work correctly. No seasonality fixing is: {noSeasonalityFixings[i]}"
                    + $" but after unsetting seasonality fixing is: {unitSeasonalityFixings[i]} for fixing date "
                    + $"{fixingDates[i]}");
            }
        }
    }

    // BOOST_CHECK_EXCEPTION(..., Error, ExpectedErrorMessage("empty Handle")).
    private static void CheckEmptyHandle(Func<double> f)
    {
        var e = Assert.ThrowsAny<NanoQLException>(() => f());
        Assert.Contains("empty Handle", e.Message, StringComparison.Ordinal);
    }

    // REPORT_FAILURE.
    private static void ReportFailure(Date d, (Date First, Date Second) res, string periodName) =>
        Assert.Fail(
            $"wrong {periodName} inflation period for Date (1 {d}), Start Date ( {res.First}), End Date ({res.Second})");

    // QL_CHECK_CLOSE, Boost's BOOST_CHECK_CLOSE: |actual - expected| within the tolerance, in percent, of both values.
    private static void CheckClose(double actual, double expected, double tolerancePercent)
    {
        double diff = Math.Abs(actual - expected), fraction = tolerancePercent * 0.01;
        Assert.True(
            SafeDivision(diff, Math.Abs(expected)) <= fraction && SafeDivision(diff, Math.Abs(actual)) <= fraction,
            $"difference between {actual:R} and {expected:R} exceeds {tolerancePercent:R}%");
    }

    // Boost's safe_fpt_division: f1 / f2, without overflow or underflow.
    private static double SafeDivision(double f1, double f2)
    {
        const double NormalMin = 2.2250738585072014e-308;
        if (f2 < 1.0 && f1 > f2 * double.MaxValue)
        {
            return double.MaxValue;
        }

        if (Math.Abs(f1) <= NormalMin || (f2 > 1.0 && f1 < f2 * NormalMin))
        {
            return 0.0;
        }

        return f1 / f2;
    }
}
