// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="CPIBond"/>'s construction (<c>cpibond.cpp:83-102</c>): the cash flows are <see cref="CPILeg"/>'s, a CPI
/// coupon per period and the notional flow last, which is the redemption; the notionals come from the cash flows; and
/// <see cref="CPIBond.GrowthOnly"/>, QuantLib's inspector at the pin, is false, as for every bond the ported
/// constructor builds.
/// </summary>
public class CPIBondConstructionTests
{
    [Fact]
    public void TheRedemptionIsTheNotionalFlow()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.June, 2024) };
        var schedule = new Schedule(
            new Date(15, Month.June, 2024),
            new Date(15, Month.June, 2027),
            new Period(1, TimeUnit.Years),
            new UnitedKingdom(),
            BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Backward,
            false,
            context);
        var bond = new CPIBond(
            2,
            100.0,
            300.0,
            new Period(3, TimeUnit.Months),
            new UKRPI(),
            CPIInterpolationType.Flat,
            schedule,
            [0.01],
            new Thirty360(Thirty360Convention.BondBasis));

        Assert.False(bond.GrowthOnly);
        Assert.Equal(4, bond.Cashflows.Length);
        Assert.All(bond.Cashflows[..^1], f => Assert.IsType<CPICoupon>(f));
        CashFlow redemption = Assert.Single(bond.Redemptions);
        Assert.Same(bond.Cashflows[^1], redemption);
        Assert.IsType<CPICashFlow>(redemption);
        Assert.Same(redemption, bond.Redemption());
        Assert.Equal([100.0, 0.0], bond.Notionals);
    }
}
