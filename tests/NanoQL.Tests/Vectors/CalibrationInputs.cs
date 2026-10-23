// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the swaption helpers, short-rate models and optimization methods that the calibration vector files describe
/// in their inputs, as their generators build them
/// (<c>tools/vectors/gen/models/shortrate/calibrationhelpers/swaptionhelper.py</c>'s and
/// <c>tools/vectors/gen/models/model.py</c>'s functions).
/// </summary>
/// <remarks>
/// <para>
/// A helper is <c>{"form", "maturity", "length", "exerciseDate", "endDate", "volatility", "index", "fixedLegTenor",
/// "fixedLegDayCounter", "floatingLegDayCounter", "termStructure", "errorType", "strike", "nominal", "type", "shift",
/// "settlementDays", "averagingMethod"}</c>, its form <c>"maturity"</c> (a maturity and a length), <c>"exerciseDate"</c>
/// (an exercise date and a length) or <c>"endDate"</c> (an exercise date and an end date) naming the constructor, its
/// index a spec of <see cref="SwapInputs"/>, its term structure a flat forward spec or <c>null</c>, and a null strike or
/// settlement days QuantLib's null.
/// </para>
/// <para>
/// A model is <c>{"type": "HullWhite", "termStructure", "a", "sigma"}</c> or <c>{"type": "Vasicek", "r0", "a", "b",
/// "sigma", "lambda"}</c>; a method <c>{"type": "LevenbergMarquardt", "epsfcn", "xtol", "gtol",
/// "useCostFunctionsJacobian"}</c> or <c>{"type": "Simplex", "lambda"}</c>.
/// </para>
/// </remarks>
internal static class CalibrationInputs
{
    /// <summary>The swaption helper a spec describes, with no pricing engine.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context, which the helper is built under.</param>
    /// <returns>The helper.</returns>
    public static SwaptionHelper SwaptionHelperOf(JsonElement spec, EvaluationContext context)
    {
        double volatility = IndexInputs.Real(spec.GetProperty("volatility"));
        IborIndex index = SwapInputs.IndexOf(spec.GetProperty("index"));
        Period fixedLegTenor = SwapInputs.PeriodOf(spec.GetProperty("fixedLegTenor"));
        DayCounter fixedLegDayCounter = CashFlowInputs.DayCounterOf(spec.GetProperty("fixedLegDayCounter"));
        DayCounter floatingLegDayCounter = CashFlowInputs.DayCounterOf(spec.GetProperty("floatingLegDayCounter"));
        YieldTermStructure termStructure = IndexInputs.CurveOf(spec.GetProperty("termStructure"))!;
        var errorType = (BlackCalibrationHelperCalibrationErrorType)spec.GetProperty("errorType").GetInt32();
        double? strike = CouponInputs.NullableReal(spec.GetProperty("strike"));
        double nominal = IndexInputs.Real(spec.GetProperty("nominal"));
        var type = (VolatilityType)spec.GetProperty("type").GetInt32();
        double shift = IndexInputs.Real(spec.GetProperty("shift"));
        int? settlementDays = CouponInputs.NullableInt(spec.GetProperty("settlementDays"));
        var averagingMethod = (RateAveragingType)spec.GetProperty("averagingMethod").GetInt32();
        return spec.GetProperty("form").GetString() switch
        {
            "maturity" => new SwaptionHelper(
                SwapInputs.PeriodOf(spec.GetProperty("maturity")),
                SwapInputs.PeriodOf(spec.GetProperty("length")),
                volatility,
                index,
                fixedLegTenor,
                fixedLegDayCounter,
                floatingLegDayCounter,
                termStructure,
                context,
                errorType,
                strike,
                nominal,
                type,
                shift,
                settlementDays,
                averagingMethod),
            "exerciseDate" => new SwaptionHelper(
                new Date(spec.GetProperty("exerciseDate").GetInt32()),
                SwapInputs.PeriodOf(spec.GetProperty("length")),
                volatility,
                index,
                fixedLegTenor,
                fixedLegDayCounter,
                floatingLegDayCounter,
                termStructure,
                context,
                errorType,
                strike,
                nominal,
                type,
                shift,
                settlementDays,
                averagingMethod),
            "endDate" => new SwaptionHelper(
                new Date(spec.GetProperty("exerciseDate").GetInt32()),
                new Date(spec.GetProperty("endDate").GetInt32()),
                volatility,
                index,
                fixedLegTenor,
                fixedLegDayCounter,
                floatingLegDayCounter,
                termStructure,
                context,
                errorType,
                strike,
                nominal,
                type,
                shift,
                settlementDays,
                averagingMethod),
            string form => throw new InvalidOperationException($"unknown helper form '{form}'"),
            null => throw new InvalidOperationException("a helper without a form"),
        };
    }

    /// <summary>
    /// The cap helper a spec describes (<c>tools/vectors/gen/models/shortrate/calibrationhelpers/caphelper.py</c>'s
    /// <c>{"length", "volatility", "index", "fixedLegFrequency", "fixedLegDayCounter", "includeFirstSwaplet",
    /// "termStructure", "errorType", "type", "shift"}</c>), with no pricing engine.
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context, which the helper is built under.</param>
    /// <returns>The helper.</returns>
    public static CapHelper CapHelperOf(JsonElement spec, EvaluationContext context) => new(
        SwapInputs.PeriodOf(spec.GetProperty("length")),
        IndexInputs.Real(spec.GetProperty("volatility")),
        SwapInputs.IndexOf(spec.GetProperty("index")),
        (Frequency)spec.GetProperty("fixedLegFrequency").GetInt32(),
        CashFlowInputs.DayCounterOf(spec.GetProperty("fixedLegDayCounter")),
        spec.GetProperty("includeFirstSwaplet").GetBoolean(),
        IndexInputs.CurveOf(spec.GetProperty("termStructure"))!,
        context,
        (BlackCalibrationHelperCalibrationErrorType)spec.GetProperty("errorType").GetInt32(),
        (VolatilityType)spec.GetProperty("type").GetInt32(),
        IndexInputs.Real(spec.GetProperty("shift")));

