// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The linear TSR pricer's day counter after a new volatility: QuantLib's constructor stores the volatility's day
/// counter (<c>lineartsrpricer.cpp:61</c>, <c>volDayCounter_</c>), which the model's slope reads (<c>GsrG</c>,
/// <c>:73</c>), and <c>setSwaptionVolatility</c> leaves it, so a pricer given another volatility prices the slope on
/// the old one's day counter: on the wheel, on a flat 3% curve, a 1-year coupon fixing in 10 years on a pricer built on
/// an Actual/365 volatility and given an Actual/360 one prices 0.0323995567530028, where a pricer built on the
/// Actual/360 volatility prices 0.03239972383363776. nanoql's copy reads its own volatility's day counter, so it prices
/// as a pricer built on it. QuantLib's check: the pricer built on the new volatility.
/// </summary>
public class LinearTsrVolatilityDayCounterTests
{
    [Fact]
    public void ACopyPricesAsAPricerBuiltOnItsVolatility()
    {
        CmsCoupon c = CmsInputs.Coupon(CmsInputs.SwapIndex(new Period(10, TimeUnit.Years)));
        var actual360 = CmsInputs.Volatility(dayCounter: new Actual360());
        LinearTsrPricer copy = new LinearTsrPricer(CmsInputs.Volatility(), 0.01).WithSwaptionVolatility(actual360);
        double copied = c.SetPricer(copy).Rate(CmsInputs.Context);
        double built = c.SetPricer(new LinearTsrPricer(actual360, 0.01)).Rate(CmsInputs.Context);
        Assert.Equal(built, copied);
        Assert.True(Math.Abs(built - 0.03239972383363776) <= 1e-15 * built, $"{built}");

        // QuantLib's set pricer
        Assert.True(Math.Abs(0.0323995567530028 - 0.03239972383363776) > 1e-7);
    }
}
