// SPDX-FileCopyrightText: Copyright (C) 2005, 2008 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/batesmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class BatesModelTests
{
    private static readonly HestonModelData[] HestonModels =
    [
        // ADI finite difference schemes for option pricing in the
        // Heston model with correlation, K.J. in t'Hout and S. Foulon,
        new("'t Hout case 1", 0.04, 1.5, 0.04, 0.3, -0.9, 0.025, 0.0),

        // Efficient numerical methods for pricing American options under
        // stochastic volatility, Samuli Ikonen and Jari Toivanen,
        new("Ikonen-Toivanen", 0.0625, 5, 0.16, 0.9, 0.1, 0.1, 0.0),

        // Not-so-complex logarithms in the Heston model,
        // Christian Kahl and Peter Jaeckel
        new("Kahl-Jaeckel", 0.16, 1.0, 0.16, 2.0, -0.8, 0.0, 0.0),

        // self defined test cases
        new("Equity case", 0.07, 2.0, 0.04, 0.55, -0.8, 0.03, 0.035),
    ];

    [Fact]
    public void TestAnalyticVsBlack()
    {
        // Testing analytic Bates engine against Black formula...
        Date settlementDate = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISDA);
        Date exerciseDate = settlementDate + new Period(6, TimeUnit.Months);

        var payoff = new PlainVanillaPayoff(OptionType.Put, 30);
        var exercise = new EuropeanExercise(exerciseDate);

        YieldTermStructure riskFreeTS = FlatRate(0.1, dayCounter, context);
        YieldTermStructure dividendTS = FlatRate(0.04, dayCounter, context);
        Real s0 = 32.0;

        Real yearFraction = dayCounter.YearFraction(settlementDate, exerciseDate);
        Real forwardPrice = s0 * Math.Exp((0.1 - 0.04) * yearFraction);
        Real expected = BlackFormulaFunctions.BlackFormula(
            payoff.OptionType, payoff.Strike, forwardPrice, Math.Sqrt(0.05 * yearFraction)) * Math.Exp(-0.1 * yearFraction);
        const Real v0 = 0.05;
        const Real kappa = 5.0;
        const Real theta = 0.05;
        const Real sigma = 1.0e-4;
        const Real rho = 0.0;
        const Real lambda = 0.0001;
        const Real nu = 0.0;
        const Real delta = 0.0001;

        var option = new VanillaOption(payoff, exercise);

        var process = new BatesProcess(riskFreeTS, dividendTS, s0, v0, kappa, theta, sigma, rho, lambda, nu, delta);

        Real calculated = BatesEngine.Calculate(option, context, new BatesModel(process), 64).NPV!.Value;

        Real tolerance = 2.0e-7;
        Real error = Math.Abs(calculated - expected);
        Assert.False(
            error > tolerance,
            $"failed to reproduce Black price with BatesEngine\n    calculated: {calculated}\n    expected:   {expected}\n    error:      {error}");

        calculated = BatesDetJumpEngine.Calculate(option, context, new BatesDetJumpModel(process, 1.0, 0.0001), 64).NPV!.Value;

        error = Math.Abs(calculated - expected);
        Assert.False(
            error > tolerance,
            $"failed to reproduce Black price with BatesDetJumpEngine\n    calculated: {calculated}\n    expected:   {expected}\n    error:      {error}");

        calculated = BatesDoubleExpEngine.Calculate(
            option, context, new BatesDoubleExpModel(process, 0.0001, 0.0001, 0.0001), 64).NPV!.Value;

        error = Math.Abs(calculated - expected);
        Assert.False(
            error > tolerance,
            $"failed to reproduce Black price with BatesDoubleExpEngine\n    calculated: {calculated}\n    expected:   {expected}\n    error:      {error}");

        calculated = BatesDoubleExpDetJumpEngine.Calculate(
            option, context, new BatesDoubleExpDetJumpModel(process, 0.0001, 0.0001, 0.0001, 0.5, 1.0, 0.0001), 64).NPV!.Value;

        error = Math.Abs(calculated - expected);
        Assert.False(
            error > tolerance,
            $"failed to reproduce Black price with BatesDoubleExpDetJumpEngine\n    calculated: {calculated}\n    expected:   {expected}\n    error:      {error}");
    }

    [Fact]
    public void TestAnalyticAndMcVsJumpDiffusion()
    {
        // Testing analytic Bates engine against Merton-76 engine...
        Date settlementDate = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISDA);

        var payoff = new PlainVanillaPayoff(OptionType.Put, 95);

        YieldTermStructure riskFreeTS = FlatRate(0.1, dayCounter, context);
        YieldTermStructure dividendTS = FlatRate(0.04, dayCounter, context);
        Real s0 = 100;

        Real v0 = 0.0433;
        BlackVolTermStructure volTS = new BlackConstantVol(settlementDate, new NullCalendar(), Math.Sqrt(v0), dayCounter);

        const Real kappa = 0.5;
        Real theta = v0;
        const Real sigma = 1.0e-4;
        const Real rho = 0.0;

        Real jumpIntensity = 2;
        Real meanLogJump = -0.2;
        Real jumpVol = 0.2;

        var batesProcess = new BatesProcess(
            riskFreeTS, dividendTS, s0, v0, kappa, theta, sigma, rho, jumpIntensity, meanLogJump, jumpVol);

        var mertonProcess = new Merton76Process(s0, dividendTS, riskFreeTS, volTS, jumpIntensity, meanLogJump, jumpVol);

        var batesModel = new BatesModel(batesProcess);

        const Real mcTol = 0.1;
        Func<VanillaOption, EvaluationContext, OneAssetOptionResults> mcBatesEngine =
            new MakeMCEuropeanHestonEngine<PseudoRandom, GeneralStatistics>(batesProcess, 1234)
                .WithStepsPerYear(2)
                .WithAntitheticVariate()
                .WithAbsoluteTolerance(mcTol)
                .ToEngine();

        for (int i = 1; i <= 5; i += 2)
        {
            Date exerciseDate = settlementDate + new Period(i, TimeUnit.Years);
            var exercise = new EuropeanExercise(exerciseDate);

            var batesOption = new VanillaOption(payoff, exercise);

            Real calculated = BatesEngine.Calculate(batesOption, context, batesModel, 160).NPV!.Value;

            Real mcCalculated = mcBatesEngine(batesOption, context).NPV!.Value;

            var mertonOption = new EuropeanOption(payoff, exercise);
            Real expected = JumpDiffusionEngine.Calculate(mertonOption, context, mertonProcess, 1e-10, 1000).NPV!.Value;

            Real tolerance = 2e-8;
            Real relError = Math.Abs(calculated - expected) / expected;
            Assert.False(
                relError > tolerance,
                $"failed to reproduce Merton76 price with semi analytic BatesEngine\n    calculated: {calculated:F8}\n    expected:   {expected:F8}\n    rel. error: {relError:F8}\n    tolerance:  {tolerance:F8}");

            Real mcError = Math.Abs(expected - mcCalculated);
            Assert.False(
                mcError > 3 * mcTol,
                $"failed to reproduce Merton76 price with Monte-Carlo BatesEngine\n    calculated: {mcCalculated:F8}\n    expected:   {expected:F8}\n    error: {mcError:F8}\n    tolerance:  {mcTol:F8}");
        }
    }

    [Fact]
    public void TestAnalyticVsMCPricing()
    {
        // Testing analytic Bates engine against Monte-Carlo engine...
        var settlementDate = new Date(30, Month.March, 2007);
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISDA);
        var exerciseDate = new Date(30, Month.March, 2012);

        var payoff = new PlainVanillaPayoff(OptionType.Put, 100);
        var exercise = new EuropeanExercise(exerciseDate);

        foreach (HestonModelData hestonModel in HestonModels)
        {
            YieldTermStructure riskFreeTS = FlatRate(hestonModel.R, dayCounter, context);
            YieldTermStructure dividendTS = FlatRate(hestonModel.Q, dayCounter, context);
            Real s0 = 100;

            var batesProcess = new BatesProcess(
                riskFreeTS, dividendTS, s0, hestonModel.V0, hestonModel.Kappa, hestonModel.Theta, hestonModel.Sigma,
                hestonModel.Rho, 2.0, -0.2, 0.1);

            const Real mcTolerance = 0.5;
            Func<VanillaOption, EvaluationContext, OneAssetOptionResults> mcEngine =
                new MakeMCEuropeanHestonEngine<PseudoRandom, GeneralStatistics>(batesProcess, 1234)
                    .WithStepsPerYear(20)
                    .WithAntitheticVariate()
                    .WithAbsoluteTolerance(mcTolerance)
                    .ToEngine();

            var batesModel = new BatesModel(batesProcess);

            var option = new VanillaOption(payoff, exercise);

            Real calculated = mcEngine(option, context).NPV!.Value;

            Real expected = BatesEngine.Calculate(option, context, batesModel, 160).NPV!.Value;

            Real fdCalculated = FdBatesVanillaEngine.Calculate(option, context, batesModel, 50, 100, 30).NPV!.Value;

            Real mcError = Math.Abs(calculated - expected);
            Assert.False(
                mcError > 3 * mcTolerance,
                $"failed to reproduce Monte-Carlo price for BatesEngine\n    parameter:  {hestonModel.Name}\n    calculated: {calculated:F8}\n    expected:   {expected:F8}\n    error: {mcError:F8}\n    tolerance:  {mcTolerance:F8}");

            const Real fdTolerance = 0.2;
            Real fdError = Math.Abs(fdCalculated - expected);
            Assert.False(
                fdError > fdTolerance,
                $"failed to reproduce PIDE price for BatesEngine\n    parameter:  {hestonModel.Name}\n    calculated: {fdCalculated:F8}\n    expected:   {expected:F8}\n    error: {fdError:F8}\n    tolerance:  {fdTolerance:F8}");
        }
    }

    [Fact]
    public void TestDAXCalibration()
    {
        /* this example is taken from A. Sepp
           Pricing European-Style Options under Jump Diffusion Processes
           with Stochstic Volatility: Applications of Fourier Transform
           http://math.ut.ee/~spartak/papers/stochjumpvols.pdf
        */

        // Testing Bates model calibration using DAX volatility data...
        var settlementDate = new Date(5, Month.July, 2002);
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        DayCounter dayCounter = new Actual365Fixed();
        Calendar calendar = new TARGET();

        int[] t = [13, 41, 75, 165, 256, 345, 524, 703];
        Rate[] r = [0.0357, 0.0349, 0.0341, 0.0355, 0.0359, 0.0368, 0.0386, 0.0401];

        var dates = new List<Date>();
        var rates = new List<Rate>();
        dates.Add(settlementDate);
        rates.Add(0.0357);
        for (int i = 0; i < 8; ++i)
        {
            dates.Add(settlementDate + t[i]);
            rates.Add(r[i]);
        }

        YieldTermStructure riskFreeTS = new ZeroCurve(dates, rates, dayCounter);

        YieldTermStructure dividendTS = new FlatForward(settlementDate, 0.0, dayCounter);

        Volatility[] v =
        [
            0.6625, 0.4875, 0.4204, 0.3667, 0.3431, 0.3267, 0.3121, 0.3121,
            0.6007, 0.4543, 0.3967, 0.3511, 0.3279, 0.3154, 0.2984, 0.2921,
            0.5084, 0.4221, 0.3718, 0.3327, 0.3155, 0.3027, 0.2919, 0.2889,
            0.4541, 0.3869, 0.3492, 0.3149, 0.2963, 0.2926, 0.2819, 0.2800,
            0.4060, 0.3607, 0.3330, 0.2999, 0.2887, 0.2811, 0.2751, 0.2775,
            0.3726, 0.3396, 0.3108, 0.2781, 0.2788, 0.2722, 0.2661, 0.2686,
            0.3550, 0.3277, 0.3012, 0.2781, 0.2781, 0.2661, 0.2661, 0.2681,
            0.3428, 0.3209, 0.2958, 0.2740, 0.2688, 0.2627, 0.2580, 0.2620,
            0.3302, 0.3062, 0.2799, 0.2631, 0.2573, 0.2533, 0.2504, 0.2544,
            0.3343, 0.2959, 0.2705, 0.2540, 0.2504, 0.2464, 0.2448, 0.2462,
            0.3460, 0.2845, 0.2624, 0.2463, 0.2425, 0.2385, 0.2373, 0.2422,
            0.3857, 0.2860, 0.2578, 0.2399, 0.2357, 0.2327, 0.2312, 0.2351,
            0.3976, 0.2860, 0.2607, 0.2356, 0.2297, 0.2268, 0.2241, 0.2320,
        ];

        Real s0 = 4468.17;
        Real[] strike = [3400, 3600, 3800, 4000, 4200, 4400, 4500, 4600, 4800, 5000, 5200, 5400, 5600];

        Real v0 = 0.0433;

        const Real kappa = 1.0;
        Real theta = v0;
        const Real sigma = 1.0;
        const Real rho = 0.0;
        const Real lambda = 1.1098;
        const Real nu = -0.1285;
        const Real delta = 0.1702;

        var process = new BatesProcess(riskFreeTS, dividendTS, s0, v0, kappa, theta, sigma, rho, lambda, nu, delta);

        var batesModel = new BatesModel(process);

        var options = new List<HestonModelHelper>();

        for (int s = 0; s < 13; ++s)
        {
            for (int m = 0; m < 8; ++m)
            {
                var maturity = new Period((int)((t[m] + 3) / 7.0), TimeUnit.Weeks); // round to weeks

                // this is the calibration helper for the bates models
                options.Add(new HestonModelHelper(
                        maturity,
                        calendar,
                        s0,
                        strike[s],
                        v[(s * 8) + m],
                        riskFreeTS,
                        dividendTS,
                        context,
                        BlackCalibrationHelperCalibrationErrorType.ImpliedVolError)
                    .SetPricingEngine<BatesModel>((o, c, model) => BatesEngine.Calculate(o, c, model, 64)));
            }
        }

        // check calibration engine
        var om = new LevenbergMarquardt();
        BatesModel calibrated = batesModel.Calibrate(options, om, new EndCriteria(400, 40, 1.0e-8, 1.0e-8, 1.0e-8), context)
            .Model;

        Real expected = 36.6;
        Real calculated = GetCalibrationError(options, calibrated, context);

        Assert.False(
            Math.Abs(calculated - expected) > 2.5,
            $"failed to calibrate the bates model\n    calculated: {calculated}\n    expected:   {expected}");

        // check pricing of derived engines
        process = new BatesProcess(riskFreeTS, dividendTS, s0, v0, kappa, theta, sigma, rho, 1.0, -0.1, 0.1);

        var hestonProcess = new HestonProcess(riskFreeTS, dividendTS, s0, v0, kappa, theta, sigma, rho);

        (Func<HestonModelHelper, HestonModelHelper> Engine, ICalibratedModel Model)[] pricingEngines =
        [
            (h => h.SetPricingEngine<BatesDetJumpModel>((o, c, model) => BatesDetJumpEngine.Calculate(o, c, model, 64)),
                new BatesDetJumpModel(process)),
            (h => h.SetPricingEngine<BatesDoubleExpModel>((o, c, model) => BatesDoubleExpEngine.Calculate(o, c, model, 64)),
                new BatesDoubleExpModel(hestonProcess, 1.0)),
            (h => h.SetPricingEngine<BatesDoubleExpDetJumpModel>(
                    (o, c, model) => BatesDoubleExpDetJumpEngine.Calculate(o, c, model, 64)),
                new BatesDoubleExpDetJumpModel(hestonProcess, 1.0)),
        ];

        Real[] expectedValues = [5896.37, 5499.29, 6497.89];

        Real tolerance = 0.1;
        for (int i = 0; i < pricingEngines.Length; ++i)
        {
            List<HestonModelHelper> helpers = [.. options.Select(pricingEngines[i].Engine)];

            calculated = Math.Abs(GetCalibrationError(helpers, pricingEngines[i].Model, context));
            Assert.False(
                Math.Abs(calculated - expectedValues[i]) > tolerance,
                $"failed to calculated prices for derived Bates models\n    calculated: {calculated}\n    expected:   {expectedValues[i]}");
        }
    }

    // The test suite's getCalibrationError, each helper priced against the model.
    private static Real GetCalibrationError(IEnumerable<HestonModelHelper> options, ICalibratedModel model, EvaluationContext context)
    {
        Real sse = 0;
        foreach (HestonModelHelper option in options)
        {
            Real diff = option.CalibrationError(model, context) * 100.0;
            sse += diff * diff;
        }

        return sse;
    }

    // The test suite's flatRate(forward, dc): a flat forward from the evaluation date.
    private static YieldTermStructure FlatRate(Rate forward, DayCounter dc, EvaluationContext context) =>
        FlatForward.Create(0, new NullCalendar(), forward, dc, context);

    // The test suite's HestonModelData.
    private readonly record struct HestonModelData(
        string Name, Real V0, Real Kappa, Real Theta, Real Sigma, Real Rho, Real R, Real Q);
}
