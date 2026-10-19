// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/termstructures.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testImplied, testImpliedObs, testFSpreaded, testFSpreadedObs, testLinearInterpolationSpreadedForwardRate, testForwardFlatInterpolationSpreadedForwardRate, testBackwardFlatInterpolationSpreadedForwardRate, testBackwardFlatInterpolationZeroRate, testCompositeZeroYieldStructures
// Replaced: testReferenceChange (Vectors/TermStructures/FlatForwardVectorTests.cs); testZSpreadedObs (TestSuite/TermStructuresTests.cs); testCreateWithNullUnderlying (Conventions/ZeroSpreadedTermStructureTests.cs); testLinkToNullUnderlying (Conventions/ZeroSpreadedTermStructureTests.cs)

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

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
