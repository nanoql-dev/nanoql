// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2009, 2010 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/hybridhestonhullwhiteprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testCompareBsmHWandHestonHW, testZeroBondPricing, testMcVanillaPricing, testMcPureHestonPricing, testAnalyticHestonHullWhitePricing, testCallableEquityPricing, testDiscretizationError, testFdmHestonHullWhiteEngine, testBsmHullWhitePricing, testSpatialDiscretizatinError, testHestonHullWhiteCalibration, testH1HWPricingEngine (S-hes's and S-core's Heston-Hull-White units)

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
}
