// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/varianceoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Moved: testIntegralHeston line 104 (Divergences/IntegralHestonVarianceOptionTests.cs)

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class VarianceOptionTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(28, Month.October, 2026) };

    [Fact]
    public void TestIntegralHeston()
    {
        // Testing variance option with integral Heston engine...
        DayCounter dc = new Actual360();
        Date today = Context.EvaluationDate;

        double s0 = 1.0;

        // QuantLib's empty dividend handle: a curve the engine doesn't read
        YieldTermStructure qTS = new FlatForward(today, 0.0, dc);
        YieldTermStructure rTS = new FlatForward(today, 0.0, dc);

        double v0 = 2.0;
        double kappa = 2.0;
        double theta = 0.01;
        double sigma = 0.1;
        double rho = -0.5;

        var process = new HestonProcess(rTS, qTS, s0, v0, kappa, theta, sigma, rho);

        double strike = 0.05;
        double nominal = 1.0;
        double T = 1.5;
        Date exDate = today + (int)(360 * T);

        Payoff payoff = new PlainVanillaPayoff(OptionType.Call, strike);

        var varianceOption1 = new VarianceOption(payoff, nominal, today, exDate);
        double calculated = IntegralHestonVarianceOptionEngine.Calculate(varianceOption1, Context, process).NPV!.Value;
        double expected = 0.9104619;
        double error = Math.Abs(calculated - expected);
        Assert.False(
            error > 1.0e-7,
            $"Failed to reproduce variance-option price:\n    expected:   {expected}\n    calculated: {calculated}"
            + $"\n    error:      {error}");

        // Moved: the check of line 104, QuantLib's put 0.0466796 from its two-dimensional integral, moves to
        // Divergences/IntegralHestonVarianceOptionTests.cs: nanoql prices a put by parity on the call's integral,
        // 0.0465915
    }
}
