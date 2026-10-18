// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="FloatingRateCouponExtensions.SetPricer"/> returns a copy where QuantLib's <c>setPricer</c> changes the
/// coupon: the original is unchanged, the copy keeps the receiver's static type and its runtime type, and a capped
/// coupon's copy carries the pricer into a copy of its underlying, as QuantLib's sets both.
/// </summary>
public class SetPricerCopyTests
{
    [Fact]
    public void SetPricerReturnsACopyOfTheReceiversType()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2025) };
        var index = new Euribor6M(new FlatForward(context.EvaluationDate, 0.03, new Actual365Fixed()));
        var start = new Date(17, Month.July, 2025);
        var end = new Date(19, Month.January, 2026);
        var vol = new ConstantOptionletVolatility(
            context.EvaluationDate, new TARGET(), BusinessDayConvention.Following, 0.2, new Actual365Fixed());
        var pricer = new BlackIborCouponPricer(context, vol);

        // The original is unchanged, and the copy keeps the static type.
        var coupon = new IborCoupon(end, 1e6, start, end, 2, index);
        IborCoupon priced = coupon.SetPricer(pricer);
        Assert.NotSame(coupon, priced);
        Assert.Null(coupon.Pricer);
        Assert.Same(pricer, priced.Pricer);
        Assert.Equal(coupon.FixingDate(), priced.FixingDate());
        Assert.Throws<NanoQLException>(() => coupon.Rate(context));
        Assert.Equal(index.Fixing(priced.FixingDate(), context), priced.Rate(context), 1e-15);

        // A subclass seen through its base keeps its runtime type.
        var selection = new StubIndexSelection(StubIndexSelectionConvention.ClosestIndex, [index]);
        IborCoupon stub = new StubIborCoupon(end, 1e6, start, end, 2, selection);
        IborCoupon pricedStub = stub.SetPricer(pricer);
        Assert.IsType<StubIborCoupon>(pricedStub);
        Assert.Null(stub.Pricer);
        Assert.Same(pricer, pricedStub.Pricer);

        // A capped coupon's copy carries the pricer into a copy of its underlying.
        var capped = new CappedFlooredIborCoupon(end, 1e6, start, end, 2, index, cap: 0.025);
        CappedFlooredIborCoupon pricedCapped = capped.SetPricer(pricer);
        Assert.Null(capped.Pricer);
        Assert.Null(capped.Underlying.Pricer);
        Assert.Same(pricer, pricedCapped.Pricer);
        Assert.Same(pricer, pricedCapped.Underlying.Pricer);
        Assert.NotSame(capped.Underlying, pricedCapped.Underlying);
        Assert.IsType<IborCoupon>(pricedCapped.Underlying);
        Assert.Throws<NanoQLException>(() => capped.Rate(context));
        Assert.True(pricedCapped.Rate(context) <= 0.025);

        // A null pricer removes it, from both.
        CappedFlooredIborCoupon cleared = pricedCapped.SetPricer(null);
        Assert.Null(cleared.Pricer);
        Assert.Null(cleared.Underlying.Pricer);
        Assert.Same(pricer, pricedCapped.Underlying.Pricer);
    }
}
