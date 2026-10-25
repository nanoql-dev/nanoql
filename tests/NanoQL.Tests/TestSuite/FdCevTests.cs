// SPDX-FileCopyrightText: Copyright (C) 2018 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/fdcev.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class FdCevTests
{
    // QL_EPSILON (qldefines.hpp): machine epsilon.
    private const double QL_EPSILON = 2.220446049250313e-16;

    [Fact]
    public void TestLocalMartingale()
    {
        // Testing local martingale property of CEV process with PDF...
        const double t = 1.0;

        const Real f0 = 2.1;
        const Real alpha = 1.75;
        Real[] betas = [-2.4, 0.23, 0.9, 1.1, 1.5];

        foreach (Real beta in betas)
        {
            var rndCalculator = new CEVRNDCalculator(f0, alpha, beta);

            const Real eps = 1e-10;
            const Real tol = 100 * eps;

            Real upperBound = 10 * rndCalculator.InvCdf(1 - eps, t);

            Real expectationValue = new GaussLobattoIntegral(10000, eps).Integrate(
                f => f * rndCalculator.Pdf(f, t), QL_EPSILON, upperBound);

            Real diff = expectationValue - f0;

            Assert.False(
                beta < 1.0 && Math.Abs(diff) > tol,
                "CEV process should be a martingale for beta < 1.0"
                + $"\n    expected:   {f0}"
                + $"\n    difference  {diff:E}"
                + $"\n    tolerance:  {tol:E}");

            Assert.False(
                beta > 1.0 && diff > -tol,
                "CEV process should only be a local martingale "
                + "for beta > 1.0. Expectation is E[F_t|F_0] < F_0"
                + $"\n    E[F_t|F_0]: {expectationValue}"
                + $"\n    F_0:        {f0}");

            // check local martingale property with Monte-Carlo simulation
            const Size nSims = 5000;

            const Size nSteps = 2000;
            const double dt = t / nSteps;
            double sqrtDt = Math.Sqrt(dt);

            var stat = new GeneralStatistics();
            var mt = new InverseCumulativeRng<MersenneTwisterUniformRng, InverseCumulativeNormal>(
                new MersenneTwisterUniformRng(42));

            if (beta > 1.2)
            {
                for (Size i = 0; i < nSims; ++i)
                {
                    Real f = f0;
                    for (Size j = 0; j < nSteps; ++j)
                    {
                        f += alpha * Math.Pow(f, beta) * mt.Next().Value * sqrtDt;

                        // std::max(0.0, f)
                        f = 0.0 < f ? f : 0.0;

                        if (f == 0.0)
                        {
                            break; // absorbing boundary
                        }
                    }

                    stat.Add(f - f0);
                }

                Real calculated = stat.Mean();
                Real error = stat.ErrorEstimate();

                Assert.False(
                    Math.Abs(calculated - diff) > 2.35 * error,
                    "failed to calculate local martingale property "
                    + "by Monte-Carlo Simulation for beta > 1.0. "
                    + $"\n    E[F_t|F_0]   : {expectationValue}"
                    + $"\n    E_MC[F_t|F_0]: {calculated + f0}"
                    + $"\n    error_MC     : {error}"
                    + $"\n    difference   : {Math.Abs(calculated - diff)}"
                    + $"\n    tolerance    : {2.35 * error}");
            }
        }
    }

    [Fact]
    public void TestFdmCevOp()
    {
        // Testing FDM constant elasticity of variance (CEV) operator...
        var today = new Date(22, Month.February, 2018);
        DayCounter dc = new Actual365Fixed();
        var context = new EvaluationContext { EvaluationDate = today };

        Date maturityDate = today + new Period(12, TimeUnit.Months);
        const Real strike = 2.3;

        OptionType[] optionTypes = [OptionType.Call, OptionType.Put];

        var exercise = new EuropeanExercise(maturityDate);

        foreach (OptionType optionType in optionTypes)
        {
            var payoff = new PlainVanillaPayoff(optionType, strike);

            YieldTermStructure rTS = new FlatForward(today, 0.15, dc);

            const Real f0 = 2.1;
            const Real alpha = 0.75;

            Real[] betas = [-2.0, -0.5, 0.45, 0.6, 0.9, 1.45];
            foreach (Real beta in betas)
            {
                var option = new VanillaOption(payoff, exercise);
                Real analyticNPV = AnalyticCEVEngine.Calculate(option, context, f0, alpha, beta, rTS).NPV!.Value;

                const Real eps = 1e-3;

                Real analyticUpNPV =
                    AnalyticCEVEngine.Calculate(option, context, f0 * (1 + eps), alpha, beta, rTS).NPV!.Value;
                Real analyticDownNPV =
                    AnalyticCEVEngine.Calculate(option, context, f0 * (1 - eps), alpha, beta, rTS).NPV!.Value;

                Real analyticDelta = (analyticUpNPV - analyticDownNPV) / (2 * eps * f0);

                OneAssetOptionResults results = FdCEVVanillaEngine.Calculate(
                    option, context, f0, alpha, beta, rTS, 100, 1000, 1, 1.0, 1e-6);

                Real calculatedNPV = results.NPV!.Value;
                Real calculatedDelta = results.Delta!.Value;

                const Real tol = 0.01;
                Assert.False(
                    Math.Abs(calculatedNPV - analyticNPV) > tol || Math.Abs(calculatedDelta - analyticDelta) > tol,
                    "failed to calculate vanilla option prices/delta "
                    + $"\n    beta            : {beta}"
                    + $"\n    option type     : {(payoff.OptionType == OptionType.Call ? "Call" : "Put")}"
                    + $"\n    analytic npv    : {analyticNPV}"
                    + $"\n    pde npv         : {calculatedNPV}"
                    + $"\n    npv difference  : {Math.Abs(calculatedNPV - analyticNPV)}"
                    + $"\n    tolerance       : {tol}"
                    + $"\n    analytic delta  : {analyticDelta}"
                    + $"\n    pde delta       : {calculatedDelta}"
                    + $"\n    delta difference: {Math.Abs(calculatedDelta - analyticDelta)}"
                    + $"\n    tolerance       : {tol}");
            }
        }
    }
}
