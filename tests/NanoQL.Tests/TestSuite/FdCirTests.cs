// SPDX-FileCopyrightText: Copyright (C) 2020 Lew Wei Hao
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/fdcir.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Moved: testFdmCIRConvergence line 105 (Divergences/FdCirModelTests.cs)

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class FdCirTests
{
    [Fact]
    public void TestFdmCIRConvergence()
    {
        // Testing FDM CIR convergence...
        FdmSchemeDesc[] schemes =
        [
            FdmSchemeDesc.Hundsdorfer(),
            FdmSchemeDesc.ModifiedCraigSneyd(),
            FdmSchemeDesc.ModifiedHundsdorfer(),
            FdmSchemeDesc.CraigSneyd(),
            FdmSchemeDesc.TrBDF2(),
            FdmSchemeDesc.CrankNicolson(),
        ];

        // set up dates
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        // our options
        const OptionType type = OptionType.Put;
        const Real underlying = 36;
        const Real strike = 40;
        const Spread dividendYield = 0.00;
        const Rate riskFreeRate = 0.06;
        const Volatility volatility = 0.20;
        Date maturity = today + 365;
        DayCounter dayCounter = new Actual365Fixed();

        var europeanExercise = new EuropeanExercise(maturity);

        YieldTermStructure flatTermStructure = new FlatForward(today, riskFreeRate, dayCounter);
        YieldTermStructure flatDividendTS = new FlatForward(today, dividendYield, dayCounter);
        BlackVolTermStructure flatVolTS = new BlackConstantVol(today, new NullCalendar(), volatility, dayCounter);
        var payoff = new PlainVanillaPayoff(type, strike);
        var bsmProcess = new BlackScholesMertonProcess(underlying, flatDividendTS, flatTermStructure, flatVolTS);

        var europeanOption = new VanillaOption(payoff, europeanExercise);

        const Real speed = 1.2188;
        const Real cirSigma = 0.02438;
        const Real level = 0.0183;
        const Real initialRate = 0.06;
        const Real rho = 0.00789;
        const Real lambda = -0.5726;
        Real newSpeed = speed + (cirSigma * lambda); // 1.0792
        Real newLevel = level * speed / (speed + (cirSigma * lambda)); // 0.0240

        var cirProcess = new CoxIngersollRossProcess(newSpeed, cirSigma, initialRate, newLevel);

        // Moved: QuantLib's 4.275 is its operator's on its rate grid, a rate with twice the CIR variance; the engine
        // prices the CIR generator on a rate grid from zero (the S-van plan's VR4 (xv), VR8), its six schemes within
        // 2e-4 of 4.2717 on the case's grid, against Black's formula at the mean path's rate and a Monte Carlo of the
        // CIR rate (Divergences/FdCirModelTests.cs)
        const Real expected = 4.2717;
        const Real tolerance = 0.0003;

        foreach (FdmSchemeDesc scheme in schemes)
        {
            Func<VanillaOption, EvaluationContext, OneAssetOptionResults> fdcirengine =
                new MakeFdCIRVanillaEngine(cirProcess, bsmProcess, rho).WithFdmSchemeDesc(scheme).ToEngine();
            Real calculated = fdcirengine(europeanOption, context).NPV!.Value;
            Assert.False(
                Math.Abs(expected - calculated) > tolerance,
                "Failed to reproduce expected npv"
                + $"\n    calculated: {calculated}"
                + $"\n    expected:   {expected}"
                + $"\n    tolerance:  {tolerance}");
        }
    }
}
