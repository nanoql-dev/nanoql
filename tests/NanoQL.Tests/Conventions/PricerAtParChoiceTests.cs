// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// An Ibor pricer computes with its own at-par choice, resolved when it was built, whichever pricer the coupon holds:
/// a pricer B asked for the rate of a coupon whose own pricer is A uses B's choice, while the coupon's own rate and its
/// inspectors use A's. The fixing reaches the pricer through the coupon's protected member, which a stub coupon
/// overrides, so a pricer prices a stub coupon on its weighted index whatever its choice. As in QuantLib, the fixing
/// still needs the coupon to hold an Ibor pricer of its own, except a stub coupon's, which reads no choice.
/// </summary>
public class PricerAtParChoiceTests
{
    [Fact]
    public void APricerComputesWithItsOwnChoice()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2025) };
        var curve = new FlatForward(context.EvaluationDate, 0.031, new Actual365Fixed());
        var index = new Euribor6M(curve);
        var atPar = new BlackIborCouponPricer(null, BlackIborCouponPricerTimingAdjustment.Black76, 1.0, false);
        var indexed = new BlackIborCouponPricer(null, BlackIborCouponPricerTimingAdjustment.Black76, 1.0, true);

        // A broken period, on which the two choices give different fixings.
        var start = new Date(17, Month.July, 2025);
        var end = new Date(19, Month.February, 2026);
        IborCoupon coupon = new IborCoupon(end, 1e6, start, end, 2, index).SetPricer(atPar);
        IborCoupon asIndexed = coupon.SetPricer(indexed);

        Assert.Equal(asIndexed.Rate(context), indexed.SwapletRate(coupon, context));
        Assert.Equal(coupon.Rate(context), atPar.SwapletRate(asIndexed, context));
        Assert.NotEqual(coupon.Rate(context), indexed.SwapletRate(coupon, context));
        Assert.NotEqual(coupon.FixingEndDate(), asIndexed.FixingEndDate());
        Assert.Equal(asIndexed.FixingMaturityDate(), asIndexed.FixingEndDate());
        Assert.Equal(coupon.FixingValueDate(), asIndexed.FixingValueDate());

        // QuantLib's fixing reads the coupon's own Ibor pricer.
        var bare = new IborCoupon(end, 1e6, start, end, 2, index);
        Assert.Throws<NanoQLException>(() => indexed.SwapletRate(bare, context));

        // A stub coupon fixes on its weighted index whatever the pricer's choice, and needs no pricer to fix.
        var selection = new StubIndexSelection(
            StubIndexSelectionConvention.Interpolated, [new Euribor3M(curve), index, new Euribor1Y(curve)]);
        var stub = new StubIborCoupon(end, 1e6, start, end, 2, selection, 1.5, 0.001);
        double weighted = stub.IborIndex.Fixing(stub.FixingDate(), context);
        Assert.Equal(weighted, stub.IndexFixing(context));
        Assert.Equal((1.5 * weighted) + 0.001, atPar.SwapletRate(stub.SetPricer(indexed), context));
        Assert.Equal((1.5 * weighted) + 0.001, indexed.SwapletRate(stub.SetPricer(atPar), context));
        Assert.Equal(stub.SetPricer(atPar).Rate(context), stub.SetPricer(indexed).Rate(context));
    }
}
