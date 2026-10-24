// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The refusals of the CMS market, its calibration and the CMS spread and digital coupons, by type, message and
/// parameter (playbook §2): QuantLib's texts, a check of an argument an <see cref="ArgumentException"/> naming it, a
/// check of a value already stored a <see cref="NanoQLException"/>.
/// </summary>
public class CmsMarketRefusalTests
{
    [Fact]
    public void TheMarketsRefusals()
    {
        VectorCase c = Case();
        EvaluationContext context = c.Context();
        SwapIndex index = SwaptionInputs.SwapIndexOf(c.Input("index"));
        SwaptionVolatilityStructure atm = CubeInputs.AtmOf(c.Input("cube").GetProperty("atm"), context);
        CmsCouponPricer pricer = CmsMarketVectorTests.PricerOf(c.Input("pricer"), atm);
        double[][] quotes = CubeInputs.Rows(c.Input("bidAskSpreads"));
        Period[] lengths = CubeInputs.Tenors(c.Input("swapLengths"));
        YieldTermStructure curve = index.ForwardingTermStructure!;
        Check(() => new CmsMarket(lengths, [index], index.IborIndex, quotes, [pricer, pricer], curve, context), "pricers", "nSwapIndexes_ (1) != pricers (2)");
        Check(() => new CmsMarket(lengths, [index], index.IborIndex, quotes[..3], [pricer], curve, context), "bidAskSpreads", "nExercise_ (4) != bidAskSpreads rows() (3)");
        Check(() => new CmsMarket(lengths, [index], index.IborIndex, [.. quotes.Select(q => new[] { q[0] })], [pricer], curve, context), "bidAskSpreads", "2*nSwapIndexes_ (2) != bidAskSpreads columns() (1)");
    }

    [Fact]
    public void TheCalibrationsRefusals()
    {
        VectorCase c = Case();
        CmsMarketCalibration calibration = CmsMarketCalibrationVectorTests.CalibrationOf(c, out EvaluationContext context);
        var cube = (XabrSwaptionVolatilityCube<SwaptionVolCubeSabrModel>)CubeInputs.CubeOf(c.Input("cube"), context);
        CmsMarket market = Market(c, context);
        Check(() => new CmsMarketCalibration(cube, market, new Matrix(3, 1, 1.0), CmsMarketCalibrationCalibrationType.OnSpread), "weights", "weights number of rows (3) must be equal to number of swap lengths (4)");
        Check(() => new CmsMarketCalibration(cube, market, new Matrix(4, 2, 1.0), CmsMarketCalibrationCalibrationType.OnSpread), "weights", "weights number of columns (2) must be equal to number of swap indexes (1");
        var type = Assert.Throws<ArgumentOutOfRangeException>(() => new CmsMarketCalibration(cube, market, new Matrix(4, 1, 1.0), (CmsMarketCalibrationCalibrationType)7));
        Assert.Equal("calibrationType", type.ParamName);
        Assert.StartsWith("unknown/illegal calibration type", type.Message, StringComparison.Ordinal);

        var criteria = new EndCriteria(20, 10, 1e-6, 1e-6, 1e-6);
        var method = new LevenbergMarquardt(1e-8, 1e-8, 1e-8);
        Check(() => calibration.Compute(criteria, method, new Vector([0.5]), false, context), "guess", "if mean reversion is not fixed, a guess must be provided");
        Check(() => calibration.Compute(criteria, method, new Vector([0.5, 0.5, 0.01]), true, context), "guess", "guess size (3) must be equal to swap tenors size (1) or greater by one if mean reversion is given as last element");
        Check(() => calibration.Compute(criteria, method, new Matrix(4, 1, 0.5), false, context), "meanReversionGuess", "if mean reversion is not fixed, a guess must be provided");
        Check(() => calibration.Compute(criteria, method, new Matrix(4, 2, 0.5), true, context), "guess", "number of swap tenors (1) must be equal to number of guess columns (2)");
        Check(() => calibration.Compute(criteria, method, new Matrix(3, 1, 0.5), true, context), "guess", "number of swap lengths (4) must be equal to number of guess rows (3)");
        Check(() => calibration.ComputeParametric(criteria, method, new Matrix(3, 1, 0.5), false, context), "meanReversionGuess", "if mean reversion is not fixed, a guess must be provided");
        Check(() => calibration.ComputeParametric(criteria, method, new Matrix(2, 1, 0.5), true, context), "guess", "number of parameters (3) must be equal to number of guess rows (2)");
    }

    [Fact]
    public void TheSpreadAndDigitalCouponsRefusals()
    {
        VectorCase c = VectorFile.Load("cxx/experimental/coupons/digitalcmsspreadcoupon").Cases.Single(x => x.Name == "cash calls at 5%");
        EvaluationContext context = c.Context();
        DigitalCmsSpreadLeg digital = DigitalCmsSpreadCouponCxxVectorTests.BuilderOf(c.Input("leg"), context);
        ImmutableArray<CashFlow> leg = digital.ToLeg();
        Check(() => CouponPricerFunctions.SetCouponPricer(leg, new BlackIborCouponPricer(context)), "pricer", "pricer not compatible with CMS spread coupon");
        var spreadCoupon = ((DigitalCmsSpreadCoupon)leg.First(cf => cf is DigitalCmsSpreadCoupon)).Underlying;
        Check(() => CouponPricerFunctions.SetCouponPricer([spreadCoupon], new BlackIborCouponPricer(context)), "pricer", "pricer not compatible with CMS spread coupon");
        var noIndex = Assert.Throws<ArgumentNullException>(() => new CmsSpreadLeg(CouponInputs.ScheduleOf(c.Input("leg").GetProperty("schedule"), context), null!));
        Assert.Equal("swapSpreadIndex", noIndex.ParamName);
        Assert.StartsWith("no index provided", noIndex.Message, StringComparison.Ordinal);
        NanoQLException e = Assert.Throws<NanoQLException>(() => ((DigitalCmsSpreadCoupon)leg.First(cf => cf is DigitalCmsSpreadCoupon)).Rate(context));
        Assert.Equal("pricer not set", e.Message);
    }

    private static VectorCase Case() => VectorFile.Load("termstructures/volatility/swaption/cmsmarketcalibration").Cases[0];

    private static void Check(Action action, string paramName, string message)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(action);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }

    private static void Check(Func<object> action, string paramName, string message) => Check(() => { _ = action(); }, paramName, message);

    // The calibration vectors' market on its case's quotes and pricer.
    private static CmsMarket Market(VectorCase c, EvaluationContext context)
    {
        SwapIndex index = SwaptionInputs.SwapIndexOf(c.Input("index"));
        SwaptionVolatilityStructure atm = CubeInputs.AtmOf(c.Input("cube").GetProperty("atm"), context);
        return new CmsMarket(
            CubeInputs.Tenors(c.Input("swapLengths")),
            [index],
            index.IborIndex,
            CubeInputs.Rows(c.Input("bidAskSpreads")),
            [CmsMarketVectorTests.PricerOf(c.Input("pricer"), atm)],
            index.ForwardingTermStructure!,
            context);
    }
}
