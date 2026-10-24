// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The CMS pricers' setters (the S-cms plan's CMR1 (iii)): QuantLib's <c>setMeanReversion</c> and
/// <c>setSwaptionVolatility</c> change the pricer in place; <see cref="IMeanRevertingPricer.WithMeanReversion"/> and
/// <see cref="CmsCouponPricer.WithSwaptionVolatility"/> return a copy of the pricer's own type, and the receiver is
/// unchanged. The linear TSR pricer keeps its settings, integrator and coupon discounting curve.
/// </summary>
public class CmsPricerCopyTests
{
    [Fact]
    public void TheMeanReversionIsSetOnACopy()
    {
        var numeric = new NumericHaganPricer(CmsInputs.Volatility(), GFunctionFactoryYieldCurveModel.NonParallelShifts, 0.01);
        NumericHaganPricer n2 = numeric.WithMeanReversion(0.03);
        Assert.Equal(0.03, n2.MeanReversion);
        Assert.Equal(0.01, numeric.MeanReversion);

        var analytic = new AnalyticHaganPricer(CmsInputs.Volatility(), GFunctionFactoryYieldCurveModel.Standard, 0.01);
        AnalyticHaganPricer a2 = analytic.WithMeanReversion(-0.02);
        Assert.Equal(-0.02, a2.MeanReversion);
        Assert.Equal(0.01, analytic.MeanReversion);

        var tsr = new LinearTsrPricer(CmsInputs.Volatility(), 0.01);
        LinearTsrPricer t2 = tsr.WithMeanReversion(0.05);
        Assert.Equal(0.05, t2.MeanReversion);
        Assert.Equal(0.01, tsr.MeanReversion);

        // through the mixin, the copy is of the pricer's own type
        foreach (IMeanRevertingPricer p in new IMeanRevertingPricer[] { numeric, analytic, tsr })
        {
            IMeanRevertingPricer copy = p.WithMeanReversion(0.04);
            Assert.IsType(p.GetType(), copy);
            Assert.Equal(0.04, copy.MeanReversion);
            Assert.Equal(0.01, p.MeanReversion);
        }
    }

    [Fact]
    public void TheVolatilityIsSetOnACopy()
    {
        SwaptionVolatilityStructure first = CmsInputs.Volatility(), second = CmsInputs.Volatility(VolatilityType.Normal);
        var integrator = new GaussKronrodNonAdaptive(1e-8, 1000, 1e-8);
        LinearTsrPricerSettings settings = new LinearTsrPricerSettings().WithVegaRatio(0.02);
        var tsr = new LinearTsrPricer(first, 0.01, CmsInputs.Curve, settings, integrator);
        LinearTsrPricer copy = tsr.WithSwaptionVolatility(second);
        Assert.Same(second, copy.SwaptionVolatility);
        Assert.Same(first, tsr.SwaptionVolatility);
        Assert.Same(settings, copy.Settings);
        Assert.Same(integrator, copy.Integrator);
        Assert.Same(CmsInputs.Curve, copy.CouponDiscountCurve);
        Assert.Equal(0.01, copy.MeanReversion);

        foreach (CmsCouponPricer p in new CmsCouponPricer[]
        {
            new NumericHaganPricer(first, GFunctionFactoryYieldCurveModel.Standard, 0.0),
            new AnalyticHaganPricer(first, GFunctionFactoryYieldCurveModel.Standard, 0.0),
            tsr,
        })
        {
            CmsCouponPricer c = p.WithSwaptionVolatility(null);
            Assert.IsType(p.GetType(), c);
            Assert.Null(c.SwaptionVolatility);
            Assert.Same(first, p.SwaptionVolatility);
        }
    }

    [Fact]
    public void TheDefaultsAreQuantLibs()
    {
        var tsr = new LinearTsrPricer(CmsInputs.Volatility(), 0.0);
        Assert.Equal(new LinearTsrPricerSettings(), tsr.Settings);
        Assert.Equal(LinearTsrPricerSettingsStrategy.RateBound, tsr.Settings.Strategy);
        Assert.Equal(0.01, tsr.Settings.VegaRatio);
        Assert.Equal(1.0E-8, tsr.Settings.PriceThreshold);
        Assert.Equal(3.0, tsr.Settings.StdDevs);
        Assert.Equal(LinearTsrPricerSettings.DefaultLowerBound, tsr.Settings.LowerRateBound);
        Assert.Equal(LinearTsrPricerSettings.DefaultUpperBound, tsr.Settings.UpperRateBound);
        Assert.True(tsr.Settings.DefaultBounds);
        Assert.Null(tsr.CouponDiscountCurve);
        var rule = Assert.IsType<GaussKronrodNonAdaptive>(tsr.Integrator);
        Assert.Equal(1E-10, rule.AbsoluteAccuracy);
        Assert.Equal(5000, rule.MaxEvaluations);
        Assert.Equal(1E-10, rule.RelativeAccuracy);

        // each strategy's setter, on default and explicit bounds
        Assert.Equal((LinearTsrPricerSettingsStrategy.PriceThreshold, 1e-6, true), Of(new LinearTsrPricerSettings().WithPriceThreshold(1e-6)));
        Assert.Equal((LinearTsrPricerSettingsStrategy.PriceThreshold, 1e-6, false), Of(new LinearTsrPricerSettings().WithPriceThreshold(1e-6, 0.0, 1.0)));
        Assert.Equal((LinearTsrPricerSettingsStrategy.BSStdDevs, 4.0, true), Of(new LinearTsrPricerSettings().WithBSStdDevs(4.0)));
        Assert.Equal((LinearTsrPricerSettingsStrategy.BSStdDevs, 4.0, false), Of(new LinearTsrPricerSettings().WithBSStdDevs(4.0, 0.0, 1.0)));
        Assert.Equal((LinearTsrPricerSettingsStrategy.VegaRatio, 0.02, false), Of(new LinearTsrPricerSettings().WithVegaRatio(0.02, 0.0, 1.0)));
        Assert.Equal((LinearTsrPricerSettingsStrategy.RateBound, 0.0, false), Of(new LinearTsrPricerSettings().WithRateBound(0.0, 1.0)));
        Assert.Equal(1.0, new LinearTsrPricerSettings().WithBSStdDevs(4.0, 0.0, 1.0).UpperRateBound);
    }

    private static (LinearTsrPricerSettingsStrategy, double, bool) Of(LinearTsrPricerSettings s) => (
        s.Strategy,
        s.Strategy switch
        {
            LinearTsrPricerSettingsStrategy.PriceThreshold => s.PriceThreshold,
            LinearTsrPricerSettingsStrategy.BSStdDevs => s.StdDevs,
            LinearTsrPricerSettingsStrategy.VegaRatio => s.VegaRatio,
            _ => s.LowerRateBound,
        },
        s.DefaultBounds);
}
