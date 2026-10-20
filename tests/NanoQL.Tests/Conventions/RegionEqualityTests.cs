// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// Regions compare by name, ordinally, whatever their codes and classes, as QuantLib's <c>operator==</c> compares them
/// (<c>region.hpp</c>); the object equality and the hash agree with it, so a region keys a dictionary by its name.
/// </summary>
public class RegionEqualityTests
{
    [Fact]
    public void RegionsCompareByName()
    {
        Region uk = new UKRegion();
        Region custom = new CustomRegion("UK", "XX");
        Assert.True(uk == custom);
        Assert.True(uk.Equals((object)custom));
        Assert.Equal(uk.GetHashCode(), custom.GetHashCode());

        Assert.False(uk.Equals((object)new EURegion()));
        Assert.False(uk.Equals((object)"UK"));
        Assert.False(uk.Equals((object?)null));
        Assert.False(new CustomRegion("uk", "UK").Equals((object)uk));

        var byRegion = new Dictionary<Region, string> { [uk] = "RPI" };
        Assert.Equal("RPI", byRegion[custom]);
    }
}
