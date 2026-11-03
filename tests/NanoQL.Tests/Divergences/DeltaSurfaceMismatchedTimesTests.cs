// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Quotes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The delta-quoted volatility surface's curves on another clock (the mismatched-dates class). QuantLib reads its
/// domestic and foreign curves at the surface's own times (<c>blackvolsurfacedelta.cpp:96-97</c>, <c>:210</c>), so
/// curves on another day counter, or dated otherwise, are read at other instants: under a surface on Actual/365 (Fixed)
/// from 15 October 2026, curves at 4% and 1% on Actual/360 give a one-year forward of 1.2159363500651499 where the same
/// curves restated on Actual/365 (Fixed), each rate times 365/360 and so the same discount at every date, give
/// 1.2164430957757022; and QuantLib's own cases' curves, floating from 1 January 2010, a TARGET holiday, and so dated 4
/// January, are read three days on: their one-month at-the-money volatility is 0.13010360399762275, where the curves
/// restated on the surface's clock give 0.13008061079668704 (QuantLib-Python 1.44). nanoql reads each curve at the
/// instant a time denotes, on its own clock; QuantLib's check, its surface on the curves restated on the surface's
/// clock, gives nanoql's values. Where the curves share the surface's day counter and reference date, QuantLib's bits
/// stand. QuantLib's cases on the floating curves are moved to the fixed surface's values
/// (<c>TestSuite/BlackVolSurfaceDeltaTests.cs</c>).
/// </summary>
public class DeltaSurfaceMismatchedTimesTests
{
    private static readonly DayCounter ActIsda = new ActualActual(ActualActualConvention.ISDA);

    [Fact]
    public void OnCurvesOnAnotherDayCounterItReadsThemAtTheInstants()
    {
        var reference = new Date(15, Month.October, 2026);
        var surface = Surface(
            reference, new Actual365Fixed(), new FlatForward(reference, 0.04, new Actual360()), new FlatForward(reference, 0.01, new Actual360()));

        // QuantLib's surface on the curves restated on Actual/365 (Fixed)
        AssertClose(1.2164430957757022, surface.AtmLevel(1.0)!.Value, 1e-14);
        Date sixMonths = reference + new Period(6, TimeUnit.Months);
        AssertClose(0.17068915015004102, surface.BlackVol(sixMonths, 1.05), 1e-13);
        AssertClose(0.11866507058201561, surface.BlackVol(sixMonths, 1.18), 1e-13);
        AssertClose(0.12983182361442408, surface.BlackVol(sixMonths, 1.30), 1e-13);

        // and not QuantLib's on the Actual/360 curves read at the surface's times
        Assert.True(Math.Abs(surface.AtmLevel(1.0)!.Value - 1.2159363500651499) > 1e-4);
    }

    [Fact]
    public void OnQuantLibsFloatingCurvesItReadsThemAtTheInstants()
    {
        var reference = new Date(1, Month.January, 2010);
        var context = new EvaluationContext { EvaluationDate = reference };
        FlatForward dts = FlatForward.Create(0, new TARGET(), 0.02, ActIsda, context);
        FlatForward fts = FlatForward.Create(0, new TARGET(), 0.035, ActIsda, context);
        Assert.Equal(new Date(4, Month.January, 2010), dts.ReferenceDate);
        BlackVolatilitySurfaceDelta surface = Surface(reference, ActIsda, dts, fts);

        // QuantLib's surface on the curves restated on the surface's clock: 1 to 4 January, the flat rate after it
        AssertClose(0.13008061079668704, surface.BlackVolSmile(reference + new Period(1, TimeUnit.Months)).Volatility(1.18), 1e-15);
        AssertClose(0.1300391514253639, surface.BlackVolSmile(reference + new Period(15, TimeUnit.Days)).Volatility(1.18), 1e-15);
        SmileSection sixMonths = surface.BlackVolSmile(reference + new Period(6, TimeUnit.Months));
        AssertClose(0.14118369654895138, sixMonths.Volatility(1.10), 1e-15);
        AssertClose(0.13624831191984227, sixMonths.Volatility(1.30), 1e-15);

        // QuantLib's cached values, its read three days on
        Assert.True(Math.Abs(surface.BlackVolSmile(reference + new Period(1, TimeUnit.Months)).Volatility(1.18) - 0.13010360399) > 1e-6);
    }

    [Fact]
    public void WhereTheCurvesShareTheSurfacesClockItIsQuantLibs()
    {
        var reference = new Date(1, Month.January, 2010);
        BlackVolatilitySurfaceDelta surface = Surface(
            reference, ActIsda, new FlatForward(reference, 0.02, ActIsda), new FlatForward(reference, 0.035, ActIsda));

        // QuantLib's values on its cases' curves, which it reads at the surface's times
        Assert.Equal(0.13010360399762275, surface.BlackVolSmile(reference + new Period(1, TimeUnit.Months)).Volatility(1.18));
        Assert.Equal(0.13007226607415612, surface.BlackVolSmile(reference + new Period(15, TimeUnit.Days)).Volatility(1.18));
        Assert.Equal(0.1411379628132459, surface.BlackVolSmile(reference + new Period(6, TimeUnit.Months)).Volatility(1.10));
    }

    // QuantLib's cases' 4 x 3 surface: 1.18 spot, the 25-delta put and call and the delta-neutral at-the-money quote
    private static BlackVolatilitySurfaceDelta Surface(Date reference, DayCounter dc, YieldTermStructure dts, YieldTermStructure fts)
    {
        var vols = new Matrix(4, 3);
        double[][] rows = [[0.15, 0.13, 0.135], [0.14, 0.11, 0.125], [0.13, 0.10, 0.12], [0.125, 0.095, 0.115]];
        for (int i = 0; i < 4; ++i)
        {
            for (int j = 0; j < 3; ++j)
            {
                vols[i, j] = rows[i][j];
            }
        }

        Date[] dates =
        [
            reference + new Period(1, TimeUnit.Months),
            reference + new Period(6, TimeUnit.Months),
            reference + new Period(1, TimeUnit.Years),
            reference + new Period(2, TimeUnit.Years),
        ];
        return new BlackVolatilitySurfaceDelta(
            reference, dates, [-0.25], [0.25], true, vols, dc, new TARGET(), 1.18, dts, fts, DeltaVolQuoteDeltaType.Spot);
    }

    private static void AssertClose(double expected, double actual, double relative) =>
        Assert.True(
            Math.Abs(actual - expected) <= relative * Math.Abs(expected),
            $"{actual:R} differs from {expected:R} by more than {relative} relative");
}
