// SPDX-FileCopyrightText: Copyright (C) 2019 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/fdsabr.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testFdmSabrOp, testOosterleeTestCaseIV, which wait for S-core's Richardson extrapolation and Sobol Brownian bridge sequences

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class FdSabrTests
{
    [Fact]
    public void TestFdmSabrCevPricing()
    {
        // Testing FDM CEV pricing with trivial SABR model...
        var today = new Date(3, Month.January, 2019);
        DayCounter dc = new Actual365Fixed();
        var context = new EvaluationContext { EvaluationDate = today };

        Date maturityDate = today + new Period(12, TimeUnit.Months);

        Real[] betas = [0.1, 0.9];
        Real[] strikes = [0.9, 1.5];

        const Real f0 = 1.2;
        const Real alpha = 0.35;
        const Real nu = 1e-3;
        const Real rho = 0.25;

        YieldTermStructure rTS = new FlatForward(today, 0.05, dc);

        var exercise = new EuropeanExercise(maturityDate);

        OptionType[] optionTypes = [OptionType.Put, OptionType.Call];

        const Real tol = 5e-5;

        foreach (OptionType optionType in optionTypes)
        {
            foreach (Real strike in strikes)
            {
                var payoff = new PlainVanillaPayoff(optionType, strike);

                var option = new VanillaOption(payoff, exercise);

                foreach (Real beta in betas)
                {
                    Real calculated = FdSabrVanillaEngine.Calculate(
                        option, context, f0, alpha, beta, nu, rho, rTS, 100, 400, 3).NPV!.Value;

                    Real expected = AnalyticCEVEngine.Calculate(option, context, f0, alpha, beta, rTS).NPV!.Value;

                    Assert.False(
                        Math.Abs(expected - calculated) > tol,
                        "failed to calculate vanilla CEV option prices"
                        + $"\n    beta            : {beta}"
                        + $"\n    strike          : {strike}"
                        + $"\n    option type     : {(payoff.OptionType == OptionType.Call ? "Call" : "Put")}"
                        + $"\n    analytic npv    : {expected}"
                        + $"\n    pde npv         : {calculated}"
                        + $"\n    npv difference  : {Math.Abs(expected - calculated)}"
                        + $"\n    tolerance       : {tol}");
                }
            }
        }
    }

    [Fact]
    public void TestFdmSabrVsVolApproximation()
    {
        // Testing FDM SABR vs approximations...
        var today = new Date(8, Month.January, 2019);
        DayCounter dc = new Actual365Fixed();
        var context = new EvaluationContext { EvaluationDate = today };

        Date maturityDate = today + new Period(6, TimeUnit.Months);
        double maturityTime = dc.YearFraction(today, maturityDate);

        YieldTermStructure rTS = new FlatForward(today, 0.05, dc);

        const Real f0 = 100;

        var bsProcess = new GeneralizedBlackScholesProcess(
            f0, rTS, rTS, BlackConstantVol.Create(0, new NullCalendar(), 0.2, dc, context));

        const Real alpha = 0.35;
        const Real beta = 0.85;
        const Real nu = 0.75;
        const Real rho = 0.85;

        Real[] strikes = [90, 100, 110];
        OptionType[] optionTypes = [OptionType.Put, OptionType.Call];

        const Real tol = 2.5e-3;
        foreach (OptionType optionType in optionTypes)
        {
            foreach (Real strike in strikes)
            {
                var option = new VanillaOption(
                    new PlainVanillaPayoff(optionType, strike), new EuropeanExercise(maturityDate));

                Real npv = FdSabrVanillaEngine.Calculate(option, context, f0, alpha, beta, nu, rho, rTS, 25, 100, 50).NPV!.Value;

                Volatility fdmVol = option.ImpliedVolatility(npv, bsProcess, context);

                Real hagenVol = SabrFunctions.SabrVolatility(strike, f0, maturityTime, alpha, beta, nu, rho);

                Real diff = Math.Abs(fdmVol - hagenVol);

                Assert.False(
                    Math.Abs(fdmVol - hagenVol) > tol,
                    "large difference between Hagen formula and FDM"
                    + $"\n    strike          : {strike}"
                    + $"\n    option type     : {(optionType == OptionType.Call ? "Call" : "Put")}"
                    + $"\n    Hagen vol       : {hagenVol}"
                    + $"\n    pde vol         : {fdmVol}"
                    + $"\n    vol difference  : {diff}"
                    + $"\n    tolerance       : {tol}");
            }
        }
    }

    [Fact]
    public void TestBenchOpSabrCase()
    {
        // Testing SABR BenchOp problem...

        // von Sydow, L, Milovanović, S, Larsson, E, In't Hout, K,
        // Wiktorsson, M, Oosterlee, C.W, Shcherbakov, V, Wyns, M,
        // Leitao Rodriguez, A, Jain, S, et al. (2018)
        // BENCHOP–SLV: the BENCHmarking project in Option
        // Pricing–Stochastic and Local Volatility problems
        // https://ir.cwi.nl/pub/28249
        var today = new Date(8, Month.January, 2019);
        DayCounter dc = new Actual365Fixed();
        var context = new EvaluationContext { EvaluationDate = today };

        YieldTermStructure rTS = new FlatForward(today, 0.0, dc);

        Size[] maturityInYears = [2, 10];

        Real[] f0s = [0.5, 0.07];
        Real[] alphas = [0.5, 0.4];
        Real[] nus = [0.4, 0.8];
        Real[] betas = [0.5, 0.5];
        Real[] rhos = [0.0, -0.6];

        Real[][] expected =
        [
            [0.221383196830866, 0.193836689413803, 0.166240814653231],
            [0.052450313614407, 0.046585753491306, 0.039291470612989],
        ];

        const Size gridX = 400;
        const Size gridY = 25;
        const Size gridT = 10;

        const Real factor = 2;

        const Real tol = 2e-4;

        for (Size i = 0; i < f0s.Length; ++i)
        {
            Date maturity = today + new Period(maturityInYears[i] * 365, TimeUnit.Days);
            double t = dc.YearFraction(today, maturity);

            Real f0 = f0s[i];
            Real alpha = alphas[i];
            Real nu = nus[i];
            Real beta = betas[i];
            Real rho = rhos[i];

            Real[] strikes = [f0 * Math.Exp(-0.1 * Math.Sqrt(t)), f0, f0 * Math.Exp(0.1 * Math.Sqrt(t))];

            for (Size j = 0; j < strikes.Length; ++j)
            {
                Real strike = strikes[j];

                var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(maturity));

                Real calculated = FdSabrVanillaEngine.Calculate(
                    option,
                    context,
                    f0,
                    alpha,
                    beta,
                    nu,
                    rho,
                    rTS,
                    (Size)(gridT * factor),
                    (Size)(gridX * factor),
                    (Size)(gridY * Math.Sqrt(factor))).NPV!.Value;
                Real diff = Math.Abs(calculated - expected[i][j]);

                Assert.False(
                    diff > tol,
                    "failed to reproduce reference values"
                    + $"\n    strike     : {strike}"
                    + $"\n    maturity   : {maturity}"
                    + $"\n    reference  : {expected[i][j]}"
                    + $"\n    calculated : {calculated}"
                    + $"\n    difference : {diff}"
                    + $"\n    tolerance  : {tol}");
            }
        }
    }
}
