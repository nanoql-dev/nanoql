// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A cube's per-strike inspectors given an index past the strike spreads. QuantLib checks neither index: the SABR
/// cube's <c>marketVolCube(i)</c> (<c>sabrswaptionvolatilitycube.hpp:218-220</c>) and the interpolated cube's
/// <c>volSpreads(i)</c> (<c>interpolatedswaptionvolatilitycube.hpp:67</c>) read past their vectors of matrices: the
/// wheel's interpolated cube returned an 8237205143316950851 × 7008297511184330863 "matrix" for <c>volSpreads(5)</c> on
/// QuantLib's five strike spreads. nanoql refuses the index with <see cref="ArgumentOutOfRangeException"/>.
/// </summary>
public class CubeIndexTests
{
    [Fact]
    public void ASabrCubesMarketVolatilitiesRefuseAnIndexPastTheStrikes()
    {
        var cube = CubeFixture.Sabr(isAtmCalibrated: false);
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => cube.MarketVolCube(5)).ParamName);
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => cube.MarketVolCube(-1)).ParamName);
        Assert.Equal(3, cube.MarketVolCube(4).Rows);
    }

    [Fact]
    public void AnInterpolatedCubesSpreadsRefuseAnIndexPastTheStrikes()
    {
        var cube = CubeFixture.Interpolated();
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => cube.VolSpreads(5)).ParamName);
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => cube.VolSpreads(-1)).ParamName);
        Assert.Equal(3, cube.VolSpreads(4).Columns);
    }
}
