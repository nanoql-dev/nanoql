// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Integration;
using static NanoQL.Excel.Tests.Xl;

namespace NanoQL.Excel.Tests;

/// <summary>
/// S-bind's C suite's bound-engine and calibration cases (bindings/c/tests/conformance.c's test_engines and
/// test_calibrations, XLP39) through the generated worksheet functions of the experimental tests' build, on S-bind's
/// test types (prefix nqlt), each with Excel's values: handles as text, enums by name, a NULL argument a missing one;
/// each result against the test types' C# references, bit for bit, a refusal #VALUE! (XLR10).
/// </summary>
[Collection(TestSettings.Name)]
public sealed class BoundEngineCases
{
    /// <summary>Initializes a new instance of the <see cref="BoundEngineCases"/> class, on a clean runtime.</summary>
    public BoundEngineCases() => Reset();

    /// <summary>test_engines: the swap, Heston, quanto, YoY, callable bond and Vanna-Volga cases.</summary>
    [Fact]
    public void Engines()
    {
        string ctx = Fx("Context"), rf = Fx("RiskFree");
        string swapEngine = H(C("nql_DiscountingSwapEngine_Bind", rf, null, null, null)), builder = Fx("SwapBuilder");
        string swap = H(C("nql_MakeVanillaSwap_ToVanillaSwap", H(C("nql_MakeVanillaSwap_WithPricingEngine", builder, swapEngine)), ctx));
        Same(C("nql_FixedVsFloatingSwap_get_FixedRate", swap), C("nqlt_References_SwapFixedRate"));
        string results = H(C("nql_DiscountingSwapEngine_Calculate__FixedVsFloatingSwap", swap, ctx, rf, null, null, null));
        Assert.True(Math.Abs(Npv(results)) < 1e-6);

        string heston = Fx("Heston"), hestonEngine = H(C("nql_AnalyticHestonEngine_Bind__HestonModel_int", null, null));
        Refused(C("nql_MakeVanillaSwap_WithPricingEngine", builder, hestonEngine));
        string helper = Fx("HestonHelper");
        string priced = H(C("nql_HestonModelHelper_SetPricingEngine", helper, hestonEngine));
        Same(C("nql_HestonModelHelper_ModelValue", priced, heston, ctx), C("nqlt_References_HestonModelValue"));
        Refused(C("nql_HestonModelHelper_SetPricingEngine", helper, H(C("nql_AnalyticHestonEngine_Bind__HestonModel_int", heston, null))));
        Refused(C("nql_HestonModelHelper_SetPricingEngine", helper, swapEngine));
        string hw = Fx("HullWhite");
        string h1hw = H(C("nql_AnalyticH1HWEngine_Bind__HestonModel_HullWhite_double_int", null, hw, 0.6, null));
        Same(C("nql_HestonModelHelper_ModelValue", H(C("nql_HestonModelHelper_SetPricingEngine", helper, h1hw)), heston, ctx), C("nqlt_References_H1HWModelValue"));
        string hhw = H(C("nql_AnalyticHestonHullWhiteEngine_Bind__HestonModel_HullWhite_int", null, hw, null));
        Same(C("nql_HestonModelHelper_ModelValue", H(C("nql_HestonModelHelper_SetPricingEngine", helper, hhw)), heston, ctx), C("nqlt_References_HestonHullWhiteModelValue"));
        Refused(C("nql_HestonModelHelper_SetPricingEngine", helper, H(C("nql_AnalyticH1HWEngine_Bind__HestonModel_HullWhite_double_int", null, null, 0.6, null))));

        string european = H(C("nql_AnalyticEuropeanEngine_Bind__GeneralizedBlackScholesProcess", (object?)null));
        string quanto = Fx("Quanto"), bsm = Fx("BlackScholes"), foreign = Fx("Foreign"), fxvol = Fx("ExchangeVolatility");
        Same(C("nql_InstrumentResults_get_NPV", H(C("nql_QuantoEngine_Calculate__QuantoVanillaOption", quanto, ctx, bsm, foreign, fxvol, 0.3, european))), C("nqlt_References_QuantoNpv"));

        // S-bind2's Monte Carlo-shaped engine: its nullable inputs neither bound nor supplied null (B2R4), its shape an
        // array of pairs, a range a row per element (XLR17), its required seed left open refused.
        string mc = H(C("nqlt_McShapedEngine_Bind", null, 4, null, 2000, null, 42, null));
        Same(C("nql_InstrumentResults_get_NPV", H(C("nql_QuantoEngine_Calculate__QuantoVanillaOption", quanto, ctx, bsm, foreign, fxvol, 0.3, mc))), C("nqlt_References_McShapedQuantoNpv", false));
        string shaped = H(C("nqlt_McShapedEngine_Bind", null, 4, null, 2000, null, 42, new object[,] { { 0.5, 1.5 }, { 1.0, 0.5 } }));
        Same(C("nql_InstrumentResults_get_NPV", H(C("nql_QuantoEngine_Calculate__QuantoVanillaOption", quanto, ctx, bsm, foreign, fxvol, 0.3, shaped))), C("nqlt_References_McShapedQuantoNpv", true));
        Refused(C("nql_QuantoEngine_Calculate__QuantoVanillaOption", quanto, ctx, bsm, foreign, fxvol, 0.3, H(C("nqlt_McShapedEngine_Bind", null, 4, null, 2000, null, null, null))));

        string market = H(C("nqlt_YoYMarket_get_Instance"));
        string yctx = H(C("nqlt_YoYMarket_get_Context", market)), index = H(C("nqlt_YoYMarket_get_Index", market));
        string yoy = H(C("nql_YoYInflationUnitDisplacedBlackCapFloorEngine_Bind", index, null, H(C("nqlt_YoYMarket_get_Nominal", market))));
        string dc = H(C("nql_ConventionSet_DayCounter", null, "Actual365Fixed")), target = H(C("nql_ConventionSet_Calendar", null, "TARGET"));
        string yhelper = H(C("nql_YoYOptionletHelper_new", 1.0, 10000.0, "Cap", "3M", dc, target, 0, index, "Flat", 0.03, 3, yoy, yctx));
        string ytrial = H(C("nql_YoYOptionletHelper_SetTermStructure", yhelper, H(C("nqlt_YoYMarket_get_Surface", market))));
        Same(C("nql_YoYOptionletHelper_ImpliedQuote", ytrial, yctx), C("nqlt_References_YoYImpliedQuote"));

        string bond = Fx("CallableBond"), bctx = Fx("BondContext"), bcurve = Fx("BondCurve"), bmodel = Fx("BondModel");
        string tree = H(C("nql_TreeCallableFixedRateBondEngine_Bind__ShortRateModel_int", bmodel, 40, null, null));
        double price = D(C("nqlt_References_OasPrice"));
        Same(C("nql_CallableBond_OAS", bond, price, bcurve, dc, "Compounded", "Semiannual", tree, bctx, null, 1e-10, 100, 0.0), C("nqlt_References_Oas"));
        Same(C("nql_CallableBond_CleanPriceOAS", bond, 0.01, bcurve, dc, "Compounded", "Semiannual", tree, bctx, null), C("nqlt_References_CleanPriceOas"));
        Same(C("nql_CallableBond_EffectiveDuration", bond, 0.01, bcurve, dc, "Compounded", "Semiannual", tree, bctx, 0.0002), C("nqlt_References_EffectiveDuration"));
        Refused(C("nql_CallableBond_OAS", bond, price, bcurve, dc, "Compounded", "Semiannual", H(C("nql_BlackCallableFixedRateBondEngine_Bind__double", 0.2, bcurve)), bctx, null, 1e-10, 100, 0.0));
        Refused(C("nql_CallableBond_OAS", bond, price, bcurve, dc, "Compounded", "Semiannual", H(C("nql_BlackCallableFixedRateBondEngine_Bind__double", null, bcurve)), bctx, null, 1e-10, 100, 0.0));

        string barrier = H(C("nql_AnalyticDoubleBarrierEngine_Bind", null, null));
        string vv = H(C("nql_VannaVolgaDoubleBarrierEngine_Calculate", Fx("DoubleBarrier"), ctx, barrier, Fx("AtmQuote"), Fx("Put25"), Fx("Call25"), 100.0, Fx("FxCurve"), Fx("FxCurve"), false, 0.0, 5));
        Same(C("nql_InstrumentResults_get_NPV", vv), C("nqlt_References_VannaVolgaNpv"));
    }

