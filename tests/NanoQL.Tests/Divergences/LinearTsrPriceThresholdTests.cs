// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The linear TSR pricer's price-threshold strategy: QuantLib's <c>strikeFromPrice</c> starts Brent at the swap rate,
/// the bracket's end (<c>lineartsrpricer.cpp:263</c>), which Brent refuses (<c>solver1d.hpp:203-206</c>), and the
/// refusal is swallowed, and the threshold it passes is the vega ratio's member (<c>:310</c>): every threshold falls
/// back to the default bounds, so on the wheel, on a flat 3% curve and a 20% volatility, a 1-year coupon fixing in 10
/// years prices 0.032367510616761526 at every threshold, the default rate bound's price. nanoql searches with the
/// threshold from the bracket's midpoint, as the vega-ratio search does. QuantLib's check: its rate-bound strategy on
/// the bounds the search finds, and the out-of-the-money price at each bound the threshold, within the search's
/// accuracy.
/// </summary>
public class LinearTsrPriceThresholdTests
{
    private const double QuantLibs = 0.032367510616761526;

    [Fact]
    public void TheBoundsAreTheThresholdsStrikes()
    {
        CmsCoupon c = CmsInputs.Coupon(CmsInputs.SwapIndex(new Period(10, TimeUnit.Years)));
        SwaptionVolatilityStructure vol = CmsInputs.Volatility();
        Date fixing = c.FixingDate();
        double s = c.SwapIndex.Fixing(fixing, CmsInputs.Context);
        var section = new AtmSmileSection(vol.SmileSection(fixing, c.SwapIndex.Tenor), s);
        const double threshold = 1e-6;

        // the bracket of each search, as the strategy brackets it at the swap rate
        double kCall = new Brent().Solve(
            k => section.OptionPrice(k, OptionType.Call) - threshold, 1e-5, (s + 2.0) / 2.0, s, Math.Min(section.MaxStrike(), 2.0));
        double kPut = new Brent().Solve(
            k => section.OptionPrice(k, OptionType.Put) - threshold, 1e-5, (1e-4 + s) / 2.0, Math.Max(section.MinStrike(), 1e-4), s);
        Assert.True(Math.Abs(section.OptionPrice(kCall, OptionType.Call) - threshold) <= 1e-7, $"{kCall}");
        Assert.True(Math.Abs(section.OptionPrice(kPut, OptionType.Put) - threshold) <= 1e-7, $"{kPut}");

        double byThreshold = Rate(c, new LinearTsrPricerSettings().WithPriceThreshold(threshold));
        double byBounds = Rate(c, new LinearTsrPricerSettings().WithRateBound(kPut, kCall));
        Assert.Equal(byBounds, byThreshold);
        Assert.True(Math.Abs(byThreshold - QuantLibs) > 1e-7, $"{byThreshold}");
    }

    [Fact]
    public void TheThresholdMovesThePrice()
    {
        CmsCoupon c = CmsInputs.Coupon(CmsInputs.SwapIndex(new Period(10, TimeUnit.Years)));
        double[] rates = [.. new[] { 1e-8, 1e-6, 1e-4 }.Select(p => Rate(c, new LinearTsrPricerSettings().WithPriceThreshold(p)))];
        Assert.True(rates[0] != rates[1] && rates[1] != rates[2], string.Join(", ", rates));

        // a lower threshold integrates further, toward the full range's price, QuantLib's at every threshold
        Assert.True(Math.Abs(rates[0] - QuantLibs) < Math.Abs(rates[2] - QuantLibs), string.Join(", ", rates));
    }

    private static double Rate(CmsCoupon c, LinearTsrPricerSettings settings) =>
        c.SetPricer(new LinearTsrPricer(CmsInputs.Volatility(), 0.01, null, settings)).Rate(CmsInputs.Context);
}
