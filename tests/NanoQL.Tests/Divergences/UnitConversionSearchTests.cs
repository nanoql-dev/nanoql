// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using static NanoQL.Tests.Conventions.CommodityMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The registry's chained search with its forbidden list passed down (fixed). QuantLib's <c>smartLookup</c> forbids
/// each unit it starts from to the searches below it, "in order to avoid cycles", but drops the list when it recurses
/// (<c>unitofmeasureconversionmanager.cpp:190</c>), so a search whose first candidate leads back recurses without end:
/// its known factors' first entry pairs thousand-barrels and barrels, so every chained search from either overflows the
/// stack. On QuantLib 1.44 (each lookup in its own process) barrels to metric tonnes overflows (exit 127) with and
/// without a user's metric-tonne-to-gallon factor of 300, as gallons to metric tonnes does, through barrels; metric
/// tonnes to barrels returns 7.14286, its first candidate the user's factor. nanoql passes the list down, as QuantLib's
/// <c>ExchangeRateManager</c> passes its own; wherever QuantLib's search returns it never revisits a unit above it, so
/// the answer is QuantLib's. Checks: the chains against the hand value through gallons, the refusal where no chain
/// exists, and QuantLib's 7.14286.
/// </summary>
public class UnitConversionSearchTests
{
    private static readonly UnitOfMeasureConversions WithTonnes = Context().UnitOfMeasureConversions.Add(
        new UnitOfMeasureConversion(None, new MTUnitOfMeasure(), new GallonUnitOfMeasure(), 300.0));

    [Fact]
    public void BarrelsToTonnesChainsThroughTheUsersGallonFactor()
    {
        UnitOfMeasureConversion c = WithTonnes.Lookup(None, new BarrelUnitOfMeasure(), new MTUnitOfMeasure());
        Assert.Equal(UnitOfMeasureConversionType.Derived, c.Type);
        Quantity tonnes = c.Convert(new Quantity(None, new BarrelUnitOfMeasure(), 1.0));
        Assert.Equal(new MTUnitOfMeasure(), tonnes.UnitOfMeasure);
        Assert.Equal(42.0 / 300.0, tonnes.Amount, 1e-15);
    }

    [Fact]
    public void GallonsToTonnesTriangulatesThroughBarrels()
    {
        UnitOfMeasureConversion c = WithTonnes.Lookup(None, new GallonUnitOfMeasure(), new MTUnitOfMeasure());
        Assert.Equal(1.0 / 300.0, c.Convert(new Quantity(None, new GallonUnitOfMeasure(), 1.0)).Amount, 1e-17);
    }

    [Fact]
    public void TonnesToBarrelsIsQuantLibs()
    {
        UnitOfMeasureConversion c = WithTonnes.Lookup(None, new MTUnitOfMeasure(), new BarrelUnitOfMeasure());
        Assert.Equal(300.0 / 42.0, c.ConversionFactor, 1e-15 * 7.2);
        Assert.Equal(7.14286, c.ConversionFactor, 1e-5);
    }

    [Fact]
    public void BarrelsToTonnesWithNoPathIsRefused()
    {
        var e = Assert.Throws<NanoQLException>(() =>
            Context().UnitOfMeasureConversions.Lookup(None, new BarrelUnitOfMeasure(), new MTUnitOfMeasure()));
        Assert.Equal("no conversion available for <NULL> from BBL to MT", e.Message);
    }
}
