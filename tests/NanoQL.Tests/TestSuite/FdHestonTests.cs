// SPDX-FileCopyrightText: Copyright (C) 2008, 2009, 2014 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2014 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/fdheston.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Not ported: testFdmHestonIntradayPricing (QL_HIGH_RESOLUTION_DATE, which the default build leaves out)

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class FdHestonTests
{
    [Fact]
    public void TestFdmHestonVarianceMesher()
    {
        // Testing FDM Heston variance mesher...
        var today = new Date(22, Month.February, 2018);
        DayCounter dc = new Actual365Fixed();
        var context = new EvaluationContext { EvaluationDate = today };

        var process = new HestonProcess(
            FlatRate(0.02, dc, context), FlatRate(0.02, dc, context), 100.0, 0.09, 1.0, 0.09, 0.2, -0.5);

        var mesher = new FdmHestonVarianceMesher(5, process, 1.0);

        System.Collections.Immutable.ImmutableArray<double> locations = mesher.Locations();

        Real[] expected = [0.0, 6.652314e-02, 9.000000e-02, 1.095781e-01, 2.563610e-01];

        const Real tol = 1e-6;
        for (int i = 0; i < locations.Length; ++i)
        {
            Real diff = Math.Abs(expected[i] - locations[i]);

            Assert.False(
                diff > tol,
                $"Failed to reproduce Heston variance mesh\n    calculated: {locations[i]}\n    expected:   {expected[i]}\n    difference  {diff:E}\n    tolerance:  {tol:E}");
        }

        LocalVolTermStructure lVol = new LocalConstantVol(today, 2.5, dc);

        var constSlvMesher = new FdmHestonLocalVolatilityVarianceMesher(5, process, lVol, 1.0);

        Real expectedVol = 2.5 * mesher.VolaEstimate;
        Real calculatedVol = constSlvMesher.VolaEstimate;

        Real diffVol = Math.Abs(calculatedVol - expectedVol);
        Assert.False(
            diffVol > tol,
            $"Failed to reproduce Heston local volatility variance estimate\n    calculated: {calculatedVol}\n    expected:   {expectedVol}\n    difference  {diffVol:E}\n    tolerance:  {tol:E}");

        const Real alpha = 0.01;
        LocalVolTermStructure leverageFct = new ParableLocalVolatility(today, 100.0, alpha, dc);

        var slvMesher = new FdmHestonLocalVolatilityVarianceMesher(5, process, leverageFct, 0.5, 1, 0.01);

        Real initialVolEstimate = new FdmHestonVarianceMesher(5, process, 0.5, 1, 0.01).VolaEstimate;

        // const Real vEst = leverageFct->localVol(0, 100) * initialVolEstimate;
        // Mathematica solution
        //    N[Integrate[
        //      alpha*((100*Exp[vEst*x*Sqrt[0.5]] - 100)^2 + 25)*
        //       PDF[NormalDistribution[0, 1], x], {x ,
        //       InverseCDF[NormalDistribution[0, 1], 0.01],
        //       InverseCDF[NormalDistribution[0, 1], 0.99]}]]
        Real leverageAvg = 0.455881 / (1 - 0.02);

        Real volaEstExpected = 0.5 * (leverageAvg + leverageFct.LocalVol(0, 100)) * initialVolEstimate;

        Real volaEstCalculated = slvMesher.VolaEstimate;

        Assert.False(
            Math.Abs(volaEstExpected - volaEstCalculated) > 0.001,
            $"Failed to reproduce Heston local volatility variance estimate\n    calculated: {calculatedVol}\n    expected:   {expectedVol}\n    difference  {Math.Abs(volaEstExpected - volaEstCalculated):E}\n    tolerance:  {tol:E}");
    }

    [Fact]
    public void TestFdmHestonBarrierVsBlackScholes()
    {
        // Testing FDM with barrier option in Heston model...

        NewBarrierOptionData[] values =
        [
            /* The data below are from
              "Option pricing formulas", E.G. Haug, McGraw-Hill 1998 pag. 72
            */
            //     barrierType, barrier, rebate,         type, strike,     s,    q,    r,    t,    v
            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Call, 90, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Call, 100, 100.0, 0.00, 0.08, 1.00, 0.30),
            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Call, 110, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Call, 90, 100.0, 0.00, 0.08, 0.25, 0.25),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Call, 100, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Call, 110, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Call, 90, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Call, 100, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Call, 110, 100.0, 0.04, 0.08, 0.50, 0.25),

            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Call, 90, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Call, 100, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Call, 110, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Call, 90, 100.0, 0.00, 0.08, 0.25, 0.25),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Call, 100, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Call, 110, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Call, 90, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Call, 100, 100.0, 0.00, 0.08, 0.40, 0.25),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Call, 110, 100.0, 0.04, 0.08, 0.50, 0.15),

            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Call, 90, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Call, 100, 100.0, 0.00, 0.08, 0.40, 0.35),
            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Call, 110, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Call, 90, 100.0, 0.04, 0.08, 0.50, 0.15),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Call, 100, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Call, 110, 100.0, 0.00, 0.00, 1.00, 0.20),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Call, 90, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Call, 100, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Call, 110, 100.0, 0.04, 0.08, 0.50, 0.30),

            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Call, 90, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Call, 100, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Call, 110, 100.0, 0.00, 0.08, 1.00, 0.30),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Call, 90, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Call, 100, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Call, 110, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Call, 90, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Call, 100, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Call, 110, 100.0, 0.04, 0.08, 0.50, 0.30),

            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Put, 90, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Put, 100, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Put, 110, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Put, 90, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Put, 100, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Put, 110, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Put, 90, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Put, 100, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Put, 110, 100.0, 0.04, 0.08, 0.50, 0.25),

            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Put, 90, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Put, 100, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Put, 110, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Put, 90, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Put, 100, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Put, 110, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Put, 90, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Put, 100, 100.0, 0.04, 0.08, 0.50, 0.25),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Put, 110, 100.0, 0.00, 0.04, 1.00, 0.15),

            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Put, 90, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Put, 100, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.DownOut, 95.0, 3.0, OptionType.Put, 110, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Put, 90, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Put, 100, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.DownOut, 100.0, 3.0, OptionType.Put, 110, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Put, 90, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Put, 100, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.UpOut, 105.0, 3.0, OptionType.Put, 110, 100.0, 0.04, 0.08, 0.50, 0.30),

            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Put, 90, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Put, 100, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.DownIn, 95.0, 3.0, OptionType.Put, 110, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Put, 90, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Put, 100, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.DownIn, 100.0, 3.0, OptionType.Put, 110, 100.0, 0.04, 0.08, 1.00, 0.15),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Put, 90, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Put, 100, 100.0, 0.04, 0.08, 0.50, 0.30),
            new(BarrierType.UpIn, 105.0, 3.0, OptionType.Put, 110, 100.0, 0.04, 0.08, 0.50, 0.30),
        ];

        DayCounter dc = new Actual365Fixed();
        var todaysDate = new Date(28, Month.March, 2004);
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        foreach (NewBarrierOptionData value in values)
        {
            Date exDate = todaysDate + TimeToDays(value.T, 365);
            var exercise = new EuropeanExercise(exDate);

            YieldTermStructure qTS = FlatRate(value.Q, dc, context);
            YieldTermStructure rTS = FlatRate(value.R, dc, context);
            BlackVolTermStructure volTS = FlatVol(value.V, dc, context);
            var bsProcess = new BlackScholesMertonProcess(value.S, qTS, rTS, volTS);

            var payoff = new PlainVanillaPayoff(value.Type, value.Strike);

            var barrierOption = new BarrierOption(value.BarrierType, value.Barrier, value.Rebate, payoff, exercise);

            Real v0 = value.V * value.V;
            var hestonProcess = new HestonProcess(rTS, qTS, value.S, v0, 1.0, v0, 0.005, 0.0);

            Real calculatedHE = FdHestonBarrierEngine.Calculate(
                barrierOption, context, new HestonModel(hestonProcess), 200, 101, 3).NPV!.Value;

            Real expected = AnalyticBarrierEngine.Calculate(barrierOption, context, bsProcess).NPV!.Value;

            const Real tol = 0.0025;
            Assert.False(
                Math.Abs(calculatedHE - expected) / expected > tol,
                $"Failed to reproduce expected Heston npv\n    calculated: {calculatedHE}\n    expected:   {expected}\n    tolerance:  {tol}");
        }
    }

    [Fact]
    public void TestFdmHestonBarrier()
    {
        // Testing FDM with barrier option for Heston model vs Black-Scholes model...
        var context = new EvaluationContext { EvaluationDate = new Date(28, Month.March, 2004) };

        Real s0 = 100.0;

        YieldTermStructure rTS = FlatRate(0.05, new Actual365Fixed(), context);
        YieldTermStructure qTS = FlatRate(0.0, new Actual365Fixed(), context);

        var hestonProcess = new HestonProcess(rTS, qTS, s0, 0.04, 2.5, 0.04, 0.66, -0.8);

        var exerciseDate = new Date(28, Month.March, 2005);

        var exercise = new EuropeanExercise(exerciseDate);

        var payoff = new PlainVanillaPayoff(OptionType.Call, 100);

        var barrierOption = new BarrierOption(BarrierType.UpOut, 135, 0.0, payoff, exercise);

        OneAssetOptionResults results = FdHestonBarrierEngine.Calculate(
            barrierOption, context, new HestonModel(hestonProcess), 50, 400, 100);

        const Real tol = 0.01;
        const Real npvExpected = 9.1530;
        const Real deltaExpected = 0.5218;
        const Real gammaExpected = -0.0354;

        Assert.False(
            Math.Abs(results.NPV!.Value - npvExpected) > tol,
            $"Failed to reproduce expected npv\n    calculated: {results.NPV}\n    expected:   {npvExpected}\n    tolerance:  {tol}");
        Assert.False(
            Math.Abs(results.Delta!.Value - deltaExpected) > tol,
            $"Failed to reproduce expected delta\n    calculated: {results.Delta}\n    expected:   {deltaExpected}\n    tolerance:  {tol}");
        Assert.False(
            Math.Abs(results.Gamma!.Value - gammaExpected) > tol,
            $"Failed to reproduce expected gamma\n    calculated: {results.Gamma}\n    expected:   {gammaExpected}\n    tolerance:  {tol}");
    }

    [Fact]
    public void TestFdmHestonAmerican()
    {
        // Testing FDM with American option in Heston model...
        var context = new EvaluationContext { EvaluationDate = new Date(28, Month.March, 2004) };

        Real s0 = 100.0;

        YieldTermStructure rTS = FlatRate(0.05, new Actual365Fixed(), context);
        YieldTermStructure qTS = FlatRate(0.0, new Actual365Fixed(), context);

        var hestonProcess = new HestonProcess(rTS, qTS, s0, 0.04, 2.5, 0.04, 0.66, -0.8);

        var exerciseDate = new Date(28, Month.March, 2005);

        var exercise = new AmericanExercise(exerciseDate);

        var payoff = new PlainVanillaPayoff(OptionType.Put, 100);

        var option = new VanillaOption(payoff, exercise);
        OneAssetOptionResults results = FdHestonVanillaEngine.Calculate(option, context, new HestonModel(hestonProcess), 200, 100, 50);

        const Real tol = 0.01;
        const Real npvExpected = 5.66032;
        const Real deltaExpected = -0.30065;
        const Real gammaExpected = 0.02202;

        Assert.False(
            Math.Abs(results.NPV!.Value - npvExpected) > tol,
            $"Failed to reproduce expected npv\n    calculated: {results.NPV}\n    expected:   {npvExpected}\n    tolerance:  {tol}");
        Assert.False(
            Math.Abs(results.Delta!.Value - deltaExpected) > tol,
            $"Failed to reproduce expected delta\n    calculated: {results.Delta}\n    expected:   {deltaExpected}\n    tolerance:  {tol}");
        Assert.False(
            Math.Abs(results.Gamma!.Value - gammaExpected) > tol,
            $"Failed to reproduce expected gamma\n    calculated: {results.Gamma}\n    expected:   {gammaExpected}\n    tolerance:  {tol}");
    }

    [Fact]
    public void TestFdmHestonIkonenToivanen()
    {
        // Testing FDM Heston for Ikonen and Toivanen tests...

        /* check prices of american puts as given in:
           From Efficient numerical methods for pricing American options under
           stochastic volatility, Samuli Ikonen, Jari Toivanen,
           http://users.jyu.fi/~tene/papers/reportB12-05.pdf
        */
        var context = new EvaluationContext { EvaluationDate = new Date(28, Month.March, 2004) };
        YieldTermStructure rTS = FlatRate(0.10, new Actual360(), context);
        YieldTermStructure qTS = FlatRate(0.0, new Actual360(), context);

        var exerciseDate = new Date(26, Month.June, 2004);

        var exercise = new AmericanExercise(exerciseDate);

        var payoff = new PlainVanillaPayoff(OptionType.Put, 10);

        var option = new VanillaOption(payoff, exercise);

        Real[] strikes = [8, 9, 10, 11, 12];
        Real[] expected = [2.00000, 1.10763, 0.520038, 0.213681, 0.082046];
        const Real tol = 0.001;

        for (int i = 0; i < strikes.Length; ++i)
        {
            Real s0 = strikes[i];
            var hestonProcess = new HestonProcess(rTS, qTS, s0, 0.0625, 5, 0.16, 0.9, 0.1);

            Real calculated = FdHestonVanillaEngine.Calculate(option, context, new HestonModel(hestonProcess), 100, 400).NPV!.Value;
            Assert.False(
                Math.Abs(calculated - expected[i]) > tol,
                $"Failed to reproduce expected npv\n    strike:     {strikes[i]}\n    calculated: {calculated}\n    expected:   {expected[i]}\n    tolerance:  {tol}");
        }
    }

    [Fact]
    public void TestFdmHestonBlackScholes()
    {
        // Testing FDM Heston with Black Scholes model...
        var context = new EvaluationContext { EvaluationDate = new Date(28, Month.March, 2004) };
        var exerciseDate = new Date(26, Month.June, 2004);

        YieldTermStructure rTS = FlatRate(0.10, new Actual360(), context);
        YieldTermStructure qTS = FlatRate(0.0, new Actual360(), context);
        BlackVolTermStructure volTS = new BlackConstantVol(rTS.ReferenceDate, new NullCalendar(), 0.25, rTS.DayCounter);

        var exercise = new EuropeanExercise(exerciseDate);

        var payoff = new PlainVanillaPayoff(OptionType.Put, 10);

        var option = new VanillaOption(payoff, exercise);

        Real[] strikes = [8, 9, 10, 11, 12];
        const Real tol = 0.0001;

        foreach (Real strike in strikes)
        {
            Real s0 = strike;

            var bsProcess = new GeneralizedBlackScholesProcess(s0, qTS, rTS, volTS);

            Real expected = AnalyticEuropeanEngine.Calculate(option, context, bsProcess).NPV!.Value;

            var hestonProcess = new HestonProcess(rTS, qTS, s0, 0.0625, 1, 0.0625, 0.0001, 0.0);

            // Hundsdorfer scheme
            Real calculated = FdHestonVanillaEngine.Calculate(option, context, new HestonModel(hestonProcess), 100, 400, 3).NPV!.Value;
            Assert.False(
                Math.Abs(calculated - expected) > tol,
                $"Failed to reproduce expected npv\n    strike:     {strike}\n    calculated: {calculated}\n    expected:   {expected}\n    tolerance:  {tol}");

            // Explicit scheme
            calculated = FdHestonVanillaEngine.Calculate(
                option, context, new HestonModel(hestonProcess), 4000, 400, 3, 0, FdmSchemeDesc.ExplicitEuler()).NPV!.Value;
            Assert.False(
                Math.Abs(calculated - expected) > tol,
                $"Failed to reproduce expected npv\n    strike:     {strike}\n    calculated: {calculated}\n    expected:   {expected}\n    tolerance:  {tol}");
        }
    }

    [Fact]
    public void TestFdmHestonEuropeanWithDividends()
    {
        // Testing FDM with European option with dividends in Heston model...
        var context = new EvaluationContext { EvaluationDate = new Date(28, Month.March, 2004) };

        Real s0 = 100.0;

        YieldTermStructure rTS = FlatRate(0.05, new Actual365Fixed(), context);
        YieldTermStructure qTS = FlatRate(0.0, new Actual365Fixed(), context);

        var hestonProcess = new HestonProcess(rTS, qTS, s0, 0.04, 2.5, 0.04, 0.66, -0.8);

        var exerciseDate = new Date(28, Month.March, 2005);

        var exercise = new AmericanExercise(exerciseDate);
        var payoff = new PlainVanillaPayoff(OptionType.Put, 100);

        Real[] dividends = [5];
        Date[] dividendDates = [new Date(28, Month.September, 2004)];

        const Real tol = 0.01;
        const Real gammaTol = 0.001;
        const Real npvExpected = 7.38216;
        const Real deltaExpected = -0.397902;
        const Real gammaExpected = 0.027747;

        var option = new VanillaOption(payoff, exercise);
        OneAssetOptionResults results = FdHestonVanillaEngine.Calculate(
            option,
            context,
            new HestonModel(hestonProcess),
            NanoQL.Coupons.DividendFunctions.DividendVector(dividendDates, dividends),
            50,
            100,
            50);

        Assert.False(
            Math.Abs(results.NPV!.Value - npvExpected) > tol,
            $"Failed to reproduce expected npv\n    calculated: {results.NPV}\n    expected:   {npvExpected}\n    tolerance:  {tol}");
        Assert.False(
            Math.Abs(results.Delta!.Value - deltaExpected) > tol,
            $"Failed to reproduce expected delta\n    calculated: {results.Delta}\n    expected:   {deltaExpected}\n    tolerance:  {tol}");
        Assert.False(
            Math.Abs(results.Gamma!.Value - gammaExpected) > gammaTol,
            $"Failed to reproduce expected gamma\n    calculated: {results.Gamma}\n    expected:   {gammaExpected}\n    tolerance:  {tol}");
    }

    [Fact]
    public void TestFdmHestonConvergence()
    {
        /* convergence tests based on
           ADI finite difference schemes for option pricing in the
           Heston model with correlation, K.J. in t'Hout and S. Foulon
        */

        // Testing FDM Heston convergence...
        HestonTestData[] values =
        [
            new(1.5, 0.04, 0.3, -0.9, 0.025, 0.0, 1.0, 100),
            new(3.0, 0.12, 0.04, 0.6, 0.01, 0.04, 1.0, 100),
            new(0.6067, 0.0707, 0.2928, -0.7571, 0.03, 0.0, 3.0, 100),
            new(2.5, 0.06, 0.5, -0.1, 0.0507, 0.0469, 0.25, 100),
        ];

        FdmSchemeDesc[] schemes =
        [
            FdmSchemeDesc.Hundsdorfer(),
            FdmSchemeDesc.ModifiedCraigSneyd(),
            FdmSchemeDesc.ModifiedHundsdorfer(),
            FdmSchemeDesc.CraigSneyd(),
            FdmSchemeDesc.TrBDF2(),
            FdmSchemeDesc.CrankNicolson(),
        ];

        int[] tn = [60];
        Real[] v0 = [0.04];

        var todaysDate = new Date(28, Month.March, 2004);
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        Real s0 = 75.0;

        foreach (FdmSchemeDesc scheme in schemes)
        {
            foreach (HestonTestData value in values)
            {
                foreach (int j in tn)
                {
                    foreach (Real k in v0)
                    {
                        YieldTermStructure rTS = FlatRate(value.R, new Actual365Fixed(), context);
                        YieldTermStructure qTS = FlatRate(value.Q, new Actual365Fixed(), context);

                        var hestonProcess = new HestonProcess(
                            rTS, qTS, s0, k, value.Kappa, value.Theta, value.Sigma, value.Rho);

                        Date exerciseDate = todaysDate + new Period((int)(value.T * 365), TimeUnit.Days);
                        var exercise = new EuropeanExercise(exerciseDate);

                        var payoff = new PlainVanillaPayoff(OptionType.Call, value.K);

                        var option = new VanillaOption(payoff, exercise);
                        Real calculated = FdHestonVanillaEngine.Calculate(
                            option, context, new HestonModel(hestonProcess), j, 101, 51, 0, scheme).NPV!.Value;

                        Real expected = AnalyticHestonEngine.Calculate(option, context, new HestonModel(hestonProcess), 144).NPV!.Value;
                        Assert.False(
                            Math.Abs(expected - calculated) / expected > 0.02 && Math.Abs(expected - calculated) > 0.002,
                            $"Failed to reproduce expected npv\n    calculated: {calculated}\n    expected:   {expected}\n    tolerance:  {0.01}");
                    }
                }
            }
        }
    }

    [Fact]
    public void TestMethodOfLinesAndCN()
    {
        // Testing method of lines to solve Heston PDEs...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(21, Month.February, 2018);
        var context = new EvaluationContext { EvaluationDate = today };

        Real spot = 100.0;
        YieldTermStructure qTS = new FlatForward(today, 0.0, dc);
        YieldTermStructure rTS = new FlatForward(today, 0.0, dc);

        const Real v0 = 0.09;
        const Real kappa = 1.0;
        const Real theta = v0;
        const Real sigma = 0.4;
        const Real rho = -0.75;

        Date maturity = today + new Period(3, TimeUnit.Months);

        var model = new HestonModel(new HestonProcess(rTS, qTS, spot, v0, kappa, theta, sigma, rho));

        const int xGrid = 21;
        const int vGrid = 7;

        var payoff = new PlainVanillaPayoff(OptionType.Put, spot);

        var option = new VanillaOption(payoff, new AmericanExercise(maturity));

        Real calculatedMoL = FdHestonVanillaEngine.Calculate(
            option, context, model, 10, xGrid, vGrid, 0, FdmSchemeDesc.MethodOfLines()).NPV!.Value;

        Real expected = FdHestonVanillaEngine.Calculate(option, context, model, 10, xGrid, vGrid, 0).NPV!.Value;

        const Real tol = 0.005;
        Real diffMoL = Math.Abs(expected - calculatedMoL);

        Assert.False(
            diffMoL > tol,
            $"Failed to reproduce european option values with MOL\n    calculated: {calculatedMoL}\n    expected:   {expected}\n    difference: {diffMoL}\n    tolerance:  {tol}");

        Real calculatedCN = FdHestonVanillaEngine.Calculate(
            option, context, model, 10, xGrid, vGrid, 0, FdmSchemeDesc.CrankNicolson()).NPV!.Value;
        Real diffCN = Math.Abs(expected - calculatedCN);

        Assert.False(
            diffCN > tol,
            $"Failed to reproduce european option values with Crank-Nicolson\n    calculated: {calculatedCN}\n    expected:   {expected}\n    difference: {diffCN}\n    tolerance:  {tol}");

        var barrierOption = new BarrierOption(BarrierType.DownOut, 85.0, 10.0, payoff, new EuropeanExercise(maturity));

        Real expectedBarrier = FdHestonBarrierEngine.Calculate(barrierOption, context, model, 100, 31, 11).NPV!.Value;

        Real calculatedBarrierMoL = FdHestonBarrierEngine.Calculate(
            barrierOption, context, model, 100, 31, 11, 0, FdmSchemeDesc.MethodOfLines()).NPV!.Value;

        const Real barrierTol = 0.01;
        Real barrierDiffMoL = Math.Abs(expectedBarrier - calculatedBarrierMoL);

        Assert.False(
            barrierDiffMoL > barrierTol,
            $"Failed to reproduce barrier option values with MOL\n    calculated: {calculatedBarrierMoL}\n    expected:   {expectedBarrier}\n    difference: {barrierDiffMoL}\n    tolerance:  {barrierTol}");

        Real calculatedBarrierCN = FdHestonBarrierEngine.Calculate(
            barrierOption, context, model, 100, 31, 11, 0, FdmSchemeDesc.CrankNicolson()).NPV!.Value;
        Real barrierDiffCN = Math.Abs(expectedBarrier - calculatedBarrierCN);

        Assert.False(
            barrierDiffCN > barrierTol,
            $"Failed to reproduce barrier option values with Crank-Nicolson\n    calculated: {calculatedBarrierCN}\n    expected:   {expectedBarrier}\n    difference: {barrierDiffCN}\n    tolerance:  {barrierTol}");
    }

    [Fact]
    public void TestSpuriousOscillations()
    {
        // Testing for spurious oscillations when solving the Heston PDEs...
        DayCounter dc = new Actual365Fixed();
        var today = new Date(7, Month.June, 2018);

        var context = new EvaluationContext { EvaluationDate = today };

        Real spot = 100.0;
        YieldTermStructure qTS = new FlatForward(today, 0.1, dc);
        YieldTermStructure rTS = new FlatForward(today, 0.0, dc);

        const Real v0 = 0.005;
        const Real kappa = 1.0;
        const Real theta = 0.005;
        const Real sigma = 0.4;
        const Real rho = -0.75;

        Date maturity = today + new Period(1, TimeUnit.Years);

        var process = new HestonProcess(rTS, qTS, spot, v0, kappa, theta, sigma, rho);

        var model = new HestonModel(process);

        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, spot), new EuropeanExercise(maturity));

        // QuantLib's FdHestonVanillaEngine(model, 6, 200, 13, 0, TrBDF2) and its getSolverDesc
        FdmSolverDesc solverDesc = FdHestonVanillaEngine.GetSolverDesc(option, context, model, [], null, 6, 200, 13, 0);

        (FdmSchemeDesc Desc, string Name, bool Spurious)[] descs =
        [
            (FdmSchemeDesc.CraigSneyd(), "Craig-Sneyd", true),
            (FdmSchemeDesc.Hundsdorfer(), "Hundsdorfer", true),
            (FdmSchemeDesc.ModifiedHundsdorfer(), "Mod. Hundsdorfer", true),
            (FdmSchemeDesc.Douglas(), "Douglas", true),
            (FdmSchemeDesc.CrankNicolson(), "Crank-Nicolson", true),
            (FdmSchemeDesc.ImplicitEuler(), "Implicit", false),
            (FdmSchemeDesc.TrBDF2(), "TR-BDF2", false),
        ];

        foreach ((FdmSchemeDesc desc, string name, bool spurious) in descs)
        {
            var solver = new FdmHestonSolver(process, solverDesc, desc);

            var gammas = new List<Real>();
            for (Real x = 99; x < 101.001; x += 0.1)
            {
                gammas.Add(solver.GammaAt(x, v0));
            }

            Real maximum = -double.MaxValue;
            for (int i = 1; i < gammas.Count; ++i)
            {
                Real diff = Math.Abs(gammas[i] - gammas[i - 1]);
                if (diff > maximum)
                {
                    maximum = diff;
                }
            }

            const Real tol = 0.01;
            bool hasSpuriousOscillations = maximum > tol;

            Assert.False(
                hasSpuriousOscillations != spurious,
                $"unable to reproduce spurious oscillation behaviour \n   scheme name          : {name}\n   oscillations observed: {hasSpuriousOscillations}\n   oscillations expected: {spurious}");
        }
    }

    [Fact]
    public void TestAmericanCallPutParity()
    {
        // Testing call/put parity for American options under the Heston model...

        // A. Battauz, M. De Donno,m A. Sbuelz:
        // The put-call symmetry for American options in
        // the Heston stochastic volatility model
        DayCounter dc = new Actual365Fixed();
        var today = new Date(15, Month.April, 2022);

        var context = new EvaluationContext { EvaluationDate = today };

        HestonProcess BuildStochProcess(OptionSpec testCase) =>
            new(
                FlatRate(testCase.R, dc, context),
                FlatRate(testCase.Q, dc, context),
                testCase.Spot,
                testCase.V0,
                testCase.Kappa,
                testCase.Theta,
                testCase.Sig,
                testCase.Rho);

        OptionSpec[] testCaseSpecs =
        [
            new(100.0, 90.0, 365, 0.02, 0.15, 0.25, 1.0, 0.09, 0.5, -0.75),
            new(100.0, 90.0, 365, 0.05, 0.20, 0.5, 1.0, 0.05, 0.75, -0.9),
        ];

        const int xGrid = 200;
        const int vGrid = 25;
        const int timeStepsPerYear = 50;

        foreach (OptionSpec testCaseSpec in testCaseSpecs)
        {
            Date maturityDate = today + new Period(testCaseSpec.MaturityInDays, TimeUnit.Days);
            double maturityTime = dc.YearFraction(today, maturityDate);
            int tGrid = (int)(maturityTime * timeStepsPerYear);

            var exercise = new AmericanExercise(today, maturityDate);

            var callOption = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, testCaseSpec.Strike), exercise);

            Real callNpv = FdHestonVanillaEngine.Calculate(
                callOption, context, new HestonModel(BuildStochProcess(testCaseSpec)), tGrid, xGrid, vGrid).NPV!.Value;

            var putOptionSpec = new OptionSpec(
                testCaseSpec.Strike,
                testCaseSpec.Spot,
                testCaseSpec.MaturityInDays,
                testCaseSpec.Q,
                testCaseSpec.R,
                testCaseSpec.V0,
                testCaseSpec.Kappa - (testCaseSpec.Sig * testCaseSpec.Rho),
                testCaseSpec.Kappa * testCaseSpec.Theta / (testCaseSpec.Kappa - (testCaseSpec.Sig * testCaseSpec.Rho)),
                testCaseSpec.Sig,
                -testCaseSpec.Rho);

            var putOption = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, putOptionSpec.Strike), exercise);

            Real putNpv = FdHestonVanillaEngine.Calculate(
                putOption, context, new HestonModel(BuildStochProcess(putOptionSpec)), tGrid, xGrid, vGrid).NPV!.Value;

            Real diff = Math.Abs(putNpv - callNpv);
            const Real tol = 0.025;

            Assert.False(
                diff > tol,
                $"failed to reproduce American call/put parity\n    Put NPV   : {putNpv}\n    Call NPV  : {callNpv}\n    difference: {diff}\n    tolerance : {tol}");
        }
    }

    // The test suite's flatRate(forward, dc): a flat forward from the evaluation date.
    private static YieldTermStructure FlatRate(Rate forward, DayCounter dc, EvaluationContext context) =>
        FlatForward.Create(0, new NullCalendar(), forward, dc, context);

    // The test suite's flatVol(volatility, dc): a constant volatility from the evaluation date.
    private static BlackVolTermStructure FlatVol(Volatility volatility, DayCounter dc, EvaluationContext context) =>
        new BlackConstantVol(context.EvaluationDate, new NullCalendar(), volatility, dc);

    // The test suite's timeToDays: Integer(std::lround(t * daysPerYear)).
    private static int TimeToDays(double t, int daysPerYear) =>
        (int)Math.Round(t * daysPerYear, MidpointRounding.AwayFromZero);

    // The test suite's NewBarrierOptionData, as testFdmHestonBarrierVsBlackScholes reads it.
    private sealed record NewBarrierOptionData(
        BarrierType BarrierType,
        double Barrier,
        double Rebate,
        OptionType Type,
        double Strike,
        double S,
        double Q,
        double R,
        double T,
        double V);

    // The test suite's HestonTestData.
    private readonly record struct HestonTestData(Real Kappa, Real Theta, Real Sigma, Real Rho, Real R, Real Q, Real T, Real K);

    // testAmericanCallPutParity's OptionSpec.
    private readonly record struct OptionSpec(
        Real Spot, Real Strike, int MaturityInDays, Real R, Real Q, Real V0, Real Kappa, Real Theta, Real Sig, Real Rho);

    // The test suite's ParableLocalVolatility: α((s₀ − s)² + 25).
    private sealed class ParableLocalVolatility : LocalVolTermStructure
    {
        private readonly Real s0;
        private readonly Real alpha;

        public ParableLocalVolatility(Date referenceDate, Real s0, Real alpha, DayCounter dayCounter)
            : base(referenceDate, new NullCalendar(), BusinessDayConvention.Following, dayCounter)
        {
            this.s0 = s0;
            this.alpha = alpha;
        }

        public override Date MaxDate => Date.MaxDate;

        public override Real MinStrike() => 0.0;

        public override Real MaxStrike() => double.MaxValue;

        protected override Volatility LocalVolImpl(double t, Real s) => alpha * (((s0 - s) * (s0 - s)) + 25.0);
    }
}
