// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using static NanoQL.Tests.Conventions.CommodityMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Two of QuantLib's known unit factors stored the right way round (the S-xrt plan's XRR4, ruled 2026-10-28). QuantLib's
/// <c>(LitreUnitOfMeasure, GallonUnitOfMeasure, 3.78541)</c> says one litre is 3.785 gallons and its
/// <c>(GallonUnitOfMeasure, MBUnitOfMeasure, 1000 * 42)</c> that one gallon is 42,000 thousand-barrels
/// (<c>unitofmeasureconversionmanager.cpp:115-122</c>), where NIST Handbook 44, Appendix C, gives one gallon as
/// 3.785 411 784 litres and, "by custom", 42 gallons a barrel; so a direct lookup converts 1,000 litres into 3,785.41
/// gallons and 1,000 thousand-barrels into 2.381e-2 gallons, the values QuantLib's <c>testDirect</c> asserts, its
/// "actual" built from the same backward numbers. nanoql stores one gallon as 3.78541 litres and one thousand-barrel as
/// 42,000 gallons: 264.17 gallons and 42,000,000 gallons, which its <c>testDirect</c> asserts.
/// </summary>
public class CommodityKnownFactorsTests
{
    [Fact]
    public void AThousandLitresAreTwoHundredSixtyFourGallons()
    {
        UnitOfMeasureConversion c = Context().UnitOfMeasureConversions.Lookup(
            None, new GallonUnitOfMeasure(), new LitreUnitOfMeasure(), UnitOfMeasureConversionType.Direct);
        Quantity gallons = c.Convert(new Quantity(None, new LitreUnitOfMeasure(), 1000.0));
        Assert.Equal(new GallonUnitOfMeasure(), gallons.UnitOfMeasure);
        Assert.Equal(1000.0 / 3.78541, gallons.Amount);
        Assert.Equal(264.172177, gallons.Amount, 1e-6);

        // QuantLib's row converts the litres the other way: 3,785.41 gallons
        Quantity quantLib = new UnitOfMeasureConversion(None, new LitreUnitOfMeasure(), new GallonUnitOfMeasure(), 3.78541)
            .Convert(new Quantity(None, new LitreUnitOfMeasure(), 1000.0));
        Assert.Equal(3785.41, quantLib.Amount, 1e-9);
    }

    [Fact]
    public void AThousandThousandBarrelsAreFortyTwoMillionGallons()
    {
        UnitOfMeasureConversion c = Context().UnitOfMeasureConversions.Lookup(
            None, new GallonUnitOfMeasure(), new MBUnitOfMeasure(), UnitOfMeasureConversionType.Direct);
        Quantity gallons = c.Convert(new Quantity(None, new MBUnitOfMeasure(), 1000.0));
        Assert.Equal(new GallonUnitOfMeasure(), gallons.UnitOfMeasure);
        Assert.Equal(42_000_000.0, gallons.Amount);

        // QuantLib's row: 2.381e-2 gallons
        Quantity quantLib = new UnitOfMeasureConversion(None, new GallonUnitOfMeasure(), new MBUnitOfMeasure(), 42000)
            .Convert(new Quantity(None, new MBUnitOfMeasure(), 1000.0));
        Assert.Equal(1000.0 / 42000.0, quantLib.Amount);
    }

    [Fact]
    public void TheTwoRowsAgreeWithTheBarrelsTheyTriangulateThrough()
    {
        // the derived lookups, through barrels, never read the two rows: a gallon is 158.987 / 42 litres and a
        // thousand-barrel 42,000 gallons there too
        UnitOfMeasureConversions registry = Context().UnitOfMeasureConversions;
        Assert.Equal(158.987 / 42.0, registry.Lookup(None, new GallonUnitOfMeasure(), new LitreUnitOfMeasure())
            .Convert(new Quantity(None, new GallonUnitOfMeasure(), 1.0)).Amount, 1e-15 * 3.8);
        Assert.Equal(42000.0, registry.Lookup(None, new MBUnitOfMeasure(), new GallonUnitOfMeasure())
            .Convert(new Quantity(None, new MBUnitOfMeasure(), 1.0)).Amount, 1e-15 * 42000.0);
        Assert.Equal(3.78541, 158.987 / 42.0, 1e-5);
    }
}
