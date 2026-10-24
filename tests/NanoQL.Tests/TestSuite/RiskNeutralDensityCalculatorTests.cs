// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: Copyright (C) 2015, 2016 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2026 Yassine Idyiahia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/riskneutraldensitycalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testDensityAgainstOptionPrices, testBSMagainstHestonRND, testLocalVolatilityRND, testSquareRootProcessRND, testBlackScholesWithSkew, testSmileSectionRNDvsBSM, testSmileSectionRNDQuantileRange, testSmileSectionRNDLowVolFatWings, testSmileSectionRNDvsHeston, testSmileSectionRNDMissingAtmLevel, testSmileSectionRNDMartingale, testSmileSectionRNDLeftTailCdf (S-van's C5 and S-hes's density calculators)

using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class RiskNeutralDensityCalculatorTests
{
    // QL_EPSILON (qldefines.hpp): machine epsilon.
    private const double QL_EPSILON = 2.220446049250313e-16;

    [Fact]
    public void TestMassAtZeroCEVProcessRND()
    {
        // Testing the mass at zero for a constant elasticity of variance (CEV) process...
        const Real f0 = 100.0;
        const double t = 2.75;

        (Real Alpha, Real Beta)[] parameters =
        [
            (0.1, 1.6),
            (0.01, 2.0),
            (10.0, 0.35),
            (50.0, 0.1),
        ];

        const Real tol = 1e-4;

        foreach ((Real alpha, Real beta) in parameters)
        {
            var calculator = new CEVRNDCalculator(f0, alpha, beta);

            Real ax = 15.0 * Math.Sqrt(t) * alpha * Math.Pow(f0, beta);

            Real calculated = new GaussLobattoIntegral(1000, 1e-8).Integrate(
                x => calculator.Pdf(x, t), Math.Max(QL_EPSILON, f0 - ax), f0 + ax)
                + calculator.MassAtZero(t);

            Assert.False(
                Math.Abs(calculated - 1.0) > tol,
                "failed to reproduce the total probability mass"
                + $"\n   alpha:     {alpha}"
                + $"\n   beta:      {beta}"
                + $"\n   prob mass: {calculated}"
                + $"\n   tolerance: {tol}");
        }
    }

    [Fact]
    public void TestCEVCDF()
    {
        // Testing CDF for a constant elasticity of variance (CEV) process...
        const Real f0 = 2.1;
        const double t = 0.75;

        const Real alpha = 0.1;
        Real[] betas = [0.45, 1.25];

        const Real tol = 1e-6;

        // QuantLib's loop starts at the second elasticity, as here
        for (Size i = 1; i < betas.Length; ++i)
        {
            Real beta = betas[i];
            var calculator = new CEVRNDCalculator(f0, alpha, beta);

            for (Real x = 1.3; x < 3.1; x += 0.1)
            {
                Real cdfValue = calculator.Cdf(x, t);
                Real calculated = calculator.InvCdf(cdfValue, t);

                Assert.False(
                    Math.Abs(x - calculated) > tol,
                    "failed to reproduce the inverse cumulative probability"
                    + $"\n   alpha:     {alpha}"
                    + $"\n   beta:      {beta}"
                    + $"\n   x:         {x}"
                    + $"\n   calculated:{calculated}"
                    + $"\n   difference:{x - calculated}"
                    + $"\n   tolerance: {tol}");
            }
        }
    }
}
