// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The G2 model at small mean reversion: QuantLib 1.44's <c>V</c> (<c>g2.cpp:89-100</c>) sums O(a²t³) differences of
/// O(1/a) terms, so its zero bond's error grows like 1/a³: on the wheel, 0.8997958885633307 at a = 1e-9,
/// 58,421.40718574682 at 1e-7 and 0.8800439590760323 at 1e-4 (against 0.88003659…, 0.88003659… and
/// 0.88004395350237115). nanoql's are QuantLib's master's forms (#2885, <c>81fac76b8</c>): the three terms through one
/// function of at and bt, its series below aτ + bτ = 2, and B with expm1. QuantLib's check: upstream's new test line,
/// <c>testG2DiscountBondForSmallMeanReversion</c>, against its 60-digit references at upstream's tolerance; and, at a =
/// 1e-4, QuantLib's own formulas evaluated in 60 digits.
/// </summary>
public class G2CancellationTests
{
    [Fact]
    public void TheZeroBondNearZeroReversion()
    {
        // upstream's cases: (r, a, sigma, b, eta, rho, t, T, x, y, the 60-digit reference)
        var today = new Date(15, Month.January, 2025);
        foreach ((double r, double a, double sigma, double b, double eta, double rho, double t, double T, double x, double y, double expected) in new[]
                 {
                     (0.03, 1e-9, 0.01, 0.1, 0.005, -0.5, 1.0, 5.0, 0.01, -0.01, 0.88003659072582035),
                     (0.03, 1e-8, 0.01, 0.1, 0.005, -0.5, 1.0, 5.0, 0.01, -0.01, 0.88003659138856715),
                     (0.03, 1e-7, 0.01, 0.1, 0.005, -0.5, 1.0, 5.0, 0.01, -0.01, 0.88003659801603423),
                     (0.03, 1e-6, 0.01, 0.1, 0.005, -0.5, 1.0, 5.0, 0.01, -0.01, 0.88003666429061573),
                     (0.03, 1e-5, 0.01, 0.1, 0.005, -0.5, 1.0, 5.0, 0.01, -0.01, 0.88003732702749102),
                     (0.03, 0.0001, 0.01, 0.1, 0.005, -0.5, 1.0, 5.0, 0.01, -0.01, 0.88004395350237115),
                     (0.03, 0.001, 0.01, 0.1, 0.005, -0.5, 1.0, 5.0, 0.01, -0.01, 0.88011012895729353),
                     (0.03, 0.1, 0.01, 1e-9, 0.005, -0.5, 1.0, 5.0, 0.01, -0.01, 0.89274066161481982),
                     (0.03, 0.1, 0.01, 1e-7, 0.005, -0.5, 1.0, 5.0, 0.01, -0.01, 0.89274065456434843),
                     (0.03, 0.1, 0.01, 1e-5, 0.005, -0.5, 1.0, 5.0, 0.01, -0.01, 0.89273994952677022),
                     (0.03, 0.1, 0.01, 0.001, 0.005, -0.5, 1.0, 5.0, 0.01, -0.01, 0.89266954128089552),
                     (0.03, 1e-7, 0.01, 2e-7, 0.005, -0.5, 1.0, 5.0, 0.01, -0.01, 0.88625548898935952),
                     (0.03, 1e-6, 0.01, 1e-6, 0.005, -0.5, 1.0, 5.0, 0.01, -0.01, 0.8862554988312159),
                     (0.03, 1e-5, 0.01, 0.001, 0.005, -0.5, 1.0, 5.0, 0.01, -0.01, 0.88618543097653418),
                     (0.03, 0.2, 0.01, 0.3, 0.005, -0.5, 1.0, 5.0, 0.01, -0.01, 0.88288328570041449),
                     (0.03, 0.4, 0.01, 0.1, 0.005, -0.5, 1.0, 5.0, 0.01, -0.01, 0.89837482145762172),
                     (0.03, 0.5, 0.01, 0.125, 0.005, -0.5, 1.0, 5.0, 0.01, -0.01, 0.899459669703478),
                     (0.03, 0.45, 0.01, 0.04, 0.005, -0.5, 1.0, 5.0, 0.01, -0.01, 0.90323235067173138),
                     (0.03, 1.0, 0.01, 0.03, 0.005, -0.5, 1.0, 5.0, 0.01, -0.01, 0.91184613030830664),
                     (0.03, 3.0, 0.01, 5.0, 0.005, -0.5, 1.0, 5.0, 0.01, -0.01, 0.88573682792113109),
                     (0.05, 1e-6, 0.02, 0.05, 0.01, 0.8, 0.5, 10.0, -0.02, 0.015, 0.6600529015892483),
                     (0.05, 0.03, 0.02, 1e-7, 0.01, -0.9, 2.0, 30.0, 0.0, 0.0, 0.23888590049897576),
                 })
        {
            var model = new G2(new FlatForward(today, r, new Actual365Fixed()), a, sigma, b, eta, rho);
            double calculated = model.DiscountBond(t, T, x, y);
            double error = Math.Abs((calculated / expected) - 1.0);
            Assert.True(error <= 1e-14, $"a {a}, b {b}: {calculated} against {expected}, {error:E}");
        }
    }

    [Fact]
    public void TheZeroBondAtSmallReversionAgainstQuantLibsFormulasInSixtyDigits()
    {
        // a = 1e-4, sigma 0.01, b 0.3, eta 0.009, rho -0.4 on a flat 3% Actual/365 (Fixed) curve: QuantLib's V, A and B
        // in 60-digit decimals give these; QuantLib 1.44 on the wheel reads 0.947404836126891 and 0.8952001362696115
        var today = new Date(15, Month.January, 2025);
        var model = new G2(new FlatForward(today, 0.03, new Actual365Fixed()), 1e-4, 0.01, 0.3, 0.009, -0.4);
        Assert.Equal(0.947404844802153981, model.DiscountBond(0.5, 2.0, 0.01, -0.005), 1e-15);
        Assert.Equal(0.895200137391020005, model.DiscountBond(2.0, 7.0, -0.01, 0.003), 1e-15);
    }
}
