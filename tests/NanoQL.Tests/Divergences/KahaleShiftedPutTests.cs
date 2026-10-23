// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Kahale's shifted puts (the S-g1d plan's G1R6 (viii)): where its own functions price a strike below the forward,
/// QuantLib's section implies the volatility from the put on the unshifted strike against the shifted forward
/// (<c>kahalesmilesection.cpp:252-256</c>), so on a shifted source every such strike's volatility reads 0. On the
/// wheel, over a SABR smile at a forward of 1% shifted by 2% with an exercise time of 5, the volatility at -1% is 0.0
/// plain and interpolated, and with interpolation at 0.5% too, where the section's own call prices are 0.0200181 and
/// 0.0063525. nanoql implies it from the put on the shifted strike. QuantLib's check: the section's own call prices,
/// which Black's formula on the shifted strike and forward at nanoql's volatility gives back, through put-call parity,
/// within the volatility solve's accuracy.
/// </summary>
public class KahaleShiftedPutTests
{
    [Fact]
    public void ThePutBranchsVolatilityGivesTheSectionsOwnPrices()
    {
        const double shift = 0.02, forward = 0.01, t = 5.0;
        var source = new SabrSmileSection(t, forward, [0.02, 0.5, 0.3, -0.2], shift);
        foreach (bool interpolate in new[] { false, true })
        {
            var kahale = new KahaleSmileSection(source, null, interpolate);
            foreach (double k in interpolate ? new[] { -0.01, -0.005, 0.0, 0.005, 0.009 } : [-0.015, -0.01, -0.005, 0.0])
            {
                double vol = kahale.Volatility(k);
                // QuantLib's: 0.0.
                Assert.True(vol > 0.0, $"{interpolate} {k}: {vol}");
                double black = BlackFormula(OptionType.Call, k + shift, forward + shift, vol * Math.Sqrt(t));
                double own = kahale.OptionPrice(k);
                // within the implied volatility's accuracy, 1e-6 in standard deviation
                double bound = 1e-6 * BlackFormulaStdDevDerivative(k + shift, forward + shift, vol * Math.Sqrt(t));
                Assert.True(Math.Abs(black - own) <= bound, $"{interpolate} {k}: {black} against {own}");
            }
        }
    }
}
