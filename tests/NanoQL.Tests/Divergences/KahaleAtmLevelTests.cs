// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Kahale's at-the-money level on a shifted source: QuantLib's section fits on the shifted forward and strikes and
/// returns that forward as its level (<c>kahalesmilesection.hpp:142</c>, <c>.cpp:59</c>), so every member of the base
/// that reads the level, the vega and the converted volatilities, reads the forward plus the shift. On the wheel, over
/// a SABR smile at a forward of 1% shifted by 2% with an exercise time of 5, its level is 0.03, its vega at 0.5%
/// 2.1203568910002753e-05 where the source's is 2.0058237006534898e-04, and its normal volatility at 0.5%
/// 0.00972286988319294 where the source's is 0.0036525005165526547, at a strike where the section keeps the source's
/// prices and volatilities. nanoql's level is the one given or the source's. QuantLib's check: the source section's own
/// members wherever the section keeps its prices; at a shift of 0 the two levels are QuantLib's.
/// </summary>
public class KahaleAtmLevelTests
{
    [Fact]
    public void TheLevelIsTheSourcesAndTheMembersReadingItTheSources()
    {
        var source = new SabrSmileSection(5.0, 0.01, [0.02, 0.5, 0.3, -0.2], 0.02);
        var kahale = new KahaleSmileSection(source);

        // QuantLib's: 0.03.
        Assert.Equal(source.AtmLevel(), kahale.AtmLevel());

        foreach (double k in new[] { 0.005, 0.009, 0.012, 0.02 })
        {
            Assert.Equal(source.OptionPrice(k), kahale.OptionPrice(k));
            Assert.Equal(source.Vega(k), kahale.Vega(k));
            Assert.Equal(source.Volatility(k, VolatilityType.Normal), kahale.Volatility(k, VolatilityType.Normal));
        }

        // QuantLib's at 0.5%, against the source's.
        Assert.True(Math.Abs((kahale.Vega(0.005) / 2.1203568910002753e-05) - 1.0) > 0.5);
        Assert.True(Math.Abs((kahale.Volatility(0.005, VolatilityType.Normal) / 0.00972286988319294) - 1.0) > 0.5);
    }
}
