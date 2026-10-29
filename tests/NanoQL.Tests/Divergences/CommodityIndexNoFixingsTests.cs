// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CommodityMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An index with no fixings read as having no quote date (the S-xrt plan's XRR5 (vi), fixed under R8). QuantLib's
/// <c>lastQuoteDate()</c> reads <c>timeSeries().lastDate()</c>, which throws "empty timeseries" on an empty history, so
/// the swaps and the future refuse an index with no fixings that the swaps' own branch prices from the forward curve with
/// a warning (the S-xrt planner's <c>xp_c1.cpp</c>: the payer swap throws on every unit). nanoql's
/// <see cref="CommodityIndex.LastQuoteDate"/> is null there, and every date is priced from the forward curve, QuantLib's
/// warning recorded. Check: the instruments on an index with no fixings price as on one whose fixings all precede their
/// first period.
/// </summary>
public class CommodityIndexNoFixingsTests
{
    [Fact]
    public void AVanillaSwapOnAnIndexWithNoFixingsPricesFromTheCurve()
    {
        CommodityIndex index = Index(new BarrelUnitOfMeasure(), 2.0);
        EnergyVanillaSwap swap = Vanilla(
            true, 1.5, new BarrelUnitOfMeasure(), index, MarchPeriods(new Quantity(None, new BarrelUnitOfMeasure(), 1000.0)),
            Flat(0.02));
        EnergyCommodityResults none = swap.Calculate(Context());
        EnergyCommodityResults old = swap.Calculate(WithFixing(Context(), index, 2.0, Today - 60));
        Assert.Null(index.LastQuoteDate(Context()));
        Assert.Equal(old.NPV, none.NPV);
        Assert.Equal(
            new PricingError(
                PricingErrorLevel.Warning, "index [IDXBBL] does not have any quotes; using forward prices from [CRVBBL]"),
            none.PricingErrors.Single());
    }

    [Fact]
    public void ABasisSwapOnTwoIndexesWithNoFixingsPricesFromTheCurves()
    {
        CommodityIndex pay = Index(new BarrelUnitOfMeasure(), 70.0, name: "PAYIDX");
        CommodityIndex receive = Index(new BarrelUnitOfMeasure(), 73.0, name: "RECIDX");
        var swap = new EnergyBasisSwap(
            new NullCalendar(), null, pay, receive, false, new Currencies.USDCurrency(), new Currencies.USDCurrency(),
            MarchPeriods(new Quantity(None, new BarrelUnitOfMeasure(), 1000.0)),
            new CommodityUnitCost(new Money(new Currencies.USDCurrency(), 0.5), new BarrelUnitOfMeasure()), None, null,
            Flat(0.02), Flat(0.02), Flat(0.02));
        EvaluationContext old = WithFixing(WithFixing(Context(), pay, 70.0, Today - 60), receive, 73.0, Today - 60);
        Assert.Equal(swap.Calculate(old).NPV, swap.Calculate(Context()).NPV);
        Assert.Equal(2, swap.Calculate(Context()).PricingErrors.Length);
    }

    [Fact]
    public void AFutureOnAnIndexWithNoFixingsPricesFromTheCurve()
    {
        CommodityIndex index = Index(new BarrelUnitOfMeasure(), 2.0);
        var future = new EnergyFuture(
            1, new Quantity(None, new BarrelUnitOfMeasure(), 10.0),
            new CommodityUnitCost(new Money(new Currencies.USDCurrency(), 1.5), new BarrelUnitOfMeasure()), index, None, null);
        EnergyCommodityResults none = future.Calculate(Context());
        Assert.Equal(future.Calculate(WithFixing(Context(), index, 2.0, Today - 60)).NPV, none.NPV);
        Assert.Equal(5.0, none.NPV!.Value, 1e-14 * 5.0);
        Assert.Equal("curve [IDXBBL] has no quotes using forward price from [CRVBBL]", none.PricingErrors.Single().Error);
    }
}
