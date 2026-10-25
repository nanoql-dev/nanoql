// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2009, 2010 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/hybridhestonhullwhiteprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testCompareBsmHWandHestonHW, testZeroBondPricing, testMcVanillaPricing, testMcPureHestonPricing, testAnalyticHestonHullWhitePricing, testCallableEquityPricing, testDiscretizationError, testFdmHestonHullWhiteEngine, testBsmHullWhitePricing, testHestonHullWhiteCalibration, testH1HWPricingEngine (S-hes's and S-core's Heston-Hull-White units)

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class HybridHestonHullWhiteProcessTests
{
    // The test suite's hestonModels.
    private static readonly HestonModelData[] HestonModels =
    [
        // ADI finite difference schemes for option pricing in the
        // Heston model with correlation, K.J. in t'Hout and S. Foulon,
        new("'t Hout case 1", 0.04, 1.5, 0.04, 0.3, -0.9, 0.025, 0.0),
        new("'t Hout case 2", 0.12, 3.0, 0.12, 0.04, 0.6, 0.01, 0.04),
        new("'t Hout case 3", 0.0707, 0.6067, 0.0707, 0.2928, -0.7571, 0.03, 0.0),
        new("'t Hout case 4", 0.06, 2.5, 0.06, 0.5, -0.1, 0.0507, 0.0469),

        // Efficient numerical methods for pricing American options under
        // stochastic volatility, Samuli Ikonen and Jari Toivanen,
        new("Ikonen-Toivanen", 0.0625, 5, 0.16, 0.9, 0.1, 0.1, 0.0),

        // Not-so-complex logarithms in the Heston model,
        // Christian Kahl and Peter Jaeckel
        new("Kahl-Jaeckel", 0.16, 1.0, 0.16, 2.0, -0.8, 0.0, 0.0),

        // self defined test cases
        new("Equity case", 0.07, 2.0, 0.04, 0.55, -0.8, 0.03, 0.035),
        new("high correlation", 0.07, 1.0, 0.04, 0.55, 0.995, 0.02, 0.04),
        new("low Vol-Of-Vol", 0.07, 1.0, 0.04, 0.001, -0.75, 0.04, 0.03),
        new("kappaEqSigRho", 0.07, 0.4, 0.04, 0.5, 0.8, 0.03, 0.03),
    ];

    // The test suite's schemes.
    private static readonly SchemeData[] Schemes =
    [
        new("HV2", Methods.FdmSchemeDesc.Hundsdorfer()),
        new("HV1", Methods.FdmSchemeDesc.ModifiedHundsdorfer()),
        new("CS", Methods.FdmSchemeDesc.CraigSneyd()),
        new("MCS", Methods.FdmSchemeDesc.ModifiedCraigSneyd()),
        new("DS", Methods.FdmSchemeDesc.Douglas()),
    ];

    [Fact]
    public void TestBsmHullWhiteEngine()
    {
        // Testing European option pricing for a BSM process with one-factor Hull-White model...
        DayCounter dc = new Actual365Fixed();

        Date today = Date.TodaysDate();
        Date maturity = today + new Period(20, TimeUnit.Years);

        var context = new EvaluationContext { EvaluationDate = today };

        const Real spot = 100.0;
        YieldTermStructure qTS = new FlatForward(today, 0.04, dc);
        YieldTermStructure rTS = new FlatForward(today, 0.0525, dc);
        BlackVolTermStructure volTS = new BlackConstantVol(today, new NullCalendar(), 0.25, dc);

        var hullWhiteModel = new HullWhite(rTS, 0.00883, 0.00526);

        GeneralizedBlackScholesProcess stochProcess = new BlackScholesMertonProcess(spot, qTS, rTS, volTS);

        Exercise exercise = new EuropeanExercise(maturity);

        Real fwd = spot * qTS.Discount(maturity) / rTS.Discount(maturity);
        StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Call, fwd);

        var option = new EuropeanOption(payoff, exercise);

        const Real tol = 1e-8;
        Real[] corr = [-0.75, -0.25, 0.0, 0.25, 0.75];
        Volatility[] expectedVol = [0.217064577, 0.243995801, 0.256402830, 0.268236596, 0.290461343];

        for (int i = 0; i < corr.Length; ++i)
        {
            OneAssetOptionResults bsmhw = AnalyticBSMHullWhiteEngine.Calculate(
                option, context, corr[i], stochProcess, hullWhiteModel);
            Real npv = bsmhw.NPV!.Value;

            BlackVolTermStructure compVolTS = new BlackConstantVol(today, new NullCalendar(), expectedVol[i], dc);

            GeneralizedBlackScholesProcess bsProcess = new BlackScholesMertonProcess(spot, qTS, rTS, compVolTS);

            var comp = new EuropeanOption(payoff, exercise);
            OneAssetOptionResults bs = AnalyticEuropeanEngine.Calculate(comp, context, bsProcess);

            Volatility impliedVol = comp.ImpliedVolatility(npv, bsProcess, context, 1e-10, 100);

            Assert.False(
                Math.Abs(impliedVol - expectedVol[i]) > tol,
                $"Failed to reproduce implied volatility\n    calculated: {impliedVol}\n    expected  : {expectedVol[i]}");
            Assert.False(
                Math.Abs((bs.NPV!.Value - npv) / npv) > tol,
                $"Failed to reproduce NPV\n    calculated: {npv}\n    expected  : {bs.NPV}");
            Assert.False(
                Math.Abs(bs.Delta!.Value - bsmhw.Delta!.Value) > tol,
                $"Failed to reproduce NPV\n    calculated: {npv}\n    expected  : {bs.NPV}");
            Assert.False(
                Math.Abs((bs.Gamma!.Value - bsmhw.Gamma!.Value) / npv) > tol,
                $"Failed to reproduce NPV\n    calculated: {npv}\n    expected  : {bs.NPV}");
            Assert.False(
                Math.Abs((bs.Theta!.Value - bsmhw.Theta!.Value) / npv) > tol,
                $"Failed to reproduce NPV\n    calculated: {npv}\n    expected  : {bs.NPV}");
            Assert.False(
                Math.Abs((bs.Vega!.Value - bsmhw.Vega!.Value) / npv) > tol,
                $"Failed to reproduce NPV\n    calculated: {npv}\n    expected  : {bs.NPV}");
        }
    }

    [Fact]
    public void TestSpatialDiscretizatinError()
    {
        // Testing spatial convergence speed of Heston engine...
        var today = new Date(27, Month.December, 2004);
        var context = new EvaluationContext { EvaluationDate = today };

        const Real maturity = 1.0;
        Real[] strikes = [75, 85, 90, 95, 100, 105, 110, 115, 120, 125, 130, 140, 150];
        int[] listOfTimeStepsPerYear = [40];

        Real[] tol = [0.02, 0.02, 0.02, 0.02, 0.05];
        foreach (int u in listOfTimeStepsPerYear)
        {
            for (int i = 0; i < Schemes.Length; ++i)
            {
                foreach (HestonModelData j in HestonModels)
                {
                    Real avgPriceDiff = 0;
                    HestonProcess hestonProcess = MakeHestonProcess(j, context);
                    var hestonModel = new HestonModel(hestonProcess);

                    int tSteps = (int)(maturity * u);

                    // QuantLib's engine with enableMultipleStrikesCaching(strikes), its cache filled by the first
                    // strike's option: CalculateStrikes on it (the S-hes plan's HR4 (ii))
                    System.Collections.Immutable.ImmutableArray<OneAssetOptionResults> fdResults =
                        FdHestonVanillaEngine.CalculateStrikes(
                            MakeVanillaOption(new VanillaOptionData(strikes[0], maturity, OptionType.Call), context),
                            strikes,
                            context,
                            hestonModel,
                            tSteps,
                            200,
                            40,
                            0,
                            Schemes[i].SchemeDesc);

                    for (int k = 0; k < strikes.Length; ++k)
                    {
                        var optionData = new VanillaOptionData(strikes[k], maturity, OptionType.Call);
                        VanillaOption option = MakeVanillaOption(optionData, context);
                        Real expected = AnalyticHestonEngine.Calculate(option, context, hestonModel, 172).NPV!.Value;

                        Real calculated = fdResults[k].NPV!.Value;

                        avgPriceDiff += Math.Abs(expected - calculated) / strikes.Length;
                    }

                    Assert.False(
                        avgPriceDiff > tol[i],
                        $"\nFailed to reproduce Heston prices\n scheme    : {Schemes[i].Name}\n model     : {j.Name}\n error     : {avgPriceDiff}\n tolerance : {tol[i]}");
                }
            }
        }
    }

    // The test suite's makeHestonProcess.
    private static HestonProcess MakeHestonProcess(HestonModelData parameters, EvaluationContext context)
    {
        Real spot = 100;

        DayCounter dayCounter = new Actual365Fixed();
        YieldTermStructure rTS = FlatForward.Create(0, new NullCalendar(), parameters.R, dayCounter, context);
        YieldTermStructure qTS = FlatForward.Create(0, new NullCalendar(), parameters.Q, dayCounter, context);

        return new HestonProcess(
            rTS, qTS, spot, parameters.V0, parameters.Kappa, parameters.Theta, parameters.Sigma, parameters.Rho);
    }

    // The test suite's makeVanillaOption.
    private static VanillaOption MakeVanillaOption(VanillaOptionData parameters, EvaluationContext context)
    {
        Date maturity = context.EvaluationDate + new Period((int)(parameters.Maturity * 365), TimeUnit.Days);
        var exercise = new EuropeanExercise(maturity);
        var payoff = new PlainVanillaPayoff(parameters.OptionType, parameters.Strike);

        return new VanillaOption(payoff, exercise);
    }

    // The test suite's HestonModelData.
    private readonly record struct HestonModelData(
        string Name, Real V0, Real Kappa, Real Theta, Real Sigma, Real Rho, Real R, Real Q);

    // The test suite's SchemeData.
    private readonly record struct SchemeData(string Name, Methods.FdmSchemeDesc SchemeDesc);

    // The test suite's VanillaOptionData.
    private readonly record struct VanillaOptionData(Real Strike, double Maturity, OptionType OptionType);
}
