// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CommodityMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An energy basis swap's basis converted from its own currency into the base (fixed). QuantLib converts it from the
/// base currency into the basis currency (<c>energybasisswap.cpp:126-129</c>), the reverse of every other conversion in
/// the swap: on QuantLib 1.44 (1,000 barrels in March 2025, the pay index at 70 and the receive index at 73 dollars a
/// barrel, a basis of 2 euros a barrel at 1.25 dollars a euro, flat zero curves) the swap with the basis on the receive
/// leg prices 4,600 and on the pay leg 1,400, the basis read as 1.6 dollars. nanoql reads it as 2.5 dollars: 5,500 and
/// 500, the hand values (73 + 2.5 − 70) × 1,000 and (73 − 70 − 2.5) × 1,000. A basis in the base currency is QuantLib's
/// either way.
/// </summary>
public class EnergyBasisSwapTests
{
    private static readonly EvaluationContext Market = Context() with
    {
        ExchangeRates = Currencies.ExchangeRates.Default.Add(new ExchangeRate(new EURCurrency(), new USDCurrency(), 1.25)),
    };

    [Theory]
    [InlineData(false, 5500.0, 4600.0000000000728)]
    [InlineData(true, 500.0, 1399.9999999999854)]
    public void ABasisInEurosIsWorthItsDollars(bool toPayLeg, double expected, double quantLib)
    {
        CommodityIndex pay = Index(new BarrelUnitOfMeasure(), 70.0, 1.0, "BPAY");
        CommodityIndex receive = Index(new BarrelUnitOfMeasure(), 73.0, 1.0, "BREC");
        var swap = new EnergyBasisSwap(
            new NullCalendar(), null, pay, receive, toPayLeg, new USDCurrency(), new USDCurrency(),
            MarchPeriods(new Quantity(None, new BarrelUnitOfMeasure(), 1000.0)),
            new CommodityUnitCost(new Money(new EURCurrency(), 2.0), new BarrelUnitOfMeasure()), None, null,
            Flat(0.0), Flat(0.0), Flat(0.0));
        EvaluationContext context = WithFixing(WithFixing(Market, pay, 70.0), receive, 73.0);
        double npv = swap.Calculate(context).NPV!.Value;
        Assert.Equal(expected, npv, 1e-10 * 5500.0);
        Assert.True(Math.Abs(npv - quantLib) > 800.0);
    }
}
