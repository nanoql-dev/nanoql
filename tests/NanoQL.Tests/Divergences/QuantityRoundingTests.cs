// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using static NanoQL.Tests.Conventions.CommodityMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Quantity's conversions unrounded. QuantLib's <c>convertTo</c> rounds every converted quantity to its target unit's
/// rounding (<c>quantity.cpp:33-40</c>), and every unit's is <c>Rounding(0)</c>, to the closest whole unit, which its
/// public constructor can't change: under automated conversion 2 MB == 1,500 BBL is true and 1,500 BBL == 2 MB false, 2
/// MB / 1,500 BBL is 1, 2 MB + 1,500 BBL is 4 MB and 1 BBL + 20 GAL is 1 BBL. nanoql's units round nothing, QuantLib's
/// default <c>Rounding()</c>: the comparisons are symmetric and the ratio and sums exact, 1.333…, 3.5 MB and 1.476…
/// BBL.
/// </summary>
public class QuantityRoundingTests
{
    private static readonly EvaluationContext Automated = Context() with
    {
        QuantitySettings = new QuantitySettings { ConversionType = QuantityConversionType.AutomatedConversion },
    };

    private static readonly Quantity TwoMB = new(None, new MBUnitOfMeasure(), 2.0);
    private static readonly Quantity FifteenHundredBarrels = new(None, new BarrelUnitOfMeasure(), 1500.0);

    [Fact]
    public void EqualityIsSymmetric()
    {
        // QuantLib's: true one way, false the other
        Assert.False(TwoMB.Equals(FifteenHundredBarrels, Automated));
        Assert.False(FifteenHundredBarrels.Equals(TwoMB, Automated));
    }

    [Fact]
    public void TheRatioIsExact()
    {
        // QuantLib's: 1
        Assert.Equal(2000.0 / 1500.0, TwoMB.Divide(FifteenHundredBarrels, Automated), 1e-15);
    }

    [Fact]
    public void TwoThousandBarrelsAndFifteenHundredAreThreeAndAHalf()
    {
        // QuantLib's: 4 MB
        Quantity sum = TwoMB.Add(FifteenHundredBarrels, Automated);
        Assert.Equal(new MBUnitOfMeasure(), sum.UnitOfMeasure);
        Assert.Equal(3.5, sum.Amount, 1e-15);
    }

    [Fact]
    public void ABarrelAndTwentyGallonsAreOnePointFourSevenSixBarrels()
    {
        // QuantLib's: 1 BBL
        Quantity sum = new Quantity(None, new BarrelUnitOfMeasure(), 1.0)
            .Add(new Quantity(None, new GallonUnitOfMeasure(), 20.0), Automated);
        Assert.Equal(1.0 + (20.0 / 42.0), sum.Amount, 1e-15);
    }

    [Fact]
    public void EveryUnitsRoundingLeavesAnAmountAsItIs()
    {
        Assert.Equal(Numerics.RoundingType.None, new MBUnitOfMeasure().Rounding.Type);
        Assert.Equal(2.7182818, new Quantity(None, new MBUnitOfMeasure(), 2.7182818).Rounded().Amount);
    }
}
