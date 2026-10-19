// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A SABR cube given fewer fixed flags than the model's four parameters. QuantLib's SABR traits read
/// <c>paramIsFixed[0..3]</c> unchecked (<c>sabrswaptionvolatilitycube.hpp:98</c>): with three flags the wheel reads
/// the fourth as free, fitting ρ to −0.9999; with none, the process ends with an access violation at the first query.
/// nanoql refuses fewer flags with <see cref="System.ArgumentException"/>, in the form of QuantLib's ZABR traits' check
/// ("ZABR model requires 5 fixed flags, but 4 provided", <c>zabrswaptionvolatilitycube.hpp:128</c>), and ignores flags
/// past the fourth, as QuantLib does. The refusal is the model's, <c>CreateInterpolation</c>'s, naming its own
/// <c>paramIsFixed</c>, and reaches the cube's caller as the model raised it.
/// </summary>
public class SabrCubeFixedFlagsTests
{
    [Fact]
    public void FewerFixedFlagsThanParametersAreRefused()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => CubeFixture.Sabr(fixedFlags: [false, true, false], isAtmCalibrated: false));
        Assert.Equal("paramIsFixed", e.ParamName);
        Assert.StartsWith("SABR model requires 4 fixed flags, but 3 provided", e.Message, StringComparison.Ordinal);

        e = Assert.Throws<ArgumentException>(() => CubeFixture.Sabr(fixedFlags: [], isAtmCalibrated: false));
        Assert.StartsWith("SABR model requires 4 fixed flags, but 0 provided", e.Message, StringComparison.Ordinal);

        Assert.Equal(
            Flat(CubeFixture.Sabr(fixedFlags: CubeFixture.BetaFixed, isAtmCalibrated: false).SparseSabrParameters()),
            Flat(CubeFixture.Sabr(fixedFlags: [false, true, false, false, true], isAtmCalibrated: false).SparseSabrParameters()));
    }

    private static double[] Flat(Numerics.Matrix m) => [.. Enumerable.Range(0, m.Rows).SelectMany(i => m.Row(i).ToArray())];
}
