// SPDX-FileCopyrightText: Copyright (C) 2026 Richard Amaya
// SPDX-FileCopyrightText: Copyright (C) 2026 Yassine Idyiahia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/piecewiseblackvariancesurface.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testSabrEquivalence, which waits for S-xeq's SABRVolTermStructure
// Not ported: testObserver (observer notifications, which an immutable surface has no use for, R1); testSmileSectionRequiresSharedPtr (QuantLib's shared_ptr requirement, which a .NET reference has no counterpart for)

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class PiecewiseBlackVarianceSurfaceTests
{
    private static readonly Date Today = new(15, Month.January, 2026);
    private static readonly DayCounter Dc = new Actual365Fixed();

    [Fact]
    public void TestExactRepricing()
    {
        // Testing exact repricing at input tenors...

        Date d1 = Today + new Period(3, TimeUnit.Months);
        Date d2 = Today + new Period(6, TimeUnit.Months);
        Date d3 = Today + new Period(1, TimeUnit.Years);

        double vol1 = 0.20, vol2 = 0.25, vol3 = 0.30;

        Date[] dates = [d1, d2, d3];
        SmileSection[] sections =
        [
            new FlatSmileSection(d1, vol1, Dc, Today),
            new FlatSmileSection(d2, vol2, Dc, Today),
            new FlatSmileSection(d3, vol3, Dc, Today),
        ];

        var surface = new PiecewiseBlackVarianceSurface(Today, dates, sections, Dc);

        double tol = 1.0e-12;
        double strike = 100.0;

        for (int i = 0; i < dates.Length; ++i)
        {
            double expected = sections[i].Variance(strike);
            double calculated = surface.BlackVariance(dates[i], strike);
            double diff = Math.Abs(calculated - expected);
            Assert.False(
                diff > tol,
                $"failed to reprice at tenor {i}\n    date:       {dates[i]}\n    calculated: {calculated:E16}"
                + $"\n    expected:   {expected:E16}\n    difference: {diff:E16}\n    tolerance:  {tol:E16}");
        }
    }

    [Fact]
    public void TestInterpolation()
    {
        // Testing linear variance interpolation between tenors...

        Date d1 = Today + new Period(6, TimeUnit.Months);
        Date d2 = Today + new Period(1, TimeUnit.Years);

        double vol1 = 0.20, vol2 = 0.30;

        Date[] dates = [d1, d2];
        SmileSection[] sections = [new FlatSmileSection(d1, vol1, Dc, Today), new FlatSmileSection(d2, vol2, Dc, Today)];

        PiecewiseBlackVarianceSurface surface = new PiecewiseBlackVarianceSurface(Today, dates, sections, Dc).EnableExtrapolation();

        double strike = 100.0;
        double t1 = Dc.YearFraction(Today, d1);
        double t2 = Dc.YearFraction(Today, d2);
        double var1 = vol1 * vol1 * t1;
        double var2 = vol2 * vol2 * t2;

        // test at midpoint between the two tenors
        double tMid = 0.5 * (t1 + t2);
        Date dMid = Today + (int)Math.Round(tMid * 365, MidpointRounding.AwayFromZero);
        double tMidActual = Dc.YearFraction(Today, dMid);
        double alpha = (tMidActual - t1) / (t2 - t1);
        double expectedVar = var1 + ((var2 - var1) * alpha);
        double calculated = surface.BlackVariance(dMid, strike);
        double diff = Math.Abs(calculated - expectedVar);
        double tol = 1.0e-12;

        Assert.False(
            diff > tol,
            $"failed to interpolate at midpoint\n    date:       {dMid}\n    time:       {tMidActual}"
            + $"\n    calculated: {calculated:E16}\n    expected:   {expectedVar:E16}\n    difference: {diff:E16}");

        // test before the first tenor (interpolation from (0,0))
        Date dEarly = Today + new Period(1, TimeUnit.Months);
        double tEarly = Dc.YearFraction(Today, dEarly);
        double expectedEarly = var1 * tEarly / t1;
        double calculatedEarly = surface.BlackVariance(dEarly, strike);
        diff = Math.Abs(calculatedEarly - expectedEarly);

        Assert.False(
            diff > tol,
            $"failed to interpolate before first tenor\n    date:       {dEarly}\n    time:       {tEarly}"
            + $"\n    calculated: {calculatedEarly:E16}\n    expected:   {expectedEarly:E16}\n    difference: {diff:E16}");
    }

    [Fact]
    public void TestBlackVolDerivation()
    {
        // Testing blackVol derivation from blackVariance...

        Date d1 = Today + new Period(6, TimeUnit.Months);
        double vol1 = 0.25;

        var surface = new PiecewiseBlackVarianceSurface(Today, [d1], [new FlatSmileSection(d1, vol1, Dc, Today)], Dc);

        double strike = 100.0;
        double var = surface.BlackVariance(d1, strike);
        double t = Dc.YearFraction(Today, d1);
        double expectedVol = Math.Sqrt(var / t);
        double calculatedVol = surface.BlackVol(d1, strike);
        double diff = Math.Abs(calculatedVol - expectedVol);
        double tol = 1.0e-12;

        Assert.False(
            diff > tol,
            $"blackVol inconsistent with blackVariance\n    blackVariance: {var:E16}\n    time:          {t}"
            + $"\n    expected vol:  {expectedVol:E16}\n    calculated:    {calculatedVol:E16}\n    difference:    {diff:E16}");
    }

    [Fact]
    public void TestExtrapolation()
    {
        // Testing flat-vol extrapolation beyond last tenor...

        Date d1 = Today + new Period(6, TimeUnit.Months);
        Date d2 = Today + new Period(1, TimeUnit.Years);
        double vol1 = 0.20, vol2 = 0.30;

        PiecewiseBlackVarianceSurface surface = new PiecewiseBlackVarianceSurface(
            Today, [d1, d2], [new FlatSmileSection(d1, vol1, Dc, Today), new FlatSmileSection(d2, vol2, Dc, Today)], Dc)
            .EnableExtrapolation();

        double strike = 100.0;
        double t2 = Dc.YearFraction(Today, d2);
        double var2 = vol2 * vol2 * t2;

        // query beyond last tenor: flat variance rate extrapolation
        Date dBeyond = Today + new Period(2, TimeUnit.Years);
        double tBeyond = Dc.YearFraction(Today, dBeyond);
        double expectedVar = var2 * tBeyond / t2;
        double calculated = surface.BlackVariance(dBeyond, strike, true);
        double diff = Math.Abs(calculated - expectedVar);
        double tol = 1.0e-12;

        Assert.False(
            diff > tol,
            $"flat-vol extrapolation failed\n    date:       {dBeyond}\n    time:       {tBeyond}"
            + $"\n    calculated: {calculated:E16}\n    expected:   {expectedVar:E16}\n    difference: {diff:E16}");

        // vol should be constant beyond last tenor
        double expectedVol = vol2;
        double calculatedVol = surface.BlackVol(dBeyond, strike, true);
        diff = Math.Abs(calculatedVol - expectedVol);

        Assert.False(
            diff > tol,
            $"flat-vol extrapolation: vol not constant\n    date:       {dBeyond}\n    calculated: {calculatedVol:E16}"
            + $"\n    expected:   {expectedVol:E16}\n    difference: {diff:E16}");
    }

    [Fact]
    public void TestStrikeDependence()
    {
        // Testing strike-dependent smile interpolation...

        Date d1 = Today + new Period(1, TimeUnit.Years);
        double t1 = Dc.YearFraction(Today, d1);

        // build a skewed smile: lower vol at high strikes
        double[] strikes = [80.0, 100.0, 120.0];
        double vol80 = 0.30, vol100 = 0.25, vol120 = 0.20;
        double sqrtT = Math.Sqrt(t1);
        double[] stdDevs = [vol80 * sqrtT, vol100 * sqrtT, vol120 * sqrtT];

        var section = new LinearInterpolatedSmileSection(d1, strikes, stdDevs, 100.0, Dc, Today);

        var surface = new PiecewiseBlackVarianceSurface(Today, [d1], [section], Dc);

        double tol = 1.0e-12;

        // check that variance matches at each strike point
        foreach (double strike in strikes)
        {
            double expected = section.Variance(strike);
            double calculated = surface.BlackVariance(d1, strike);
            double diff = Math.Abs(calculated - expected);
            Assert.False(
                diff > tol,
                $"strike-dependent repricing failed at strike {strike}\n    calculated: {calculated:E16}"
                + $"\n    expected:   {expected:E16}\n    difference: {diff:E16}");
        }

        // check different vol at different strikes
        double var80 = surface.BlackVariance(d1, 80.0);
        double var120 = surface.BlackVariance(d1, 120.0);

        Assert.False(
            var80 <= var120,
            $"expected higher variance at lower strike (skew)\n    var(80):  {var80:E16}\n    var(120): {var120:E16}");
    }

    [Fact]
    public void TestMultiTenorSmileInterpolation()
    {
        // Testing interpolation between tenors with different smiles...

        Date d1 = Today + new Period(6, TimeUnit.Months);
        Date d2 = Today + new Period(1, TimeUnit.Years);
        double t1 = Dc.YearFraction(Today, d1);
        double t2 = Dc.YearFraction(Today, d2);

        // tenor 1: moderate skew
        double[] strikes = [80.0, 100.0, 120.0];
        double sqrtT1 = Math.Sqrt(t1);
        double[] stdDevs1 = [0.25 * sqrtT1, 0.20 * sqrtT1, 0.18 * sqrtT1];
        var section1 = new LinearInterpolatedSmileSection(d1, strikes, stdDevs1, 100.0, Dc, Today);

        // tenor 2: steeper skew, higher overall level
        double sqrtT2 = Math.Sqrt(t2);
        double[] stdDevs2 = [0.35 * sqrtT2, 0.25 * sqrtT2, 0.20 * sqrtT2];
        var section2 = new LinearInterpolatedSmileSection(d2, strikes, stdDevs2, 100.0, Dc, Today);

        var surface = new PiecewiseBlackVarianceSurface(Today, [d1, d2], [section1, section2], Dc);

        // query at midpoint time, at each strike
        Date dMid = Today + new Period(9, TimeUnit.Months);
        double tMid = Dc.YearFraction(Today, dMid);
        double alpha = (tMid - t1) / (t2 - t1);
        double tol = 1.0e-12;

        foreach (double strike in strikes)
        {
            double var1 = section1.Variance(strike);
            double var2 = section2.Variance(strike);
            double expected = var1 + ((var2 - var1) * alpha);
            double calculated = surface.BlackVariance(dMid, strike);
            double diff = Math.Abs(calculated - expected);
            Assert.False(
                diff > tol,
                $"multi-tenor smile interpolation failed at strike {strike}\n    time:       {tMid}"
                + $"\n    calculated: {calculated:E16}\n    expected:   {expected:E16}\n    difference: {diff:E16}");
        }

        // verify the skew shape is preserved at the midpoint:
        // lower strikes should have higher variance
        double varMid80 = surface.BlackVariance(dMid, 80.0);
        double varMid100 = surface.BlackVariance(dMid, 100.0);
        double varMid120 = surface.BlackVariance(dMid, 120.0);

        Assert.False(
            varMid80 <= varMid100 || varMid100 <= varMid120,
            $"skew not preserved at interpolated tenor\n    var(80):  {varMid80:E16}\n    var(100): {varMid100:E16}"
            + $"\n    var(120): {varMid120:E16}");

        // calendar arbitrage check: total variance must be non-decreasing in time
        foreach (double strike in strikes)
        {
            double var_d1 = surface.BlackVariance(d1, strike);
            double var_dMid = surface.BlackVariance(dMid, strike);
            double var_d2 = surface.BlackVariance(d2, strike);

            Assert.False(
                var_d1 > var_dMid + tol,
                $"calendar arbitrage: variance decreased from d1 to dMid at strike {strike}"
                + $"\n    var(d1):   {var_d1:E16}\n    var(dMid): {var_dMid:E16}");
            Assert.False(
                var_dMid > var_d2 + tol,
                $"calendar arbitrage: variance decreased from dMid to d2 at strike {strike}"
                + $"\n    var(dMid): {var_dMid:E16}\n    var(d2):   {var_d2:E16}");
        }

        // butterfly arbitrage check: d^2w/dK^2 >= 0 at the interpolated tenor
        double dK = 1.0;
        double[] butterflyStrikes = [85.0, 90.0, 95.0, 100.0, 105.0, 110.0, 115.0];

        foreach (double k in butterflyStrikes)
        {
            double w = surface.BlackVariance(dMid, k);
            double w_p = surface.BlackVariance(dMid, k + dK);
            double w_m = surface.BlackVariance(dMid, k - dK);

            double d2wdK2 = (w_p + w_m - (2.0 * w)) / (dK * dK);

            Assert.False(
                d2wdK2 < -1.0e-10,
                $"butterfly arbitrage: d^2w/dK^2 < 0 at midpoint strike {k}\n    w(K-dK): {w_m:E16}\n    w(K):    {w:E16}"
                + $"\n    w(K+dK): {w_p:E16}\n    d2w/dK2: {d2wdK2:E16}");
        }
    }

    [Fact]
    public void TestMakeFromGrid()
    {
        // Testing makeFromGrid factory method...

        Date d1 = Today + new Period(6, TimeUnit.Months);
        Date d2 = Today + new Period(1, TimeUnit.Years);

        // strikes x dates matrix (rows = strikes, columns = dates)
        double[] strikes = [80.0, 100.0, 120.0];
        var blackVols = new Matrix(3, 2);
        blackVols[0, 0] = 0.25;
        blackVols[0, 1] = 0.30; // K=80
        blackVols[1, 0] = 0.20;
        blackVols[1, 1] = 0.25; // K=100
        blackVols[2, 0] = 0.18;
        blackVols[2, 1] = 0.20; // K=120

        Date[] dates = [d1, d2];

        PiecewiseBlackVarianceSurface surface = PiecewiseBlackVarianceSurface.MakeFromGrid(Today, dates, strikes, blackVols, Dc);

        double tol = 1.0e-12;

        // check that each input vol is exactly repriced
        for (int j = 0; j < dates.Length; ++j)
        {
            double t = Dc.YearFraction(Today, dates[j]);
            for (int i = 0; i < strikes.Length; ++i)
            {
                double expectedVar = blackVols[i, j] * blackVols[i, j] * t;
                double calculated = surface.BlackVariance(dates[j], strikes[i]);
                double diff = Math.Abs(calculated - expectedVar);
                Assert.False(
                    diff > tol,
                    $"makeFromGrid failed to reprice\n    date:       {dates[j]}\n    strike:     {strikes[i]}"
                    + $"\n    input vol:  {blackVols[i, j]}\n    calculated: {calculated:E16}\n    expected:   {expectedVar:E16}");
            }
        }

        // verify skew is preserved (higher var at lower strike)
        double var80 = surface.BlackVariance(d1, 80.0);
        double var120 = surface.BlackVariance(d1, 120.0);

        Assert.False(var80 <= var120, $"makeFromGrid: skew not preserved\n    var(80):  {var80:E16}\n    var(120): {var120:E16}");

        // verify interpolation between strikes (K=90, between K=80 and K=100)
        double t1 = Dc.YearFraction(Today, d1);
        double vol90 = 0.5 * (blackVols[0, 0] + blackVols[1, 0]); // linear interp
        double expectedVar90 = vol90 * vol90 * t1;
        double calculated90 = surface.BlackVariance(d1, 90.0);
        double diff90 = Math.Abs(calculated90 - expectedVar90);

        Assert.False(
            diff90 > tol,
            $"makeFromGrid: between-strike interpolation failed\n    strike:     90.0\n    calculated: {calculated90:E16}"
            + $"\n    expected:   {expectedVar90:E16}\n    difference: {diff90:E16}");

        // verify interpolation between tenors at a grid strike
        double t2 = Dc.YearFraction(Today, d2);
        Date dMid = Today + new Period(9, TimeUnit.Months);
        double tMid = Dc.YearFraction(Today, dMid);
        double alpha = (tMid - t1) / (t2 - t1);
        double var1_100 = blackVols[1, 0] * blackVols[1, 0] * t1;
        double var2_100 = blackVols[1, 1] * blackVols[1, 1] * t2;
        double expectedVarMid = var1_100 + ((var2_100 - var1_100) * alpha);
        double calculatedMid = surface.BlackVariance(dMid, 100.0);
        double diffMid = Math.Abs(calculatedMid - expectedVarMid);

        Assert.False(
            diffMid > tol,
            $"makeFromGrid: between-tenor interpolation failed\n    date:       {dMid}\n    strike:     100.0"
            + $"\n    calculated: {calculatedMid:E16}\n    expected:   {expectedVarMid:E16}\n    difference: {diffMid:E16}");
    }

    [Fact]
    public void TestConstructorValidation()
    {
        // Testing constructor validation checks...

        Date d1 = Today + new Period(6, TimeUnit.Months);
        Date d2 = Today + new Period(1, TimeUnit.Years);
        double vol = 0.25;
        var section1 = new FlatSmileSection(d1, vol, Dc, Today);
        var section2 = new FlatSmileSection(d2, vol, Dc, Today);

        // empty dates
        ExpectError("at least one date", () => new PiecewiseBlackVarianceSurface(Today, [], [], Dc));

        // mismatched sizes (2 dates, 1 section)
        ExpectError("mismatch", () => new PiecewiseBlackVarianceSurface(Today, [d1, d2], [section1], Dc));

        // first date on reference date
        ExpectError(
            "must be after reference",
            () => new PiecewiseBlackVarianceSurface(Today, [Today], [new FlatSmileSection(Today, vol, Dc, Today)], Dc));

        // unsorted dates
        ExpectError("sorted and unique", () => new PiecewiseBlackVarianceSurface(Today, [d2, d1], [section2, section1], Dc));

        // duplicate dates
        ExpectError("sorted and unique", () => new PiecewiseBlackVarianceSurface(Today, [d1, d1], [section1, section1], Dc));

        // null smile section
        ExpectError("null smile section", () => new PiecewiseBlackVarianceSurface(Today, [d1], [null!], Dc));
    }

    [Fact]
    public void TestMakeFromGridValidation()
    {
        // Testing makeFromGrid validation checks...

        Date d1 = Today + new Period(6, TimeUnit.Months);
        Date[] dates = [d1];
        double[] strikes = [80.0, 100.0, 120.0];

        // row mismatch (3 strikes but 2-row matrix)
        var wrongRows = new Matrix(2, 1);
        wrongRows[0, 0] = 0.20;
        wrongRows[1, 0] = 0.25;

        ExpectError("strikes", () => PiecewiseBlackVarianceSurface.MakeFromGrid(Today, dates, strikes, wrongRows, Dc));

        // column mismatch (1 date but 2-column matrix)
        var wrongCols = new Matrix(3, 2);
        for (int i = 0; i < 3; ++i)
        {
            for (int j = 0; j < 2; ++j)
            {
                wrongCols[i, j] = 0.20;
            }
        }

        ExpectError("dates", () => PiecewiseBlackVarianceSurface.MakeFromGrid(Today, dates, strikes, wrongCols, Dc));
    }

    [Fact]
    public void TestAccessors()
    {
        // Testing dayCounter, maxDate, minStrike, maxStrike...

        Date d1 = Today + new Period(6, TimeUnit.Months);
        Date d2 = Today + new Period(1, TimeUnit.Years);
        double vol = 0.25;

        var surface = new PiecewiseBlackVarianceSurface(
            Today, [d1, d2], [new FlatSmileSection(d1, vol, Dc, Today), new FlatSmileSection(d2, vol, Dc, Today)], Dc);

        Assert.True(
            surface.DayCounter.Name == Dc.Name,
            $"dayCounter mismatch\n    expected: {Dc.Name}\n    got:      {surface.DayCounter.Name}");
        Assert.True(surface.MaxDate == d2, $"maxDate mismatch\n    expected: {d2}\n    got:      {surface.MaxDate}");
        Assert.True(surface.MinStrike() == -double.MaxValue, "minStrike not QL_MIN_REAL");
        Assert.True(surface.MaxStrike() == double.MaxValue, "maxStrike not QL_MAX_REAL");
    }

    [Fact]
    public void TestZeroTimeVariance()
    {
        // Testing blackVariance at reference date (t=0)...

        Date d1 = Today + new Period(6, TimeUnit.Months);

        var surface = new PiecewiseBlackVarianceSurface(Today, [d1], [new FlatSmileSection(d1, 0.25, Dc, Today)], Dc);

        double calculated = surface.BlackVariance(Today, 100.0);

        Assert.True(calculated == 0.0, $"blackVariance at t=0 should be exactly 0.0\n    calculated: {calculated:E16}");
    }

    [Fact]
    public void TestSingleTenorSurface()
    {
        // Testing surface with a single tenor...

        Date d1 = Today + new Period(1, TimeUnit.Years);
        double vol = 0.25;
        double t1 = Dc.YearFraction(Today, d1);

        PiecewiseBlackVarianceSurface surface =
            new PiecewiseBlackVarianceSurface(Today, [d1], [new FlatSmileSection(d1, vol, Dc, Today)], Dc).EnableExtrapolation();

        double strike = 100.0;
        double var1 = vol * vol * t1;
        double tol = 1.0e-12;

        // exact at tenor
        double calculated = surface.BlackVariance(d1, strike);
        double diff = Math.Abs(calculated - var1);
        Assert.False(
            diff > tol, $"single tenor: failed at exact tenor\n    calculated: {calculated:E16}\n    expected:   {var1:E16}");

        // before tenor: linear from (0,0)
        Date dEarly = Today + new Period(3, TimeUnit.Months);
        double tEarly = Dc.YearFraction(Today, dEarly);
        double expectedEarly = var1 * tEarly / t1;
        double calculatedEarly = surface.BlackVariance(dEarly, strike);
        diff = Math.Abs(calculatedEarly - expectedEarly);
        Assert.False(
            diff > tol,
            $"single tenor: failed before tenor\n    calculated: {calculatedEarly:E16}\n    expected:   {expectedEarly:E16}");

        // after tenor: flat vol extrapolation
        Date dLate = Today + new Period(2, TimeUnit.Years);
        double tLate = Dc.YearFraction(Today, dLate);
        double expectedLate = var1 * tLate / t1;
        double calculatedLate = surface.BlackVariance(dLate, strike, true);
        diff = Math.Abs(calculatedLate - expectedLate);
        Assert.False(
            diff > tol,
            $"single tenor: failed after tenor\n    calculated: {calculatedLate:E16}\n    expected:   {expectedLate:E16}");
    }

    [Fact]
    public void TestRaggedStrikeGrids()
    {
        // Testing surface with ragged strike grids across tenors...

        Date d1 = Today + new Period(6, TimeUnit.Months);
        Date d2 = Today + new Period(1, TimeUnit.Years);
        double t1 = Dc.YearFraction(Today, d1);
        double t2 = Dc.YearFraction(Today, d2);

        // tenor 1: strikes [80, 100, 120] — skewed
        double[] strikes1 = [80.0, 100.0, 120.0];
        double sqrtT1 = Math.Sqrt(t1);
        double[] stdDevs1 = [0.30 * sqrtT1, 0.20 * sqrtT1, 0.18 * sqrtT1];
        var section1 = new LinearInterpolatedSmileSection(d1, strikes1, stdDevs1, 100.0, Dc, Today);

        // tenor 2: strikes [70, 90, 110, 130] — different grid, wider
        double[] strikes2 = [70.0, 90.0, 110.0, 130.0];
        double sqrtT2 = Math.Sqrt(t2);
        double[] stdDevs2 = [0.35 * sqrtT2, 0.25 * sqrtT2, 0.22 * sqrtT2, 0.20 * sqrtT2];
        var section2 = new LinearInterpolatedSmileSection(d2, strikes2, stdDevs2, 100.0, Dc, Today);

        var surface = new PiecewiseBlackVarianceSurface(Today, [d1, d2], [section1, section2], Dc);

        double tol = 1.0e-12;

        // exact repricing at each tenor's own strike grid
        foreach (double i in strikes1)
        {
            double expected = section1.Variance(i);
            double calculated = surface.BlackVariance(d1, i);
            Assert.False(
                Math.Abs(calculated - expected) > tol,
                $"ragged grid: failed to reprice at tenor 1 strike {i}\n    calculated: {calculated:E16}\n    expected:   {expected:E16}");
        }

        foreach (double i in strikes2)
        {
            double expected = section2.Variance(i);
            double calculated = surface.BlackVariance(d2, i);
            Assert.False(
                Math.Abs(calculated - expected) > tol,
                $"ragged grid: failed to reprice at tenor 2 strike {i}\n    calculated: {calculated:E16}\n    expected:   {expected:E16}");
        }

        // strike 75 is inside section 2's grid [70..130] but outside
        // section 1's grid [80..120] — must throw without extrapolation
        double strike = 75.0;
        Date dMid = Today + new Period(9, TimeUnit.Months);
        ExpectError("outside the range", () => surface.BlackVariance(dMid, strike));

        // enable extrapolation — now the query should succeed
        surface = surface.EnableExtrapolation();

        double tMid = Dc.YearFraction(Today, dMid);
        double alpha = (tMid - t1) / (t2 - t1);
        double var1 = section1.Variance(strike);
        double var2 = section2.Variance(strike);
        double expectedMid = var1 + ((var2 - var1) * alpha);
        double calculatedMid = surface.BlackVariance(dMid, strike);
        Assert.False(
            Math.Abs(calculatedMid - expectedMid) > tol,
            $"ragged grid: interpolation at off-grid strike {strike}\n    calculated: {calculatedMid:E16}\n    expected:   {expectedMid:E16}");

        // calendar arbitrage: variance non-decreasing across tenors
        double[] testStrikes = [70.0, 80.0, 90.0, 100.0, 110.0, 120.0, 130.0];
        foreach (double testStrike in testStrikes)
        {
            double var_d1 = surface.BlackVariance(d1, testStrike, true);
            double var_dMid = surface.BlackVariance(dMid, testStrike, true);
            double var_d2 = surface.BlackVariance(d2, testStrike, true);

            Assert.False(
                var_d1 > var_dMid + tol,
                $"ragged grid: calendar arbitrage d1->dMid at strike {testStrike}\n    var(d1):   {var_d1:E16}\n    var(dMid): {var_dMid:E16}");
            Assert.False(
                var_dMid > var_d2 + tol,
                $"ragged grid: calendar arbitrage dMid->d2 at strike {testStrike}\n    var(dMid): {var_dMid:E16}\n    var(d2):   {var_d2:E16}");
        }
    }

    [Fact]
    public void TestSingleSectionConstructor()
    {
        // Testing single-section constructor...

        Date expiry = Today + new Period(6, TimeUnit.Months);
        double vol = 0.20;

        var smile = new FlatSmileSection(expiry, vol, Dc, Today);
        PiecewiseBlackVarianceSurface surface = new PiecewiseBlackVarianceSurface(Today, expiry, smile, Dc).EnableExtrapolation();

        double tol = 1.0e-12;
        double strike = 100.0;

        // Vol should be constant at any maturity (flat vol extrapolation)
        Period[] tenors =
        [
            new(1, TimeUnit.Months), new(3, TimeUnit.Months), new(6, TimeUnit.Months), new(1, TimeUnit.Years),
            new(2, TimeUnit.Years),
        ];
        foreach (Period tenor in tenors)
        {
            Date d = Today + tenor;
            double calculated = surface.BlackVol(d, strike, true);
            Assert.False(
                Math.Abs(calculated - vol) > tol,
                $"single-section constructor vol mismatch at {tenor}\n    calculated: {calculated:F12}\n    expected:   {vol:F12}");
        }
    }

    [Fact]
    public void TestSviSmileSection()
    {
        // Testing with SVI smile section...

        double forward = 100.0;
        Date expiry = Today + new Period(6, TimeUnit.Months);
        double T = Dc.YearFraction(Today, expiry);

        // SVI parameters: a, b, sigma, rho, m
        double[] sviParams = [0.04, 0.1, 0.3, -0.4, 0.0];

        var sviSmile = new SviSmileSection(T, forward, sviParams);

        var surface = new PiecewiseBlackVarianceSurface(Today, expiry, sviSmile, Dc);

        // Verify the surface reproduces the smile at its expiry
        double tol = 1.0e-10;
        double[] strikes = [80.0, 90.0, 100.0, 110.0, 120.0];

        foreach (double strike in strikes)
        {
            double fromSmile = sviSmile.Volatility(strike);
            double fromSurface = surface.BlackVol(expiry, strike);
            Assert.False(
                Math.Abs(fromSmile - fromSurface) > tol,
                $"SVI smile/surface mismatch\n    strike:     {strike}\n    smile:      {fromSmile:F12}\n    surface:    {fromSurface:F12}");
        }

        // Verify smile produces a non-flat surface (SVI has skew)
        double volLow = surface.BlackVol(expiry, 80.0);
        double volAtm = surface.BlackVol(expiry, 100.0);
        double volHigh = surface.BlackVol(expiry, 120.0);

        Assert.False(
            Math.Abs(volLow - volAtm) < 1.0e-6 && Math.Abs(volHigh - volAtm) < 1.0e-6,
            $"SVI surface appears flat - expected a smile\n    vol(80):  {volLow:F6}\n    vol(100): {volAtm:F6}\n    vol(120): {volHigh:F6}");
    }

    [Fact]
    public void TestLocalVolFdPricingFromSabrSmiles()
    {
        // Testing FD local-vol pricing from SABR smile surface...

        var context = new EvaluationContext { EvaluationDate = Today };
        double s0 = 100.0;
        double r = 0.03;
        double q = 0.01;

        var rTS = new FlatForward(Today, r, Dc);
        var qTS = new FlatForward(Today, q, Dc);

        // SABR parameters
        double alpha = 0.3;
        double beta = 0.7;
        double nu = 0.4;
        double rho = -0.4;
        double[] sabrParams = [alpha, beta, nu, rho];

        // Build multi-tenor SABR surface
        Period[] tenors = [new(3, TimeUnit.Months), new(6, TimeUnit.Months), new(1, TimeUnit.Years), new(2, TimeUnit.Years)];
        var dates = new List<Date>();
        var smiles = new List<SmileSection>();

        foreach (Period tenor in tenors)
        {
            Date expiry = Today + tenor;
            double t = Dc.YearFraction(Today, expiry);
            double fwd = s0 * Math.Exp((r - q) * t);
            dates.Add(expiry);
            smiles.Add(new SabrSmileSection(t, fwd, sabrParams));
        }

        PiecewiseBlackVarianceSurface volSurface = new PiecewiseBlackVarianceSurface(Today, dates, smiles, Dc).EnableExtrapolation();

        // Build BS process and FD engine with local vol
        var process = new BlackScholesMertonProcess(s0, qTS, rTS, volSurface);

        // Price European options at various strikes and maturities
        double tol = 0.01; // 1 cent tolerance for FD vs analytic

        double[] strikes = [90.0, 100.0, 110.0];
        Period[] optionTenors = [new(6, TimeUnit.Months), new(1, TimeUnit.Years)];

        foreach (Period tenor in optionTenors)
        {
            Date expiry = Today + tenor;
            var exercise = new EuropeanExercise(expiry);

            foreach (double strike in strikes)
            {
                var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), exercise);

                double analyticPrice = AnalyticEuropeanEngine.Calculate(option, context, process).NPV!.Value;

                // localVol = true
                double fdPrice = FdBlackScholesVanillaEngine.Calculate(
                    option, context, process, tGrid: 100, xGrid: 200, dampingSteps: 0, schemeDesc: FdmSchemeDesc.Douglas(), localVol: true)
                    .NPV!.Value;

                double diff = Math.Abs(fdPrice - analyticPrice);

                Assert.False(
                    diff > tol,
                    $"FD local-vol price deviates from analytic\n    tenor:     {tenor}\n    strike:    {strike}"
                    + $"\n    analytic:  {analyticPrice:F6}\n    FD:        {fdPrice:F6}\n    diff:      {diff:F6}\n    tolerance: {tol:F6}");
            }
        }
    }

    [Fact]
    public void TestSmileSectionFromBlackVolSurface()
    {
        // Testing SmileSection extraction from BlackVolTermStructure...

        // 1. Vol-native surface (default adapter path)
        // BlackConstantVol has no native SmileSection — the base class
        // adapter wraps blackVol() into a SmileSection on the fly.
        double flatVol = 0.20;
        var constVol = new BlackConstantVol(Today, new NullCalendar(), flatVol, Dc);

        Date maturity = Today + new Period(1, TimeUnit.Years);
        SmileSection smile = constVol.SmileSection(maturity);

        double tolerance = 1.0e-12;
        foreach (double strike in new[] { 80.0, 100.0, 120.0 })
        {
            double v = smile.Volatility(strike);
            Assert.False(
                Math.Abs(v - flatVol) > tolerance,
                $"vol-native: failed to reproduce flat vol\n    strike:     {strike}\n    expected:   {flatVol:F12}\n    calculated: {v:F12}");
        }

        // 2. Smile-native surface, at a stored tenor (override path)
        // PiecewiseBlackVarianceSurface stores SmileSection objects and
        // overrides smileSectionImpl() to return them directly at stored tenors.
        Date d1 = Today + new Period(6, TimeUnit.Months);
        Date d2 = Today + new Period(1, TimeUnit.Years);
        double[] strikes = [80.0, 90.0, 100.0, 110.0, 120.0];
        double[] vols1 = [0.30, 0.25, 0.20, 0.22, 0.28];
        double[] vols2 = [0.28, 0.23, 0.19, 0.21, 0.26];
        double sqrtT1 = Math.Sqrt(Dc.YearFraction(Today, d1));
        double sqrtT2 = Math.Sqrt(Dc.YearFraction(Today, d2));

        double[] stdDevs1 = [.. vols1.Select(v => v * sqrtT1)];
        double[] stdDevs2 = [.. vols2.Select(v => v * sqrtT2)];

        var section1 = new LinearInterpolatedSmileSection(d1, strikes, stdDevs1, 100.0, Dc, Today);
        var section2 = new LinearInterpolatedSmileSection(d2, strikes, stdDevs2, 100.0, Dc, Today);
        var surface = new PiecewiseBlackVarianceSurface(Today, [d1, d2], [section1, section2], Dc);

        // At a stored tenor: should return the same SmileSection object (pointer equality)
        SmileSection smile1 = surface.SmileSection(d1);
        Assert.True(
            ReferenceEquals(smile1, section1),
            "at stored tenor: smileSection(d1) did not return the stored SmileSection (pointer mismatch)");

        SmileSection smile2 = surface.SmileSection(d2);
        Assert.True(
            ReferenceEquals(smile2, section2),
            "at stored tenor: smileSection(d2) did not return the stored SmileSection (pointer mismatch)");

        // At a stored tenor: volatilities should match exactly
        foreach (double strike in strikes)
        {
            double surfaceVol = surface.BlackVol(d1, strike);
            double sectionVol = smile1.Volatility(strike);
            Assert.False(
                Math.Abs(surfaceVol - sectionVol) > tolerance,
                $"at stored tenor: vol mismatch\n    strike:      {strike}\n    surface vol: {surfaceVol:F12}\n    section vol: {sectionVol:F12}");
        }

        // 3. Smile-native surface, between tenors (adapter fallback path)
        // Between stored tenors, smileSectionImpl() falls back to the
        // default adapter which queries blackVol() per strike.
        Date dMid = Today + new Period(9, TimeUnit.Months);
        SmileSection smileMid = surface.SmileSection(dMid);

        // Between tenors: should NOT be either stored section (it's an adapter)
        Assert.False(
            ReferenceEquals(smileMid, section1) || ReferenceEquals(smileMid, section2),
            "between tenors: smileSection(dMid) should not return a stored SmileSection");

        // Between tenors: adapter should reproduce blackVol() at each strike
        foreach (double strike in strikes)
        {
            double surfaceVol = surface.BlackVol(dMid, strike);
            double sectionVol = smileMid.Volatility(strike);
            Assert.False(
                Math.Abs(surfaceVol - sectionVol) > tolerance,
                $"between tenors: vol mismatch\n    strike:      {strike}\n    surface vol: {surfaceVol:F12}\n    section vol: {sectionVol:F12}");
        }
    }

    // QuantLib's ExpectedErrorMessage: the call throws, its message containing the text.
    private static void ExpectError(string text, Func<object> f)
    {
        Exception e = Assert.ThrowsAny<Exception>(f);
        Assert.True(e is ArgumentException or NanoQLException, $"unexpected exception type {e.GetType()}");
        Assert.Contains(text, e.Message, StringComparison.Ordinal);
    }
}
