// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The refusals of the CMS pricers, <see cref="MakeCms"/>, the Gauss-Kronrod rules and Newton's solver, by type,
/// message and parameter (playbook §2): QuantLib's texts, a check of an argument an <see cref="ArgumentException"/>
/// naming it, a check of a value already stored a <see cref="NanoQLException"/>.
/// </summary>
public class CmsRefusalTests
{
    private static readonly SwapIndex Index = CmsInputs.SwapIndex(new Period(10, TimeUnit.Years));

    [Fact]
    public void ThePricersRefusals()
    {
        CmsCoupon c = CmsInputs.Coupon(Index);
        var ibor = new IborCoupon(c.Date, 1.0, c.AccrualStartDate, c.AccrualEndDate, 2, CmsInputs.Ibor);
        CmsCouponPricer[] pricers =
        [
            new NumericHaganPricer(CmsInputs.Volatility(), GFunctionFactoryYieldCurveModel.Standard, 0.0),
            new AnalyticHaganPricer(CmsInputs.Volatility(), GFunctionFactoryYieldCurveModel.Standard, 0.0),
            new LinearTsrPricer(CmsInputs.Volatility(), 0.0),
        ];
        foreach (CmsCouponPricer pricer in pricers)
        {
            Check(() => pricer.SwapletRate(ibor, CmsInputs.Context), "coupon", "CMS coupon needed");
            NanoQLException e = Assert.Throws<NanoQLException>(() => pricer.WithSwaptionVolatility(null).SwapletRate(c, CmsInputs.Context));
            Assert.Equal("empty Handle cannot be dereferenced", e.Message);
        }

        // the Hagan pricers' own: the yield-curve model, an empty accrual period, a shifted lognormal volatility
        var model = Assert.Throws<ArgumentOutOfRangeException>(
            () => new NumericHaganPricer(CmsInputs.Volatility(), (GFunctionFactoryYieldCurveModel)4, 0.0));
        Assert.Equal("modelOfYieldCurve", model.ParamName);
        Assert.StartsWith("unknown/illegal gFunction type", model.Message, StringComparison.Ordinal);
        var empty = new CmsCoupon(c.AccrualStartDate, 1.0, c.AccrualStartDate, c.AccrualStartDate, 2, Index);
        Assert.Equal("null accrual period", Assert.Throws<NanoQLException>(() => pricers[0].SwapletRate(empty, CmsInputs.Context)).Message);
        var shifted = new NumericHaganPricer(CmsInputs.Volatility(shift: 0.01), GFunctionFactoryYieldCurveModel.Standard, 0.0);
        Check(() => shifted.SwapletRate(c, CmsInputs.Context), "volatilityStructure", "VanillaOptionPricer: a normal or a zero-shift lognormal volatility is required");
        Check(() => new MarketQuotedOptionPricer(0.03, c.FixingDate(), Index.Tenor, CmsInputs.Volatility(shift: 0.01)), "volatilityStructure", "VanillaOptionPricer: a normal or a zero-shift lognormal volatility is required");

        // the TSR pricer's own: its volatility at construction, the settings' strategy
        Assert.Equal("swaptionVol", Assert.Throws<ArgumentNullException>(() => new LinearTsrPricer(null!, 0.0)).ParamName);
        var strategy = Assert.Throws<ArgumentOutOfRangeException>(() => new LinearTsrPricer(
            CmsInputs.Volatility(), 0.0, settings: new LinearTsrPricerSettings { Strategy = (LinearTsrPricerSettingsStrategy)9 }));
        Assert.Equal("settings", strategy.ParamName);
        Assert.StartsWith("Unknown strategy (9)", strategy.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MakeCmsRefusals()
    {
        Assert.Equal("swapIndex", Assert.Throws<ArgumentNullException>(() => new MakeCms(new Period(10, TimeUnit.Years), null!)).ParamName);
        var pricer = new AnalyticHaganPricer(CmsInputs.Volatility(), GFunctionFactoryYieldCurveModel.Standard, 0.0);
        var tenor = new Period(5, TimeUnit.Years);
        NanoQLException noPricer = Assert.Throws<NanoQLException>(() => new MakeCms(tenor, Index).WithAtmSpread().ToSwap(CmsInputs.Context));
        Assert.Equal("no CmsCouponPricer set (yet)", noPricer.Message);

        // the ATM spread prices both legs on their indexes' curves
        var noCurve = new SwapIndex(
            "EuriborSwapIsdaFixA",
            new Period(10, TimeUnit.Years),
            2,
            Index.Currency,
            new TARGET(),
            new Period(1, TimeUnit.Years),
            BusinessDayConvention.ModifiedFollowing,
            new Thirty360(Thirty360Convention.BondBasis),
            new Euribor6M());
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => new MakeCms(tenor, noCurve).WithCmsCouponPricer(pricer).WithAtmSpread().ToSwap(CmsInputs.Context));
        Assert.Equal("null term structure set to this instance of Euribor6M Actual/360", e.Message);
        e = Assert.Throws<NanoQLException>(
            () => new MakeCms(tenor, noCurve, CmsInputs.Ibor).WithCmsCouponPricer(pricer).WithAtmSpread().ToSwap(CmsInputs.Context));
        Assert.Equal($"null term structure set to this instance of {noCurve.Name}", e.Message);
    }

    [Fact]
    public void TheRulesAndTheSolversRefusals()
    {
        Check(() => new GaussKronrodAdaptive(1e-6, 14), "maxFunctionEvaluations", "required maxEvaluations (14) not allowed. It must be >= 15");
        _ = new GaussKronrodAdaptive(1e-6, 15);
        NanoQLException adaptive = Assert.Throws<NanoQLException>(() => new GaussKronrodAdaptive(1e-12, 15).Integrate(Math.Sqrt, 0.0, 1.0));
        Assert.Equal("maximum number of function evaluations exceeded", adaptive.Message);

        // a NaN bound reaches the non-adaptive rule's own check, whose text QuantLib closes with a stray parenthesis
        var rule = new GaussKronrodNonAdaptive(1e-6, 100, 1e-6);
        Check(() => rule.Integrate(Math.Exp, 0.0, double.NaN), "b", "b must be greater than a)");

        // Newton's iteration from 10 towards ln 2 moves one unit an evaluation
        NanoQLException newton = Assert.Throws<NanoQLException>(
            () => new Newton().SetMaxEvaluations(4).Solve(x => Math.Exp(x) - 2.0, Math.Exp, 1e-10, 10.0, 0.0, 20.0));
        Assert.Equal("maximum number of function evaluations (4) exceeded", newton.Message);
    }

    private static void Check(Action action, string paramName, string message)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(action);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }

    private static void Check(Func<object> action, string paramName, string message) => Check(() => { _ = action(); }, paramName, message);
}
