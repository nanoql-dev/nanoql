// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The linear TSR pricer's reversion cut: QuantLib's <c>GsrG</c> reads every reversion with |a| below 1e-4 as 0
/// (<c>lineartsrpricer.cpp:71-79</c>), so the model's slope jumps there: on the wheel, on a flat 3% curve and a 20%
/// volatility, a 1-year coupon fixing in 10 years prices 0.03235538786871865 for every a below 1e-4 and
/// 0.03235551192457936 at 1e-4, a jump of 1.2e-7 in its convexity adjustment of 1.9e-3. nanoql takes −expm1(−a·τ)/a
/// below the cut (§3.3's expm1 class) and τ at a = 0, with QuantLib's form at and above it, which loses at most 1e-12
/// there. QuantLib's checks: its own values at 0 and at the cut.
/// </summary>
public class LinearTsrReversionCutTests
{
    [Fact]
    public void ThePriceIsContinuousAcrossTheCut()
    {
        double below = Rate(1e-4 * (1.0 - 1e-12));
        double at = Rate(1e-4);
        Assert.True(Math.Abs(below - at) <= 1e-12, $"{below} {at}");
        Assert.True(Math.Abs(at - 0.03235551192457936) <= 1e-15 * at, $"{at}");

        // QuantLib's jump
        Assert.True(Math.Abs(0.03235538786871865 - 0.03235551192457936) > 1e-7);
    }

    [Fact]
    public void TheLimitAtZeroIsQuantLibsValue()
    {
        double zero = Rate(0.0);
        Assert.True(Math.Abs(zero - 0.03235538786871865) <= 1e-15 * zero, $"{zero}");
        Assert.True(Math.Abs(Rate(1e-14) - zero) <= 1e-15, $"{Rate(1e-14)}");
        Assert.True(Math.Abs(Rate(-1e-14) - zero) <= 1e-15, $"{Rate(-1e-14)}");

        // in between, the reversion moves the price as it does above the cut, where QuantLib's reads it
        double slope = (Rate(1e-4) - zero) / 1e-4;
        Assert.True(Math.Abs(((Rate(5e-5) - zero) / 5e-5) - slope) <= 1e-3 * Math.Abs(slope));
    }

    private static double Rate(double a) =>
        CmsInputs.Coupon(CmsInputs.SwapIndex(new Period(10, TimeUnit.Years)))
            .SetPricer(new LinearTsrPricer(CmsInputs.Volatility(), a, null, new LinearTsrPricerSettings().WithRateBound(0.0001, 2.0)))
            .Rate(CmsInputs.Context);
}
