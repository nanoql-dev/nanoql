// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/termstructures.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Replaced: testReferenceChange (Vectors/TermStructures/FlatForwardVectorTests.cs); testImpliedObs (TestSuite/TermStructuresTests.cs); testFSpreadedObs (TestSuite/TermStructuresTests.cs); testZSpreadedObs (TestSuite/TermStructuresTests.cs); testCreateWithNullUnderlying (Conventions/ZeroSpreadedTermStructureTests.cs); testLinkToNullUnderlying (Conventions/ZeroSpreadedTermStructureTests.cs)
// Moved: testBackwardFlatInterpolationZeroRate line 508 (Divergences/ForwardSpreadFirstDateTests.cs)

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class TermStructuresTests
{
    private static readonly Datum[] DepositData =
    [
        new(1, TimeUnit.Months, 4.581),
        new(2, TimeUnit.Months, 4.573),
        new(3, TimeUnit.Months, 4.557),
        new(6, TimeUnit.Months, 4.496),
        new(9, TimeUnit.Months, 4.490),
    ];

    private static readonly Datum[] SwapData =
    [
        new(1, TimeUnit.Years, 4.54),
        new(5, TimeUnit.Years, 4.99),
        new(10, TimeUnit.Years, 5.47),
        new(20, TimeUnit.Years, 5.89),
        new(30, TimeUnit.Years, 5.96),
    ];

    [Fact]
    public void TestImplied()
    {
        // Testing consistency of implied term structure...

        var vars = new CommonVars();

        Real tolerance = 1.0e-10;
        Date today = vars.Context.EvaluationDate;
        Date newToday = today + new Period(3, TimeUnit.Years);
        Date newSettlement = vars.Calendar.Advance(newToday, vars.SettlementDays, TimeUnit.Days);
        Date testDate = newSettlement + new Period(5, TimeUnit.Years);

        // QuantLib changes the flat rate's quote under one implied curve; each rate is a new pair of curves here
        for (int i = 1; i < 4; i++)
        {
            YieldTermStructure termStructure =
                FlatForward.Create(vars.SettlementDays, new NullCalendar(), i / 100.0, new Actual360(), vars.Context);
            YieldTermStructure implied = new ImpliedTermStructure(termStructure, newSettlement);
            DiscountFactor baseDiscount = termStructure.Discount(newSettlement);
            DiscountFactor discount = termStructure.Discount(testDate);
            DiscountFactor impliedDiscount = implied.Discount(testDate);
            if (Math.Abs(discount - (baseDiscount * impliedDiscount)) > tolerance)
            {
                Assert.Fail(
                    "unable to reproduce discount from implied curve\n"
                    + $"    calculated: {baseDiscount * impliedDiscount:F10}\n"
                    + $"    expected:   {discount:F10}");
            }
        }
    }

    // Replaces testImpliedObs, which checks that the curve notifies its observers when its handle is relinked: a nanoql
    // curve doesn't change, so a new original makes a new curve, which gives the new original's discount ratios, and the
    // curve built before is unchanged.
    [Fact]
    public void ARebuiltImpliedCurveFollowsItsNewOriginal()
    {
        var vars = new CommonVars();

        Real tolerance = 1.0e-10;
        Date today = vars.Context.EvaluationDate;
        Date newToday = today + new Period(3, TimeUnit.Years);
        Date newSettlement = vars.Calendar.Advance(newToday, vars.SettlementDays, TimeUnit.Days);
        YieldTermStructure first = new FlatForward(vars.TermStructure.ReferenceDate, 0.03, new Actual360());
        var implied = new ImpliedTermStructure(first, newSettlement);
        var rebuilt = new ImpliedTermStructure(vars.TermStructure, newSettlement);

        foreach (int years in new[] { 1, 5, 10, 20 })
        {
            Date testDate = newSettlement + new Period(years, TimeUnit.Years);
            DiscountFactor expected = vars.TermStructure.Discount(testDate) / vars.TermStructure.Discount(newSettlement);
            DiscountFactor before = first.Discount(testDate) / first.Discount(newSettlement);
            Assert.True(
                Math.Abs(rebuilt.Discount(testDate) - expected) <= tolerance,
                $"unable to reproduce discount from the rebuilt curve at {years} years\n"
                + $"    calculated: {rebuilt.Discount(testDate):F10}\n    expected:   {expected:F10}");
            Assert.True(
                Math.Abs(implied.Discount(testDate) - before) <= tolerance,
                $"the curve built before changed at {years} years\n"
                + $"    calculated: {implied.Discount(testDate):F10}\n    expected:   {before:F10}");
        }
    }

    [Fact]
    public void TestFSpreaded()
    {
        // Testing consistency of forward-spreaded term structure...

        var vars = new CommonVars();

        Real tolerance = 1.0e-10;
        Real me = 0.01;
        YieldTermStructure spreaded = new ForwardSpreadedTermStructure(vars.TermStructure, me);
        Date testDate = vars.TermStructure.ReferenceDate + new Period(5, TimeUnit.Years);
        DayCounter tsdc = vars.TermStructure.DayCounter;
        DayCounter sprdc = spreaded.DayCounter;
        Rate forward =
            vars.TermStructure.ForwardRate(testDate, testDate, tsdc, Compounding.Continuous, Frequency.NoFrequency).Rate;
        Rate spreadedForward =
            spreaded.ForwardRate(testDate, testDate, sprdc, Compounding.Continuous, Frequency.NoFrequency).Rate;
        if (Math.Abs(forward - (spreadedForward - me)) > tolerance)
        {
            Assert.Fail(
                "unable to reproduce forward from spreaded curve\n"
                + $"    calculated: {DataFormattersFunctions.Rate(spreadedForward - me)}\n"
                + $"    expected:   {DataFormattersFunctions.Rate(forward)}");
        }
    }

    // Replaces testFSpreadedObs, which checks that the curve notifies its observers when its handle is relinked and
    // its quote changes: a nanoql curve doesn't change, so a new underlying and a new spread make a new curve, which
    // gives the new underlying's forward rates plus the new spread, and the curve built before is unchanged.
    [Fact]
    public void ARebuiltForwardSpreadedCurveFollowsItsNewUnderlyingAndSpread()
    {
        var vars = new CommonVars();

        Real tolerance = 1.0e-10;
        YieldTermStructure first = new FlatForward(vars.TermStructure.ReferenceDate, 0.03, new Actual360());
        var spreaded = new ForwardSpreadedTermStructure(first, 0.01);
        var rebuilt = new ForwardSpreadedTermStructure(vars.TermStructure, 0.005);

        DayCounter rfdc = vars.TermStructure.DayCounter;
        foreach (int years in new[] { 1, 5, 10, 20 })
        {
            Date testDate = vars.TermStructure.ReferenceDate + new Period(years, TimeUnit.Years);
            Rate forward =
                vars.TermStructure.ForwardRate(testDate, testDate, rfdc, Compounding.Continuous, Frequency.NoFrequency).Rate;
            Rate firstForward =
                first.ForwardRate(testDate, testDate, rfdc, Compounding.Continuous, Frequency.NoFrequency).Rate;
            Rate rebuiltForward =
                rebuilt.ForwardRate(testDate, testDate, rfdc, Compounding.Continuous, Frequency.NoFrequency).Rate;
            Rate spreadedForward =
                spreaded.ForwardRate(testDate, testDate, rfdc, Compounding.Continuous, Frequency.NoFrequency).Rate;
            Assert.True(
                Math.Abs(forward - (rebuiltForward - 0.005)) <= tolerance,
                $"unable to reproduce forward from the rebuilt curve at {years} years\n"
                + $"    calculated: {rebuiltForward - 0.005:F10}\n    expected:   {forward:F10}");
            Assert.True(
                Math.Abs(firstForward - (spreadedForward - 0.01)) <= tolerance,
                $"the curve built before changed at {years} years\n"
                + $"    calculated: {spreadedForward - 0.01:F10}\n    expected:   {firstForward:F10}");
        }
    }

    [Fact]
    public void TestZSpreaded()
    {
        // Testing consistency of zero-spreaded term structure...

        var vars = new CommonVars();

        Real tolerance = 1.0e-10;
        Real me = 0.01;
        YieldTermStructure spreaded = new ZeroSpreadedTermStructure(vars.TermStructure, me);
        Date testDate = vars.TermStructure.ReferenceDate + new Period(5, TimeUnit.Years);
        DayCounter rfdc = vars.TermStructure.DayCounter;
        Rate zero = vars.TermStructure.ZeroRate(testDate, rfdc, Compounding.Continuous, Frequency.NoFrequency).Rate;
        Rate spreadedZero = spreaded.ZeroRate(testDate, rfdc, Compounding.Continuous, Frequency.NoFrequency).Rate;
        if (Math.Abs(zero - (spreadedZero - me)) > tolerance)
        {
            Assert.Fail(
                "unable to reproduce zero yield from spreaded curve\n"
                + $"    calculated: {spreadedZero - me:F10}\n"
                + $"    expected:   {zero:F10}");
        }
    }

    // Replaces testZSpreadedObs, which checks that the curve notifies its observers when its handle is relinked and
    // its quote changes: a nanoql curve doesn't change, so a new underlying and a new spread make a new curve, which
    // gives the new underlying's zero rates plus the new spread, and the curve built before is unchanged.
    [Fact]
    public void ARebuiltZeroSpreadedCurveFollowsItsNewUnderlyingAndSpread()
    {
        var vars = new CommonVars();

        Real tolerance = 1.0e-10;
        var spreaded = new ZeroSpreadedTermStructure(vars.DummyTermStructure, 0.01);
        var rebuilt = new ZeroSpreadedTermStructure(vars.TermStructure, 0.005);

        DayCounter rfdc = vars.TermStructure.DayCounter;
        foreach (int years in new[] { 1, 5, 10, 20 })
        {
            Date testDate = vars.TermStructure.ReferenceDate + new Period(years, TimeUnit.Years);
            Rate zero = vars.TermStructure.ZeroRate(testDate, rfdc, Compounding.Continuous, Frequency.NoFrequency).Rate;
            Rate dummyZero =
                vars.DummyTermStructure.ZeroRate(testDate, rfdc, Compounding.Continuous, Frequency.NoFrequency).Rate;
            Rate rebuiltZero = rebuilt.ZeroRate(testDate, rfdc, Compounding.Continuous, Frequency.NoFrequency).Rate;
            Rate spreadedZero = spreaded.ZeroRate(testDate, rfdc, Compounding.Continuous, Frequency.NoFrequency).Rate;
            Assert.True(
                Math.Abs(zero - (rebuiltZero - 0.005)) <= tolerance,
                $"unable to reproduce zero yield from the rebuilt curve at {years} years\n"
                + $"    calculated: {rebuiltZero - 0.005:F10}\n    expected:   {zero:F10}");
            Assert.True(
                Math.Abs(dummyZero - (spreadedZero - 0.01)) <= tolerance,
                $"the curve built before changed at {years} years\n"
                + $"    calculated: {spreadedZero - 0.01:F10}\n    expected:   {dummyZero:F10}");
        }
    }

    [Fact]
    public void TestLinearInterpolationSpreadedForwardRate()
    {
        // Testing linear interpolation of forward rates between two dates...

        var vars = new CommonVars();

        // Define the forward rate spreads
        Calendar calendar = vars.Calendar;
        DayCounter dc = vars.TermStructure.DayCounter;
        Date today = vars.Context.EvaluationDate;
        Date settlement = calendar.Advance(today, vars.SettlementDays, TimeUnit.Days);
        double[] spreads = [0.02, 0.03];

        // Define the dates where spreads are specified
        Date[] spreadDates =
            [vars.Calendar.Advance(today, 100, TimeUnit.Days), vars.Calendar.Advance(today, 150, TimeUnit.Days)];

        Date interpolationDate = vars.Calendar.Advance(today, 120, TimeUnit.Days);

        // Create the forward spreaded term structure
        YieldTermStructure spreadedTermStructure =
            new InterpolatedPiecewiseForwardSpreadedTermStructure<Linear>(vars.TermStructure, spreads, spreadDates);

        // Reference dates
        Date d0 = vars.Calendar.Advance(today, 100, TimeUnit.Days);
        Date d1 = vars.Calendar.Advance(today, 150, TimeUnit.Days);
        Date d2 = vars.Calendar.Advance(today, 120, TimeUnit.Days);

        // Compute expected interpolated forward rate
        Real time0 = dc.YearFraction(settlement, d0);
        Real time1 = dc.YearFraction(settlement, d1);
        Real time2 = dc.YearFraction(settlement, d2);

        Real m = (0.03 - 0.02) / (time1 - time0);

        double t = dc.YearFraction(settlement, interpolationDate);
        Rate expectedForwardRate =
            vars.TermStructure.ForwardRate(t, t, Compounding.Continuous, Frequency.NoFrequency, true).Rate
            + ((m * (time2 - time0)) + 0.02);
        Rate interpolatedForwardRate =
            spreadedTermStructure.ForwardRate(t, t, Compounding.Continuous, Frequency.NoFrequency, true).Rate;

        Real tolerance = 1e-9;

        if (Math.Abs(interpolatedForwardRate - expectedForwardRate) > tolerance)
        {
            Assert.Fail(
                "unable to reproduce interpolated forward rate\n"
                + $"    calculated: {DataFormattersFunctions.Rate(interpolatedForwardRate)}\n"
                + $"    expected: {DataFormattersFunctions.Rate(expectedForwardRate)}");
        }
    }

    [Fact]
    public void TestForwardFlatInterpolationSpreadedForwardRate()
    {
        // Testing forward flat interpolation of forward rates between two dates...

        var vars = new CommonVars();

        DayCounter dc = vars.TermStructure.DayCounter;
        Date today = vars.Context.EvaluationDate;

        // Define the forward rate spreads
        double[] spreads = [0.02, 0.03];

        // Define the spread dates
        Date[] spreadDates =
            [vars.Calendar.Advance(today, 75, TimeUnit.Days), vars.Calendar.Advance(today, 260, TimeUnit.Days)];

        Date interpolationDate = vars.Calendar.Advance(today, 100, TimeUnit.Days);

        // Create the forward spreaded term structure using ForwardFlat interpolation
        YieldTermStructure spreadedTermStructure =
            new InterpolatedPiecewiseForwardSpreadedTermStructure<ForwardFlat>(vars.TermStructure, spreads, spreadDates);

        double t = dc.YearFraction(today, interpolationDate);
        Rate expectedForwardRate =
            vars.TermStructure.ForwardRate(t, t, Compounding.Continuous, Frequency.NoFrequency, true).Rate + spreads[0];

        Rate interpolatedForwardRate =
            spreadedTermStructure.ForwardRate(t, t, Compounding.Continuous, Frequency.NoFrequency, true).Rate;

        Real tolerance = 1e-9;

        if (Math.Abs(interpolatedForwardRate - expectedForwardRate) > tolerance)
        {
            Assert.Fail(
                "unable to reproduce interpolated forward rate\n"
                + $"    calculated: {DataFormattersFunctions.Rate(interpolatedForwardRate)}\n"
                + $"    expected: {DataFormattersFunctions.Rate(expectedForwardRate)}");
        }
    }

    [Fact]
    public void TestBackwardFlatInterpolationSpreadedForwardRate()
    {
        // Testing backward flat interpolation of forward rates between two dates...

        var vars = new CommonVars();

        DayCounter dc = vars.TermStructure.DayCounter;
        Date today = vars.Context.EvaluationDate;

        // Define the forward rate spreads
        double[] spreads = [0.02, 0.03, 0.04];

        // Define the spread dates
        Date[] spreadDates =
        [
            vars.Calendar.Advance(today, 100, TimeUnit.Days),
            vars.Calendar.Advance(today, 200, TimeUnit.Days),
            vars.Calendar.Advance(today, 300, TimeUnit.Days),
        ];

        Date interpolationDate = vars.Calendar.Advance(today, 110, TimeUnit.Days);

        YieldTermStructure spreadedTermStructure =
            new InterpolatedPiecewiseForwardSpreadedTermStructure<BackwardFlat>(vars.TermStructure, spreads, spreadDates);

        double t = dc.YearFraction(today, interpolationDate);
        Rate expectedForwardRate =
            vars.TermStructure.ForwardRate(t, t, Compounding.Continuous, Frequency.NoFrequency, true).Rate + spreads[1];

        Rate interpolatedForwardRate =
            spreadedTermStructure.ForwardRate(t, t, Compounding.Continuous, Frequency.NoFrequency, true).Rate;

        Real tolerance = 1e-9;

        if (Math.Abs(interpolatedForwardRate - expectedForwardRate) > tolerance)
        {
            Assert.Fail(
                "unable to reproduce interpolated forward rate\n"
                + $"    calculated: {DataFormattersFunctions.Rate(interpolatedForwardRate)}\n"
                + $"    expected: {DataFormattersFunctions.Rate(expectedForwardRate)}");
        }
    }

    // Moved: the check of line 508, QuantLib's zero rate from the spread's backward-flat primitive from the first
    // spread's time, which leaves out the flat spread before that time, moves to
    // Divergences/ForwardSpreadFirstDateTests.cs. The case checks the zero rate by QuantLib's own formula over the
    // spreads with the first one prepended at the reference date: the spread's integral from the reference date.
    [Fact]
    public void TestBackwardFlatInterpolationZeroRate()
    {
        // Testing backward flat interpolation of zero rates between two dates...

        var vars = new CommonVars();
        DayCounter dc = vars.TermStructure.DayCounter;
        Date today = vars.Context.EvaluationDate;
        Date referenceDate = vars.TermStructure.ReferenceDate;

        // Define the zero rate spreads
        double[] spreads = [0.02, 0.03, 0.04];

        // Define the spread dates
        Date[] spreadDates =
        [
            vars.Calendar.Advance(today, 100, TimeUnit.Days),
            vars.Calendar.Advance(today, 200, TimeUnit.Days),
            vars.Calendar.Advance(today, 300, TimeUnit.Days),
        ];

        // the spreads' times, with the reference date's prepended carrying the first spread
        var times = new double[spreadDates.Length + 1];
        var spreadValues = new double[spreadDates.Length + 1];
        times[0] = 0.0;
        spreadValues[0] = spreads[0];
        for (int i = 0; i < spreadDates.Length; i++)
        {
            times[i + 1] = dc.YearFraction(referenceDate, spreadDates[i]);
            spreadValues[i + 1] = spreads[i];
        }

        Date interpolationDate = vars.Calendar.Advance(today, 110, TimeUnit.Days);

        YieldTermStructure spreadedTermStructure =
            new InterpolatedPiecewiseForwardSpreadedTermStructure<BackwardFlat>(vars.TermStructure, spreads, spreadDates);

        var bckFlatInterpolation = new BackwardFlatInterpolation(times, spreadValues);

        double t = dc.YearFraction(today, interpolationDate);
        Rate nonSpreadedRate = vars.TermStructure.ZeroRate(t, Compounding.Continuous, Frequency.NoFrequency, true).Rate;
        Rate spreadPrimitive = bckFlatInterpolation.Primitive(t, true) / t;
        Rate expectedZeroRate = nonSpreadedRate + spreadPrimitive;

        Rate interpolatedZeroRate =
            spreadedTermStructure.ZeroRate(t, Compounding.Continuous, Frequency.NoFrequency, true).Rate;

        Real tolerance = 1e-9;

        if (Math.Abs(interpolatedZeroRate - expectedZeroRate) > tolerance)
        {
            Assert.Fail(
                "unable to reproduce interpolated zero rate\n"
                + $"    calculated: {DataFormattersFunctions.Rate(interpolatedZeroRate)}\n"
                + $"    expected: {DataFormattersFunctions.Rate(expectedZeroRate)}");
        }
    }

    [Fact]
    public void TestCompositeZeroYieldStructures()
    {
        // Testing composite zero yield structures...

        // QuantLib sets the evaluation date to 10 November 2017, which the curves, dated by their first nodes, don't read

        // First curve
        Date[] dates =
        [
            new(10, Month.November, 2017), new(13, Month.November, 2017), new(12, Month.February, 2018),
            new(10, Month.May, 2018), new(10, Month.August, 2018), new(12, Month.November, 2018),
            new(21, Month.December, 2018), new(15, Month.January, 2020), new(31, Month.March, 2021),
            new(28, Month.February, 2023), new(21, Month.December, 2026), new(31, Month.January, 2030),
            new(28, Month.February, 2031), new(31, Month.March, 2036), new(28, Month.February, 2041),
            new(28, Month.February, 2048), new(31, Month.December, 2141),
        ];

        double[] rates =
        [
            0.0655823213132524, 0.0655823213132524, 0.0699455024156877,
            0.0799107139233497, 0.0813931951022577, 0.0841615820666691,
            0.0501297919004145, 0.0823483583439658, 0.0860720030924466,
            0.0922887604375688, 0.10588902278996, 0.117021968693922,
            0.109824660896137, 0.109231572878364, 0.119218123236241,
            0.128647300167664, 0.0506086995288751,
        ];

        YieldTermStructure termStructure1 = new ForwardCurve(dates, rates, new Actual365Fixed(), new NullCalendar());

        // Second curve
        dates =
        [
            new(10, Month.November, 2017), new(13, Month.November, 2017), new(11, Month.December, 2017),
            new(12, Month.February, 2018), new(10, Month.May, 2018), new(31, Month.January, 2022),
            new(7, Month.December, 2023), new(31, Month.January, 2025), new(31, Month.March, 2028),
            new(7, Month.December, 2033), new(1, Month.February, 2038), new(2, Month.April, 2046),
            new(2, Month.January, 2051), new(31, Month.December, 2141),
        ];

        rates =
        [
            0.056656806197189, 0.056656806197189, 0.0419541633454473, 0.0286681050019797,
            0.0148840226959593, 0.0246680238374363, 0.0255349067810599, 0.0298907184711927,
            0.0263943927922053, 0.0291924526539802, 0.0270049276163556, 0.028775807327614,
            0.0293567711641792, 0.010518655099659,
        ];

        YieldTermStructure termStructure2 = new ForwardCurve(dates, rates, new Actual365Fixed(), new NullCalendar());

        YieldTermStructure compoundCurve = new CompositeZeroYieldStructure(termStructure1, termStructure2, Sub);

        // Expected values
        dates =
        [
            new(10, Month.November, 2017), new(15, Month.December, 2017), new(15, Month.June, 2018),
            new(15, Month.September, 2029), new(15, Month.September, 2038), new(15, Month.March, 2046),
            new(15, Month.December, 2141),
        ];

        rates =
        [
            0.00892551511527986, 0.0278755322562788, 0.0512001768603456, 0.0729941474263546,
            0.0778333309498459, 0.0828451659139004, 0.0503573807521742,
        ];

        Real tolerance = 1.0e-10;
        for (int i = 0; i < dates.Length; ++i)
        {
            Rate actual = compoundCurve.ZeroRate(dates[i], new Actual365Fixed(), Compounding.Continuous).Rate;
            Rate expected = rates[i];

            if (Math.Abs(actual - expected) > tolerance)
            {
                Assert.Fail(
                    "unable to reproduce zero yield rate from composite input curve\n"
                    + $"    calculated: {actual:F10}\n"
                    + $"    expected:   {expected:F10}");
            }
        }
    }

    [Fact]
    public void TestNullTimeToReference()
    {
        // Testing zero-rate calculation for null time-to-reference...

        Rate rate = 0.02;
        var dayCount = new Thirty360(Thirty360Convention.BondBasis);
        var curve = new FlatForward(new Date(30, Month.August, 2023), rate, dayCount);

        // the time between August 30th and 31st is null for the 30/360 day count convention
        Rate expected = rate;
        Rate calculated = curve.ZeroRate(new Date(31, Month.August, 2023), dayCount, Compounding.Continuous).Rate;
        Real tolerance = 1.0e-10;

        if (Math.Abs(calculated - expected) > tolerance)
        {
            Assert.Fail(
                "unable to reproduce zero yield rate from curve\n"
                + $"    calculated: {calculated:F10}\n"
                + $"    expected:   {expected:F10}");
        }
    }

    private static Real Sub(Real x, Real y) => x - y;

    private readonly record struct Datum(Integer N, TimeUnit Units, Rate Rate);

    private sealed class CommonVars
    {
        public CommonVars()
        {
            Calendar = new TARGET();
            SettlementDays = 2;
            Date today = Calendar.Adjust(Date.TodaysDate());
            Context = new EvaluationContext { EvaluationDate = today };
            Date settlement = Calendar.Advance(today, SettlementDays, TimeUnit.Days);
            int deposits = DepositData.Length, swaps = SwapData.Length;

            var instruments = new RateHelper[deposits + swaps];
            for (int i = 0; i < deposits; i++)
            {
                instruments[i] = new DepositRateHelper(
                    DepositData[i].Rate / 100,
                    new Period(DepositData[i].N, DepositData[i].Units),
                    SettlementDays,
                    Calendar,
                    BusinessDayConvention.ModifiedFollowing,
                    true,
                    new Actual360(),
                    Context);
            }

            var index = new IborIndex(
                "dummy",
                new Period(6, TimeUnit.Months),
                SettlementDays,
                null,
                Calendar,
                BusinessDayConvention.ModifiedFollowing,
                false,
                new Actual360());
            for (int i = 0; i < swaps; ++i)
            {
                instruments[i + deposits] = new SwapRateHelper(
                    SwapData[i].Rate / 100,
                    new Period(SwapData[i].N, SwapData[i].Units),
                    Calendar,
                    Frequency.Annual,
                    BusinessDayConvention.Unadjusted,
                    new Thirty360(Thirty360Convention.BondBasis),
                    index,
                    Context);
            }

            TermStructure = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
                settlement, instruments, new Actual360(), Context);
            DummyTermStructure = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
                settlement, instruments, new Actual360(), Context);
        }

        public Calendar Calendar { get; }

        public Natural SettlementDays { get; }

        public EvaluationContext Context { get; }

        public YieldTermStructure TermStructure { get; }

        public YieldTermStructure DummyTermStructure { get; }
    }
}
