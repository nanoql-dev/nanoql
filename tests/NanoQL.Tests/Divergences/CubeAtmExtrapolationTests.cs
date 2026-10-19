// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A cube's at-the-money structure's extrapolation. QuantLib's cube turns extrapolation on in the structure it is given
/// (<c>swaptionvolcube.cpp:68</c>), for every holder of it: on the wheel, the at-the-money matrix refused a 40-year
/// option before a cube was built on it ("date (June 16th, 2065) is past max curve date (June 16th, 2055)") and returned
/// 0.09799630288922362 after, at a strike of 3%, while the cube's own flag stayed off. nanoql's cube holds a copy of the
/// structure with extrapolation enabled, and leaves the caller's structure as it was.
/// </summary>
public class CubeAtmExtrapolationTests
{
    [Fact]
    public void TheCallersStructureIsLeftUnchanged()
    {
        var atm = CubeFixture.AtmMatrix();
        Date fortyYears = new TARGET().Advance(CubeFixture.Today, 40, TimeUnit.Years);
        var tenYears = new Period(10, TimeUnit.Years);
        Assert.False(atm.AllowsExtrapolation);

        foreach (TermStructures.SwaptionVolatilityCube cube in new TermStructures.SwaptionVolatilityCube[]
                 { CubeFixture.Sabr(atm: atm, isAtmCalibrated: false), CubeFixture.Interpolated(atm: atm) })
        {
            Assert.False(atm.AllowsExtrapolation);
            Assert.Throws<NanoQLException>(() => atm.Volatility(fortyYears, tenYears, 0.03));
            Assert.True(cube.AtmVol.AllowsExtrapolation);
            Assert.Equal(0.09799630288922362, cube.AtmVol.Volatility(fortyYears, tenYears, 0.03));
            Assert.False(cube.AllowsExtrapolation);
        }
    }
}
