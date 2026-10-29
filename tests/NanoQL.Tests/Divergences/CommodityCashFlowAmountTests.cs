// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using static NanoQL.Tests.Conventions.CommodityMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="CommodityCashFlow.Amount"/> undiscounted (the S-xrt plan's XRR9, ruled 2026-10-28). QuantLib's
/// <c>amount()</c> returns the discounted amount (<c>commoditycashflow.hpp:56</c>), where <c>CashFlow::amount()</c> is
/// documented as "the actual amount paid at the cash flow date" (<c>cashflow.hpp:62-66</c>), so a consumer reading
/// these as cash flows discounts them twice: on the run's <c>probes\xi_c1.cpp</c> payer at 1.5, April's
/// <c>amount()</c> is 496.72311389766833 for a payment of 500. nanoql's <see cref="CommodityCashFlow.Amount"/> is the
/// 500, the discounted one <see cref="CommodityCashFlow.DiscountedAmount"/>. Checks: each amount times its discount
/// factor is its discounted amount, and where the swap's curves are one, the discounted amounts sum to the NPV before
/// the secondary costs.
/// </summary>
public class CommodityCashFlowAmountTests
{
    [Fact]
    public void AnAmountIsWhatIsPaidOnTheDate()
    {
        CommodityIndex index = Index(new BarrelUnitOfMeasure(), 2.0, 1.0, "PAYIDX");
        var costs = new Dictionary<string, object> { ["clearing"] = new Money(new Currencies.USDCurrency(), 25.0) };
        EnergyVanillaSwap swap = Vanilla(
            true, 1.5, new BarrelUnitOfMeasure(), index,
            MarchPeriods(new Quantity(None, new BarrelUnitOfMeasure(), 1000.0), new Time.Date(31, Time.Month.May, 2025)),
            Flat(0.03), costs);
        EvaluationContext context = WithFixing(Context(), index, 2.0);
        EnergyCommodityResults r = swap.Calculate(context);
        double sum = 0.0;
        foreach (CommodityCashFlow c in r.PaymentCashFlows.Values)
        {
            Assert.Equal(c.UndiscountedAmount.Value, c.Amount(context));
            Assert.Equal(c.DiscountedAmount.Value, c.Amount(context) * c.DiscountFactor, 1e-12 * 500.0);
            sum += c.DiscountedAmount.Value;
        }

        Assert.Equal(r.NPV!.Value + 25.0, sum, 1e-10 * 1500.0);
        CommodityCashFlow april = r.PaymentCashFlows.Values.First();
        Assert.Equal(500.0, april.Amount(context), 1e-10 * 500.0);
        Assert.Equal(496.72311389766833, april.DiscountedAmount.Value, 1e-10 * 500.0);
    }
}
