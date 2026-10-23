// SPDX-FileCopyrightText: Copyright (C) 2014 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/piecewisezerospreadedtermstructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Replaced: testQuoteChanging (TestSuite/PiecewiseZeroSpreadedTermStructureTests.cs)

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class PiecewiseZeroSpreadedTermStructureTests
{
    [Fact]
    public void TestFlatInterpolationLeft()
    {
        // Testing flat interpolation before the first spreaded date...

        var vars = new CommonVars();

        double[] spreads = [0.02, 0.03];

        Date[] spreadDates =
        [
            vars.Calendar.Advance(vars.Today, 8, TimeUnit.Months),
            vars.Calendar.Advance(vars.Today, 15, TimeUnit.Months),
        ];

        Date interpolationDate = vars.Calendar.Advance(vars.Today, 6, TimeUnit.Months);

        ZeroYieldStructure spreadedTermStructure =
            new PiecewiseZeroSpreadedTermStructure(vars.TermStructure, spreads, spreadDates);

        double t = vars.DayCount.YearFraction(vars.Today, interpolationDate);
        Rate interpolatedZeroRate = spreadedTermStructure.ZeroRate(t, vars.Compounding).Rate;

        Real tolerance = 1e-9;

        Real expectedRate = vars.TermStructure.ZeroRate(t, vars.Compounding).Rate + spreads[0];

        CheckRate(interpolatedZeroRate, expectedRate, tolerance);
    }

    [Fact]
    public void TestFlatInterpolationRight()
    {
        // Testing flat interpolation after the last spreaded date...

        var vars = new CommonVars();

        double[] spreads = [0.02, 0.03];

        Date[] spreadDates =
        [
            vars.Calendar.Advance(vars.Today, 8, TimeUnit.Months),
            vars.Calendar.Advance(vars.Today, 15, TimeUnit.Months),
        ];

        Date interpolationDate = vars.Calendar.Advance(vars.Today, 20, TimeUnit.Months);

        ZeroYieldStructure spreadedTermStructure =
            new PiecewiseZeroSpreadedTermStructure(vars.TermStructure, spreads, spreadDates);
        spreadedTermStructure = spreadedTermStructure.EnableExtrapolation();

        double t = vars.DayCount.YearFraction(vars.Today, interpolationDate);
        Rate interpolatedZeroRate = spreadedTermStructure.ZeroRate(t, vars.Compounding).Rate;

        Real tolerance = 1e-9;

        Real expectedRate = vars.TermStructure.ZeroRate(t, vars.Compounding).Rate + spreads[1];

        CheckRate(interpolatedZeroRate, expectedRate, tolerance);
    }

    [Fact]
    public void TestLinearInterpolationMultipleSpreads()
    {
        // Testing linear interpolation with more than two spreaded dates...

        var vars = new CommonVars();

        double[] spreads = [0.02, 0.02, 0.035, 0.04];

        Date[] spreadDates =
        [
            vars.Calendar.Advance(vars.Today, 90, TimeUnit.Days),
            vars.Calendar.Advance(vars.Today, 150, TimeUnit.Days),
            vars.Calendar.Advance(vars.Today, 30, TimeUnit.Months),
            vars.Calendar.Advance(vars.Today, 40, TimeUnit.Months),
        ];

        Date interpolationDate = vars.Calendar.Advance(vars.Today, 120, TimeUnit.Days);

        ZeroYieldStructure spreadedTermStructure =
            new PiecewiseZeroSpreadedTermStructure(vars.TermStructure, spreads, spreadDates);

        double t = vars.DayCount.YearFraction(vars.Today, interpolationDate);
        Rate interpolatedZeroRate = spreadedTermStructure.ZeroRate(t, vars.Compounding).Rate;

        Real tolerance = 1e-9;

        Real expectedRate = vars.TermStructure.ZeroRate(t, vars.Compounding).Rate + spreads[0];

        CheckRate(interpolatedZeroRate, expectedRate, tolerance);
    }

    [Fact]
    public void TestLinearInterpolation()
    {
        // Testing linear interpolation between two dates...

        var vars = new CommonVars();

        double[] spreads = [0.02, 0.03];

        Date[] spreadDates =
        [
            vars.Calendar.Advance(vars.Today, 100, TimeUnit.Days),
            vars.Calendar.Advance(vars.Today, 150, TimeUnit.Days),
        ];

        Date interpolationDate = vars.Calendar.Advance(vars.Today, 120, TimeUnit.Days);

        ZeroYieldStructure spreadedTermStructure =
            new InterpolatedPiecewiseZeroSpreadedTermStructure<Linear>(vars.TermStructure, spreads, spreadDates);

        Date d0 = vars.Calendar.Advance(vars.Today, 100, TimeUnit.Days);
        Date d1 = vars.Calendar.Advance(vars.Today, 150, TimeUnit.Days);
        Date d2 = vars.Calendar.Advance(vars.Today, 120, TimeUnit.Days);

        Real m = (0.03 - 0.02) / vars.DayCount.YearFraction(d0, d1);
        Real expectedRate = (m * vars.DayCount.YearFraction(d0, d2)) + 0.054;

        double t = vars.DayCount.YearFraction(vars.SettlementDate, interpolationDate);
        Rate interpolatedZeroRate = spreadedTermStructure.ZeroRate(t, vars.Compounding).Rate;

        Real tolerance = 1e-9;

        CheckRate(interpolatedZeroRate, expectedRate, tolerance);
    }

    [Fact]
    public void TestForwardFlatInterpolation()
    {
        // Testing forward flat interpolation between two dates...

        var vars = new CommonVars();

        double[] spreads = [0.02, 0.03];

        Date[] spreadDates =
        [
            vars.Calendar.Advance(vars.Today, 75, TimeUnit.Days),
            vars.Calendar.Advance(vars.Today, 260, TimeUnit.Days),
        ];

        Date interpolationDate = vars.Calendar.Advance(vars.Today, 100, TimeUnit.Days);

        ZeroYieldStructure spreadedTermStructure =
            new InterpolatedPiecewiseZeroSpreadedTermStructure<ForwardFlat>(vars.TermStructure, spreads, spreadDates);

        double t = vars.DayCount.YearFraction(vars.Today, interpolationDate);
        Rate interpolatedZeroRate = spreadedTermStructure.ZeroRate(t, vars.Compounding).Rate;

        Real tolerance = 1e-9;

        Real expectedRate = vars.TermStructure.ZeroRate(t, vars.Compounding).Rate + spreads[0];

        CheckRate(interpolatedZeroRate, expectedRate, tolerance);
    }

    [Fact]
    public void TestBackwardFlatInterpolation()
    {
        // Testing backward flat interpolation between two dates...

        var vars = new CommonVars();

        double[] spreads = [0.02, 0.03, 0.04];

        Date[] spreadDates =
        [
            vars.Calendar.Advance(vars.Today, 100, TimeUnit.Days),
            vars.Calendar.Advance(vars.Today, 200, TimeUnit.Days),
            vars.Calendar.Advance(vars.Today, 300, TimeUnit.Days),
        ];

        Date interpolationDate = vars.Calendar.Advance(vars.Today, 110, TimeUnit.Days);

        ZeroYieldStructure spreadedTermStructure =
            new InterpolatedPiecewiseZeroSpreadedTermStructure<BackwardFlat>(vars.TermStructure, spreads, spreadDates);

        double t = vars.DayCount.YearFraction(vars.Today, interpolationDate);
        Rate interpolatedZeroRate = spreadedTermStructure.ZeroRate(t, vars.Compounding).Rate;

        Real tolerance = 1e-9;

        Real expectedRate = vars.TermStructure.ZeroRate(t, vars.Compounding).Rate + spreads[1];

        CheckRate(interpolatedZeroRate, expectedRate, tolerance);
    }

    [Fact]
    public void TestDefaultInterpolation()
    {
        // Testing default interpolation between two dates...

        var vars = new CommonVars();

        double[] spreads = [0.02, 0.02];

        Date[] spreadDates =
        [
            vars.Calendar.Advance(vars.Today, 75, TimeUnit.Days),
            vars.Calendar.Advance(vars.Today, 160, TimeUnit.Days),
        ];

        Date interpolationDate = vars.Calendar.Advance(vars.Today, 100, TimeUnit.Days);

        ZeroYieldStructure spreadedTermStructure =
            new PiecewiseZeroSpreadedTermStructure(vars.TermStructure, spreads, spreadDates);

        double t = vars.DayCount.YearFraction(vars.Today, interpolationDate);
        Rate interpolatedZeroRate = spreadedTermStructure.ZeroRate(t, vars.Compounding).Rate;

        Real tolerance = 1e-9;

        Real expectedRate = vars.TermStructure.ZeroRate(t, vars.Compounding).Rate + spreads[0];

        CheckRate(interpolatedZeroRate, expectedRate, tolerance);
    }

    [Fact]
    public void TestSetInterpolationFactory()
    {
        // Testing factory constructor with additional parameters...

        var vars = new CommonVars();

        double[] spreads = [0.02, 0.03, 0.01];

        Date[] spreadDates =
        [
            vars.Calendar.Advance(vars.Today, 8, TimeUnit.Months),
            vars.Calendar.Advance(vars.Today, 15, TimeUnit.Months),
            vars.Calendar.Advance(vars.Today, 25, TimeUnit.Months),
        ];

        Date interpolationDate = vars.Calendar.Advance(vars.Today, 11, TimeUnit.Months);

        Frequency freq = Frequency.NoFrequency;

        ZeroYieldStructure spreadedTermStructure = new InterpolatedPiecewiseZeroSpreadedTermStructure<Cubic>(
            vars.TermStructure,
            spreads,
            spreadDates,
            vars.Compounding,
            freq,
            new Cubic(CubicInterpolationDerivativeApprox.Spline, false));

        double t = vars.DayCount.YearFraction(vars.Today, interpolationDate);
        Rate interpolatedZeroRate = spreadedTermStructure.ZeroRate(t, vars.Compounding).Rate;

        Real tolerance = 1e-9;

        Real expectedRate = vars.TermStructure.ZeroRate(t, vars.Compounding).Rate + 0.026065770863;

        CheckRate(interpolatedZeroRate, expectedRate, tolerance);
    }

    [Fact]
    public void TestMaxDate()
    {
        // Testing term structure max date...

        var vars = new CommonVars();

        double[] spreads = [0.02, 0.03];

        Date[] spreadDates =
        [
            vars.Calendar.Advance(vars.Today, 8, TimeUnit.Months),
            vars.Calendar.Advance(vars.Today, 15, TimeUnit.Months),
        ];

        ZeroYieldStructure spreadedTermStructure =
            new PiecewiseZeroSpreadedTermStructure(vars.TermStructure, spreads, spreadDates);

        Date maxDate = spreadedTermStructure.MaxDate;

        Date expectedDate = vars.TermStructure.MaxDate < spreadDates[^1] ? vars.TermStructure.MaxDate : spreadDates[^1];

        if (maxDate != expectedDate)
        {
            Assert.Fail(
                "unable to reproduce max date\n"
                + $"    calculated: {maxDate}\n"
                + $"    expected: {expectedDate}");
        }
    }

    // Replaces testQuoteChanging, which changes a spread's quote under the curve and checks that the curve follows:
    // a nanoql curve doesn't change, so the new spread makes a new curve, which gives the new spread, and the curve
    // built before keeps the old one.
    [Fact]
    public void ARebuiltCurveFollowsItsNewSpread()
    {
        var vars = new CommonVars();

        Date[] spreadDates =
        [
            vars.Calendar.Advance(vars.Today, 100, TimeUnit.Days),
            vars.Calendar.Advance(vars.Today, 150, TimeUnit.Days),
        ];

        Date interpolationDate = vars.Calendar.Advance(vars.Today, 120, TimeUnit.Days);

        ZeroYieldStructure spreadedTermStructure = new InterpolatedPiecewiseZeroSpreadedTermStructure<BackwardFlat>(
            vars.TermStructure, [0.02, 0.03], spreadDates);

        double t = vars.DayCount.YearFraction(vars.SettlementDate, interpolationDate);
        Rate interpolatedZeroRate = spreadedTermStructure.ZeroRate(t, vars.Compounding).Rate;

        Real tolerance = 1e-9;
        Real expectedRate = vars.TermStructure.ZeroRate(t, vars.Compounding).Rate + 0.03;

        CheckRate(interpolatedZeroRate, expectedRate, tolerance);

        ZeroYieldStructure rebuilt = new InterpolatedPiecewiseZeroSpreadedTermStructure<BackwardFlat>(
            vars.TermStructure, [0.02, 0.025], spreadDates);
        CheckRate(
            rebuilt.ZeroRate(t, vars.Compounding).Rate, vars.TermStructure.ZeroRate(t, vars.Compounding).Rate + 0.025, tolerance);
        CheckRate(spreadedTermStructure.ZeroRate(t, vars.Compounding).Rate, expectedRate, tolerance);
    }

    private static void CheckRate(Rate interpolatedZeroRate, Real expectedRate, Real tolerance)
    {
        if (Math.Abs(interpolatedZeroRate - expectedRate) > tolerance)
        {
            Assert.Fail(
                "unable to reproduce interpolated rate\n"
                + $"    calculated: {DataFormattersFunctions.Rate(interpolatedZeroRate)}\n"
                + $"    expected: {DataFormattersFunctions.Rate(expectedRate)}");
        }
    }

    private sealed class CommonVars
    {
        public CommonVars()
        {
            Calendar = new TARGET();
            SettlementDays = 2;
            Today = new Date(9, Month.June, 2009);
            Compounding = Compounding.Continuous;
            DayCount = new Actual360();
            SettlementDate = Calendar.Advance(Today, SettlementDays, TimeUnit.Days);

            int[] ts = [13, 41, 75, 165, 256, 345, 524, 703];
            Rate[] r = [0.035, 0.033, 0.034, 0.034, 0.036, 0.037, 0.039, 0.040];
            var rates = new List<Rate> { 0.035 };
            var dates = new List<Date> { SettlementDate };
            for (int i = 0; i < 8; ++i)
            {
                dates.Add(Calendar.Advance(Today, ts[i], TimeUnit.Days));
                rates.Add(r[i]);
            }

            TermStructure = new ZeroCurve(dates, rates, DayCount);
        }

        public Calendar Calendar { get; }

        public Natural SettlementDays { get; }

        public DayCounter DayCount { get; }

        public Compounding Compounding { get; }

        public YieldTermStructure TermStructure { get; }

        public Date Today { get; }

        public Date SettlementDate { get; }
    }
}
