// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A volatility type outside <see cref="VolatilityType"/>. QuantLib's SABR functions and the SABR fit take any value
/// other than <c>Normal</c> as lognormal (<c>sabr.cpp:87, 142</c>), while <c>SmileSection::optionPrice</c> takes any
/// value other than <c>ShiftedLognormal</c> as Bachelier's (<c>smilesection.cpp:84-88</c>): on the wheel, type 2 gives
/// the SABR volatility 1.125831262004863 at a strike of 4% on a forward of 3.5%, the lognormal one, and a SABR
/// section of type 2 prices a call there at 0.4466461204232711, Bachelier's price on that lognormal volatility, where
/// the lognormal section's is 0.013594842378425748. nanoql refuses the value on entry with
/// <see cref="ArgumentOutOfRangeException"/>, as its other members taking an enum do.
/// </summary>
public class SabrVolatilityTypeTests
{
    [Fact]
    public void AnUnknownVolatilityTypeIsRefused()
    {
        const VolatilityType unknown = (VolatilityType)2;
        AssertRefused(
            "volatilityType", () => SabrFunctions.SabrVolatility(0.04, 0.035, 1.0, 0.3, 0.6, 0.02, 0.01, unknown));
        AssertRefused(
            "volatilityType",
            () => SabrFunctions.ShiftedSabrVolatility(0.04, 0.035, 1.0, 0.3, 0.6, 0.02, 0.01, 0.0, unknown));
        AssertRefused(
            "volatilityType",
            () => SabrFunctions.UnsafeShiftedSabrVolatility(0.04, 0.035, 1.0, 0.3, 0.6, 0.02, 0.01, 0.0, unknown));
        AssertRefused(
            "volatilityType", () => SabrFunctions.UnsafeSabrVolatility(0.04, 0.035, 1.0, 0.3, 0.6, 0.02, 0.01, unknown));
        AssertRefused("volatilityType", () => new SabrSmileSection(1.0, 0.035, [0.3, 0.6, 0.02, 0.01], 0.0, unknown));
        double[] strikes = [0.03, 0.035, 0.04];
        AssertRefused(
            "volatilityType",
            () => new SABRInterpolation(
                strikes, [0.2, 0.19, 0.21], 1.0, 0.035, null, 0.5, null, null, false, true, false, false,
                volatilityType: unknown));
    }

    private static void AssertRefused(string paramName, Action call)
    {
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(call);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith("Unknown volatility type (2)", e.Message, StringComparison.Ordinal);
    }
}
