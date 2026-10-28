// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.TermStructures;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The n-th-to-default upfront on the swap's own nominal (the S-xcr plan's XR8 (xi)): QuantLib's is the basket's
/// remaining notional times the rate (<c>integralntdengine.cpp:157-163</c>), 5 for a 1,000 nominal on a 100 basket (the
/// planner's <c>xp_engines.cpp</c>, D42: the swap at its fair premium reads an NPV of 5, its upfront). The check is
/// QuantLib where the nominal is the basket's notional.
/// </summary>
public class NthToDefaultUpfrontTests
{
    [Fact]
    public void TheUpfrontIsTheNominalsRate()
    {
        Basket basket = new Basket(
            Today, ["A", "B", "C"], [30.0, 30.0, 40.0], PoolOf(["A", "B", "C"], [Flat(Today, 0.02), Flat(Today, 0.02), Flat(Today, 0.02)]), Context)
            .SetLossModel(Constant(3, 0.3));
        double discount = Discount().Discount(new Time.Date(1, Time.Month.September, 2006));

        NthToDefaultResults large = Ntd(basket, 1, 1000.0, 0.05);
        NthToDefaultResults onNotional = Ntd(basket, 1, 100.0, 0.05);

        Assert.Equal(1000.0 * 0.05 * discount, large.UpfrontPremiumValue!.Value, 1e-12);
        Assert.Equal(100.0 * 0.05 * discount, onNotional.UpfrontPremiumValue!.Value, 1e-12);
        Assert.Equal(10.0 * onNotional.UpfrontPremiumValue!.Value, large.UpfrontPremiumValue!.Value, 1e-12);
    }
}
