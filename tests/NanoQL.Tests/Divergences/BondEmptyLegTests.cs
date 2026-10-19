// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A bond on an empty leg, the default of QuantLib's two public constructors, and a bond on a leg holding a null cash
/// flow. QuantLib builds the bond on an empty leg and leaves its notional schedule empty (<c>bond.cpp:42, 72</c>), and
/// its notional then reads the schedule's last date (<c>bond.cpp:117</c>), past the end of an empty vector, which is
/// undefined: the wheel ends the process on <c>notional()</c>, <c>isTradable()</c>, <c>accruedAmount()</c> and
/// <c>cleanPrice()</c>. nanoql builds the bond as QuantLib does, with QuantLib's defined answers (its NPV the expired
/// bond's 0, its maturity date refused "empty leg", its redemption "multiple redemption cash flows given"), and its
/// <see cref="Bond.Notional"/> refuses where QuantLib reads past the end, with <see cref="NanoQLException"/>. A null cash
/// flow is dereferenced by QuantLib's sort (<c>bond.cpp:43-44, 74-75</c>), which is undefined, and the wheel refuses
/// <c>None</c> in a leg; nanoql refuses it at construction with <see cref="ArgumentException"/>, as a swap does.
/// </summary>
public class BondEmptyLegTests
{
    private static readonly Date Today = new(16, Month.June, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void ABondOnAnEmptyLegBuildsAndRefusesItsNotional()
    {
        var curve = new FlatForward(Today, 0.03, new Actual365Fixed());
        var issue = new Date(1, Month.June, 2025);
        Bond[] bonds =
        [
            new Bond(2, new TARGET()),
            new Bond(2, new TARGET(), issue, []),
            new Bond(2, new TARGET(), 100.0, null),
            new Bond(2, new TARGET(), 100.0, new Date(16, Month.June, 2030), issue),
        ];

        foreach (Bond bond in bonds)
        {
            Assert.Empty(bond.Cashflows);
            Assert.Empty(bond.Notionals);

            // QuantLib's defined answers: expired, so priced at 0 with a settlement value of 0.
            Assert.True(bond.IsExpired(Context));
            BondResults results = DiscountingBondEngine.Calculate(bond, Context, curve);
            Assert.Equal(0.0, results.NPV);
            Assert.Equal(0.0, results.SettlementValue);
            Assert.Equal(
                "multiple redemption cash flows given", Assert.Throws<NanoQLException>(() => bond.Redemption()).Message);

            // Where QuantLib reads past the empty notional schedule's end, nanoql refuses.
            Assert.Throws<NanoQLException>(() => bond.Notional(Context));
            Assert.Throws<NanoQLException>(() => bond.Notional(Context, new Date(16, Month.June, 2026)));
            Assert.Throws<NanoQLException>(() => bond.IsTradable(Context));
            Assert.Throws<NanoQLException>(() => bond.AccruedAmount(Context));
            Assert.Throws<NanoQLException>(() => bond.CleanPrice(results, Context));
        }

        // The maturity date is the one given, else the leg's, which QuantLib refuses for an empty leg.
        Assert.StartsWith("empty leg", Assert.Throws<ArgumentException>(() => bonds[0].MaturityDate()).Message, StringComparison.Ordinal);
        Assert.Equal(new Date(16, Month.June, 2030), bonds[3].MaturityDate());
    }

    [Fact]
    public void ANullCashFlowIsRefusedAtConstruction()
    {
        var coupon = new FixedRateCoupon(
            new Date(16, Month.June, 2026), 100.0, 0.03, new Actual365Fixed(), Today, new Date(16, Month.June, 2026));
        ImmutableArray<CashFlow> leg = [coupon, null!];

        ArgumentException byCoupons = Assert.Throws<ArgumentException>(() => new Bond(2, new TARGET(), null, leg));
        Assert.Equal("coupons", byCoupons.ParamName);
        ArgumentException byCashflows = Assert.Throws<ArgumentException>(
            () => new Bond(2, new TARGET(), 100.0, null, null, leg));
        Assert.Equal("cashflows", byCashflows.ParamName);

        // The leg without it builds.
        Assert.Equal(2, new Bond(2, new TARGET(), null, [coupon]).Cashflows.Length);
    }
}
