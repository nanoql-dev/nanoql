// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="ProxyIbor"/>'s forecast, the gearing times the proxied fixing plus the spread, and its coupons
/// forecasting through it (fixed). QuantLib's <c>forecastFixing</c> returns gearing × proxied × spread, and an Ibor
/// coupon on the proxy forecasts from the proxy's forwarding curve, which it lacks. On QuantLib 1.44 (gearing 2 on
/// Euribor 6M, spread 0.001) the proxied fixing 0.0298114585668 forecasts 5.96229171335e-05 for 0.0606229171335, and
/// the coupon's rate refuses "null term structure set to this instance of PX6M Actual/360". nanoql's forecast is
/// gearing × proxied + spread, and the coupon's rate is it.
/// </summary>
public class ProxyIborTests
{
    private static readonly Date Today = new(15, Month.June, 2026);

    [Fact]
    public void TheForecastIsTheGearedProxiedFixingPlusTheSpread()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        var euribor = new Euribor6M(new FlatForward(Today, 0.03, new Actual365Fixed()));
        ProxyIbor proxy = Proxy(euribor);
        Date fixingDate = new TARGET().Advance(Today, 1, TimeUnit.Years);
        double proxied = euribor.Fixing(fixingDate, context);
        Assert.Equal((2.0 * proxied) + 0.001, proxy.Fixing(fixingDate, context));
        Assert.True(Math.Abs(proxy.Fixing(fixingDate, context) - (2.0 * proxied * 0.001)) > 0.05);
    }

    [Fact]
    public void ACouponOnTheProxyForecastsThroughIt()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        var euribor = new Euribor6M(new FlatForward(Today, 0.03, new Actual365Fixed()));
        ProxyIbor proxy = Proxy(euribor);
        Date start = new TARGET().Advance(Today, 1, TimeUnit.Years);
        Date end = new TARGET().Advance(start, 6, TimeUnit.Months);
        var coupon = new IborCoupon(end, 100.0, start, end, 2, proxy, pricer: new BlackIborCouponPricer(context));
        Assert.Equal(proxy.Fixing(coupon.FixingDate(), context), coupon.Rate(context));
    }

    private static ProxyIbor Proxy(IborIndex proxied) => new(
        "PX", new Period(6, TimeUnit.Months), 2, new EURCurrency(), new TARGET(), BusinessDayConvention.ModifiedFollowing,
        false, new Actual360(), 2.0, proxied, 0.001);
}
