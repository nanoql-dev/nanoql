// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/shortratemodels.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testCachedHullWhite, testCachedHullWhiteFixedReversion and testCachedHullWhite2, which wait for S-bond's swaption helpers and calibration; testSwaps, which waits for B3.4's TreeVanillaSwapEngine; testExtendedCoxIngersollRossUpdatesWhenTermStructureRelinks and testExtendedCoxIngersollRossDiscountFactor, which wait for B3.1's ExtendedCoxIngersollRoss
// Replaced: testHullWhiteUpdatesR0WhenTermStructureRelinks (Conventions/ModelRebuildTests.cs)

using NanoQL.Models;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class ShortRateModelsTests
{
    [Fact]
    public void TestFuturesConvexityBias()
    {
        // Testing Hull-White futures convexity bias...

        // G. Kirikos, D. Novak, "Convexity Conundrums", Risk Magazine, March 1997
        Real futureQuote = 94.0;
        Real sigma = 0.015;
        double t = 5.0;
        Real tolerance = 0.0000001;

        foreach ((double T, Real a, Real expectedForward) in (ReadOnlySpan<(double, Real, Real)>)
                 [
                     (5.25, 0.03, 0.0573037),
                     (5.25, 1e-4, 0.0568627),
                     (5.25, 0.0, 0.0568611),
                     (5.001, 0.03, 0.0575736),
                     (5.0, 0.03, 0.0575747),
                 ])
        {
            Rate futureImpliedRate = (100.0 - futureQuote) / 100.0;
            Rate calculatedForward = futureImpliedRate - HullWhite.ConvexityBias(futureQuote, t, T, sigma, a);

            Real error = Math.Abs(calculatedForward - expectedForward);
            Assert.True(
                error < tolerance,
                $"Failed to reproduce convexity bias:\ncalculated: {calculatedForward}\n  expected: {expectedForward}"
                + $"\n     error: {error:E}\n tolerance: {tolerance:E}");
        }
    }

    [Fact]
    public void TestVasicekDiscountFactorForSmallMeanReversion()
    {
        // Testing zero-bond pricing for Vasicek model with small mean reversion...

        const Rate r0 = 0.05;
        const Real a = 1e-12;
        const Real b = 0.05;
        const Volatility sigma = 0.01;
        const Real lambda = 0.0;
        const double now = 0.0;
        const double maturity = 1.0;

        var model = new Vasicek(r0, a, b, sigma, lambda);

        Real expected = Math.Exp((-r0 * maturity) + (sigma * sigma * maturity * maturity * maturity / 6.0));
        Real calculated = model.DiscountBond(now, maturity, r0);

        const Real tolerance = 1e-12;
        Real error = Math.Abs(expected - calculated);
        Assert.True(
            error <= tolerance,
            $"Failed to reproduce small-mean-reversion zero-bond price:\n  calculated: {calculated}\n  expected  : {expected}"
            + $"\n  error     : {error:E}\n  tolerance : {tolerance:E}");
    }
}
