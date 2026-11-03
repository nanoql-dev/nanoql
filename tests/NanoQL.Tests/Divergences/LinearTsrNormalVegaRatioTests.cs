// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The linear TSR pricer's vega-ratio strategy on a normal section: QuantLib's <c>strikeFromVegaRatio</c> reads
/// <c>SmileSection::vega</c> outside its <c>try</c> (<c>lineartsrpricer.cpp:227-228</c>), which refuses a normal
/// section (<c>smilesection.cpp:119-128</c>), so every normal-volatility coupon on the strategy refuses: on the
/// wheel, on a flat 3% curve and a 0.8% normal volatility, "vega for normal smilesection not yet implemented".
/// nanoql's search reads a normal section's vega from QuantLib's own Bachelier derivative
/// (<c>bachelierBlackFormulaStdDevDerivative</c>) times √T, the ratio scale-free; <see cref="SmileSection.Vega"/>
/// keeps QuantLib's refusal. QuantLib's checks: its lognormal values, and the bound found has the ratio of
/// QuantLib's Bachelier derivatives the strategy asks.
/// </summary>
public class LinearTsrNormalVegaRatioTests
{
    [Fact]
    public void ALognormalSectionKeepsQuantLibsValue()
    {
        double rate = Rate(CmsInputs.Volatility(), new LinearTsrPricerSettings().WithVegaRatio(0.01));
        Assert.True(Math.Abs(rate - 0.03236523559788551) <= 1e-15 * rate, $"{rate}");
    }

    [Fact]
    public void ANormalSectionsBoundsHaveTheRatio()
    {
        SwaptionVolatilityStructure vol = CmsInputs.Volatility(VolatilityType.Normal);
        CmsCoupon c = CmsInputs.Coupon(CmsInputs.SwapIndex(new Period(10, TimeUnit.Years)));
        Date fixing = c.FixingDate();
        double s = c.SwapIndex.Fixing(fixing, CmsInputs.Context);
        SmileSection section = new AtmSmileSection(vol.SmileSection(fixing, c.SwapIndex.Tenor), s);
        double Vega(double k) =>
            BlackFormulaFunctions.BachelierBlackFormulaStdDevDerivative(k, s, Math.Sqrt(section.Variance(k)))
            * Math.Sqrt(section.ExerciseTime);
        double target = Vega(s) * 0.01;

        // the strategy's brackets at the swap rate, on the default bounds: [s, 2] for a call, [−2, s] for a put
        double kCall = new Brent().Solve(k => Vega(k) - target, 1e-5, (s + 2.0) / 2.0, s, 2.0);
        double kPut = new Brent().Solve(k => Vega(k) - target, 1e-5, (-2.0 + s) / 2.0, -2.0, s);
        Assert.True(Math.Abs((Vega(kCall) / Vega(s)) - 0.01) <= 1e-3, $"{kCall}");
        Assert.True(Math.Abs((Vega(kPut) / Vega(s)) - 0.01) <= 1e-3, $"{kPut}");

        double byRatio = Rate(vol, new LinearTsrPricerSettings().WithVegaRatio(0.01));
        Assert.Equal(Rate(vol, new LinearTsrPricerSettings().WithRateBound(kPut, kCall)), byRatio);

        // the section's own vega refuses, as QuantLib's
        Assert.Equal(
            "vega for normal smilesection not yet implemented",
            Assert.Throws<NanoQLException>(() => section.Vega(s)).Message);
    }

    private static double Rate(SwaptionVolatilityStructure vol, LinearTsrPricerSettings settings) =>
        CmsInputs.Coupon(CmsInputs.SwapIndex(new Period(10, TimeUnit.Years)))
            .SetPricer(new LinearTsrPricer(vol, 0.01, null, settings)).Rate(CmsInputs.Context);
}
