// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.Tests.TestSuite;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A CPI price surface's put-call fills compounded over the maturity in years (the S-xrt run's R2-1, fixed under R8).
/// QuantLib's surface compounds the at-the-money rate and the strike over <c>mat.length()</c>, the maturity's length in
/// its own unit (<c>cpicapfloortermpricesurface.hpp:248-252</c>), so a maturity in months compounds over its month
/// count. On QuantLib 1.44 (the run's <c>c2\probes\xb_cpimonths.cpp</c> and <c>xb_cpiyears.cpp</c>:
/// test-suite/inflationcpicapfloor.cpp's case with its maturities written as 36 to 360 months), the 36-month cap filled
/// at the floor strike of −1% is 2.137020233466961, where the same surface on 3 to 30 years fills 0.11890267275321309;
/// QuantLib's case passes either way, reading only the quoted strikes. nanoql compounds over QuantLib's own
/// <c>years</c> of the period: 36 months price as 3 years, QuantLib's bits, and a maturity in weeks or days, which has no
/// length in years, is refused.
/// </summary>
public class CPICapFloorPriceSurfaceTests
{
    [Theory]
    [InlineData(-0.01, 0.11890267275321309)]
    [InlineData(0.0, 0.092142126147838899)]
    [InlineData(0.01, 0.066062203731440672)]
    [InlineData(0.02, 0.041986241745908259)]
    public void AMaturityInMonthsFillsAsTheSameInYears(double floorStrike, double quantLibYears)
    {
        var common = new InflationCPICapFloorTests.CommonVars();
        CPICapFloorTermPriceSurface months = Surface(common, 12);
        CPICapFloorTermPriceSurface years = Surface(common, 1);
        var m36 = new Period(36, TimeUnit.Months);
        Assert.Equal(quantLibYears, years.CapPrice(new Period(3, TimeUnit.Years), floorStrike), 1e-15);
        Assert.Equal(years.CapPrice(new Period(3, TimeUnit.Years), floorStrike), months.CapPrice(m36, floorStrike));
        Assert.True(months.CapPrice(m36, floorStrike) < 0.2); // QuantLib's 36 months fill 0.88 to 2.14
    }

    [Fact]
    public void AMaturityInWeeksIsRefused()
    {
        var common = new InflationCPICapFloorTests.CommonVars();
        Period[] maturities = [new(26, TimeUnit.Weeks), .. common.CfMaturitiesUK[1..]];
        var e = Assert.Throws<NanoQLException>(() => Create(common, maturities));
        Assert.Equal("cannot convert Weeks into Years", e.Message);
    }

    // The case's surface, its maturities in years, or in months of twelve times the length.
    private static CPICapFloorTermPriceSurface Surface(InflationCPICapFloorTests.CommonVars common, int perYear) =>
        Create(common, [.. common.CfMaturitiesUK.Select(p => new Period(p.Length * perYear, perYear == 1 ? TimeUnit.Years : TimeUnit.Months))]);

    private static CPICapFloorTermPriceSurface Create(InflationCPICapFloorTests.CommonVars common, Period[] maturities) =>
        InterpolatedCPICapFloorTermPriceSurface<Bilinear>.Create(
            1.0,
            common.BaseZeroRate,
            common.ObservationLag,
            common.Calendar,
            common.Convention,
            common.DcZCIIS,
            common.Ii,
            CPIInterpolationType.Flat,
            common.NominalUK,
            common.CStrikesUK,
            common.FStrikesUK,
            maturities,
            common.CPriceUK,
            common.FPriceUK,
            common.Context);
}
