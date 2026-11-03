// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CommodityMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The energy future's lots, secondary costs and quote (fixed), on a future of 10 units at 1.5 a unit on an index at 2
/// a unit.
/// <list type="bullet">
/// <item><description>QuantLib's quantity conversion factor already carries the index's lot and the value multiplies by
/// it again: lot 1,000 prices 5,000,000 for (2 − 1.5) × 10 × 1,000 = 5,000. nanoql multiplies
/// once.</description></item>
/// <item><description>QuantLib charges a unit cost converted to the base unit on the quantity in its own unit without
/// its lots (<c>energyfuture.cpp:107-108</c>): a fee of 0.1 a unit on 10 units reads 1 in barrels, 42 in gallons and
/// 1,000 in thousand-barrels, 1 each by hand (the future prices 4, 8,778 and 4,999,000). nanoql charges it on the
/// base-unit quantity the value uses, lots included.</description></item>
/// <item><description>Within QuantLib's window (the last quote date the evaluation date or the day before) QuantLib
/// reads the evaluation date's fixing alone, so with the last fixing yesterday it refuses ("missing quote for
/// [FIDX1]"), and reads it through the index's fixing, which refuses a Saturday evaluation on a TARGET index with
/// Friday's fixing ("January 18th, 2025 is not a valid fixing date"). nanoql reads the stored history's entry for the
/// evaluation date, else the day before's.</description></item>
/// </list>
/// </summary>
public class EnergyFutureTests
{
    [Theory]
    [InlineData(1.0, 5.0)]
    [InlineData(1000.0, 5000.0)]
    public void TheLotMultipliesTheValueOnce(double lot, double expected)
    {
        // QuantLib's: 5 and 5,000,000
        CommodityIndex index = Index(new BarrelUnitOfMeasure(), 2.0, lot, "IDXLOT");
        EnergyCommodityResults r = Future(index, new BarrelUnitOfMeasure(), null).Calculate(WithFixing(Context(), index, 2.0));
        Assert.Equal(expected, r.NPV!.Value, 1e-12 * expected);
    }

    [Theory]
    [InlineData("BBL", 1.0)]
    [InlineData("GAL", 1.0)]
    [InlineData("MB", 1.0)]
    [InlineData("BBL", 1000.0)]
    [InlineData("GAL", 1000.0)]
    public void AUnitCostIsChargedOnTheQuantityDelivered(string unit, double lot)
    {
        // QuantLib's fees at one lot: 1, 42 and 1,000
        UnitOfMeasure u = Unit(unit);
        CommodityIndex index = Index(u, 2.0, lot, "IDXFEE" + unit);
        var costs = new Dictionary<string, object> { ["fee"] = new CommodityUnitCost(new Money(new USDCurrency(), 0.1), u) };
        EnergyCommodityResults r = Future(index, u, costs).Calculate(WithFixing(Context(), index, 2.0));
        Assert.Equal(1.0 * lot, r.SecondaryCostAmounts["fee"].Value, 1e-12 * lot);
        Assert.Equal(4.0 * lot, r.NPV!.Value, 1e-12 * 5.0 * lot);
    }

    [Fact]
    public void YesterdaysFixingQuotesTheFutureWhenTodaysIsMissing()
    {
        // QuantLib's refuses: "missing quote"
        CommodityIndex index = Index(new BarrelUnitOfMeasure(), 2.0, 1.0, "IDXYDAY");
        EnergyCommodityResults r = Future(index, new BarrelUnitOfMeasure(), null)
            .Calculate(WithFixing(Context(), index, 2.2, Today - 1));
        Assert.Equal((2.2 - 1.5) * 10.0, r.NPV!.Value, 1e-12 * 7.0);
        Assert.Empty(r.PricingErrors);
    }

    [Fact]
    public void TodaysFixingQuotesTheFutureAsQuantLibs()
    {
        // QuantLib's 4 at a fixing of 1.9 today (2 less a fee of 1; here no fee)
        CommodityIndex index = Index(new BarrelUnitOfMeasure(), 2.0, 1.0, "IDXTDAY");
        EvaluationContext context = WithFixing(WithFixing(Context(), index, 2.2, Today - 1), index, 1.9, Today);
        Assert.Equal((1.9 - 1.5) * 10.0, Future(index, new BarrelUnitOfMeasure(), null).Calculate(context).NPV!.Value, 1e-12 * 4.0);
    }

    [Fact]
    public void ASaturdayEvaluationOnAWeekdayIndexReadsFridaysFixing()
    {
        // QuantLib's refuses: "January 18th, 2025 is not a valid fixing date"
        var saturday = new Date(18, Month.January, 2025);
        var curve = new CommodityCurve(
            "CSAT", None, new USDCurrency(), new BarrelUnitOfMeasure(), new TARGET(), [saturday, saturday + 365], [2.0, 2.0]);
        var index = new CommodityIndex(
            "SATIDX", None, new USDCurrency(), new BarrelUnitOfMeasure(), new TARGET(), 1.0, curve,
            ImmutableSortedDictionary<Date, ExchangeContract>.Empty, 0);
        var context = new EvaluationContext { EvaluationDate = saturday };
        context = context with { Fixings = context.Fixings.AddFixing(index, new Date(17, Month.January, 2025), 2.2) };
        Assert.Equal((2.2 - 1.5) * 10.0, Future(index, new BarrelUnitOfMeasure(), null).Calculate(context).NPV!.Value, 1e-12 * 7.0);
    }

    private static EnergyFuture Future(CommodityIndex index, UnitOfMeasure unit, Dictionary<string, object>? costs) =>
        new(1, new Quantity(None, unit, 10.0), new CommodityUnitCost(new Money(new USDCurrency(), 1.5), unit), index, None, costs);
}
