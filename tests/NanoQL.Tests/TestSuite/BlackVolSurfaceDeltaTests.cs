// SPDX-FileCopyrightText: Copyright (C) 2019 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: Copyright (C) 2020 Skandinaviska Enskilda Banken AB (publ)
// SPDX-FileCopyrightText: Copyright (C) 2025 Paolo D'Elia
// SPDX-FileCopyrightText: Copyright (C) 2026 Yassine Idyiahia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/blackvolsurfacedelta.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Moved: testBlackVolSurfaceDeltaNonConstantVol line 115 (Divergences/DeltaSurfaceMismatchedTimesTests.cs); testTimeExtrapolation line 172 (Divergences/DeltaSurfaceMismatchedTimesTests.cs); testSmileInterpolation line 247 (Divergences/DeltaSurfaceMismatchedTimesTests.cs)

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Quotes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class BlackVolSurfaceDeltaTests
{
    private static readonly Date RefDate = new(1, Month.January, 2010);
    private static readonly EvaluationContext Context = new() { EvaluationDate = RefDate };
    private static readonly DayCounter ActIsda = new ActualActual(ActualActualConvention.ISDA);

    [Fact]
    public void TestBlackVolSurfaceDeltaConstantVol()
    {
        // Testing BlackVolatilitySurfaceDelta...

        double constVol = 0.10; // 10%

        // Setup a 2x2
        Date[] dates = [new(1, Month.January, 2011), new(1, Month.January, 2012)];
        double[] putDeltas = [-0.25];
        double[] callDeltas = [0.25];
        bool hasAtm = false;
        var blackVolMatrix = new Matrix(2, 2, constVol);

        // dummy spot and zero yield curve
        double spot = 1.0;
        FlatForward dts = FlatForward.Create(0, new TARGET(), 0.011, ActIsda, Context);
        FlatForward fts = FlatForward.Create(0, new TARGET(), 0.012, ActIsda, Context);

        // build a vol surface
        var surface = new BlackVolatilitySurfaceDelta(
            RefDate, dates, putDeltas, callDeltas, hasAtm, blackVolMatrix, ActIsda, new TARGET(), spot, dts, fts);

        // ask for volatility at lots of points, should be constVol at every point
        // make sure we ask for vols outside 25D and 2Y
        foreach (double t in new[] { 0.25, 0.5, 1.0, 1.5, 2.0, 2.5, 10.0 })
        {
            for (double k = 0.5; k < 2.0; k += 0.05)
            {
                double vol = surface.BlackVol(t, k);
                CheckClose(vol, constVol, 1e-12);
            }
        }
    }

    [Fact]
    public void TestBlackVolSurfaceDeltaNonConstantVol()
    {
        // Testing BlackVolatilitySurfaceDelta with non constant vol surface...

        BlackVolatilitySurfaceDelta surface = Surface(
            DeltaVolQuoteAtmType.AtmDeltaNeutral,
            BlackVolatilitySurfaceDeltaSmileInterpolationMethod.Linear,
            BlackVolTimeExtrapolationType.FlatVolatility);

        // Testing ATM strike
        double atmStrike = 1.18;

        // Moved: the case's curves are floating from 1 January 2010, a TARGET holiday, so dated 4 January; QuantLib
        // reads them at the surface's times, nanoql at the instants: QuantLib's own surface on the curves restated on
        // the surface's clock gives these values; QuantLib's cached ones are its read on the floating curves
        // (Divergences/DeltaSurfaceMismatchedTimesTests.cs)

        // ask for volatility at 1M (present in the matrix)
        SmileSection smile1M = surface.BlackVolSmile(RefDate + new Period(1, TimeUnit.Months));
        CheckClose(smile1M.Volatility(atmStrike), 0.130080610797, 1e-8);

        // ask for volatility at 15D (using time interpolation) should be the same as the 1M
        SmileSection smile15D = surface.BlackVolSmile(RefDate + new Period(15, TimeUnit.Days));
        CheckClose(smile15D.Volatility(atmStrike), 0.130039151425, 1e-8);

        // ask for volatility at 3M (using time interpolation)
        SmileSection smile3M = surface.BlackVolSmile(RefDate + new Period(3, TimeUnit.Months));
        CheckClose(smile3M.Volatility(atmStrike), 0.115040988757, 1e-8);

        // ask for volatility at 6M for "extreme" strikes (using strike extrapolation)
        double lowStrike = 1.10;
        double highStrike = 1.30;

        SmileSection smile6M = surface.BlackVolSmile(RefDate + new Period(6, TimeUnit.Months));
        CheckClose(smile6M.Volatility(lowStrike), 0.141183696549, 1e-8);
        CheckClose(smile6M.Volatility(highStrike), 0.136248311920, 1e-8);
    }

    [Fact]
    public void TestTimeExtrapolation()
    {
        // Testing time extrapolation of BlackVolatilitySurfaceDelta...

        double atmStrike = 1.18;
        Date twoYears = RefDate + new Period(2, TimeUnit.Years), threeYears = RefDate + new Period(3, TimeUnit.Years);

        // Moved: the case's curves are floating from 1 January 2010, a TARGET holiday, so dated 4 January; QuantLib
        // reads them at the surface's times, nanoql at the instants: QuantLib's own surface on the curves restated on
        // the surface's clock gives these values; QuantLib's cached ones are its read on the floating curves
        // (Divergences/DeltaSurfaceMismatchedTimesTests.cs)

        // flat volatility extrapolation

        BlackVolatilitySurfaceDelta surface1 = Surface(
            DeltaVolQuoteAtmType.AtmSpot,
            BlackVolatilitySurfaceDeltaSmileInterpolationMethod.Linear,
            BlackVolTimeExtrapolationType.FlatVolatility);

        CheckClose(surface1.BlackVol(twoYears, atmStrike), 0.095, 1e-8);
        CheckClose(surface1.BlackVol(twoYears, atmStrike - 0.1), 0.116862197394, 1e-8);
        CheckClose(surface1.BlackVol(twoYears, atmStrike + 0.1), 0.114348263176, 1e-8);
        CheckClose(surface1.BlackVol(threeYears, atmStrike), 0.095, 1e-8);
        CheckClose(surface1.BlackVol(threeYears, atmStrike - 0.1), 0.116862197394, 1e-8);
        CheckClose(surface1.BlackVol(threeYears, atmStrike + 0.1), 0.114348263176, 1e-8);

        // linear variance extrapolation

        BlackVolatilitySurfaceDelta surface2 = Surface(
            DeltaVolQuoteAtmType.AtmSpot,
            BlackVolatilitySurfaceDeltaSmileInterpolationMethod.Linear,
            BlackVolTimeExtrapolationType.LinearVariance);

        CheckClose(surface2.BlackVol(twoYears, atmStrike), 0.095, 1e-8);
        CheckClose(surface2.BlackVol(twoYears, atmStrike - 0.1), 0.116862197394, 1e-8);
        CheckClose(surface2.BlackVol(twoYears, atmStrike + 0.1), 0.114348263176, 1e-8);
        CheckClose(surface2.BlackVol(threeYears, atmStrike), 0.09327379053, 1e-8);
        CheckClose(surface2.BlackVol(threeYears, atmStrike - 0.1), 0.111755949786, 1e-8);
        CheckClose(surface2.BlackVol(threeYears, atmStrike + 0.1), 0.111252023750, 1e-8);

        // delegate to underlying interpolator (also linear variance)

        BlackVolatilitySurfaceDelta surface3 = Surface(
            DeltaVolQuoteAtmType.AtmSpot,
            BlackVolatilitySurfaceDeltaSmileInterpolationMethod.Linear,
            BlackVolTimeExtrapolationType.UseInterpolator).EnableExtrapolation();

        CheckClose(surface3.BlackVol(twoYears, atmStrike), 0.095, 1e-8);
        CheckClose(surface3.BlackVol(twoYears, atmStrike - 0.1), 0.116862197394, 1e-8);
        CheckClose(surface3.BlackVol(twoYears, atmStrike + 0.1), 0.114348263176, 1e-8);
        CheckClose(surface3.BlackVol(threeYears, atmStrike), 0.09327379053, 1e-8);
        CheckClose(surface3.BlackVol(threeYears, atmStrike - 0.1), 0.111755949786, 1e-8);
        CheckClose(surface3.BlackVol(threeYears, atmStrike + 0.1), 0.111252023750, 1e-8);
    }

    [Fact]
    public void TestSmileInterpolation()
    {
        // Testing smile interpolation of BlackVolatilitySurfaceDelta...

        double atmStrike = 1.18;
        Date sixMonths = RefDate + new Period(6, TimeUnit.Months);

        // just cache some values to check that they're not insane and that they don't change unexpectedly

        // Moved: the case's curves are floating from 1 January 2010, a TARGET holiday, so dated 4 January; QuantLib
        // reads them at the surface's times, nanoql at the instants: QuantLib's own surface on the curves restated on
        // the surface's clock gives these values; QuantLib's cached ones are its read on the floating curves
        // (Divergences/DeltaSurfaceMismatchedTimesTests.cs)

        SmileSection smile = Surface(
            DeltaVolQuoteAtmType.AtmSpot,
            BlackVolatilitySurfaceDeltaSmileInterpolationMethod.Linear,
            BlackVolTimeExtrapolationType.FlatVolatility).BlackVolSmile(sixMonths);
        CheckClose(smile.Volatility(atmStrike), 0.11, 1e-8);
        CheckClose(smile.Volatility(atmStrike - 0.1), 0.148882189245, 1e-8);
        CheckClose(smile.Volatility(atmStrike + 0.1), 0.132590836455, 1e-8);
        CheckClose(smile.Volatility(smile.MinStrike() - 0.1), 0.178882189245, 1e-8);
        CheckClose(smile.Volatility(smile.MaxStrike() + 0.1), 0.147590836455, 1e-8);
        CheckClose(smile.Volatility(smile.MinStrike() - 0.5), 0.334410946227, 1e-8);
        CheckClose(smile.Volatility(smile.MaxStrike() + 0.5), 0.237954182275, 1e-8);

        smile = Surface(
            DeltaVolQuoteAtmType.AtmSpot,
            BlackVolatilitySurfaceDeltaSmileInterpolationMethod.NaturalCubic,
            BlackVolTimeExtrapolationType.FlatVolatility).BlackVolSmile(sixMonths);
        CheckClose(smile.Volatility(atmStrike), 0.11, 1e-8);
        CheckClose(smile.Volatility(atmStrike - 0.1), 0.152933981038, 1e-8);
        CheckClose(smile.Volatility(atmStrike + 0.1), 0.135414276783, 1e-8);
        CheckClose(smile.Volatility(smile.MinStrike() - 0.1), 0.165666032880, 1e-8);
        CheckClose(smile.Volatility(smile.MaxStrike() + 0.1), 0.133265942292, 1e-8);

        // cubic being cubic
        CheckClose(smile.Volatility(smile.MinStrike() - 0.5), 0.0, 1e-8);
        CheckClose(smile.Volatility(smile.MaxStrike() + 0.5), 0.0, 1e-8);

        smile = Surface(
            DeltaVolQuoteAtmType.AtmSpot,
            BlackVolatilitySurfaceDeltaSmileInterpolationMethod.FinancialCubic,
            BlackVolTimeExtrapolationType.FlatVolatility).BlackVolSmile(sixMonths);
        CheckClose(smile.Volatility(atmStrike), 0.11, 1e-8);
        CheckClose(smile.Volatility(atmStrike - 0.1), 0.152933981038, 1e-8);
        CheckClose(smile.Volatility(atmStrike + 0.1), 0.135414276783, 1e-8);
        CheckClose(smile.Volatility(smile.MinStrike() - 0.1), 0.165666032880, 1e-8);
        CheckClose(smile.Volatility(smile.MaxStrike() + 0.1), 0.133265942292, 1e-8);
        CheckClose(smile.Volatility(smile.MinStrike() - 0.5), 0.0, 1e-8);
        CheckClose(smile.Volatility(smile.MaxStrike() + 0.5), 0.0, 1e-8);

        smile = Surface(
            DeltaVolQuoteAtmType.AtmSpot,
            BlackVolatilitySurfaceDeltaSmileInterpolationMethod.CubicSpline,
            BlackVolTimeExtrapolationType.FlatVolatility).BlackVolSmile(sixMonths);
        CheckClose(smile.Volatility(atmStrike), 0.11, 1e-8);
        CheckClose(smile.Volatility(atmStrike - 0.1), 0.152325165032, 1e-8);
        CheckClose(smile.Volatility(atmStrike + 0.1), 0.136144466904, 1e-8);
        CheckClose(smile.Volatility(smile.MinStrike() - 0.1), 0.167651872217, 1e-8);
        CheckClose(smile.Volatility(smile.MaxStrike() + 0.1), 0.129561278838, 1e-8);
        CheckClose(smile.Volatility(smile.MinStrike() - 0.5), 0.0, 1e-8);
        CheckClose(smile.Volatility(smile.MaxStrike() + 0.5), 0.0, 1e-8);
    }

    [Fact]
    public void TestSmileSectionWithAtmLevel()
    {
        // Testing SmileSection from a vol surface that overrides atmLevel(Time)...

        // BlackVolatilitySurfaceDelta carries spot + yield curves and overrides
        // atmLevel(Time), so the SmileSection returned by smileSection() has a
        // usable atmLevel() and its optionPrice() works without throwing.

        DayCounter dc = ActIsda;

        Date[] dates = [new(1, Month.January, 2011), new(1, Month.January, 2012)];
        double[] putDeltas = [-0.25];
        double[] callDeltas = [0.25];
        var volMatrix = new Matrix(2, 2, 0.10);

        double spot = 1.0;
        var dts = new FlatForward(RefDate, 0.011, dc);
        var fts = new FlatForward(RefDate, 0.012, dc);

        var surface = new BlackVolatilitySurfaceDelta(
            RefDate, dates, putDeltas, callDeltas, false, volMatrix, dc, new TARGET(), spot, dts, fts);

        var maturity = new Date(1, Month.July, 2011);
        SmileSection smile = surface.SmileSection(maturity);

        // atmLevel matches spot * df_q / df_r (FX-style forward)
        double expectedFwd = spot * fts.Discount(maturity) / dts.Discount(maturity);
        double atm = smile.AtmLevel()!.Value;
        double tolerance = 1.0e-12;
        Assert.False(
            Math.Abs(atm - expectedFwd) > tolerance,
            $"smile atmLevel mismatch\n    calculated: {atm:F12}\n    expected  : {expectedFwd:F12}");

        // optionPrice() works (without the atmLevel(Time) override it would
        // throw a QL_REQUIRE) and matches the Black formula at the forward.
        double df = dts.Discount(maturity);
        double callPrice = smile.OptionPrice(expectedFwd, OptionType.Call, df);
        double T = surface.TimeFromReference(maturity);
        double vol = smile.Volatility(expectedFwd);
        double expectedCall = BlackFormulaFunctions.BlackFormula(OptionType.Call, expectedFwd, expectedFwd, vol * Math.Sqrt(T), df);
        Assert.False(
            Math.Abs(callPrice - expectedCall) > tolerance,
            $"smile optionPrice mismatch\n    calculated: {callPrice:F12}\n    expected  : {expectedCall:F12}");
    }

    // The 4 x 3 surface of the cases after the first: 1.18 spot, 2% domestic and 3.5% foreign floating curves.
    private static BlackVolatilitySurfaceDelta Surface(
        DeltaVolQuoteAtmType atmType,
        BlackVolatilitySurfaceDeltaSmileInterpolationMethod method,
        BlackVolTimeExtrapolationType timeExtrapolation)
    {
        var vols = new Matrix(4, 3);
        double[][] rows =
        [
            [0.15, 0.13, 0.135], // 1M
            [0.14, 0.11, 0.125], // 6M
            [0.13, 0.10, 0.12], // 1Y
            [0.125, 0.095, 0.115], // 2Y
        ];
        for (int i = 0; i < 4; ++i)
        {
            for (int j = 0; j < 3; ++j)
            {
                vols[i, j] = rows[i][j];
            }
        }

        Date[] dates =
        [
            RefDate + new Period(1, TimeUnit.Months),
            RefDate + new Period(6, TimeUnit.Months),
            RefDate + new Period(1, TimeUnit.Years),
            RefDate + new Period(2, TimeUnit.Years),
        ];
        double[] putDeltas = [-0.25];
        double[] callDeltas = [0.25];
        bool hasAtm = true;

        double spot = 1.18;
        FlatForward dts = FlatForward.Create(0, new TARGET(), 0.02, ActIsda, Context);
        FlatForward fts = FlatForward.Create(0, new TARGET(), 0.035, ActIsda, Context);

        return new BlackVolatilitySurfaceDelta(
            RefDate,
            dates,
            putDeltas,
            callDeltas,
            hasAtm,
            vols,
            ActIsda,
            new TARGET(),
            spot,
            dts,
            fts,
            DeltaVolQuoteDeltaType.Spot,
            atmType,
            null,
            method,
            false,
            timeExtrapolation);
    }

    // QL_CHECK_CLOSE, Boost's BOOST_CHECK_CLOSE: |a - b| within the tolerance, in percent, of both values.
    private static void CheckClose(double a, double b, double tolerancePercent)
    {
        double diff = Math.Abs(a - b);
        Assert.True(
            diff <= tolerancePercent / 100.0 * Math.Abs(a) && diff <= tolerancePercent / 100.0 * Math.Abs(b),
            $"difference between {a} and {b} exceeds {tolerancePercent}%");
    }
}
