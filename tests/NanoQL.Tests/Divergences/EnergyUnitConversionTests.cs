// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Tests.Conventions;
using static NanoQL.Tests.Conventions.CommodityMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The energy instruments' unit conversions (fixed). QuantLib's <c>calculateUomConversionFactor</c> returns the
/// looked-up conversion's stored factor whatever its orientation, so it is one unit's value in the other for 25 of the
/// 30 ordered pairs of its six petroleum units and the reciprocal for five (BBL→MB, BBL→kl, BBL→KL_tk, GAL→BBL, l→BBL),
/// and its call sites multiply a quantity by the factor from the base to its unit and a price by the factor from its
/// unit to the base, so one is inverted: a payer vanilla swap on 1,000 units a month at a 0.5 spread prices 500 in
/// barrels, 882,000 in gallons and 500,000,000 in thousand-barrels, and a fee of 0.1 a unit reads 100, 176,400 and 10⁸
/// (flat zero curves). nanoql's factor is one unit's value in the other, the conversion's own convert of a unit
/// quantity; a quantity converts to the base by the factor from its unit, and a price, an index's forward price, a
/// curve's basis and a unit cost by the factor from the base (or the index's unit) to theirs. The check is the hand
/// value, the spread and the fee converted through barrels by QuantLib's known factors, on every unit; QuantLib's bits
/// stand on the base unit, where every factor is 1.
/// </summary>
public class EnergyUnitConversionTests
{
    public static TheoryData<string, string> Pairs()
    {
        var data = new TheoryData<string, string>();
        foreach (string from in BarrelsPerUnit.Keys)
        {
            foreach (string to in BarrelsPerUnit.Keys)
            {
                if (from != to)
                {
                    data.Add(from, to);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void TheFactorIsOneUnitsValueInTheOther(string from, string to)
    {
        double factor = CommodityPricingHelperFunctions.CalculateUomConversionFactor(None, Unit(from), Unit(to), Context());
        double truth = BarrelsPerUnit[from] / BarrelsPerUnit[to];
        Assert.Equal(truth, factor, 1e-14 * truth);
    }

    [Fact]
    public void QuantLibsStoredFactorIsTheReciprocalOnFivePairs()
    {
        // QuantLib's calculateUomConversionFactor is the found conversion's ConversionFactor
        EvaluationContext context = Context();
        string[] inverted =
        [
            .. from f in BarrelsPerUnit.Keys
               from t in BarrelsPerUnit.Keys
               where f != t
               let stored = context.UnitOfMeasureConversions.Lookup(None, Unit(f), Unit(t)).ConversionFactor
               let truth = BarrelsPerUnit[f] / BarrelsPerUnit[t]
               where Math.Abs((stored * truth) - 1.0) < 1e-9 && Math.Abs(truth - 1.0) > 1e-9
               select $"{f}->{t}",
        ];
        Assert.Equal(
            new[] { "BBL->MB", "BBL->kl", "BBL->KL_tk", "GAL->BBL", "l->BBL" }.Order(StringComparer.Ordinal),
            inverted.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("BBL", 500.0)]
    [InlineData("GAL", 882_000.0)]
    [InlineData("MB", 500_000_000.0)]
    public void APayerSwapInAnyUnitIsWorthItsSpreadTimesItsQuantity(string unit, double quantLib)
    {
        // the swap: 1,000 units in March 2025, the index at 2 a unit, the fixed price 1.5 a unit
        UnitOfMeasure u = Unit(unit);
        CommodityIndex index = Index(u, 2.0);
        EvaluationContext context = WithFixing(Context(), index, 2.0);
        EnergyVanillaSwap swap = Vanilla(true, 1.5, u, index, MarchPeriods(new Quantity(None, u, 1000.0)), Flat(0.0));
        double npv = swap.Calculate(context).NPV!.Value;
        Assert.Equal(500.0, npv, 1e-10 * 500.0);
        if (unit != "BBL")
        {
            Assert.True(Math.Abs(npv - quantLib) > 1e5, $"QuantLib's {quantLib}");
        }
    }

    [Theory]
    [InlineData("BBL", 100.0)]
    [InlineData("GAL", 176_400.0)]
    [InlineData("MB", 100_000_000.0)]
    public void AUnitCostIsChargedPerUnitDelivered(string unit, double quantLib)
    {
        // the swap fee: 0.1 a unit on 1,000 units
        UnitOfMeasure u = Unit(unit);
        CommodityIndex index = Index(u, 2.0);
        EvaluationContext context = WithFixing(Context(), index, 2.0);
        var costs = new Dictionary<string, object> { ["fee"] = new CommodityUnitCost(new Money(new Currencies.USDCurrency(), 0.1), u) };
        EnergyVanillaSwap swap = Vanilla(true, 1.5, u, index, MarchPeriods(new Quantity(None, u, 1000.0)), Flat(0.0), costs);
        EnergyCommodityResults results = swap.Calculate(context);
        Assert.Equal(100.0, results.SecondaryCostAmounts["fee"].Value, 1e-10 * 100.0);
        Assert.Equal(400.0, results.NPV!.Value, 1e-10 * 500.0);
        Assert.Equal(unit == "BBL", Math.Abs(results.SecondaryCostAmounts["fee"].Value - quantLib) < 1e-8);
    }

    [Theory]
    [InlineData("MB", "GAL", "BBL", "l")]
    [InlineData("GAL", "MB", "kl", "BBL")]
    [InlineData("l", "KL_tk", "GAL", "MB")]
    [InlineData("kl", "BBL", "MB", "GAL")]
    [InlineData("BBL", "l", "KL_tk", "kl")]
    public void EveryUnitCombinationPricesItsHandValue(string quantityUnit, string indexUnit, string fixedUnit, string costUnit)
    {
        // a monthly quantity of 3 units, the index at 84 a barrel and the fixed price at 80 a barrel, each quoted in its
        // unit, a fee of 0.15 a barrel quoted in its unit, all on a 3% curve: the hand value converts each through barrels
        double q = 3.0 / BarrelsPerUnit[quantityUnit] * BarrelsPerUnit["BBL"];
        double indexPrice = 84.0 * BarrelsPerUnit[indexUnit];
        double fixedPrice = 80.0 * BarrelsPerUnit[fixedUnit];
        double fee = 0.15 * BarrelsPerUnit[costUnit];
        CommodityIndex index = Index(Unit(indexUnit), indexPrice);
        EvaluationContext context = WithFixing(Context(), index, indexPrice);
        var costs = new Dictionary<string, object>
        {
            ["fee"] = new CommodityUnitCost(new Money(new Currencies.USDCurrency(), fee), Unit(costUnit)),
        };
        var curve = Flat(0.03);
        EnergyVanillaSwap swap = Vanilla(
            true, fixedPrice, Unit(fixedUnit), index, MarchPeriods(new Quantity(None, Unit(quantityUnit), q)), curve, costs);
        EnergyCommodityResults results = swap.Calculate(context);
        double barrels = 3.0;
        double expected = ((84.0 - 80.0) * barrels * curve.Discount(new Time.Date(5, Time.Month.April, 2025))) - (0.15 * barrels);
        Assert.Equal(expected, results.NPV!.Value, 1e-12 * 252.0);
    }

    [Fact]
    public void AnIndexsForwardPriceConvertsIntoItsUnitAsAPrice()
    {
        // a curve at 84 a barrel read by an index in gallons: 2 a gallon; QuantLib multiplies by its stored barrels-to-
        // gallons factor, 42, for 3,528
        var curve = new CommodityCurve(
            "CRVBBL", None, new Currencies.USDCurrency(), new BarrelUnitOfMeasure(), new Time.NullCalendar(),
            [Today, Today + 1095], [84.0, 84.0]);
        var index = new CommodityIndex(
            "GALIDX", None, new Currencies.USDCurrency(), new GallonUnitOfMeasure(), new Time.NullCalendar(), 1.0, curve,
            null, 0);
        Assert.Equal(2.0, index.ForwardPrice(Today + 30, Context()), 1e-14 * 2.0);
        Assert.Equal(3528.0, 84.0 * Context().UnitOfMeasureConversions.Lookup(
            None, new BarrelUnitOfMeasure(), new GallonUnitOfMeasure()).ConversionFactor);
    }

    [Fact]
    public void ABasisCurveInThousandBarrelsAddsItsPricePerBarrel()
    {
        // a basis of 1,000 a thousand-barrel is 1 a barrel; QuantLib's factor from MB to BBL, 1,000, makes it 10⁶
        var basis = new CommodityCurve(
            "BASMB", None, new Currencies.USDCurrency(), new MBUnitOfMeasure(), new Time.NullCalendar(),
            [Today, Today + 1095], [1000.0, 1000.0]);
        CommodityCurve curve = new CommodityCurve(
                "OUTBBL", None, new Currencies.USDCurrency(), new BarrelUnitOfMeasure(), new Time.NullCalendar(),
                [Today, Today + 1095], [70.0, 70.0])
            .SetBasisOfCurve(basis, Context());
        Assert.Equal(1.0, curve.BasisOfPrice(Today + 30), 1e-15);
        Assert.Equal(71.0, curve.Price(Today + 30, null, 0), 1e-14 * 71.0);
    }
}