    /// <summary>test_calibrations: Andreasen-Huge from a set of pairs, GSR's and Hull-White's calibrations to C#'s parameters.</summary>
    [Fact]
    public void Calibrations()
    {
        // The calibration set read back as tuples: a range, a row per element, the option's handle and its volatility.
        var set = Assert.IsType<object[,]>(C("nqlt_Fixtures_get_CalibrationSet"));
        Assert.Equal((9, 2), (set.GetLength(0), set.GetLength(1)));
        string rf = Fx("RiskFree"), dividend = Fx("Dividend"), bfgs = H(C("nql_BFGS_new"));
        string ah = H(C("nql_AndreasenHugeVolatilityInterpl_new", set, 100.0, rf, dividend, "CubicSpline", "Call", 100, null, null, null, null));
        string ahb = H(C("nql_AndreasenHugeVolatilityInterpl_new", set, 100.0, rf, dividend, "CubicSpline", "Call", 100, null, null, bfgs, null));
        Same(C("nql_AndreasenHugeVolatilityInterpl_LocalVol", ah, 1.0, 110.0), C("nqlt_References_AndreasenHugeLocalVol", false));
        Same(C("nql_AndreasenHugeVolatilityInterpl_LocalVol", ahb, 1.0, 110.0), C("nqlt_References_AndreasenHugeLocalVol", true));

        string cctx = Fx("CalibrationContext"), gsr = Fx("Gsr"), hw = Fx("CalibrationHullWhite");
        string g1d = H(C("nql_Gaussian1dSwaptionEngine_Bind", null, null, null, null, null, null, null)), capEngine = H(C("nql_AnalyticCapFloorEngine_Bind", null, null));
        var swaptions = Assert.IsType<object[,]>(C("nqlt_Fixtures_get_Swaptions"));
        string[] priced = [.. Enumerable.Range(0, 3).Select(k => H(C("nql_SwaptionHelper_SetPricingEngine", swaptions[k, 0], g1d)))];
        Same(C("nql_SwaptionHelper_ModelValue", priced[1], gsr, cctx), C("nqlt_References_GsrSwaptionModelValue"));
        Refused(C("nql_SwaptionHelper_ModelValue", priced[0], hw, cctx));
        var caps = Assert.IsType<object[,]>(C("nqlt_Fixtures_get_Caps"));
        string[] capped = [.. Enumerable.Range(0, 3).Select(k => H(C("nql_CapHelper_SetPricingEngine", caps[k, 0], capEngine)))];
        Same(C("nql_CapHelper_ModelValue", capped[1], hw, cctx), C("nqlt_References_HullWhiteCapModelValue"));
        Refused(C("nql_CapHelper_ModelValue", capped[0], gsr, cctx));

        string method = Fx("Method"), criteria = Fx("Criteria");
        string gres = H(C("nql_Gsr_CalibrateVolatilitiesIterative", gsr, Col(priced), method, criteria, cctx, null, null));
        AllSame(C("nql_Gsr_Params", H(C("nql_CalibrationResults_Gsr_get_Model", gres))), C("nqlt_References_GsrVolatilities"));
        string hres = H(C("nql_CalibratedModelExtensions_Calibrate", hw, Col(capped), method, criteria, cctx, null, null, null));
        AllSame(C("nql_CalibratedModel_Params", H(C("nql_CalibrationResults_ICalibratedModel_get_Model", hres))), C("nqlt_References_HullWhiteCaps"));
    }

    private static string Fx(string name) => H(C("nqlt_Fixtures_get_" + name));

    private static double Npv(string results) => D(C("nql_InstrumentResults_get_NPV", results));

    private static void Same(object got, object want) => Assert.Equal(BitConverter.DoubleToInt64Bits(D(want)), BitConverter.DoubleToInt64Bits(D(got)));

    private static void Refused(object r) => Assert.Equal(ExcelError.ExcelErrorValue, Assert.IsType<ExcelError>(r));

    private static void AllSame(object got, object want)
    {
        var g = Assert.IsType<object[,]>(got);
        var w = Assert.IsType<object[,]>(want);
        Assert.True(g.Length > 0);
        Assert.Equal(w.Cast<object>().Select(x => BitConverter.DoubleToInt64Bits((double)x)), g.Cast<object>().Select(x => BitConverter.DoubleToInt64Bits((double)x)));
    }
}
