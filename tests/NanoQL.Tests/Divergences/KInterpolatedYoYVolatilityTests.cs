// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.TestSuite;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The k-interpolated year-on-year surface reads its stripped curves at its own time (fixed). QuantLib's
/// <c>volatilityImpl(Time)</c> turns the time back into a date (whole years and the fraction's days at 365 a year) and
/// reads each curve at that date through the curve's own lag, so the curves are read a lag early
/// (<c>kinterpolatedyoyoptionletvolatilitysurface.hpp</c>). On QuantLib 1.44 (through QuantLib-Python:
/// test-suite/inflationvolatility.cpp's euro surface observed flat, stripped at a slope of −0.5) the surface at 1 March
/// 2009 and the strike of −1% is 0.011930033082068286, where its own strip, read through QuantLib's <c>Dslice</c>, is
/// 0.011319592966991554. nanoql's surface at a date and a strike is that strike's stripped curve at the date, both
/// through the public date route, wherever the two observe the index alike and the surface is on the stripper's clock,
/// the price surface's reference date and day counter (settlement days 0, as here); on another clock it reads the
/// curves early or late by the clocks' difference, up to 2.7e-5 at 2 settlement days and 4.2e-5 on Actual/360 on this
/// market, within QuantLib's lag error. QuantLib's case reads <c>Dslice</c>, which is QuantLib's.
/// </summary>
public class KInterpolatedYoYVolatilityTests
{
    [Fact]
    public void TheSurfaceAtAStrikeIsItsStrippedCurve()
    {
        InflationVolatilityTests.Market m = InflationVolatilityTests.Setup();
        var surf = InterpolatedYoYCapFloorTermPriceSurface<Bicubic, Cubic>.Create(
            0, new Period(3, TimeUnit.Months), m.YoYIndexEU, CPIInterpolationType.Flat, m.NominalEUR,
            new Actual365Fixed(), new TARGET(), BusinessDayConvention.ModifiedFollowing, m.CStrikesEU, m.FStrikesEU,
            m.CfMaturitiesEU, m.CPriceEU, m.FPriceEU, m.Context);
        var k = KInterpolatedYoYOptionletVolatilitySurface<Linear>.Create(
            0, new TARGET(), BusinessDayConvention.ModifiedFollowing, new Actual365Fixed(), surf.ObservationLag, surf,
            (c, ctx, v) => YoYInflationUnitDisplacedBlackCapFloorEngine.Calculate(c, ctx, m.YoYIndexEU, v, m.NominalEUR),
            new InterpolatedYoYOptionletStripper<Linear>(), -0.5, m.Context);
        foreach (Date d in new[] { new Date(1, Month.March, 2009), new Date(1, Month.December, 2010), new Date(15, Month.June, 2012) })
        {
            for (int i = 0; i < k.Stripped.Strikes.Length; i++)
            {
                double strike = k.Stripped.Strikes[i];
                Assert.Equal(k.Stripped.Curves[i].Volatility(d, strike), k.Volatility(d, strike));
            }
        }

        // QuantLib's surface and its own strip at 1 March 2009, −1%
        Assert.Equal(0.011319592966991554, k.Volatility(new Date(1, Month.March, 2009), -0.01), 1e-12);
        Assert.True(Math.Abs(k.Volatility(new Date(1, Month.March, 2009), -0.01) - 0.011930033082068286) > 5e-4);
    }
}
