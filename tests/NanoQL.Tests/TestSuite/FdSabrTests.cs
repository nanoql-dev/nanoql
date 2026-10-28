// SPDX-FileCopyrightText: Copyright (C) 2019 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/fdsabr.cpp @ v1.44
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
public class FdSabrTests
{
    [Fact]
    public void TestFdmSabrOp()
    {
        // Testing FDM SABR operator...

        var today = new Date(22, Month.February, 2018);
        DayCounter dc = new Actual365Fixed();
        var context = new EvaluationContext { EvaluationDate = today };

        Date maturityDate = today + new Period(2, TimeUnit.Years);
        Real maturityTime = dc.YearFraction(today, maturityDate);

        const Real strike = 1.5;

        var exercise = new EuropeanExercise(maturityDate);

        var putPayoff = new PlainVanillaPayoff(OptionType.Put, strike);
        var callPayoff = new PlainVanillaPayoff(OptionType.Call, strike);

        var optionPut = new VanillaOption(putPayoff, exercise);
        var optionCall = new VanillaOption(callPayoff, exercise);

        YieldTermStructure rTS = new FlatForward(today, 0.0, dc);

        const Real f0 = 1.0;
        const Real alpha = 0.35;
        const Real nu = 1.0;
        const Real rho = 0.25;

        Real[] betas = [0.25, 0.6];

        // flatVol(0.2, dc): a constant volatility from the evaluation date
        var bsProcess = new GeneralizedBlackScholesProcess(
            f0, rTS, rTS, new BlackConstantVol(today, new NullCalendar(), 0.2, dc));

        var errors = new List<string>();
        foreach (Real beta in betas)
        {
            Real pdePut = FdSabrVanillaEngine.Calculate(
                optionPut, context, f0, alpha, beta, nu, rho, rTS, 100, 400, 100).NPV!.Value;

            // check put/call parity
            Real pdeCall = FdSabrVanillaEngine.Calculate(
                optionCall, context, f0, alpha, beta, nu, rho, rTS, 100, 400, 100).NPV!.Value;

            Real pdeFwd = pdeCall - pdePut;

            Real parityDiff = Math.Abs(pdeFwd - (f0 - strike));
            const Real parityTol = 1e-4;
            if (parityDiff > parityTol)
            {
                errors.Add(
                    "failed to validate the call/put parity"
                    + $"\n    beta           : {beta}"
                    + $"\n    strike         : {strike}"
                    + $"\n    fwd (call/put) : {pdeFwd}"
                    + $"\n    fwd (f0-strike): {f0 - strike}"
                    + $"\n    diff           : {parityDiff}"
                    + $"\n    tol            : {parityTol}");
            }

            Real putPdeImplVol = optionPut.ImpliedVolatility(pdePut, bsProcess, context, 1e-6);

            Real McSabr(Real dt) => SabrMonteCarloPricer(f0, maturityTime, putPayoff, alpha, beta, nu, rho, dt);

            Real mcNPV = new RichardsonExtrapolation(McSabr, 1 / 4.0).Evaluate(4.0, 2.0);

            Real putMcImplVol = optionPut.ImpliedVolatility(mcNPV, bsProcess, context, 1e-6);

            Real volDiff = Math.Abs(putPdeImplVol - putMcImplVol);

            const Real volTol = 5e-3;
            if (volDiff > volTol)
            {
                errors.Add(
                    "failed to validate PDE against MC implied volatility"
                    + $"\n    beta         : {beta}"
                    + $"\n    strike       : {strike}"
                    + $"\n    PDE impl vol : {putPdeImplVol}"
                    + $"\n    MC  impl vol : {putMcImplVol}"
                    + $"\n    diff         : {volDiff}"
                    + $"\n    tol          : {volTol}");
            }
        }

        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

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
    public void TestOosterleeTestCaseIV()
    {
        // Testing Chen, Oosterlee and Weide test case IV...

        var today = new Date(8, Month.January, 2019);
        DayCounter dc = new Actual365Fixed();
        var context = new EvaluationContext { EvaluationDate = today };

        YieldTermStructure rTS = new FlatForward(today, 0.0, dc);

        const Real f0 = 0.07;
        const Real alpha = 0.4;
        const Real nu = 0.8;
        const Real beta = 0.4;
        const Real rho = -0.6;

        Period[] maturities = [new(2, TimeUnit.Years), new(5, TimeUnit.Years), new(10, TimeUnit.Years)];

        Real[] strikes = [0.4 * f0, f0, 1.6 * f0];

        const Real tol = 0.00035;
        var errors = new List<string>();
        for (Size i = 0; i < maturities.Length; ++i)
        {
            Date maturityDate = today + maturities[i];
            Real maturityTime = dc.YearFraction(today, maturityDate);

            var timeSteps = (Size)(5 * maturityTime);

            var exercise = new EuropeanExercise(maturityDate);

            for (Size j = 0; j < strikes.Length; ++j)
            {
                var payoff = new PlainVanillaPayoff(OptionType.Call, strikes[j]);

                var option = new VanillaOption(payoff, exercise);

                Real calculated = FdSabrVanillaEngine.Calculate(
                    option, context, f0, alpha, beta, nu, rho, rTS, timeSteps, 200, 21).NPV!.Value;

                Size referenceResults = (i * 3) + j;

                Real expected = new RichardsonExtrapolation(
                    t => OsterleeReferenceResults(referenceResults, t), 1 / 16.0, 1).Evaluate(2.0);

                Real diff = Math.Abs(calculated - expected);
                if (diff > tol)
                {
                    errors.Add(
                        "can not reproduce reference values from Monte-Carlo"
                        + $"\n    strike     : {payoff.Strike}"
                        + $"\n    maturity   : {maturityDate}"
                        + $"\n    reference  : {expected}"
                        + $"\n    calculated : {calculated}"
                        + $"\n    difference : {diff}"
                        + $"\n    tolerance  : {tol}");
                }
            }
        }

        Assert.True(errors.Count == 0, string.Join("\n", errors));
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

    // fdsabr.cpp's OsterleeReferenceResults' table (lines 135-139): each case's Monte Carlo reference at steps 1/16 and
    // 1/32.
    private static readonly Real[][] OsterleeData =
    [
        [0.0610, 0.0604], [0.0468, 0.0463], [0.0347, 0.0343],
        [0.0632, 0.0625], [0.0512, 0.0506], [0.0406, 0.0400],
        [0.0635, 0.0630], [0.0523, 0.0520], [0.0422, 0.0421],
    ];

    // fdsabr.cpp's SabrMonteCarloPricer (lines 45-112): the payoff's mean over 64K Euler paths on Sobol Brownian
    // bridges.
    private static Real SabrMonteCarloPricer(
        Real f0, Real maturity, Payoff payoff, Real alpha, Real beta, Real nu, Real rho, Real dt)
    {
        const Size nSims = 64 * 1024;

        Real timeStepsPerYear = 1.0 / dt;
        var timeSteps = (Size)((maturity * timeStepsPerYear) + 1e-8);

        Real sqrtDt = Math.Sqrt(dt);
        Real w = Math.Sqrt(1.0 - (rho * rho));

        Real logAlpha = Math.Log(alpha);

        var rsg = new SobolBrownianBridgeRsg(2, timeSteps, SobolBrownianGeneratorBaseOrdering.Diagonal, 12345);

        var stats = new GeneralStatistics();

        for (Size i = 0; i < nSims; ++i)
        {
            Real f = f0;
            Real a = logAlpha;

            ReadOnlySpan<Real> n = rsg.NextSequence().Value.Span;

            for (Size j = 0; j < timeSteps && f > 0.0; ++j)
            {
                Real r1 = n[j];
                Real r2 = (rho * r1) + (n[j + timeSteps] * w);

                // simple Euler method
                f += Math.Exp(a) * Math.Pow(f, beta) * r1 * sqrtDt;
                a += (-0.5 * nu * nu * dt) + (nu * r2 * sqrtDt);
            }

            f = Math.Max(0.0, f);
            stats.Add(payoff.Evaluate(f));
        }

        return stats.Mean();
    }

    // fdsabr.cpp's OsterleeReferenceResults (lines 114-132): the table's value at step 1/16 or 1/32.
    private static Real OsterleeReferenceResults(Size i, Real t)
    {
        Size k;
        if (ComparisonFunctions.CloseEnough(t, 1 / 16.0))
        {
            k = 0;
        }
        else if (ComparisonFunctions.CloseEnough(t, 1 / 32.0))
        {
            k = 1;
        }
        else
        {
            throw new NanoQLException("unmatched reference result lookup");
        }

        return OsterleeData[i][k];
    }
}
