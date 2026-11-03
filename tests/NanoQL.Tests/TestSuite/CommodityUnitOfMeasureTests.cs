// SPDX-FileCopyrightText: Copyright (C) 2010 Manas Bhatt
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/commodityunitofmeasure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Experimental;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

/// <summary>
/// QuantLib's commodity unit-of-measure cases. <c>testDirect</c> builds its "actual" conversions the right way round
/// for the two known factors QuantLib stores backward: one gallon is 3.78541 litres and one thousand-barrel 42,000
/// gallons, so 1,000 litres are 264.17 gallons and 1,000 MB 42,000,000 gallons, where QuantLib's case asserts 3,785.41
/// and 2.381e-2 (kept beside in <c>Divergences/CommodityKnownFactorsTests.cs</c>).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CommodityUnitOfMeasureTests
{
    [Fact]
    public void TestCommodityType()
    {
        var commodityType = new CommodityType("Heating Oil", "HO");

        Assert.Equal("Heating Oil", commodityType.Name);
        Assert.Equal("HO", commodityType.Code);
    }

    [Fact]
    public void TestDirect()
    {
        // Testing direct commodity unit of measure conversions...
        EvaluationContext context = Context();
        UnitOfMeasureConversions uomManager = context.UnitOfMeasureConversions;
        var none = new NullCommodityType();

        // MB to BBL
        Quantity actual = new UnitOfMeasureConversion(none, new MBUnitOfMeasure(), new BarrelUnitOfMeasure(), 1000)
            .Convert(new Quantity(none, new MBUnitOfMeasure(), 1000));
        Quantity calc = uomManager.Lookup(
                none, new BarrelUnitOfMeasure(), new MBUnitOfMeasure(), UnitOfMeasureConversionType.Direct)
            .Convert(new Quantity(none, new MBUnitOfMeasure(), 1000));
        Assert.True(QuantityFunctions.Close(calc, actual, context), Message("MB to BBL", actual, calc));

        // BBL to Gallon
        actual = new UnitOfMeasureConversion(none, new BarrelUnitOfMeasure(), new GallonUnitOfMeasure(), 42)
            .Convert(new Quantity(none, new GallonUnitOfMeasure(), 1000));
        calc = uomManager.Lookup(
                none, new BarrelUnitOfMeasure(), new GallonUnitOfMeasure(), UnitOfMeasureConversionType.Direct)
            .Convert(new Quantity(none, new GallonUnitOfMeasure(), 1000));
        Assert.True(QuantityFunctions.Close(calc, actual, context), Message("BBL to Gallon", actual, calc));

        // BBL to Litre
        actual = new UnitOfMeasureConversion(none, new BarrelUnitOfMeasure(), new LitreUnitOfMeasure(), 158.987)
            .Convert(new Quantity(none, new LitreUnitOfMeasure(), 1000));
        calc = uomManager.Lookup(
                none, new BarrelUnitOfMeasure(), new LitreUnitOfMeasure(), UnitOfMeasureConversionType.Direct)
            .Convert(new Quantity(none, new LitreUnitOfMeasure(), 1000));
        Assert.True(QuantityFunctions.Close(calc, actual, context), Message("BBL to Litre", actual, calc));

        // BBL to KL
        actual = new UnitOfMeasureConversion(none, new KilolitreUnitOfMeasure(), new BarrelUnitOfMeasure(), 6.28981)
            .Convert(new Quantity(none, new KilolitreUnitOfMeasure(), 1000));
        calc = uomManager.Lookup(
                none, new BarrelUnitOfMeasure(), new KilolitreUnitOfMeasure(), UnitOfMeasureConversionType.Direct)
            .Convert(new Quantity(none, new KilolitreUnitOfMeasure(), 1000));
        Assert.True(QuantityFunctions.Close(calc, actual, context), Message("BBL to KiloLitre", actual, calc));

        // MB to Gallon: one thousand-barrel is 42,000 gallons (QuantLib's (GAL, MB, 42000))
        actual = new UnitOfMeasureConversion(none, new MBUnitOfMeasure(), new GallonUnitOfMeasure(), 42000)
            .Convert(new Quantity(none, new MBUnitOfMeasure(), 1000));
        calc = uomManager.Lookup(
                none, new GallonUnitOfMeasure(), new MBUnitOfMeasure(), UnitOfMeasureConversionType.Direct)
            .Convert(new Quantity(none, new MBUnitOfMeasure(), 1000));
        Assert.True(QuantityFunctions.Close(calc, actual, context), Message("MB to Gallon", actual, calc));

        // Gallon to Litre: one gallon is 3.78541 litres (QuantLib's (l, GAL, 3.78541))
        actual = new UnitOfMeasureConversion(none, new GallonUnitOfMeasure(), new LitreUnitOfMeasure(), 3.78541)
            .Convert(new Quantity(none, new LitreUnitOfMeasure(), 1000));
        calc = uomManager.Lookup(
                none, new GallonUnitOfMeasure(), new LitreUnitOfMeasure(), UnitOfMeasureConversionType.Direct)
            .Convert(new Quantity(none, new LitreUnitOfMeasure(), 1000));
        Assert.True(QuantityFunctions.Close(calc, actual, context), Message("Gallon to Litre", actual, calc));
    }

    private static EvaluationContext Context() => new() { EvaluationDate = new Date(1, Month.January, 2010) };

    private static string Message(string conversion, Quantity actual, Quantity calc) =>
        $"Wrong result for {conversion} Conversion: \n    actual:     {actual}\n    calculated: {calc}";
}
