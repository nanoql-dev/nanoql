// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CommodityMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An energy swap's payments in the currency of the leg the net amount goes through, at that currency's rate (the S-xrt
/// plan's XRR5 (viii), fixed under R8). QuantLib's vanilla swap files its fixed leg under the pay currency and its
/// floating leg under the receive currency whichever way it faces (<c>energyvanillaswap.cpp:104-113</c>), and nanoql
/// keeps that; but it pairs a payer's payment currency with the other leg's rate, and gives a receiver a pay-receive
/// flag of 0, so a receiver's payment never changes currency (<c>:220-225</c>); the basis swap pairs a payment's
/// currency with the other leg's rate too (<c>energybasisswap.cpp:263-266</c>). On QuantLib 1.44 (the run's
/// <c>probes\xi_c1.cpp</c>: paying dollars and receiving euros at 1.25 dollars a euro, 3% flat), a payer vanilla swap
/// at 1.5 on 1,000 barrels a month from March to May 2025 on an index at 2 is paid 496.72311389766810 "EUR", its dollar
/// amount, in April, and a payer at 2.5 pays −397.37849111813631 "USD", its euro amount; a receiver pays and is paid in
/// dollars. A basis swap on 1,000 barrels in March, its pay index at 70, is paid 2,980.3386833860422 "EUR" with its
/// receive index at 73 and pays −2,384.2709467087875 "USD" at 67, the other leg's amount each time. nanoql pays a payer
/// in euros and takes its payments in dollars, each at its own rate (397.37849111813467 EUR and −496.72311389767009
/// USD); a receiver is paid its fixed leg in dollars, QuantLib's payments, and pays its floating leg in euros; a basis
/// swap pays 2,384.27 EUR and takes −2,980.34 USD. The NPV, in the base currency, is QuantLib's either way.
/// </summary>
public class EnergySwapPaymentTests
{
    private static readonly EvaluationContext Market = Context() with
    {
        ExchangeRates = Currencies.ExchangeRates.Default.Add(new ExchangeRate(new EURCurrency(), new USDCurrency(), 1.25)),
    };

    [Theory]
    [InlineData(true, 1.5, "EUR", 1486.4618664481416)]
    [InlineData(true, 2.5, "USD", -1486.4618664481445)]
    [InlineData(false, 1.5, "EUR", -1486.4618664481416)]
    [InlineData(false, 2.5, "USD", 1486.4618664481445)]
    public void APaymentIsInTheCurrencyOfItsLegAtThatCurrencysRate(bool payer, double fixedPrice, string currency, double npv)
    {
        EnergyCommodityResults r = VanillaResults(payer, fixedPrice);
        Assert.Equal(npv, r.NPV!.Value, 1e-10 * 1500.0);
        Assert.Equal(3, r.PaymentCashFlows.Count);
        AssertPayments(r, currency);
    }

    [Fact]
    public void QuantLibsAprilPaymentsPairTheOtherLegsRate()
    {
        // the payer at 1.5: QuantLib's 496.72311389766810 "EUR" is the dollar amount; nanoql's the euro amount
        CommodityCashFlow april = VanillaResults(true, 1.5).PaymentCashFlows.Values.First();
        Assert.Equal(496.7231138976681, april.DiscountedAmount.Value, 1e-10 * 500.0);
        Assert.Equal(496.7231138976681 * 0.8, april.DiscountedPaymentAmount.Value, 1e-10 * 500.0);
    }

    [Fact]
    public void AReceiversFixedLegPaymentsAreQuantLibs()
    {
        // the receiver at 2.5 is paid its fixed leg in dollars, QuantLib's 496.72311389767037 USD in April
        CommodityCashFlow april = VanillaResults(false, 2.5).PaymentCashFlows.Values.First();
        Assert.Equal("USD", april.DiscountedPaymentAmount.Currency!.Code);
        Assert.Equal(496.72311389767037, april.DiscountedPaymentAmount.Value);
    }

    [Theory]
    [InlineData(73.0, "EUR", 2980.3386833860422, 2980.3386833860422)]
    [InlineData(67.0, "USD", -2980.338683385984, -2384.2709467087875)]
    public void ABasisSwapsPaymentIsInTheCurrencyOfItsLegAtThatCurrencysRate(
        double receivePrice, string currency, double npv, double quantLib)
    {
        CommodityIndex pay = Index(new BarrelUnitOfMeasure(), 70.0, 1.0, "PPAY");
        CommodityIndex receive = Index(new BarrelUnitOfMeasure(), receivePrice, 1.0, "PREC");
        var swap = new EnergyBasisSwap(
            new NullCalendar(), null, pay, receive, false, new USDCurrency(), new EURCurrency(),
            MarchPeriods(new Quantity(None, new BarrelUnitOfMeasure(), 1000.0)),
            new CommodityUnitCost(new Money(new USDCurrency(), 0.0), new BarrelUnitOfMeasure()), None, null,
            Flat(0.03), Flat(0.03), Flat(0.03));
        EnergyCommodityResults r = swap.Calculate(WithFixing(WithFixing(Market, pay, 70.0), receive, receivePrice));
        Assert.Equal(npv, r.NPV!.Value, 1e-10 * 3000.0);
        Assert.Single(r.PaymentCashFlows);
        AssertPayments(r, currency);
        Assert.True(Math.Abs(r.PaymentCashFlows.Values.Single().DiscountedPaymentAmount.Value - quantLib) > 500.0);
    }

    private static EnergyCommodityResults VanillaResults(bool payer, double fixedPrice)
    {
        CommodityIndex index = Index(new BarrelUnitOfMeasure(), 2.0, 1.0, "PAYIDX");
        EnergyVanillaSwap swap = Vanilla(
            payer, fixedPrice, new BarrelUnitOfMeasure(), index,
            MarchPeriods(new Quantity(None, new BarrelUnitOfMeasure(), 1000.0), new Date(31, Month.May, 2025)),
            Flat(0.03), receiveCurrency: new EURCurrency());
        return swap.Calculate(WithFixing(Market, index, 2.0));
    }

    // each payment against its base amount at its currency's rate, on its leg's discount factor
    private static void AssertPayments(EnergyCommodityResults r, string currency)
    {
        double fx = currency == "EUR" ? 0.8 : 1.0;
        foreach (CommodityCashFlow c in r.PaymentCashFlows.Values)
        {
            Assert.Equal(currency, c.DiscountedPaymentAmount.Currency!.Code);
            Assert.Equal(c.UndiscountedAmount.Value * fx, c.UndiscountedPaymentAmount.Value, 1e-12 * 3000.0);
            Assert.Equal(
                c.UndiscountedAmount.Value * c.PaymentDiscountFactor * fx, c.DiscountedPaymentAmount.Value, 1e-12 * 3000.0);
        }
    }
}
