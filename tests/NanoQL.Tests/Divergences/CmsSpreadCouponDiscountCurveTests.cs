// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The lognormal CMS spread pricer without a coupon discount curve discounts each coupon on its own first swap index's
/// curve. QuantLib's <c>initialize</c> fills its empty coupon curve handle with the first coupon's index's curve and
/// keeps it (<c>lognormalcmsspreadpricer.cpp</c>, <c>couponDiscountCurve_ = …</c>), so one pricer serving coupons on
/// different indexes discounts every later coupon on the first's curve: its price is the price on a pricer given that
/// curve, which this fact computes as QuantLib's. Rates don't read the curve.
/// </summary>
public class CmsSpreadCouponDiscountCurveTests
{
    [Fact]
    public void EachCouponIsDiscountedOnItsOwnIndexCurve()
    {
        var refDate = new Date(23, Month.February, 2018);
        var context = new EvaluationContext { EvaluationDate = refDate };
        YieldTermStructure curveA = new FlatForward(refDate, 0.02, new Actual365Fixed());
        YieldTermStructure curveB = new FlatForward(refDate, 0.05, new Actual365Fixed());
        var vol = new ConstantSwaptionVolatility(
            refDate, new TARGET(), BusinessDayConvention.Following, 0.20, new Actual365Fixed());

        CmsSpreadCoupon Coupon(YieldTermStructure curve) => new(
            new Date(23, Month.February, 2029), 10000.0, new Date(23, Month.February, 2028), new Date(23, Month.February, 2029),
            2, new SwapSpreadIndex(
                "cms10y2y",
                new EuriborSwapIsdaFixA(10 * TimeUnit.Years, curve, curve),
                new EuriborSwapIsdaFixA(2 * TimeUnit.Years, curve, curve)),
            1.0, 0.0, null, null, new Actual360(), false);

        var shared = new LognormalCmsSpreadPricer(new LinearTsrPricer(vol, 0.01), 0.6, null, 32);
        var onA = new LognormalCmsSpreadPricer(new LinearTsrPricer(vol, 0.01), 0.6, curveA, 32);
        var onB = new LognormalCmsSpreadPricer(new LinearTsrPricer(vol, 0.01), 0.6, curveB, 32);
        CmsSpreadCoupon a = Coupon(curveA);
        CmsSpreadCoupon b = Coupon(curveB);

        // the first coupon priced, then the second on the same pricer
        Assert.Equal(onA.SwapletPrice(a, context), shared.SwapletPrice(a, context));
        double price = shared.SwapletPrice(b, context);
        Assert.Equal(onB.SwapletPrice(b, context), price);

        // QuantLib's: the second coupon on the first's curve
        double quantLib = onA.SwapletPrice(b, context);
        Assert.True(Math.Abs(price - quantLib) > 1e-3 * Math.Abs(price));
        Assert.Equal(shared.SwapletRate(b, context), onA.SwapletRate(b, context));
    }
}