    /// <summary>A cap helper priced by the analytic cap/floor engine on the affine model it is given.</summary>
    /// <param name="helper">The helper.</param>
    /// <returns>The helper's copy with the engine.</returns>
    public static CapHelper WithAnalytic(CapHelper helper) =>
        helper.SetPricingEngine<OneFactorAffineModel>((cap, c, m) => AnalyticCapFloorEngine.Calculate(cap, c, m));

    /// <summary>A helper priced by the Jamshidian engine on the model it is given.</summary>
    /// <param name="helper">The helper.</param>
    /// <param name="termStructure">The engine's term structure, for a model with no curve of its own, or null.</param>
    /// <returns>The helper's copy with the engine.</returns>
    public static SwaptionHelper WithJamshidian(SwaptionHelper helper, YieldTermStructure? termStructure) =>
        helper.SetPricingEngine<OneFactorAffineModel>((s, c, m) => JamshidianSwaptionEngine.Calculate(s, c, m, termStructure));

    /// <summary>The Hull-White model a spec describes, on the given curve, or on the spec's own.</summary>
    /// <param name="spec">The spec: <c>{"a", "sigma"}</c>, with an optional <c>"termStructure"</c>.</param>
    /// <param name="termStructure">The curve, when the spec has none.</param>
    /// <returns>The model.</returns>
    public static HullWhite HullWhiteOf(JsonElement spec, YieldTermStructure? termStructure = null) => new(
        spec.TryGetProperty("termStructure", out JsonElement ts) ? CashFlowInputs.CurveOf(ts) : termStructure!,
        IndexInputs.Real(spec.GetProperty("a")),
        IndexInputs.Real(spec.GetProperty("sigma")));

    /// <summary>The Vasicek model a spec describes.</summary>
    /// <param name="spec">The spec: <c>{"r0", "a", "b", "sigma", "lambda"}</c>.</param>
    /// <returns>The model.</returns>
    public static Vasicek VasicekOf(JsonElement spec) => new(
        IndexInputs.Real(spec.GetProperty("r0")),
        IndexInputs.Real(spec.GetProperty("a")),
        IndexInputs.Real(spec.GetProperty("b")),
        IndexInputs.Real(spec.GetProperty("sigma")),
        IndexInputs.Real(spec.GetProperty("lambda")));

    /// <summary>
    /// The model a spec describes: a Hull-White, a Vasicek, a Cox-Ingersoll-Ross (<c>{"r0", "theta", "k",
    /// "sigma"}</c>) or an extended Cox-Ingersoll-Ross (<c>{"termStructure", "theta", "k", "sigma", "x0"}</c>), the
    /// last two with the Feller constraint, the only one the wheel builds.
    /// </summary>
    /// <param name="spec">The spec, with its <c>"type"</c>.</param>
    /// <returns>The model.</returns>
    public static OneFactorAffineModel ModelOf(JsonElement spec) => spec.GetProperty("type").GetString() switch
    {
        "HullWhite" => HullWhiteOf(spec),
        "Vasicek" => VasicekOf(spec),
        "CoxIngersollRoss" => new CoxIngersollRoss(
            IndexInputs.Real(spec.GetProperty("r0")),
            IndexInputs.Real(spec.GetProperty("theta")),
            IndexInputs.Real(spec.GetProperty("k")),
            IndexInputs.Real(spec.GetProperty("sigma"))),
        "ExtendedCoxIngersollRoss" => new ExtendedCoxIngersollRoss(
            CashFlowInputs.CurveOf(spec.GetProperty("termStructure")),
            IndexInputs.Real(spec.GetProperty("theta")),
            IndexInputs.Real(spec.GetProperty("k")),
            IndexInputs.Real(spec.GetProperty("sigma")),
            IndexInputs.Real(spec.GetProperty("x0"))),
        string type => throw new InvalidOperationException($"unknown model '{type}'"),
        null => throw new InvalidOperationException("a model without a type"),
    };

    /// <summary>The optimization method a spec describes.</summary>
    /// <param name="spec">The spec, with its <c>"type"</c>.</param>
    /// <returns>The method.</returns>
    public static OptimizationMethod MethodOf(JsonElement spec) => spec.GetProperty("type").GetString() switch
    {
        "LevenbergMarquardt" => new LevenbergMarquardt(
            IndexInputs.Real(spec.GetProperty("epsfcn")),
            IndexInputs.Real(spec.GetProperty("xtol")),
            IndexInputs.Real(spec.GetProperty("gtol")),
            spec.GetProperty("useCostFunctionsJacobian").GetBoolean()),
        "Simplex" => new Simplex(IndexInputs.Real(spec.GetProperty("lambda"))),
        string type => throw new InvalidOperationException($"unknown method '{type}'"),
        null => throw new InvalidOperationException("a method without a type"),
    };
}
