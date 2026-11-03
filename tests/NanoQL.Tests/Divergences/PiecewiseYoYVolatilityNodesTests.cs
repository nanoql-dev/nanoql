// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.TestSuite;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A bootstrapped year-on-year volatility curve's nodes from its dates and data (QuantLib's master's #2941, taken ahead
/// of the pin). QuantLib 1.44's <c>nodes()</c> returns a list only the data constructor fills
/// (<c>yoyinflationoptionletvolatilitystructure2.hpp</c>), so a <c>PiecewiseYoYOptionletVolatilityCurve</c>'s is empty:
/// on QuantLib 1.44 (QuantLib's <c>testYoYPriceSurfaceToVol</c> with #2941's inspectable stripper) each of the 11
/// stripped curves has 0 nodes beside 8 dates and 8 data. nanoql's, as master's, are its dates with its data; #2941's
/// own check is in <c>TestSuite/InflationVolatilityTests.cs</c>.
/// </summary>
public class PiecewiseYoYVolatilityNodesTests
{
    [Fact]
    public void AStrippedCurvesNodesAreItsDatesAndData()
    {
        InflationVolatilityTests.Market m = InflationVolatilityTests.Setup();
        var surf = InflationVolatilityTests.SetupPriceSurface(m);
        var k = KInterpolatedYoYOptionletVolatilitySurface<Linear>.Create(
            0, new TARGET(), BusinessDayConvention.ModifiedFollowing, new Actual365Fixed(), surf.ObservationLag, surf,
            (c, ctx, v) => YoYInflationUnitDisplacedBlackCapFloorEngine.Calculate(c, ctx, m.YoYIndexEU, v, m.NominalEUR),
            new InterpolatedYoYOptionletStripper<Linear>(), -0.5, m.Context);
        Assert.Equal(11, k.Stripped.Curves.Length);
        foreach (YoYOptionletVolatilitySurface c in k.Stripped.Curves)
        {
            var curve = (IInterpolatedCurve<YoYInflationVolatilityTraits, Linear>)c;
            Assert.Equal(8, curve.Nodes().Length);
            Assert.Equal(curve.Dates(), curve.Nodes().Select(n => n.Key));
            Assert.Equal(curve.Data(), curve.Nodes().Select(n => n.Value));
        }
    }
}
