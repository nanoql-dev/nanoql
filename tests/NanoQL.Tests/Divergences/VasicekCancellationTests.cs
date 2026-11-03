// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Vasicek model at small mean reversion: QuantLib 1.44's A and B cut to their a = 0 forms below √ε, dropping terms
/// of order a·τ, and above it subtract O(1/a) terms (<c>vasicek.cpp:36-58</c>), so its zero bond is anywhere between
/// 4.6e-163 and 9.1e131 for a between 1.5e-8 and 1e-7 (the wheel, below) and still off by 5.4e-4 at 1e-6. nanoql's are
/// QuantLib's master's forms (#2882, <c>da854f617</c>): B as τ·(1 − e^(−aτ))/(aτ) and the exponent of A by the φ and h
/// series below aτ = 1, the bond option's variance likewise. QuantLib's check: upstream's new test lines,
/// <c>testVasicekDiscountFactorJustAboveSmallMeanReversionCutoff</c> and
/// <c>testVasicekDiscountBondOptionForSmallMeanReversion</c>, against 60-digit references at upstream's tolerances.
/// </summary>
public class VasicekCancellationTests
{
    [Fact]
    public void TheZeroBondJustAboveTheCutoff()
    {
        // (r0, a, b, sigma, lambda, maturity, the 60-digit reference); QuantLib 1.44 on the wheel, in order:
        // 4.595159967318222e-163, 9.110983579440905e131, 9808.929352074143, 11.768434373686008, 0.8620335395273923,
        // 0.8625011425372132, 0.8625023144044159; 0.8751733190429474, 0.0, 20.883051804881248, 0.8751643312670587,
        // 0.8743013437979879, then the reference's value at 0.05, 0.4 and 2.
        foreach ((double r0, double a, double b, double sigma, double lambda, double maturity, double expected) in new[]
                 {
                     (0.03, 1.5e-8, 0.03, 0.01, 0.0, 5.0, 0.86250298709518503),
                     (0.03, 2.0e-8, 0.03, 0.01, 0.0, 5.0, 0.86250298706149353),
                     (0.03, 5.0e-8, 0.03, 0.01, 0.0, 5.0, 0.86250298685934446),
                     (0.03, 1.0e-7, 0.03, 0.01, 0.0, 5.0, 0.86250298652242929),
                     (0.03, 1.0e-6, 0.03, 0.01, 0.0, 5.0, 0.8625029804579708),
                     (0.03, 1.0e-5, 0.03, 0.01, 0.0, 5.0, 0.86250291981478866),
                     (0.03, 1.0e-4, 0.03, 0.01, 0.0, 5.0, 0.86250231352326301),
                     (0.05, 1.0e-9, 0.04, 0.02, -0.3, 10.0, 0.87517331816777411),
                     (0.05, 1.5e-8, 0.04, 0.02, -0.3, 10.0, 0.87517330591534837),
                     (0.05, 1.0e-7, 0.04, 0.02, -0.3, 10.0, 0.87517323152564763),
                     (0.05, 1.0e-5, 0.04, 0.02, -0.3, 10.0, 0.87516456763064299),
                     (0.05, 1.0e-3, 0.04, 0.02, -0.3, 10.0, 0.87430134378833069),
                     (0.05, 0.05, 0.04, 0.02, -0.3, 10.0, 0.8382610373371705),
                     (0.05, 0.4, 0.04, 0.02, -0.3, 10.0, 0.73828243526215487),
                     (0.05, 2.0, 0.04, 0.02, -0.3, 10.0, 0.68657658594684234),
                 })
        {
            double calculated = new Vasicek(r0, a, b, sigma, lambda).DiscountBond(0.0, maturity, r0);
            Assert.True(Math.Abs((calculated / expected) - 1.0) <= 1e-14, $"a = {a}: {calculated} against {expected}");
        }
    }

    [Fact]
    public void TheBondOptionAtSmallReversion()
    {
        // r0 0.05, b 0.05, sigma 0.02, lambda 0.1; a strike of 0.9 at 2 years on a 7-year bond. QuantLib 1.44 on the
        // wheel: at a = 1e-7 a call of 0.0 and a put of 0.01046968065660516; at 1e-5, 0.006119357979071066 and
        // 0.13114983157311866.
        foreach ((double a, double call, double put) in new[]
                 {
                     (1.0e-7, 0.0061208618684224086, 0.13114554144413559),
                     (1.0e-5, 0.006120132213223925, 0.13114486562595751),
                     (0.1, 0.0015560169600970369, 0.12618440799746233),
                 })
        {
            var model = new Vasicek(0.05, a, 0.05, 0.02, 0.1);
            Assert.True(Math.Abs(model.DiscountBondOption(OptionType.Call, 0.9, 2.0, 7.0) - call) <= 1e-13, $"call at a = {a}");
            Assert.True(Math.Abs(model.DiscountBondOption(OptionType.Put, 0.9, 2.0, 7.0) - put) <= 1e-13, $"put at a = {a}");
        }
    }
}
