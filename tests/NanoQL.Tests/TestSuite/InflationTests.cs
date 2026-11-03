// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2008 Piero Del Boca
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/inflation.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Replaced: testNotifications (Vectors/Coupons/IndexedCashFlowVectorTests.cs)
// Not ported: testCpiAsIndexInterpolation (CPI::AsIndex, deprecated in QuantLib 1.43, D5)

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.TermStructures.InflationTermStructureFunctions;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

/// <summary>
/// QuantLib's inflation tests, <c>test-suite/inflation.cpp</c>: indexes, fixings, periods, curves, swaps and their
/// bootstrap.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InflationTests
{
    private static readonly Period Months1 = new(1, TimeUnit.Months);

    // Adapted: the fixings go into the case's evaluation context.
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

        var ukhicp = new UKHICP();
        if (ukhicp.Name != "UK HICP"
            || ukhicp.Frequency != Frequency.Monthly
            || ukhicp.Revised
            || !(ukhicp.AvailabilityLag == Months1))
        {
            Assert.Fail(
                $"wrong UK HICP data ({ukhicp.Name}, {ukhicp.Frequency}, {ukhicp.Revised}, , {ukhicp.AvailabilityLag})");
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

        var aucpi = new AUCPI(Frequency.Quarterly, false);
        context = AddFixing(context, aucpi, new Date(15, December, 2007), 100.0);

        var expected = new Date(1, October, 2007);
        if (aucpi.LastFixingDate(context) != expected)
        {
            Assert.Fail(
                $"Wrong last fixing date for quarterly AU CPI\n    calculated: {aucpi.LastFixingDate(context)}"
                + $"\n    expected:   {expected}");
        }
    }

    // Adapted: QuantLib's setSeasonality on the bootstrapped curve becomes a copy of the curve with the seasonality,
    // bootstrapped again, which a new index forecasts through (lines 453-509); the fixings go into the case's
    // evaluation context, and the swaps are priced by calling the engine.
    [Fact]
    public void TestZeroTermStructure()
    {
        // Testing zero inflation term structure...

        // try the Zero UK
        Calendar calendar = new UnitedKingdom();
        BusinessDayConvention bdc = BusinessDayConvention.ModifiedFollowing;
        Date evaluationDate = calendar.Adjust(new Date(13, August, 2007));
        var context = new EvaluationContext { EvaluationDate = evaluationDate };

        // fixing data
        var from = new Date(1, January, 2005);
        var to = new Date(1, July, 2007);
        Schedule rpiSchedule = new MakeSchedule().From(from).To(to).WithFrequency(Frequency.Monthly).ToSchedule();

        var ii = new UKRPI();
        for (int i = 0; i < UkRpiFixData.Length; i++)
        {
            context = AddFixing(context, ii, rpiSchedule[i], UkRpiFixData[i]);
        }

        YieldTermStructure nominalTS = NominalTermStructure();

        // now build the zero inflation curve
        Period observationLag = Months3;
        DayCounter dc = new Thirty360(Thirty360Convention.BondBasis);
        Frequency frequency = Frequency.Monthly;
        CPIInterpolationType interpolation = CPIInterpolationType.Flat;

        EvaluationContext helperContext = context;
        List<BootstrapHelper<ZeroInflationTermStructure>> helpers = MakeHelpers<ZeroInflationTermStructure>(
            ZcData,
            (quote, maturity) => new ZeroCouponInflationSwapHelper(
                quote, observationLag, maturity, calendar, bdc, dc, ii, interpolation, helperContext));

        var firstHelper = (ZeroCouponInflationSwapHelper)helpers[0];
        var firstCashFlow = (ZeroInflationCashFlow)firstHelper.Swap.InflationLeg[0];
        Assert.Equal(new Date(13, May, 2008), firstCashFlow.FixingDate);

        Date baseDate = ii.LastFixingDate(context);

        var pZITS = new PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits>(
            evaluationDate, baseDate, frequency, dc, helpers, context);

        // ===========================================================================================
        // first check that the quoted swaps are repriced correctly

        const double eps = 1.0e-7;
        const double basisPoint = 1.0e-4;

        ZeroInflationTermStructure hz = pZITS;
        ii = new UKRPI(hz);

        foreach (Datum datum in ZcData)
        {
            var nzcis = new ZeroCouponInflationSwap(
                SwapType.Payer,
                1000000.0,
                evaluationDate,
                datum.Date,
                calendar,
                bdc,
                dc,
                datum.Rate / 100.0,
                ii,
                observationLag,
                interpolation);
            SwapResults results = DiscountingSwapEngine.Calculate(nzcis, context, nominalTS);

            Assert.True(
                Math.Abs(results.NPV!.Value) < eps,
                "zero-coupon inflation swap does not reprice to zero"
                + $"\n    NPV:      {results.NPV}\n    maturity: {nzcis.MaturityDate()}\n    rate:     {nzcis.FixedRate}");

            var nzcisBumped = new ZeroCouponInflationSwap(
                SwapType.Payer,
                1000000.0,
                evaluationDate,
                datum.Date,
                calendar,
                bdc,
                dc,
                (datum.Rate / 100.0) + basisPoint,
                ii,
                observationLag,
                interpolation);
            SwapResults bumped = DiscountingSwapEngine.Calculate(nzcisBumped, context, nominalTS);

            double expected = bumped.LegNPV[0]!.Value - results.LegNPV[0]!.Value;
            double fixedLegBPS = nzcis.FixedLegBPS(results);
            Assert.True(
                Math.Abs(fixedLegBPS - expected) < eps,
                "zero-coupon inflation swap does not have correct fixedLegBPS"
                + $"\n    actual:   {fixedLegBPS}\n    expected: {expected}\n    maturity: {nzcis.MaturityDate()}"
                + $"\n    rate:     {nzcis.FixedRate}");
        }

        // ===========================================================================================
        // now test the forecasting capability of the index.

        from = hz.ReferenceDate;
        to = hz.MaxDate - Months1; // a bit of margin for adjustments
        Schedule testIndex = new MakeSchedule()
            .From(from)
            .To(to)
            .WithTenor(Months1)
            .WithCalendar(new UnitedKingdom())
            .WithConvention(BusinessDayConvention.ModifiedFollowing)
            .ToSchedule();

        // we are testing UKRPI which is not interpolated
        Date bd = hz.BaseDate;
        double bf = ii.Fixing(bd, context);
        foreach (Date d in testIndex.Dates)
        {
            double z = hz.ZeroRate(d);
            double t = hz.DayCounter.YearFraction(bd, InflationPeriod(d, ii.Frequency).Start);
            double calc = bf * Math.Pow(1 + z, t);
            if (t <= 0)
            {
                calc = ii.Fixing(d, context, false); // still historical
            }

            double forecast = ii.Fixing(d, context, true);
            Assert.True(
                Math.Abs(calc - forecast) <= eps,
                $"inflation index does not forecast correctly\n    date:        {d}\n    base date:   {bd}"
                + $"\n    base fixing: {bf}\n    expected:    {calc}\n    forecast:    {forecast}");
        }

        // ===========================================================================================
        // Add a seasonality correction.  The curve should recalculate and still reprice the swaps.

        (_, Date nextBaseDate) = InflationPeriod(hz.BaseDate, ii.Frequency);
        var seasonalityBaseDate = new Date(31, January, nextBaseDate.Year);
        double[] seasonalityFactors =
        [
            1.003245,
            1.000000,
            0.999715,
            1.000495,
            1.000929,
            0.998687,
            0.995949,
            0.994682,
            0.995949,
            1.000519,
            1.003705,
            1.004186,
        ];

        var nonUnitSeasonality =
            new MultiplicativePriceSeasonality(seasonalityBaseDate, Frequency.Monthly, seasonalityFactors);

        ii = new UKRPI(pZITS.SetSeasonality(nonUnitSeasonality, context));

        foreach (Datum datum in ZcData)
        {
            var nzcis = new ZeroCouponInflationSwap(
                SwapType.Payer,
                1000000.0,
                evaluationDate,
                datum.Date,
                calendar,
                bdc,
                dc,
                datum.Rate / 100.0,
                ii,
                observationLag,
                interpolation);
            SwapResults results = DiscountingSwapEngine.Calculate(nzcis, context, nominalTS);

            Assert.True(
                Math.Abs(results.NPV!.Value) < eps,
                "zero-coupon inflation swap does not reprice to zero"
                + $"\n    NPV:      {results.NPV}\n    maturity: {nzcis.MaturityDate()}\n    rate:     {datum.Rate}");
        }
    }

    // Adapted: the base date is a function of the context, called by the constructor, so the lazy curve is built once
    // the fixings are in the context, and its helpers take their quotes at construction.
    [Fact]
    public void TestZeroTermStructureLazyBaseDate()
    {
        // UKRPI conventions
        Calendar calendar = new UnitedKingdom();
        BusinessDayConvention bdc = BusinessDayConvention.ModifiedFollowing;
        Period observationLag = Months3;
        CPIInterpolationType interpolation = CPIInterpolationType.Flat;
        DayCounter dc = new Thirty360(Thirty360Convention.BondBasis);
        Frequency frequency = Frequency.Monthly;
        Date evaluationDate = calendar.Adjust(new Date(13, August, 2007));
        var context = new EvaluationContext { EvaluationDate = evaluationDate };

        // zc swaps, their quotes set
        var ii = new UKRPI();
        var helpers = new List<BootstrapHelper<ZeroInflationTermStructure>>();
        foreach (Datum datum in ZcData)
        {
            helpers.Add(new ZeroCouponInflationSwapHelper(
                datum.Rate / 100.0, observationLag, datum.Date, calendar, bdc, dc, ii, interpolation, context));
        }

        // set fixings
        var from = new Date(1, January, 2005);
        var to = new Date(1, July, 2007);
        Schedule rpiSchedule = new MakeSchedule().From(from).To(to).WithFrequency(Frequency.Monthly).ToSchedule();

        for (int i = 0; i < UkRpiFixData.Length; i++)
        {
            context = AddFixing(context, ii, rpiSchedule[i], UkRpiFixData[i]);
        }

        // Create a curve that will use lastFixingDate as the baseDate, which it computes in its context.
        var curveLazy = new PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits>(
            evaluationDate, ctx => ii.LastFixingDate(ctx), frequency, dc, helpers, context);

        // Create a curve with an explicit baseDate and check that the lazy curve
        // produces the same results.
        var curve = new PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits>(
            evaluationDate, ii.LastFixingDate(context), frequency, dc, helpers, context);

        Assert.Equal(curve.BaseDate, curveLazy.BaseDate);
        Assert.True(curveLazy.Nodes().SequenceEqual(curve.Nodes()));
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

    // Adapted: QuantLib's deprecated interpolated() check (line 769) isn't ported: the index never interpolates.
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

    // Adapted: QuantLib's deprecated interpolated() check (line 826) isn't ported; the fixings go into the case's
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

    // Adapted: the fixings go into the case's evaluation context, and the swaps are priced by calling the engine.
    [Fact]
    public void TestYYTermStructure()
    {
        // Testing year-on-year inflation term structure...

        // try the YY UK
        Calendar calendar = new UnitedKingdom();
        BusinessDayConvention bdc = BusinessDayConvention.ModifiedFollowing;
        Date evaluationDate = calendar.Adjust(new Date(13, August, 2007));
        var context = new EvaluationContext { EvaluationDate = evaluationDate };

        // fixing data
        var rpi = new UKRPI();
        var iir = new YoYInflationIndex(rpi);
        context = AddUkRpiFixings(context, rpi);

        YieldTermStructure nominalTS = NominalTermStructure();

        // now build the YoY inflation curve
        Period observationLag = new(2, TimeUnit.Months);
        DayCounter dc = new Thirty360(Thirty360Convention.BondBasis);
        CPIInterpolationType interpolation = CPIInterpolationType.Flat;

        // now build the helpers ...
        EvaluationContext helperContext = context;
        List<BootstrapHelper<YoYInflationTermStructure>> helpers = MakeHelpers<YoYInflationTermStructure>(
            YyData,
            (quote, maturity) => new YearOnYearInflationSwapHelper(
                quote, observationLag, maturity, calendar, bdc, dc, iir, interpolation, nominalTS, helperContext));

        var firstHelper = (YearOnYearInflationSwapHelper)helpers[0];
        var firstCashFlow = (YoYInflationCoupon)firstHelper.Swap.YoYLeg[0];
        Assert.Equal(new Date(13, June, 2008), firstCashFlow.FixingDate());

        Date baseDate = rpi.LastFixingDate(context);
        double baseYYRate = YyData[0].Rate / 100.0;
        var pYYTS = new PiecewiseYoYInflationCurve<Linear, IterativeBootstrap, YoYInflationTraits>(
            evaluationDate, baseDate, baseYYRate, iir.Frequency, dc, helpers, context);

        // validation
        // yoy swaps should reprice to zero
        // yy rates should not equal yySwap rates
        double eps = 0.000001;

        // make sure that the index has the latest yoy term structure
        iir = new YoYInflationIndex(rpi, pYYTS);

        for (int j = 1; j < YyData.Length; j++)
        {
            Date from = nominalTS.ReferenceDate;
            Date to = YyData[j].Date;
            Schedule yoySchedule = new MakeSchedule()
                .From(from)
                .To(to)
                .WithConvention(BusinessDayConvention.Unadjusted) // fixed leg gets calendar from
                .WithCalendar(calendar) // schedule
                .WithTenor(new Period(1, TimeUnit.Years))
                .Backwards()
                .ToSchedule();

            var yyS2 = new YearOnYearInflationSwap(
                SwapType.Payer,
                1000000.0,
                yoySchedule, // fixed schedule, but same as yoy
                YyData[j].Rate / 100.0,
                dc,
                yoySchedule,
                iir,
                observationLag,
                interpolation,
                0.0, // spread on index
                dc,
                new UnitedKingdom());

            YearOnYearInflationSwapResults results = DiscountingSwapEngine.Calculate(yyS2, context, nominalTS);

            Assert.True(
                Math.Abs(results.NPV!.Value) < eps,
                $"fresh yoy swap NPV!=0 from TS swap quote for pt {j}, is {YyData[j].Rate / 100.0} vs YoY rate "
                + $"{pYYTS.YoyRate(YyData[j].Date - observationLag)} at quote date {YyData[j].Date - observationLag}, "
                + $"NPV of a fresh yoy swap is {results.NPV}\n      fair rate {results.FairRate} payment "
                + $"{yyS2.PaymentConvention}");
        }

        int jj = 3;
        for (int k = 0; k < 14; k++)
        {
            Date from = nominalTS.ReferenceDate - (k * Months1);
            Date to = YyData[jj].Date - (k * Months1);
            Schedule yoySchedule = new MakeSchedule()
                .From(from)
                .To(to)
                .WithConvention(BusinessDayConvention.Unadjusted) // fixed leg gets calendar from
                .WithCalendar(calendar) // schedule
                .WithTenor(new Period(1, TimeUnit.Years))
                .Backwards()
                .ToSchedule();

            var yyS3 = new YearOnYearInflationSwap(
                SwapType.Payer,
                1000000.0,
                yoySchedule, // fixed schedule, but same as yoy
                YyData[jj].Rate / 100.0,
                dc,
                yoySchedule,
                iir,
                observationLag,
                interpolation,
                0.0, // spread on index
                dc,
                new UnitedKingdom());

            YearOnYearInflationSwapResults results = DiscountingSwapEngine.Calculate(yyS3, context, nominalTS);

            Assert.True(
                Math.Abs(results.NPV!.Value) < 20000.0,
                $"unexpected size of aged YoY swap, aged {k} months: YY aged NPV = {results.NPV}, legs "
                + $"{results.LegNPV[0]} and {results.LegNPV[1]}");
        }
    }

    // Adapted: QuantLib's fairRate() and fairSpread() refuse "result not available" where the results hold none, a
    // null here; the fixings go into the case's evaluation context.
    [Fact]
    public void TestZeroBpsYoYInflationSwapFairRateAndSpread()
    {
        // Testing YoY inflation swap fair rate/spread with zero BPS...

        Calendar calendar = new UnitedKingdom();
        Date evaluationDate = calendar.Adjust(new Date(13, August, 2007));
        var context = new EvaluationContext { EvaluationDate = evaluationDate };

        var rpi = new UKRPI();
        var iir = new YoYInflationIndex(rpi);
        context = AddUkRpiFixings(context, rpi);

        YieldTermStructure nominalTS = NominalTermStructure();

        Period observationLag = new(2, TimeUnit.Months);
        DayCounter dc = new Thirty360(Thirty360Convention.BondBasis);
        CPIInterpolationType interpolation = CPIInterpolationType.Flat;

        Date[] yoyDates = [evaluationDate, new Date(13, August, 2012)];
        double[] yoyRates = [0.03, 0.03];
        InterpolatedYoYInflationCurve<Linear> yoyTs = new InterpolatedYoYInflationCurve<Linear>(
            evaluationDate, yoyDates, yoyRates, iir.Frequency, dc).EnableExtrapolation();

        iir = new YoYInflationIndex(rpi, yoyTs);

        Schedule yoySchedule = new MakeSchedule()
            .From(nominalTS.ReferenceDate)
            .To(new Date(13, August, 2012))
            .WithConvention(BusinessDayConvention.Unadjusted)
            .WithCalendar(calendar)
            .WithTenor(new Period(1, TimeUnit.Years))
            .Backwards()
            .ToSchedule();

        var swap = new YearOnYearInflationSwap(
            SwapType.Payer,
            0.0,
            yoySchedule,
            0.03,
            dc,
            yoySchedule,
            iir,
            observationLag,
            interpolation,
            0.0,
            dc,
            new UnitedKingdom());

        YearOnYearInflationSwapResults results = DiscountingSwapEngine.Calculate(swap, context, nominalTS);

        Assert.False(swap.IsExpired(context));
        Assert.Equal(0.0, results.LegBPS[0]);
        Assert.Equal(0.0, results.LegBPS[1]);

        // QuantLib's "result not available"
        Assert.Null(results.FairRate);
        Assert.Null(results.FairSpread);
    }

    // Adapted: as the zero-BPS case; the expired swap is priced in a context a year after its maturity.
    [Fact]
    public void TestExpiredYoYInflationSwapFairRateAndSpread()
    {
        // Testing YoY inflation swap fair rate/spread for expired swap...

        Calendar calendar = new UnitedKingdom();
        Date evaluationDate = calendar.Adjust(new Date(13, August, 2007));
        var context = new EvaluationContext { EvaluationDate = evaluationDate };

        var rpi = new UKRPI();
        var iir = new YoYInflationIndex(rpi);
        context = AddUkRpiFixings(context, rpi);

        YieldTermStructure nominalTS = NominalTermStructure();

        Period observationLag = new(2, TimeUnit.Months);
        DayCounter dc = new Thirty360(Thirty360Convention.BondBasis);
        CPIInterpolationType interpolation = CPIInterpolationType.Flat;
        var maturity = new Date(13, August, 2012);

        Date[] yoyDates = [evaluationDate, maturity];
        double[] yoyRates = [0.03, 0.03];
        InterpolatedYoYInflationCurve<Linear> yoyTs = new InterpolatedYoYInflationCurve<Linear>(
            evaluationDate, yoyDates, yoyRates, iir.Frequency, dc).EnableExtrapolation();

        iir = new YoYInflationIndex(rpi, yoyTs);

        Schedule yoySchedule = new MakeSchedule()
            .From(nominalTS.ReferenceDate)
            .To(maturity)
            .WithConvention(BusinessDayConvention.Unadjusted)
            .WithCalendar(calendar)
            .WithTenor(new Period(1, TimeUnit.Years))
            .Backwards()
            .ToSchedule();

        var swap = new YearOnYearInflationSwap(
            SwapType.Payer,
            1000000.0,
            yoySchedule,
            0.03,
            dc,
            yoySchedule,
            iir,
            observationLag,
            interpolation,
            0.0,
            dc,
            new UnitedKingdom());

        context = context with { EvaluationDate = maturity + new Period(1, TimeUnit.Years) };

        Assert.True(swap.IsExpired(context));
        YearOnYearInflationSwapResults results = DiscountingSwapEngine.Calculate(swap, context, nominalTS);

        // QuantLib's "result not available"
        Assert.Null(results.FairRate);
        Assert.Null(results.FairSpread);
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

    // Adapted: the fixings go into the case's evaluation context; enableExtrapolation returns a copy.
    [Fact]
    public void TestExtrapolationRegression()
    {
        // Testing inflation term structure regression when extrapolating...

        Calendar calendar = new UnitedKingdom();
        BusinessDayConvention bdc = BusinessDayConvention.ModifiedFollowing;
        Date evaluationDate = calendar.Adjust(new Date(13, August, 2007));
        var context = new EvaluationContext { EvaluationDate = evaluationDate };

        var from = new Date(1, January, 2005);
        var to = new Date(1, July, 2007);
        Schedule rpiSchedule = new MakeSchedule().From(from).To(to).WithFrequency(Frequency.Monthly).ToSchedule();

        var rpi = new UKRPI();
        for (int i = 0; i < UkRpiFixData.Length; i++)
        {
            context = AddFixing(context, rpi, rpiSchedule[i], UkRpiFixData[i]);
        }

        YieldTermStructure nominalTS = NominalTermStructure();

        // now build the zero inflation curve
        Period observationLag = Months3;
        DayCounter dc = new Thirty360(Thirty360Convention.BondBasis);
        Frequency frequency = Frequency.Monthly;
        CPIInterpolationType interpolation = CPIInterpolationType.Flat;

        EvaluationContext helperContext = context;
        List<BootstrapHelper<ZeroInflationTermStructure>> helpers = MakeHelpers<ZeroInflationTermStructure>(
            ZcData,
            (quote, maturity) => new ZeroCouponInflationSwapHelper(
                quote, observationLag, maturity, calendar, bdc, dc, rpi, interpolation, helperContext));

        Date baseDate = rpi.LastFixingDate(context);

        PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits> pZITS =
            new PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits>(
                evaluationDate, baseDate, frequency, dc, helpers, context).EnableExtrapolation();

        _ = pZITS.ZeroRate(10.0);

        var yoy = new YoYInflationIndex(rpi);

        observationLag = new Period(2, TimeUnit.Months);

        // now build the helpers ...
        Period yoyObservationLag = observationLag;
        List<BootstrapHelper<YoYInflationTermStructure>> yoyHelpers = MakeHelpers<YoYInflationTermStructure>(
            YyData,
            (quote, maturity) => new YearOnYearInflationSwapHelper(
                quote, yoyObservationLag, maturity, calendar, bdc, dc, yoy, interpolation, nominalTS, helperContext));

        double baseYYRate = YyData[0].Rate / 100.0;
        PiecewiseYoYInflationCurve<Linear, IterativeBootstrap, YoYInflationTraits> pYYTS =
            new PiecewiseYoYInflationCurve<Linear, IterativeBootstrap, YoYInflationTraits>(
                evaluationDate, baseDate, baseYYRate, yoy.Frequency, dc, yoyHelpers, context).EnableExtrapolation();

        _ = pYYTS.YoyRate(10.0);
    }

    // Adapted: the fixings go into each evaluation date's context, which takes the place of clearFixings.
    [Fact]
    public void TestUsCpiLinearBootstrapAtMonthStart()
    {
        // Testing US CPI bootstrap with CPI::Linear across evaluation dates...

        // US CPI zero-coupon inflation swaps use daily linear interpolation
        // between monthly index values (matching TIPS conventions).
        // With sub-annual helpers, the pillar assignment must account for
        // interpolation weight to avoid "root not bracketed" failures
        // at month-start.  See https://github.com/lballabio/QuantLib/issues/2454
        //
        // Conventions: T+2 settlement (US GovernmentBond calendar),
        // 3-month observation lag, unadjusted maturity dates.
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        Period observationLag = Months3;
        DayCounter dc = new Thirty360(Thirty360Convention.BondBasis);
        var baseDate = new Date(1, November, 2025);

        int failureCount = 0;

        for (var evalDate = new Date(1, February, 2026); evalDate <= new Date(28, February, 2026); evalDate++)
        {
            var context = new EvaluationContext { EvaluationDate = evalDate };

            var index = new USCPI();

            context = AddFixings(context, index, UsCpiFixings);

            var helpers = new List<BootstrapHelper<ZeroInflationTermStructure>>();
            Date startDate = calendar.Advance(evalDate, 2, TimeUnit.Days);
            foreach ((Period tenor, double rate) in UsCpiSwapData)
            {
                Date endDate = startDate + tenor;
                helpers.Add(new ZeroCouponInflationSwapHelper(
                    rate,
                    observationLag,
                    startDate,
                    endDate,
                    calendar,
                    BusinessDayConvention.ModifiedFollowing,
                    dc,
                    index,
                    CPIInterpolationType.Linear));
            }

            try
            {
                var curve = new PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits>(
                    evalDate, baseDate, Frequency.Monthly, dc, helpers, context);
                _ = curve.ZeroRate(evalDate + new Period(1, TimeUnit.Years));
            }
            catch (Exception)
            {
                failureCount++;
            }
        }

        Assert.Equal(0, failureCount);
    }

    // Adapted: as the US CPI case.
    [Fact]
    public void TestEuHicpFlatBootstrapAtMonthStart()
    {
        // Testing EU HICP bootstrap with CPI::Flat across evaluation dates...

        // EUR HICP zero-coupon inflation swaps use flat (monthly)
        // observation — no daily interpolation between monthly fixings.
        // With CPI::Flat the pillar is always the left node and the
        // helper has full sensitivity, so month-start is not an issue.
        // This test confirms no regression with the pillar assignment
        // changes and that GlobalBootstrap works for EUR conventions.
        //
        // Conventions: T+2 settlement (TARGET calendar),
        // 3-month observation lag, unadjusted maturity dates.
        (Period Tenor, double Rate)[] swapData =
        [
            (new(1, TimeUnit.Years), 0.0182), (new(2, TimeUnit.Years), 0.0178),
            (new(3, TimeUnit.Years), 0.0185), (new(4, TimeUnit.Years), 0.0188),
            (new(5, TimeUnit.Years), 0.0190), (new(7, TimeUnit.Years), 0.0195),
            (new(10, TimeUnit.Years), 0.0201), (new(15, TimeUnit.Years), 0.0210),
            (new(20, TimeUnit.Years), 0.0218), (new(30, TimeUnit.Years), 0.0229),
        ];

        Calendar calendar = new TARGET();
        Period observationLag = Months3;
        DayCounter dc = new Thirty360(Thirty360Convention.BondBasis);
        var baseDate = new Date(1, December, 2025);

        // EU HICP ex-tobacco monthly fixings, approximate 2025 values
        (Date, double)[] fixings =
        [
            (new(1, January, 2025), 126.42), (new(1, February, 2025), 126.81),
            (new(1, March, 2025), 127.19), (new(1, April, 2025), 127.51),
            (new(1, May, 2025), 127.62), (new(1, June, 2025), 127.85),
            (new(1, July, 2025), 127.23), (new(1, August, 2025), 127.58),
            (new(1, September, 2025), 128.07), (new(1, October, 2025), 128.41),
            (new(1, November, 2025), 128.62), (new(1, December, 2025), 128.89),
        ];

        int failureCount = 0;
        int globalFailureCount = 0;

        for (var evalDate = new Date(1, February, 2026); evalDate <= new Date(28, February, 2026); evalDate++)
        {
            var context = new EvaluationContext { EvaluationDate = evalDate };

            var index = new EUHICPXT();

            context = AddFixings(context, index, fixings);

            var helpers = new List<BootstrapHelper<ZeroInflationTermStructure>>();
            Date startDate = calendar.Advance(evalDate, 2, TimeUnit.Days);
            foreach ((Period tenor, double rate) in swapData)
            {
                Date endDate = startDate + tenor;
                helpers.Add(new ZeroCouponInflationSwapHelper(
                    rate,
                    observationLag,
                    startDate,
                    endDate,
                    calendar,
                    BusinessDayConvention.ModifiedFollowing,
                    dc,
                    index,
                    CPIInterpolationType.Flat));
            }

            // IterativeBootstrap
            try
            {
                var curve = new PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits>(
                    evalDate, baseDate, Frequency.Monthly, dc, helpers, context);
                _ = curve.ZeroRate(evalDate + new Period(1, TimeUnit.Years));
            }
            catch (Exception)
            {
                failureCount++;
            }

            // GlobalBootstrap
            try
            {
                var curve = new PiecewiseZeroInflationCurve<Linear, GlobalBootstrap, ZeroInflationTraits>(
                    evalDate, baseDate, Frequency.Monthly, dc, helpers, context);
                _ = curve.ZeroRate(evalDate + new Period(1, TimeUnit.Years));
            }
            catch (Exception)
            {
                globalFailureCount++;
            }
        }

        Assert.Equal(0, failureCount);
        Assert.Equal(0, globalFailureCount);
    }

    // Adapted: as the US CPI case.
    [Fact]
    public void TestUkRpiFlatBootstrapAtMonthStart()
    {
        // Testing UK RPI bootstrap with CPI::Flat across evaluation dates...

        // UK RPI zero-coupon inflation swaps use flat (monthly)
        // observation with a 2-month observation lag (shorter than the
        // 3-month lag used for EUR and US).  UK market convention is
        // T+0 settlement on the London calendar.
        //
        // This test confirms bootstrap stability across the month with
        // UK-specific conventions.  As with EUR, CPI::Flat avoids the
        // pillar sensitivity issue, but this exercises the different lag
        // and settlement.
        (Period Tenor, double Rate)[] swapData =
        [
            (new(1, TimeUnit.Years), 0.0335), (new(2, TimeUnit.Years), 0.0328),
            (new(3, TimeUnit.Years), 0.0322), (new(5, TimeUnit.Years), 0.0318),
            (new(7, TimeUnit.Years), 0.0316), (new(10, TimeUnit.Years), 0.0315),
            (new(15, TimeUnit.Years), 0.0320), (new(20, TimeUnit.Years), 0.0325),
            (new(30, TimeUnit.Years), 0.0330), (new(50, TimeUnit.Years), 0.0332),
        ];

        Calendar calendar = new UnitedKingdom();
        Period observationLag = new(2, TimeUnit.Months);
        DayCounter dc = new Thirty360(Thirty360Convention.BondBasis);
        var baseDate = new Date(1, December, 2025);

        // UK RPI monthly fixings, approximate 2025 values
        (Date, double)[] fixings =
        [
            (new(1, January, 2025), 378.2), (new(1, February, 2025), 379.1),
            (new(1, March, 2025), 380.3), (new(1, April, 2025), 381.5),
            (new(1, May, 2025), 382.0), (new(1, June, 2025), 382.4),
            (new(1, July, 2025), 381.8), (new(1, August, 2025), 382.1),
            (new(1, September, 2025), 383.0), (new(1, October, 2025), 383.5),
            (new(1, November, 2025), 383.9), (new(1, December, 2025), 384.2),
        ];

        int failureCount = 0;
        int globalFailureCount = 0;

        for (var evalDate = new Date(1, February, 2026); evalDate <= new Date(28, February, 2026); evalDate++)
        {
            var context = new EvaluationContext { EvaluationDate = evalDate };

            var index = new UKRPI();

            context = AddFixings(context, index, fixings);

            var helpers = new List<BootstrapHelper<ZeroInflationTermStructure>>();

            // UK RPI: T+0 settlement
            Date startDate = evalDate;
            foreach ((Period tenor, double rate) in swapData)
            {
                Date endDate = startDate + tenor;
                helpers.Add(new ZeroCouponInflationSwapHelper(
                    rate,
                    observationLag,
                    startDate,
                    endDate,
                    calendar,
                    BusinessDayConvention.ModifiedFollowing,
                    dc,
                    index,
                    CPIInterpolationType.Flat));
            }

            // IterativeBootstrap
            try
            {
                var curve = new PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits>(
                    evalDate, baseDate, Frequency.Monthly, dc, helpers, context);
                _ = curve.ZeroRate(evalDate + new Period(1, TimeUnit.Years));
            }
            catch (Exception)
            {
                failureCount++;
            }

            // GlobalBootstrap
            try
            {
                var curve = new PiecewiseZeroInflationCurve<Linear, GlobalBootstrap, ZeroInflationTraits>(
                    evalDate, baseDate, Frequency.Monthly, dc, helpers, context);
                _ = curve.ZeroRate(evalDate + new Period(1, TimeUnit.Years));
            }
            catch (Exception)
            {
                globalFailureCount++;
            }
        }

        Assert.Equal(0, failureCount);
        Assert.Equal(0, globalFailureCount);
    }

    // Adapted: as the US CPI case.
    [Fact]
    public void TestUsCpiLinearGlobalBootstrapAtMonthStart()
    {
        // Testing US CPI bootstrap with GlobalBootstrap across evaluation dates...

        // GlobalBootstrap solves all curve nodes simultaneously via
        // Levenberg-Marquardt.  For CPI::Linear with sub-annual helpers,
        // this provides additional robustness: if two helpers map to the
        // same pillar date, GlobalBootstrap deduplicates pillars and
        // treats both helpers as separate error terms rather than
        // hard-failing on duplicate pillar dates.
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        Period observationLag = Months3;
        DayCounter dc = new Thirty360(Thirty360Convention.BondBasis);
        var baseDate = new Date(1, November, 2025);

        int failureCount = 0;

        for (var evalDate = new Date(1, February, 2026); evalDate <= new Date(28, February, 2026); evalDate++)
        {
            var context = new EvaluationContext { EvaluationDate = evalDate };

            var index = new USCPI();

            context = AddFixings(context, index, UsCpiFixings);

            var helpers = new List<BootstrapHelper<ZeroInflationTermStructure>>();
            Date startDate = calendar.Advance(evalDate, 2, TimeUnit.Days);
            foreach ((Period tenor, double rate) in UsCpiSwapData)
            {
                Date endDate = startDate + tenor;
                helpers.Add(new ZeroCouponInflationSwapHelper(
                    rate,
                    observationLag,
                    startDate,
                    endDate,
                    calendar,
                    BusinessDayConvention.ModifiedFollowing,
                    dc,
                    index,
                    CPIInterpolationType.Linear));
            }

            try
            {
                var curve = new PiecewiseZeroInflationCurve<Linear, GlobalBootstrap, ZeroInflationTraits>(
                    evalDate, baseDate, Frequency.Monthly, dc, helpers, context);
                _ = curve.ZeroRate(evalDate + new Period(1, TimeUnit.Years));
            }
            catch (Exception)
            {
                failureCount++;
            }
        }

        Assert.Equal(0, failureCount);
    }

    // Adapted: as the US CPI case.
    [Fact]
    public void TestPillarCollisionWithDifferentMonthLengths()
    {
        // Testing CPI::Linear pillar assignment across months of different length...

        // With T+0 settlement and sub-annual helpers including a 13-month
        // tenor, consecutive helpers can mature in months of different
        // length.  When the maturity day is near mid-month (day 15-16),
        // the interpolation weight can cross the 0.5 threshold in one
        // month but not another — e.g. day 16 in February (28 days,
        // w = 15/28 = 0.536 → RIGHT) vs March (31 days, w = 15/31 =
        // 0.484 → LEFT).  If pillar assignment used the maturity date,
        // this would cause two helpers to map to the same pillar,
        // crashing IterativeBootstrap.
        //
        // The fix uses startDate_ (shared across all helpers) for the
        // weight calculation, so all helpers switch LEFT/RIGHT in sync.
        //
        // We loop February and March with T+0 settlement and a 13M
        // helper.  Without the startDate_ fix, Feb 16 and Mar 16 fail.
        // See https://github.com/lballabio/QuantLib/issues/2454
        (Period Tenor, double Rate)[] swapData =
        [
            (new(3, TimeUnit.Months), 0.0285), (new(4, TimeUnit.Months), 0.0268),
            (new(5, TimeUnit.Months), 0.0252), (new(6, TimeUnit.Months), 0.0241),
            (new(7, TimeUnit.Months), 0.0237), (new(8, TimeUnit.Months), 0.0232),
            (new(9, TimeUnit.Months), 0.0229), (new(10, TimeUnit.Months), 0.0225),
            (new(11, TimeUnit.Months), 0.0223), (new(1, TimeUnit.Years), 0.0221),
            (new(13, TimeUnit.Months), 0.0220),
            (new(18, TimeUnit.Months), 0.0230), (new(2, TimeUnit.Years), 0.0238),
            (new(5, TimeUnit.Years), 0.0245), (new(10, TimeUnit.Years), 0.0252),
            (new(30, TimeUnit.Years), 0.0260),
        ];

        Calendar calendar = new NullCalendar();
        Period observationLag = Months3;
        DayCounter dc = new Thirty360(Thirty360Convention.BondBasis);
        var baseDate = new Date(1, November, 2025);

        (Date, double)[] fixings =
        [
            .. UsCpiFixings,
            (new(1, January, 2026), 314.012), (new(1, February, 2026), 314.382),
            (new(1, March, 2026), 314.715),
        ];

        int failureCount = 0;

        // Loop February and March with T+0 settlement
        for (var evalDate = new Date(1, February, 2026); evalDate <= new Date(31, March, 2026); evalDate++)
        {
            var context = new EvaluationContext { EvaluationDate = evalDate };

            var index = new USCPI();

            context = AddFixings(context, index, fixings);

            var helpers = new List<BootstrapHelper<ZeroInflationTermStructure>>();
            Date startDate = evalDate; // T+0 settlement
            foreach ((Period tenor, double rate) in swapData)
            {
                Date endDate = startDate + tenor;
                helpers.Add(new ZeroCouponInflationSwapHelper(
                    rate,
                    observationLag,
                    startDate,
                    endDate,
                    calendar,
                    BusinessDayConvention.Unadjusted,
                    dc,
                    index,
                    CPIInterpolationType.Linear));
            }

            try
            {
                var curve = new PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits>(
                    evalDate, baseDate, Frequency.Monthly, dc, helpers, context);
                _ = curve.ZeroRate(evalDate + new Period(1, TimeUnit.Years));
            }
            catch (Exception)
            {
                failureCount++;
            }
        }

        Assert.Equal(0, failureCount);
    }

    // The UK RPI fixings of the zero and year-on-year cases, January 2005 to July 2007.
    private static readonly double[] UkRpiFixData =
    [
        189.9, 189.9, 189.6, 190.5, 191.6, 192.0,
        192.2, 192.2, 192.6, 193.1, 193.3, 193.6,
        194.1, 193.4, 194.2, 195.0, 196.5, 197.7,
        198.5, 198.5, 199.2, 200.1, 200.4, 201.1,
        202.7, 201.6, 203.1, 204.4, 205.4, 206.2,
        207.3,
    ];

    // The zero-coupon swaps of the zero cases.
    private static readonly Datum[] ZcData =
    [
        new(new Date(13, August, 2008), 2.93),
        new(new Date(13, August, 2009), 2.95),
        new(new Date(13, August, 2010), 2.965),
        new(new Date(15, August, 2011), 2.98),
        new(new Date(13, August, 2012), 3.0),
        new(new Date(13, August, 2014), 3.06),
        new(new Date(13, August, 2017), 3.175),
        new(new Date(13, August, 2019), 3.243),
        new(new Date(15, August, 2022), 3.293),
        new(new Date(14, August, 2027), 3.338),
        new(new Date(13, August, 2032), 3.348),
        new(new Date(15, August, 2037), 3.348),
        new(new Date(13, August, 2047), 3.308),
        new(new Date(13, August, 2057), 3.228),
    ];

    // The year-on-year swaps of the year-on-year cases.
    private static readonly Datum[] YyData =
    [
        new(new Date(13, August, 2008), 2.95),
        new(new Date(13, August, 2009), 2.95),
        new(new Date(13, August, 2010), 2.93),
        new(new Date(15, August, 2011), 2.955),
        new(new Date(13, August, 2012), 2.945),
        new(new Date(13, August, 2013), 2.985),
        new(new Date(13, August, 2014), 3.01),
        new(new Date(13, August, 2015), 3.035),
        new(new Date(13, August, 2016), 3.055), // note that
        new(new Date(13, August, 2017), 3.075), // some dates will be on
        new(new Date(13, August, 2019), 3.105), // holidays but the payment
        new(new Date(15, August, 2022), 3.135), // calendar will roll them
        new(new Date(13, August, 2027), 3.155),
        new(new Date(13, August, 2032), 3.145),
        new(new Date(13, August, 2037), 3.145),
    ];

    // The US CPI swaps of the two US CPI month-start cases.
    private static readonly (Period Tenor, double Rate)[] UsCpiSwapData =
    [
        (new(3, TimeUnit.Months), 0.0285), (new(4, TimeUnit.Months), 0.0268),
        (new(5, TimeUnit.Months), 0.0252), (new(6, TimeUnit.Months), 0.0241),
        (new(7, TimeUnit.Months), 0.0237), (new(8, TimeUnit.Months), 0.0232),
        (new(9, TimeUnit.Months), 0.0229), (new(10, TimeUnit.Months), 0.0225),
        (new(11, TimeUnit.Months), 0.0223), (new(1, TimeUnit.Years), 0.0221),
        (new(18, TimeUnit.Months), 0.0230), (new(2, TimeUnit.Years), 0.0238),
        (new(5, TimeUnit.Years), 0.0245), (new(10, TimeUnit.Years), 0.0252),
        (new(30, TimeUnit.Years), 0.0260),
    ];

    // US CPI-U (NSA) monthly fixings, approximate 2025 values
    private static readonly (Date Date, double Value)[] UsCpiFixings =
    [
        (new(1, January, 2025), 309.685), (new(1, February, 2025), 310.326),
        (new(1, March, 2025), 311.054), (new(1, April, 2025), 311.538),
        (new(1, May, 2025), 311.862), (new(1, June, 2025), 312.104),
        (new(1, July, 2025), 312.332), (new(1, August, 2025), 312.558),
        (new(1, September, 2025), 312.816), (new(1, October, 2025), 313.025),
        (new(1, November, 2025), 313.314), (new(1, December, 2025), 313.580),
    ];

    // nominalTermStructure() (lines 74-78).
    private static YieldTermStructure NominalTermStructure() =>
        new FlatForward(new Date(13, August, 2007), 0.05, new Actual360());

    // makeHelpers (lines 80-95), each quote a rate in percent.
    private static List<BootstrapHelper<T>> MakeHelpers<T>(Datum[] iiData, Func<double, Date, BootstrapHelper<T>> makeHelper)
        where T : TermStructure
    {
        var instruments = new List<BootstrapHelper<T>>();
        foreach (Datum datum in iiData)
        {
            Date maturity = datum.Date;
            double quote = datum.Rate / 100.0;
            BootstrapHelper<T> anInstrument = makeHelper(quote, maturity);
            instruments.Add(anInstrument);
        }

        return instruments;
    }

    // The year-on-year cases' fixings: UK RPI from January 2005 to July 2007, on a monthly schedule rolled on the UK
    // calendar.
    private static EvaluationContext AddUkRpiFixings(EvaluationContext context, UKRPI rpi)
    {
        var from = new Date(1, January, 2005);
        var to = new Date(1, July, 2007);
        Schedule rpiSchedule = new MakeSchedule()
            .From(from)
            .To(to)
            .WithTenor(Months1)
            .WithCalendar(new UnitedKingdom())
            .WithConvention(BusinessDayConvention.ModifiedFollowing)
            .ToSchedule();
        for (int i = 0; i < UkRpiFixData.Length; i++)
        {
            context = AddFixing(context, rpi, rpiSchedule[i], UkRpiFixData[i]);
        }

        return context;
    }

    private static EvaluationContext AddFixings(
        EvaluationContext context, FinancialIndex index, IEnumerable<(Date Date, double Value)> fixings)
    {
        foreach ((Date d, double v) in fixings)
        {
            context = AddFixing(context, index, d, v);
        }

        return context;
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

    // QuantLib's Datum (lines 69-72).
    private readonly record struct Datum(Date Date, double Rate);
}
