// SPDX-FileCopyrightText: Copyright (C) 2017, 2018 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/andreasenhugevolatilityinterpl.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class AndreasenHugeVolatilityInterplTests
{
    // QuantLib's test runner's evaluation date (quantlibglobalfixture.cpp:62-63)
    private static readonly Date RunnerDate = new(16, Month.September, 2015);

    private const double QL_EPSILON = 2.220446049250313E-16;

    [Fact]
    public void TestAndreasenHugePut()
    {
        // Testing Andreasen-Huge example with Put calibration...

        CalibrationData data = AndreasenHugeExampleData();

        var expected = new CalibrationResults(
            AndreasenHugeVolatilityInterplCalibrationType.Put,
            AndreasenHugeVolatilityInterplInterpolationType.CubicSpline,
            0.0015,
            0.00035,
            0.0020,
            0.00035);

        TestAndreasenHugeVolatilityInterpolation(data, expected);
    }

    [Fact]
    public void TestAndreasenHugeCall()
    {
        // Testing Andreasen-Huge example with Call calibration...

        CalibrationData data = AndreasenHugeExampleData();

        var expected = new CalibrationResults(
            AndreasenHugeVolatilityInterplCalibrationType.Call,
            AndreasenHugeVolatilityInterplInterpolationType.CubicSpline,
            0.0015,
            0.00035,
            0.0015,
            0.00035);

        TestAndreasenHugeVolatilityInterpolation(data, expected);
    }

    [Fact]
    public void TestAndreasenHugeCallPut()
    {
        // Testing Andreasen-Huge example with instantaneous Call and Put calibration...

        CalibrationData data = AndreasenHugeExampleData();

        var expected = new CalibrationResults(
            AndreasenHugeVolatilityInterplCalibrationType.CallPut,
            AndreasenHugeVolatilityInterplInterpolationType.CubicSpline,
            0.0015,
            0.00035,
            0.0015,
            0.00035);

        TestAndreasenHugeVolatilityInterpolation(data, expected);
    }

    [Fact]
    public void TestLinearInterpolation()
    {
        // Testing Andreasen-Huge example with linear interpolation...

        CalibrationData data = AndreasenHugeExampleData();

        var expected = new CalibrationResults(
            AndreasenHugeVolatilityInterplCalibrationType.CallPut,
            AndreasenHugeVolatilityInterplInterpolationType.Linear,
            0.0020,
            0.00015,
            0.0040,
            0.00035);

        TestAndreasenHugeVolatilityInterpolation(data, expected);
    }

    [Fact]
    public void TestPiecewiseConstantInterpolation()
    {
        // Testing Andreasen-Huge example with piecewise constant interpolation...

        CalibrationData data = AndreasenHugeExampleData();

        var expected = new CalibrationResults(
            AndreasenHugeVolatilityInterplCalibrationType.CallPut,
            AndreasenHugeVolatilityInterplInterpolationType.PiecewiseConstant,
            0.0025,
            0.00025,
            0.0040,
            0.00035);

        TestAndreasenHugeVolatilityInterpolation(data, expected);
    }

    [Fact]
    public void TestTimeDependentInterestRates()
    {
        // Testing Andreasen-Huge volatility interpolation with time dependent interest rates and dividend yield...

        CalibrationData data = AndreasenHugeExampleData();

        DayCounter dc = data.RTS.DayCounter;
        Date today = data.RTS.ReferenceDate;
        var context = new EvaluationContext { EvaluationDate = today };

        double[] r = [0.0167, 0.023, 0.03234, 0.034, 0.038, 0.042, 0.047, 0.053];
        double[] q = [0.01, 0.011, 0.013, 0.014, 0.02, 0.025, 0.067, 0.072];

        Date[] dates =
        [
            today,
            today + new Period(41, TimeUnit.Days),
            today + new Period(75, TimeUnit.Days),
            today + new Period(165, TimeUnit.Days),
            today + new Period(256, TimeUnit.Days),
            today + new Period(345, TimeUnit.Days),
            today + new Period(524, TimeUnit.Days),
            today + new Period(2190, TimeUnit.Days),
        ];

        var rTS = new ZeroCurve(dates, r, dc);
        var qTS = new ZeroCurve(dates, q, dc);

        CalibrationData origData = AndreasenHugeExampleData();
        (VanillaOption Option, double Volatility)[] calibrationSet = [.. origData.CalibrationSet];

        double spot = origData.Spot;

        var hestonModel = new HestonModel(new HestonProcess(rTS, qTS, spot, 0.09, 2.0, 0.09, 0.4, -0.75));

        for (int i = 0; i < calibrationSet.Length; ++i)
        {
            VanillaOption option = calibrationSet[i].Option;

            var payoff = (PlainVanillaPayoff)option.Payoff!;

            double strike = payoff.Strike;
            OptionType optionType = payoff.OptionType;

            double t = dc.YearFraction(today, option.Exercise.LastDate());

            double discount = rTS.Discount(t);
            double fwd = spot * qTS.Discount(t) / discount;

            double npv = AnalyticHestonEngine.Calculate(
                option,
                context,
                hestonModel,
                AnalyticHestonEngineComplexLogFormula.AndersenPiterbarg,
                FourierIntegration.DiscreteTrapezoid(128)).NPV!.Value;

            double impliedVol = BlackFormulaFunctions.BlackFormulaImpliedStdDevLiRS(
                optionType, strike, fwd, npv, discount, 0.0, null, 1.0, 1e-12) / Math.Sqrt(t);

            calibrationSet[i] = (option, impliedVol);
        }

        var irData = new CalibrationData(spot, rTS, qTS, calibrationSet);

        var expected = new CalibrationResults(
            AndreasenHugeVolatilityInterplCalibrationType.CallPut,
            AndreasenHugeVolatilityInterplInterpolationType.CubicSpline,
            0.0020,
            0.0003,
            0.0020,
            0.0004);

        TestAndreasenHugeVolatilityInterpolation(irData, expected);
    }

    [Fact]
    public void TestSingleOptionCalibration()
    {
        // Testing Andreasen-Huge volatility interpolation with a single option...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(4, Month.January, 2018);

        var rTS = new FlatForward(today, 0.025, dc);
        var qTS = new FlatForward(today, 0.085, dc);

        double strike = 10.0;
        double vol = 0.3;
        Date maturity = today + new Period(1, TimeUnit.Years);
        double spot = strike;

        (VanillaOption, double)[] calibrationSet =
        [
            (new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(maturity)), vol),
        ];

        AndreasenHugeVolatilityInterplInterpolationType[] interpl =
        [
            AndreasenHugeVolatilityInterplInterpolationType.Linear,
            AndreasenHugeVolatilityInterplInterpolationType.CubicSpline,
            AndreasenHugeVolatilityInterplInterpolationType.PiecewiseConstant,
        ];

        AndreasenHugeVolatilityInterplCalibrationType[] calibrationType =
        [
            AndreasenHugeVolatilityInterplCalibrationType.Put,
            AndreasenHugeVolatilityInterplCalibrationType.Call,
            AndreasenHugeVolatilityInterplCalibrationType.CallPut,
        ];

        foreach (AndreasenHugeVolatilityInterplInterpolationType i in interpl)
        {
            foreach (AndreasenHugeVolatilityInterplCalibrationType j in calibrationType)
            {
                var andreasenHugeVolInterplation = new AndreasenHugeVolatilityInterpl(calibrationSet, spot, rTS, qTS, i, j, 50);

                var volatilityAdapter = new AndreasenHugeVolatilityAdapter(andreasenHugeVolInterplation);

                double calculated = volatilityAdapter.BlackVol(maturity, strike);
                double expected = vol;

                Assert.False(
                    Math.Abs(calculated - expected) > 1e-4,
                    $"Failed to reproduce single option calibration\n    calculated: {calculated}\n    expected:   {expected}");
            }
        }
    }

    [Fact]
    public void TestArbitrageFree()
    {
        // Testing Andreasen-Huge volatility interpolation gives arbitrage free prices...

        CalibrationData[] data = [BorovkovaExampleData(), ArbitrageData()];

        foreach (CalibrationData i in data)
        {
            double spot = i.Spot;
            YieldTermStructure rTS = i.RTS;
            YieldTermStructure qTS = i.QTS;

            DayCounter dc = rTS.DayCounter;
            Date today = rTS.ReferenceDate;

            var andreasenHugeVolInterplation = new AndreasenHugeVolatilityInterpl(
                i.CalibrationSet,
                spot,
                rTS,
                qTS,
                AndreasenHugeVolatilityInterplInterpolationType.CubicSpline,
                AndreasenHugeVolatilityInterplCalibrationType.CallPut,
                5000);

            var volatilityAdapter = new AndreasenHugeVolatilityAdapter(andreasenHugeVolInterplation);

            for (double m = -0.7; m < 0.7; m += 0.05)
            {
                for (int weeks = 6; weeks < 52; ++weeks)
                {
                    Date maturityDate = today + new Period(weeks, TimeUnit.Weeks);

                    double t = dc.YearFraction(today, maturityDate);

                    double fwd = spot * qTS.Discount(t) / rTS.Discount(t);

                    // J. Gatheral, Arbitrage-free SVI volatility surfaces
                    // http://mfe.baruch.cuny.edu/wp-content/uploads/2013/01/OsakaSVI2012.pdf
                    const double eps = 0.025;
                    double k = fwd * Math.Exp(m);
                    double km = fwd * Math.Exp(m - eps);
                    double kp = fwd * Math.Exp(m + eps);

                    double w = volatilityAdapter.BlackVariance(t, k, true);
                    double w_p = volatilityAdapter.BlackVariance(t, kp, true);
                    double w_m = volatilityAdapter.BlackVariance(t, km, true);

                    double w1 = (w_p - w_m) / (2 * eps);
                    double w2 = (w_p + w_m - (2 * w)) / (eps * eps);

                    double g_k = Squared(1 - (m * w1 / (2 * w))) - (w1 * w1 / 4 * ((1 / w) + 0.25)) + (0.5 * w2);

                    Assert.False(
                        g_k < 0,
                        $"No-arbitrage condition g_k >= 0 failed\n    strike:  {k}\n    forward: {fwd}\n    time:    {t}\n    g_k:    {g_k}");

                    const double deltaT = 1.0 / 365.0;
                    double fwdpt = spot * qTS.Discount(t + deltaT) / rTS.Discount(t + deltaT);

                    double kpt = fwdpt * Math.Exp(m);
                    double w_pt = volatilityAdapter.BlackVariance(t + deltaT, kpt, true);

                    double w_t = (w_pt - w) / deltaT;
                    Assert.False(
                        w_t < -1e-8,
                        $"No-arbitrage condition w_t >= 0 failed\n    strike:  {k}\n    forward: {fwd}\n    time:    {t}\n    w        {w}\n    w_t:     {w_t}");
                }
            }
        }
    }

    [Fact]
    public void TestBarrierOptionPricing()
    {
        // Testing Barrier option pricing with Andreasen-Huge local volatility surface...

        var today = new Date(4, Month.January, 2018);
        var context = new EvaluationContext { EvaluationDate = RunnerDate };
        DayCounter dc = new Actual365Fixed();

        YieldTermStructure rTS = new FlatForward(today, 0.01, dc);
        YieldTermStructure qTS = new FlatForward(today, 0.03, dc);

        const double Spot = 100;
        var hestonModel = new HestonModel(new HestonProcess(rTS, qTS, Spot, 0.04, 2.0, 0.04, 0.4, -0.75));

        BlackVolTermStructure hestonVol = new HestonBlackVolSurface(hestonModel);

        var dupireLocalVolProcess = new GeneralizedBlackScholesProcess(Spot, qTS, rTS, hestonVol);

        double[] strikes = [25, 50, 75, 90, 100, 110, 125, 150, 200, 400];
        int[] maturityMonths = [1, 3, 6, 9, 12];

        var calibrationSet = new List<(VanillaOption Option, double Volatility)>();

        foreach (double strike in strikes)
        {
            foreach (int maturityMonth in maturityMonths)
            {
                Date maturityDate = today + new Period(maturityMonth, TimeUnit.Months);
                double t = dc.YearFraction(today, maturityDate);

                double vol = hestonVol.BlackVol(t, strike);

                double mn = Math.Log(Spot / strike) / Math.Sqrt(t);

                if (Math.Abs(mn) < 3.07 * vol)
                {
                    calibrationSet.Add((
                        new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(maturityDate)),
                        vol));
                }
            }
        }

        var andreasenHugeVolInterplation = new AndreasenHugeVolatilityInterpl([.. calibrationSet], Spot, rTS, qTS);

        var localVolAdapter = new AndreasenHugeLocalVolAdapter(andreasenHugeVolInterplation);

        var andreasenHugeLocalVolProcess = new GeneralizedBlackScholesProcess(Spot, qTS, rTS, hestonVol, localVolAdapter);

        const double Strike = 120.0;
        const double Barrier = 80.0;
        const double Rebate = 0.0;
        Date maturity = today + new Period(1, TimeUnit.Years);
        const BarrierType BarrierType = BarrierType.DownOut;

        var barrierOption = new BarrierOption(
            BarrierType, Barrier, Rebate, new PlainVanillaPayoff(OptionType.Put, Strike), new EuropeanExercise(maturity));

        double dupireNPV = FdBlackScholesBarrierEngine.Calculate(
            barrierOption, context, dupireLocalVolProcess, default, 50, 100, 0, FdmSchemeDesc.Douglas(), true, 0.2).NPV!.Value;

        double andreasenHugeNPV = FdBlackScholesBarrierEngine.Calculate(
            barrierOption, context, andreasenHugeLocalVolProcess, default, 200, 400, 0, FdmSchemeDesc.Douglas(), true, 0.25).NPV!.Value;

        const double Tol = 0.15;
        double diff = Math.Abs(andreasenHugeNPV - dupireNPV);

        Assert.False(
            diff > Tol,
            "failed to reproduce barrier prices with Andreasen-Huge local volatility surface"
            + $"\n    Andreasen-Huge price: {andreasenHugeNPV}\n    Dupire formula price: {dupireNPV}"
            + $"\n    diff:                 {diff}\n    tolerance:            {Tol}");
        Assert.True(dupireNPV > 1.0, $"the barrier option prices {dupireNPV}");
    }

    [Fact]
    public void TestPeterAndFabiensExample()
    {
        // Testing Peter's and Fabien's SABR example...

        // http://chasethedevil.github.io/post/andreasen-huge-extrapolation/

        (CalibrationData data, double[] parameter) = SabrData();

        var andreasenHugeVolInterplation = new AndreasenHugeVolatilityInterpl(data.CalibrationSet, data.Spot, data.RTS, data.QTS);

        var volAdapter = new AndreasenHugeVolatilityAdapter(andreasenHugeVolInterplation);

        double alpha = parameter[0];
        double beta = parameter[1];
        double nu = parameter[2];
        double rho = parameter[3];
        double forward = parameter[4];
        double maturity = parameter[5];

        for (double strike = 0.02; strike < 0.06; strike += 0.001)
        {
            double sabrVol = SabrFunctions.SabrVolatility(strike, forward, maturity, alpha, beta, nu, rho);

            double ahVol = volAdapter.BlackVol(maturity, strike, true);

            const double tol = 0.0005;
            double diff = Math.Abs(sabrVol - ahVol);

            Assert.False(
                double.IsNaN(ahVol) || diff > 0.005,
                "failed to reproduce SABR volatility with Andreasen-Huge interpolation"
                + $"\n    Andreasen-Huge vol: {ahVol}\n    SABR volatility:    {sabrVol}\n    diff:               {diff}\n    tolerance:          {tol}");
        }
    }

    [Fact]
    public void TestDifferentOptimizers()
    {
        // Testing different optimizer for Andreasen-Huge volatility interpolation...

        CalibrationData data = SabrData().Data;

        OptimizationMethod[] optimizationMethods =
        [
            new LevenbergMarquardt(),
            new BFGS(),
            new Simplex(0.2),
        ];

        foreach (OptimizationMethod optimizationMethod in optimizationMethods)
        {
            double avgError = new AndreasenHugeVolatilityInterpl(
                    data.CalibrationSet,
                    data.Spot,
                    data.RTS,
                    data.QTS,
                    AndreasenHugeVolatilityInterplInterpolationType.CubicSpline,
                    AndreasenHugeVolatilityInterplCalibrationType.Call,
                    400,
                    null,
                    null,
                    optimizationMethod)
                .CalibrationError().Avg;

            Assert.False(
                double.IsNaN(avgError) || avgError > 0.0001,
                "failed to calibrate Andreasen-Huge volatility interpolation with different optimizera"
                + $"\n    calibration error: {avgError}");
        }
    }

    [Fact]
    public void TestMovingReferenceDate()
    {
        // Testing that reference date of adapter surface moves along with evaluation date...

        // nanoql's structures are immutable: the curve, the interpolation and the adapters are built again at the new
        // evaluation date, where QuantLib's follow the date through the curve's handle
        var today = new Date(4, Month.January, 2018);

        DayCounter dc = new Actual365Fixed();
        Date maturity = today + new Period(1, TimeUnit.Months);

        const double s0 = 100.0;
        const double impliedVol = 0.2;
        const double tol = 1e-8;

        (AndreasenHugeVolatilityAdapter, AndreasenHugeLocalVolAdapter) Build(Date evaluationDate)
        {
            var context = new EvaluationContext { EvaluationDate = evaluationDate };
            YieldTermStructure ts = FlatForward.Create(0, new NullCalendar(), 0.04, dc, context);

            (VanillaOption, double)[] calibrationSet =
            [
                (new VanillaOption(new PlainVanillaPayoff(OptionType.Call, s0), new EuropeanExercise(maturity)), impliedVol),
            ];

            var andreasenHugeVolInterplation = new AndreasenHugeVolatilityInterpl(calibrationSet, s0, ts, ts);
            return (new AndreasenHugeVolatilityAdapter(andreasenHugeVolInterplation, tol),
                new AndreasenHugeLocalVolAdapter(andreasenHugeVolInterplation));
        }

        (AndreasenHugeVolatilityAdapter volatilityAdapter, AndreasenHugeLocalVolAdapter localVolAdapter) = Build(today);

        Date volRefDate = volatilityAdapter.ReferenceDate;
        Date localRefDate = localVolAdapter.ReferenceDate;

        Assert.False(
            volRefDate != today || localRefDate != today,
            $"reference dates should match today's date\n    today                     : {today}"
            + $"\n    local vol reference date  : {localRefDate}\n    implied vol reference date: {volRefDate}");

        var modToday = new Date(15, Month.January, 2018);
        (volatilityAdapter, localVolAdapter) = Build(modToday);

        Date modVolRefDate = volatilityAdapter.ReferenceDate;
        Date modLocalRefDate = localVolAdapter.ReferenceDate;

        Assert.False(
            modVolRefDate != modToday || modLocalRefDate != modToday,
            $"reference dates should match modified today's date\n    today                     : {modToday}"
            + $"\n    local vol reference date  : {modLocalRefDate}\n    implied vol reference date: {modVolRefDate}");

        // test update method
        double modImpliedVol = volatilityAdapter.BlackVol(maturity, s0, true);

        double diff = Math.Abs(modImpliedVol - impliedVol);
        Assert.False(
            diff > 10 * tol,
            $"modified implied vol should match direct calculation\n    implied vol         : {impliedVol}"
            + $"\n    modified implied vol: {modImpliedVol}\n    difference          : {diff}\n    tolerance           : {tol}");
    }

    [Fact]
    public void TestFlatVolCalibration()
    {
        // Testing Andreasen-Huge example with flat volatility surface...

        var reference = new Date(1, Month.November, 2019);
        DayCounter dc = new Actual365Fixed();

        Date[] expiries =
        [
            reference + new Period(1, TimeUnit.Months), reference + new Period(3, TimeUnit.Months),
            reference + new Period(6, TimeUnit.Months), reference + new Period(9, TimeUnit.Months),
            reference + new Period(1, TimeUnit.Years), reference + new Period(2, TimeUnit.Years),
            reference + new Period(3, TimeUnit.Years), reference + new Period(4, TimeUnit.Years),
            reference + new Period(5, TimeUnit.Years), reference + new Period(7, TimeUnit.Years),
            reference + new Period(10, TimeUnit.Years),
        ];

        double[] moneyness = [0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.1, 1.2, 1.3, 1.4, 1.5];

        double spot = 100.0;
        var rTS = new FlatForward(reference, 0.02, dc);
        var qTS = new FlatForward(reference, 0.0, dc);
        double vol = 0.18;

        var calibrationSet = new List<(VanillaOption, double)>();
        foreach (Date expiry in expiries)
        {
            var exercise = new EuropeanExercise(expiry);

            double t = rTS.TimeFromReference(expiry);
            double fwd = spot / rTS.Discount(t) * qTS.Discount(t);

            foreach (double m in moneyness)
            {
                double strike = fwd * m;
                double mn = Math.Log(fwd / strike) / Math.Sqrt(t);

                if (Math.Abs(mn) < 3.72 * vol)
                {
                    var option = new VanillaOption(
                        new PlainVanillaPayoff((strike > fwd) ? OptionType.Call : OptionType.Put, strike), exercise);

                    calibrationSet.Add((option, vol));
                }
            }
        }

        var flatVolData = new CalibrationData(spot, rTS, qTS, [.. calibrationSet]);

        var expected = new CalibrationResults(
            AndreasenHugeVolatilityInterplCalibrationType.Put,
            AndreasenHugeVolatilityInterplInterpolationType.CubicSpline,
            1e-10,
            1e-10,
            0.0006,
            0.0002);

        TestAndreasenHugeVolatilityInterpolation(flatVolData, expected);
    }

    [Fact]
    public void TestAndreasenHugeVolatilityAdapterAtmLevel()
    {
        // Testing atmLevel on AndreasenHugeVolatilityAdapter...

        var today = new Date(4, Month.March, 2026);
        DayCounter dc = new Actual365Fixed();

        const double s0 = 100.0;
        const double r = 0.05, q = 0.02;

        var rTS = new FlatForward(today, r, dc);
        var qTS = new FlatForward(today, q, dc);
        const double vol = 0.20;

        var exercise = new EuropeanExercise(today + new Period(1, TimeUnit.Years));
        (VanillaOption, double)[] calibrationSet =
        [
            .. new[] { 80.0, 100.0, 120.0 }.Select(strike => (new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), exercise), vol)),
        ];

        var interpl = new AndreasenHugeVolatilityInterpl(calibrationSet, s0, rTS, qTS);
        var surface = new AndreasenHugeVolatilityAdapter(interpl);

        const double tol = 1e-12;
        foreach (double t in new[] { 0.25, 0.5, 1.0 })
        {
            double expected = s0 * qTS.Discount(t) / rTS.Discount(t);
            double calculated = surface.AtmLevel(t)!.Value;
            Assert.False(
                Math.Abs(calculated - expected) > tol,
                $"AndreasenHugeVolatilityAdapter::atmLevel mismatch\n   t:          {t}\n   calculated: {calculated}"
                + $"\n   expected:   {expected}\n   diff:       {calculated - expected}\n   tolerance:  {tol}");
        }
    }

    private static CalibrationData AndreasenHugeExampleData()
    {
        // This is the example market data from the original paper
        // Andreasen J., Huge B., 2010. Volatility Interpolation
        // https://ssrn.com/abstract=1694972

        double spot = 2772.7;

        double[] maturityTimes = [0.025, 0.101, 0.197, 0.274, 0.523, 0.772, 1.769, 2.267, 2.784, 3.781, 4.778, 5.774];

        double[][] raw =
        [
            [0.5131, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.3366, 0.3291, 0.0000, 0.0000],
            [0.5864, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.3178, 0.3129, 0.3008, 0.0000],
            [0.6597, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.3019, 0.2976, 0.2975, 0.0000],
            [0.7330, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.2863, 0.2848, 0.2848, 0.0000],
            [0.7697, 0.0000, 0.0000, 0.0000, 0.3262, 0.3079, 0.3001, 0.2843, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000],
            [0.8063, 0.0000, 0.0000, 0.0000, 0.3058, 0.2936, 0.2876, 0.2753, 0.2713, 0.2711, 0.2711, 0.2722, 0.2809],
            [0.8430, 0.0000, 0.0000, 0.0000, 0.2887, 0.2798, 0.2750, 0.2666, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000],
            [0.8613, 0.3365, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000],
            [0.8796, 0.3216, 0.2906, 0.2764, 0.2717, 0.2663, 0.2637, 0.2575, 0.2555, 0.2580, 0.2585, 0.2611, 0.2693],
            [0.8979, 0.3043, 0.2797, 0.2672, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000],
            [0.9163, 0.2880, 0.2690, 0.2578, 0.2557, 0.2531, 0.2519, 0.2497, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000],
            [0.9346, 0.2724, 0.2590, 0.2489, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000],
            [0.9529, 0.2586, 0.2488, 0.2405, 0.2407, 0.2404, 0.2411, 0.2418, 0.2410, 0.2448, 0.2469, 0.2501, 0.2584],
            [0.9712, 0.2466, 0.2390, 0.2329, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000],
            [0.9896, 0.2358, 0.2300, 0.2253, 0.2269, 0.2284, 0.2299, 0.2347, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000],
            [1.0079, 0.2247, 0.2213, 0.2184, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000],
            [1.0262, 0.2159, 0.2140, 0.2123, 0.2142, 0.2173, 0.2198, 0.2283, 0.2275, 0.2322, 0.2384, 0.2392, 0.2486],
            [1.0445, 0.2091, 0.2076, 0.2069, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000],
            [1.0629, 0.2056, 0.2024, 0.2025, 0.2039, 0.2074, 0.2104, 0.2213, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000],
            [1.0812, 0.2045, 0.1982, 0.1984, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000],
            [1.0995, 0.2025, 0.1959, 0.1944, 0.1962, 0.1988, 0.2022, 0.2151, 0.2161, 0.2219, 0.2269, 0.2305, 0.2399],
            [1.1178, 0.1933, 0.1929, 0.1920, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000],
            [1.1362, 0.0000, 0.0000, 0.0000, 0.1902, 0.1914, 0.1950, 0.2091, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000],
            [1.1728, 0.0000, 0.0000, 0.0000, 0.1885, 0.1854, 0.1888, 0.2039, 0.2058, 0.2122, 0.2186, 0.2223, 0.2321],
            [1.2095, 0.0000, 0.0000, 0.0000, 0.1867, 0.1811, 0.1839, 0.1990, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000],
            [1.2461, 0.0000, 0.0000, 0.0000, 0.1871, 0.1785, 0.1793, 0.1945, 0.0000, 0.2054, 0.2103, 0.2164, 0.2251],
            [1.3194, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.1988, 0.2054, 0.2105, 0.2190],
            [1.3927, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.1930, 0.2002, 0.2054, 0.2135],
            [1.4660, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.1849, 0.1964, 0.2012, 0.0000],
        ];

        DayCounter dc = new Actual365Fixed();
        var today = new Date(1, Month.March, 2010);

        var rTS = new FlatForward(today, 0.0, dc);
        var qTS = new FlatForward(today, 0.0, dc);

        var calibrationSet = new List<(VanillaOption, double)>();

        foreach (double[] i in raw)
        {
            double strike = spot * i[0];

            for (int j = 1; j < i.Length; ++j)
            {
                if (i[j] > QL_EPSILON)
                {
                    Date maturity = today + new Period((int)(365 * maturityTimes[j - 1]), TimeUnit.Days);

                    double impliedVol = i[j];

                    calibrationSet.Add((
                        new VanillaOption(
                            new PlainVanillaPayoff((strike < spot) ? OptionType.Put : OptionType.Call, strike),
                            new EuropeanExercise(maturity)),
                        impliedVol));
                }
            }
        }

        return new CalibrationData(spot, rTS, qTS, [.. calibrationSet]);
    }

    private static void TestAndreasenHugeVolatilityInterpolation(CalibrationData data, CalibrationResults expected)
    {
        YieldTermStructure rTS = data.RTS;
        YieldTermStructure qTS = data.QTS;

        DayCounter dc = rTS.DayCounter;
        Date today = rTS.ReferenceDate;
        var context = new EvaluationContext { EvaluationDate = today };

        double spot = data.Spot;

        (VanillaOption Option, double Volatility)[] calibrationSet = data.CalibrationSet;

        var andreasenHugeVolInterplation = new AndreasenHugeVolatilityInterpl(
            calibrationSet, spot, rTS, qTS, expected.InterpolationType, expected.CalibrationType);

        AndreasenHugeCalibrationError error = andreasenHugeVolInterplation.CalibrationError();

        double maxError = error.Max;
        double avgError = error.Avg;

        Assert.False(
            maxError > expected.MaxError || avgError > expected.AvgError,
            $"Failed to reproduce calibration error\n    max calibration error:     {maxError}"
            + $"\n    average calibration error: {avgError}\n    expected max error:        {expected.MaxError}"
            + $"\n    expected average error:    {expected.AvgError}");

        var volatilityAdapter = new AndreasenHugeVolatilityAdapter(andreasenHugeVolInterplation, 1e-12);

        var localVolAdapter = new AndreasenHugeLocalVolAdapter(andreasenHugeVolInterplation);

        var localVolProcess = new GeneralizedBlackScholesProcess(spot, qTS, rTS, volatilityAdapter, localVolAdapter);

        double lvAvgError = 0.0, lvMaxError = 0.0;
        for (int i = 0, n = 0; i < calibrationSet.Length; ++i)
        {
            VanillaOption option = calibrationSet[i].Option;

            var payoff = (PlainVanillaPayoff)option.Payoff!;
            double strike = payoff.Strike;
            OptionType optionType = payoff.OptionType;

            double t = dc.YearFraction(today, option.Exercise.LastDate());

            double expectedVol = calibrationSet[i].Volatility;
            double calculatedVol = volatilityAdapter.BlackVol(t, strike, true);

            double diffVol = Math.Abs(expectedVol - calculatedVol);
            double tol = Math.Max(1e-10, 1.01 * maxError);

            Assert.False(
                diffVol > tol,
                $"Failed to reproduce calibration option price\n    calculated: {calculatedVol}\n    expected:   {expectedVol}"
                + $"\n    difference: {diffVol}\n    tolerance:  {tol}");

            double npv = FdBlackScholesVanillaEngine.Calculate(
                option,
                context,
                localVolProcess,
                tGrid: Math.Max(30, (int)(100 * t)),
                xGrid: 200,
                dampingSteps: 0,
                schemeDesc: FdmSchemeDesc.Douglas(),
                localVol: true).NPV!.Value;

            double discount = rTS.Discount(t);
            double fwd = spot * qTS.Discount(t) / discount;

            double lvImpliedVol = BlackFormulaFunctions.BlackFormulaImpliedStdDevLiRS(
                optionType, strike, fwd, npv, discount, 0.0, null, 1.0, 1e-12) / Math.Sqrt(t);

            double lvError = Math.Abs(lvImpliedVol - expectedVol);

            lvMaxError = Math.Max(lvError, lvMaxError);

            lvAvgError = ((n * lvAvgError) + lvError) / (n + 1);

            ++n;
        }

        Assert.False(
            lvMaxError > expected.LvMaxError || avgError > expected.LvAvgError,
            $"Failed to reproduce local volatility calibration error\n    max calibration error:     {lvMaxError}"
            + $"\n    average calibration error: {lvAvgError}\n    expected max error:        {expected.LvMaxError}"
            + $"\n    expected average error:    {expected.LvAvgError}");
    }

    private static CalibrationData BorovkovaExampleData()
    {
        // see Svetlana Borovkova, Ferry J. Permana
        // Implied volatility in oil markets
        // http://www.researchgate.net/publication/46493859_Implied_volatility_in_oil_markets

        DayCounter dc = new Actual365Fixed();
        var today = new Date(4, Month.January, 2018);

        var rTS = new FlatForward(today, 0.025, dc);
        var qTS = new FlatForward(today, 0.085, dc);

        double spot = 100;

        const double b1 = 0.35;
        const double b2 = 0.03;
        const double b3 = 0.005;
        const double b4 = -0.02;
        const double b5 = -0.005;

        double[] strikes = [35, 50, 75, 100, 125, 150, 200, 300];
        int[] maturityMonths = [1, 3, 6, 9, 12, 15, 18, 24];

        var calibrationSet = new List<(VanillaOption, double)>();

        foreach (double strike in strikes)
        {
            foreach (int maturityMonth in maturityMonths)
            {
                Date maturityDate = today + new Period(maturityMonth, TimeUnit.Months);
                double t = dc.YearFraction(today, maturityDate);

                double fwd = spot * qTS.Discount(t) / rTS.Discount(t);
                double mn = Math.Log(fwd / strike) / Math.Sqrt(t);

                double vol = b1 + (b2 * mn) + (b3 * mn * mn) + (b4 * t) + (b5 * mn * t);

                if (Math.Abs(mn) < 3.71 * vol)
                {
                    calibrationSet.Add((
                        new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(maturityDate)), vol));
                }
            }
        }

        return new CalibrationData(spot, rTS, qTS, [.. calibrationSet]);
    }

    private static CalibrationData ArbitrageData()
    {
        DayCounter dc = new Actual365Fixed();
        var today = new Date(4, Month.January, 2018);

        var rTS = new FlatForward(today, 0.13, dc);
        var qTS = new FlatForward(today, 0.03, dc);

        double spot = 100;

        double[] strikes = [100, 100, 100, 150];
        int[] maturities = [1, 3, 6, 6];
        double[] vols = [0.25, 0.35, 0.05, 0.35];
        var calibrationSet = new List<(VanillaOption, double)>();

        for (int i = 0; i < strikes.Length; ++i)
        {
            double strike = strikes[i];
            Date maturityDate = today + new Period(maturities[i], TimeUnit.Months);
            double vol = vols[i];

            calibrationSet.Add((
                new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(maturityDate)), vol));
        }

        return new CalibrationData(spot, rTS, qTS, [.. calibrationSet]);
    }

    private static (CalibrationData Data, double[] Parameter) SabrData()
    {
        DayCounter dc = new Actual365Fixed();
        var today = new Date(4, Month.January, 2018);

        const double alpha = 0.15;
        const double beta = 0.8;
        const double nu = 0.5;
        const double rho = -0.48;
        const double forward = 0.03;
        const int maturityInYears = 20;

        Date maturityDate = today + new Period(maturityInYears, TimeUnit.Years);
        double maturity = dc.YearFraction(today, maturityDate);

        var calibrationSet = new List<(VanillaOption, double)>();

        double[] strikes = [0.02, 0.025, 0.03, 0.035, 0.04, 0.05, 0.06];

        foreach (double strike in strikes)
        {
            double vol = SabrFunctions.SabrVolatility(strike, forward, maturity, alpha, beta, nu, rho);

            calibrationSet.Add((
                new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(maturityDate)), vol));
        }

        var rTS = new FlatForward(today, forward, dc);
        var qTS = new FlatForward(today, forward, dc);

        double spot = forward;

        var data = new CalibrationData(spot, rTS, qTS, [.. calibrationSet]);

        double[] parameter = [alpha, beta, nu, rho, forward, maturity];

        return (data, parameter);
    }

    private static double Squared(double x) => x * x;

    private sealed record CalibrationData(
        double Spot, YieldTermStructure RTS, YieldTermStructure QTS, (VanillaOption Option, double Volatility)[] CalibrationSet);

    private sealed record CalibrationResults(
        AndreasenHugeVolatilityInterplCalibrationType CalibrationType,
        AndreasenHugeVolatilityInterplInterpolationType InterpolationType,
        double MaxError,
        double AvgError,
        double LvMaxError,
        double LvAvgError);
}
