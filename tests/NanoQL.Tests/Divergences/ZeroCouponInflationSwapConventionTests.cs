// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Tests.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="ZeroCouponInflationSwap"/>'s inflation-leg convention given as Following. QuantLib's default for it is
/// <c>BusinessDayConvention()</c>, which is Following (0), and the constructor replaces that value by the fixed leg's
/// convention (<c>zerocouponinflationswap.cpp:73-74</c>), so an explicit Following is silently replaced (QuantLib's
/// defect, R8). nanoql's parameter is a nullable convention, null for the fixed leg's, so a given Following is kept.
/// For a swap from 15 June 2024 to Saturday 31 May 2025 on the UK calendar, its fixed leg ModifiedFollowing, the
/// wheel's inflation leg asked for Following pays on 30 May 2025, as the fixed leg does; nanoql's pays on 2 June.
/// </summary>
public class ZeroCouponInflationSwapConventionTests
{
    [Fact]
    public void AGivenFollowingIsKept()
    {
        var swap = new ZeroCouponInflationSwap(
            SwapType.Payer,
            1e6,
            InflationFixture.Today,
            new Date(31, Month.May, 2025),
            new UnitedKingdom(),
            BusinessDayConvention.ModifiedFollowing,
            new Actual365Fixed(),
            0.03,
            new UKRPI(),
            new Period(3, TimeUnit.Months),
            CPIInterpolationType.Flat,
            false,
            new UnitedKingdom(),
            BusinessDayConvention.Following);

        Assert.Equal(new Date(30, Month.May, 2025), swap.FixedLeg[0].Date);

        // QuantLib's: May 30th, 2025
        Assert.Equal(new Date(2, Month.June, 2025), swap.InflationLeg[0].Date);
        Assert.Equal(BusinessDayConvention.Following, swap.InflationConvention);
    }
}
