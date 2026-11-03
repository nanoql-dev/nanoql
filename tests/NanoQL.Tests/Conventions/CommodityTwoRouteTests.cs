// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CommodityMarkets;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The commodities' two routes: the energy swaps against their discounted commodity cash flows, on one curve, and each
/// quantity's conversion into another unit and back.
/// </summary>
public class CommodityTwoRouteTests
{
    [Theory]
    [InlineData(true, 1.5)]
    [InlineData(false, 1.5)]
    [InlineData(true, 2.5)]
    public void AVanillaSwapsNpvIsItsDiscountedCashFlowsLessItsCosts(bool payer, double fixedPrice)
    {
        CommodityIndex index = Index(new GallonUnitOfMeasure(), 2.0 / 42.0, 1.0, "GALIDX");
        var costs = new Dictionary<string, object>
        {
            ["fee"] = new CommodityUnitCost(new Money(new USDCurrency(), 0.02), new GallonUnitOfMeasure()),
            ["clearing"] = new Money(new USDCurrency(), 40.0),
        };
        EnergyVanillaSwap swap = Vanilla(
            payer, fixedPrice * 1000.0, new MBUnitOfMeasure(), index,
            MarchPeriods(new Quantity(None, new BarrelUnitOfMeasure(), 1000.0), new Date(31, Month.July, 2025)), Flat(0.04), costs);
        EnergyCommodityResults r = swap.Calculate(WithFixing(Context(), index, 2.0 / 42.0));
        double flows = r.PaymentCashFlows.Values.Sum(c => c.DiscountedAmount.Value);
        double charged = r.SecondaryCostAmounts.Values.Sum(m => m.Value);
        Assert.Equal(flows - charged, r.NPV!.Value, 1e-10 * 3000.0);
        Assert.Equal(40.0 + (0.02 * 42.0 * 5000.0), charged, 1e-10 * 4240.0);
    }

    [Fact]
    public void ABasisSwapsNpvIsItsDiscountedCashFlowsLessItsCosts()
    {
        CommodityIndex pay = Index(new BarrelUnitOfMeasure(), 70.0, 1.0, "BPAY");
        CommodityIndex receive = Index(new MBUnitOfMeasure(), 73_000.0, 1.0, "BREC");
        var swap = new EnergyBasisSwap(
            new NullCalendar(), null, pay, receive, true, new USDCurrency(), new USDCurrency(),
            MarchPeriods(new Quantity(None, new BarrelUnitOfMeasure(), 1000.0), new Date(31, Month.May, 2025)),
            new CommodityUnitCost(new Money(new USDCurrency(), 0.05), new GallonUnitOfMeasure()), None,
            new Dictionary<string, object> { ["clearing"] = new Money(new USDCurrency(), 40.0) },
            Flat(0.04), Flat(0.04), Flat(0.04));
        EnergyCommodityResults r = swap.Calculate(WithFixing(WithFixing(Context(), pay, 70.0), receive, 73_000.0));
        double flows = r.PaymentCashFlows.Values.Sum(c => c.DiscountedAmount.Value);
        Assert.Equal(flows - 40.0, r.NPV!.Value, 1e-10 * 3000.0);

        // each month: (73 − 70 − 2.1) a barrel on 1,000 barrels
        Assert.Equal(900.0, r.PaymentCashFlows.Values.First().UndiscountedAmount.Value, 1e-10 * 900.0);
    }

    [Theory]
    [MemberData(nameof(EnergyUnitConversionTestsPairs))]
    public void AQuantityConvertedAndBackIsItself(string from, string to)
    {
        EvaluationContext context = Context() with
        {
            QuantitySettings = new QuantitySettings { ConversionType = QuantityConversionType.AutomatedConversion },
        };
        var q = new Quantity(None, Unit(from), 123.456);
        Quantity zero = new(None, Unit(to), 0.0);
        Quantity there = zero.Add(q, context);
        Quantity back = new Quantity(None, Unit(from), 0.0).Add(there, context);
        Assert.Equal(Unit(to), there.UnitOfMeasure);
        Assert.Equal(q.Amount, back.Amount, 1e-13 * 123.456);
        Assert.True(QuantityFunctions.CloseEnough(back, q, context, 1000));
    }

    public static TheoryData<string, string> EnergyUnitConversionTestsPairs() => Divergences.EnergyUnitConversionTests.Pairs();
}
