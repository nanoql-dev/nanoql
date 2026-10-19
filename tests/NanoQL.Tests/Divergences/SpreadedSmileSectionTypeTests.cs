// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The converted volatility of a spreaded smile section. QuantLib's <c>SpreadedSmileSection</c> default-constructs its
/// <c>SmileSection</c> base (<c>spreadedsmilesection.cpp:26-31</c>), so the base's
/// <c>volatility(strike, type, shift)</c> compares the type asked for with an uninitialized member
/// (<c>smilesection.cpp:131-136</c>), and the section returns its own volatility or one converted through option prices
/// by chance, observed both ways. nanoql's spreaded section gives its base the underlying section's exercise time, day
/// counter, volatility type and shift, so the comparison reads the underlying's type: asked for its own type and shift,
/// it returns its own volatility.
/// </summary>
public class SpreadedSmileSectionTypeTests
{
    [Fact]
    public void TheConvertedVolatilityComparesTheUnderlyingsType()
    {
        var normal = new FlatSmileSection(1.0, 0.0080, null, 0.03, VolatilityType.Normal);
        var spreadedNormal = new SpreadedSmileSection(normal, 0.0005);
        Assert.Equal(normal.Volatility(0.035) + 0.0005, spreadedNormal.Volatility(0.035));
        Assert.Equal(spreadedNormal.Volatility(0.035), spreadedNormal.Volatility(0.035, VolatilityType.Normal));

        var shifted = new SabrSmileSection(1.0, 0.03, [0.2, 0.5, 0.3, -0.1], 0.01);
        var spreadedShifted = new SpreadedSmileSection(shifted, 0.01);
        Assert.Equal(
            spreadedShifted.Volatility(0.035), spreadedShifted.Volatility(0.035, VolatilityType.ShiftedLognormal, 0.01));

        // Another type is converted, from the section's own option prices.
        Assert.NotEqual(spreadedNormal.Volatility(0.035), spreadedNormal.Volatility(0.035, VolatilityType.ShiftedLognormal));
    }
}
