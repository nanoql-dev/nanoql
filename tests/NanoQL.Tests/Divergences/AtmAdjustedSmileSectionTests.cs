// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The ATM-adjusted section's prices (the S-g1d plan's G1R6 (xxiii)): QuantLib's option price, digital, vega and
/// density are the source's at the adjusted strike (<c>atmadjustedsmilesection.hpp:49-69</c>), priced on the source's
/// forward, while the section's level is the given one and its volatility the source's recentred: without recentering
/// every price ignores the level it reports, and with it the prices agree with the section's own level and volatility
/// only under Bachelier's formula. On the harness, over a SABR smile at a forward of 2% shifted by 1% with an exercise
/// time of 2, a section at a level of 2.5% prices a call at 1% at 0.010130320486628017, the source's, and recentred a
/// call at 2.5% at 0.002100781256289137, the source's at 2%. nanoql prices on the section's own level and volatility, as
/// AtmSmileSection does. QuantLib's check: its own SmileSection formulas on that level and volatility.
/// </summary>
public class AtmAdjustedSmileSectionTests
{
    private static readonly SabrSmileSection Source = new(2.0, 0.02, [0.03, 0.6, 0.4, -0.3], 0.01);

    [Fact]
    public void ThePricesAreOnTheSectionsOwnLevel()
    {
        var section = new AtmAdjustedSmileSection(Source, 0.025);
        CheckOwnLevel(section);

        // QuantLib's call at 1%: the source's.
        Assert.True(Math.Abs(section.OptionPrice(0.01) - 0.010130320486628017) > 1e-4);
    }

    [Fact]
    public void TheRecentredPricesAreOnTheSectionsOwnLevel()
    {
        var section = new AtmAdjustedSmileSection(Source, 0.025, true);
        CheckOwnLevel(section);

        // The recentred volatility is the source's at the strike moved by its level less the given one.
        Assert.Equal(Source.Volatility(0.01 - 0.005), section.Volatility(0.01));

        // QuantLib's call at 2.5%: the source's at 2%.
        Assert.True(Math.Abs(section.OptionPrice(0.025) - 0.002100781256289137) > 1e-4);
    }

    // Black's formula on the section's level and shift at its own volatility, the call, the put and the vega.
    private static void CheckOwnLevel(AtmAdjustedSmileSection section)
    {
        foreach (double k in new[] { 0.01, 0.025, 0.04 })
        {
            double stdDev = Math.Sqrt(section.Variance(k));
            Assert.Equal(BlackFormula(OptionType.Call, k, 0.025, stdDev, 1.0, 0.01), section.OptionPrice(k));
            Assert.Equal(BlackFormula(OptionType.Put, k, 0.025, stdDev, 1.0, 0.01), section.OptionPrice(k, OptionType.Put));
            Assert.Equal(BlackFormulaVolDerivative(k, 0.025, stdDev, 2.0, 1.0, 0.01) * 0.01, section.Vega(k));
        }
    }
}
