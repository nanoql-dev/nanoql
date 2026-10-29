// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/noarbsabr.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Reflection;
using NanoQL.Experimental;
using NanoQL.TermStructures;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class NoArbSabrTests
{
    [Fact]
    public void TestAbsorptionMatrix()
    {
        // Testing no-arbitrage Sabr absorption matrix...

        // check some points explicitly against the external file's contents

        // sigmaI, beta, rho, nu, tau, absorptions
        CheckD0(1, 0.01, 0.75, 0.1, 0.25, 60342); // upper left corner
        CheckD0(0.8, 0.01, 0.75, 0.1, 0.25, 12148);
        CheckD0(0.05, 0.01, 0.75, 0.1, 0.25, 0);
        CheckD0(1, 0.01, 0.75, 0.1, 10.0, 1890509);
        CheckD0(0.8, 0.01, 0.75, 0.1, 10.0, 1740233);
        CheckD0(0.05, 0.01, 0.75, 0.1, 10.0, 0);
        CheckD0(1, 0.01, 0.75, 0.1, 30.0, 2174176);
        CheckD0(0.8, 0.01, 0.75, 0.1, 30.0, 2090672);
        CheckD0(0.05, 0.01, 0.75, 0.1, 30.0, 31);
        CheckD0(0.35, 0.10, -0.75, 0.1, 0.25, 0);
        CheckD0(0.35, 0.10, -0.75, 0.1, 14.75, 1087841);
        CheckD0(0.35, 0.10, -0.75, 0.1, 30.0, 1406569);
        CheckD0(0.24, 0.90, 0.50, 0.8, 1.25, 27);
        CheckD0(0.24, 0.90, 0.50, 0.8, 25.75, 167541);
        CheckD0(0.05, 0.90, -0.75, 0.8, 2.0, 17);
        CheckD0(0.05, 0.90, -0.75, 0.8, 30.0, 42100); // lower right corner
    }

    [Fact]
    public void TestConsistencyWithHagan()
    {
        // Testing consistency of noarb-sabr with Hagan et al (2002)

        // parameters taken from Doust's paper, figure 3

        double tau = 1.0;
        double beta = 0.5;
        double alpha = 0.026;
        double rho = -0.1;
        double nu = 0.4;
        double f = 0.0488;

        var sabr = new SabrSmileSection(tau, f, [alpha, beta, nu, rho]);
        var noarbsabr = new NoArbSabrSmileSection(tau, f, [alpha, beta, nu, rho]);

        double absProb = noarbsabr.Model.AbsorptionProbability;
        Assert.False(absProb > 1E-10 || absProb < 0.0, $"absorption probability should be close to zero, but is {absProb}");

        double strike = 0.0001;
        while (strike < 0.15)
        {
            // test vanilla prices
            double sabrPrice = sabr.OptionPrice(strike);
            double noarbsabrPrice = noarbsabr.OptionPrice(strike);
            Assert.False(
                Math.Abs(sabrPrice - noarbsabrPrice) > 1e-5,
                $"incosistent Hagan price ({sabrPrice}) and noarb-sabr price ({noarbsabrPrice}) at strike {strike}");

            // test digitals
            double sabrDigital = sabr.DigitalOptionPrice(strike);
            double noarbsabrDigital = noarbsabr.DigitalOptionPrice(strike);
            Assert.False(
                Math.Abs(sabrDigital - noarbsabrDigital) > 1e-3,
                $"incosistent Hagan digital ({sabrDigital}) and noarb-sabr digital ({noarbsabrDigital}) at strike {strike}");

            // test density
            double sabrDensity = sabr.Density(strike);
            double noarbsabrDensity = noarbsabr.Density(strike);
            Assert.False(
                Math.Abs(sabrDensity - noarbsabrDensity) > 1e-0,
                $"incosistent Hagan density ({sabrDensity}) and noarb-sabr density ({noarbsabrDensity}) at strike {strike}");
            strike += 0.0001;
        }
    }

    // QuantLib's checkD0, on detail::D0Interpolator (internal here, reached by reflection)
    private static void CheckD0(double sigmaI, double beta, double rho, double nu, double tau, int absorptions)
    {
        double forward = 0.03; // does not matter in the end
        double alpha = sigmaI / Math.Pow(forward, beta - 1.0);
        Type type = typeof(NoArbSabrModel).Assembly.GetType("NanoQL.Experimental.NoArbSabrD0Interpolator", throwOnError: true)!;
        object d = Activator.CreateInstance(
            type, BindingFlags.NonPublic | BindingFlags.Instance, null, [forward, tau, alpha, beta, nu, rho], null)!;
        double d0 = (double)type.GetMethod("Value", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(d, null)!;
        double nsim = (double)typeof(NoArbSabrModel).GetField("Nsim", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        Assert.False(
            Math.Abs((d0 * nsim) - absorptions) > 0.1,
            $"failed to reproduce number of absorptions at sigmaI={sigmaI}, beta={beta}, rho={rho}, nu={nu} tau={tau}: "
            + $"D0Interpolator says {d0 * nsim} while the reference value is {absorptions}");
    }
}
