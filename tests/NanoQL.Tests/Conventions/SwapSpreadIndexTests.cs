// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The swap spread index's name (the S-g1d plan's G1D8): QuantLib's text, each swap index's name with its gearing to
/// four decimals, <c>"&lt;name 1&gt;(&lt;gearing 1&gt;) + &lt;name 2&gt;(&lt;gearing 2&gt;)"</c>, its family name the
/// one given. On the wheel, the 10-year EUR swap rate less half the 2-year's is "EuriborSwapIsdaFixA10Y 30/360 (Bond
/// Basis)(1.0000) + EuriborSwapIsdaFixA2Y 30/360 (Bond Basis)(-0.5000)", and gearings of 1.23456789 and 0.000049 read
/// 1.2346 and 0.0000. It allows no native fixings, its fixings its indexes' (<c>swapspreadindex.hpp:45</c>).
/// </summary>
public class SwapSpreadIndexTests
{
    [Fact]
    public void TheNameShowsTheGearingsToFourDecimals()
    {
        YieldTermStructure curve = new FlatForward(new Date(15, Month.January, 2025), 0.03, new Actual365Fixed());
        var tenYears = new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), curve);
        var twoYears = new EuriborSwapIsdaFixA(new Period(2, TimeUnit.Years), curve);

        var index = new SwapSpreadIndex("CmsSpread", tenYears, twoYears, 1.0, -0.5);
        var rounded = new SwapSpreadIndex("CmsSpread", tenYears, twoYears, 1.23456789, 0.000049);

        Assert.Equal("EuriborSwapIsdaFixA10Y 30/360 (Bond Basis)(1.0000) + EuriborSwapIsdaFixA2Y 30/360 (Bond Basis)(-0.5000)", index.Name);
        Assert.Equal("CmsSpread", index.FamilyName);
        Assert.False(index.AllowsNativeFixings);
        Assert.Equal("EuriborSwapIsdaFixA10Y 30/360 (Bond Basis)(1.2346) + EuriborSwapIsdaFixA2Y 30/360 (Bond Basis)(0.0000)", rounded.Name);
    }
}
