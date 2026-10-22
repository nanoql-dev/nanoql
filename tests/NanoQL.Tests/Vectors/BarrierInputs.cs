// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The barrier vectors' specs, as <c>tools/vectors/gen/pricingengines/barrier/analyticbarrierengine.py</c> writes
/// them: a barrier {"barrierType" (QuantLib's integer), "barrier", "rebate"}, with a payoff and an exercise as
/// <see cref="EquityInputs"/> reads them.
/// </summary>
internal static class BarrierInputs
{
    /// <summary>The barrier option a barrier spec, a payoff spec and an exercise spec describe.</summary>
    /// <param name="barrier">The barrier spec.</param>
    /// <param name="payoff">The payoff spec.</param>
    /// <param name="exercise">The exercise spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The option.</returns>
    public static BarrierOption OptionOf(
        JsonElement barrier, JsonElement payoff, JsonElement exercise, EvaluationContext context) =>
        new(
            (BarrierType)barrier.GetProperty("barrierType").GetInt32(),
            barrier.GetProperty("barrier").GetDouble(),
            barrier.GetProperty("rebate").GetDouble(),
            PayoffOf(payoff),
            ExerciseOf(exercise, context));

    /// <summary>
    /// Prices a pricing spec of tools/vectors/gen/pricingengines/barrier/fdblackscholesbarrierengine.py with
    /// <see cref="FdBlackScholesBarrierEngine"/> or <see cref="FdBlackScholesRebateEngine"/>.
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <param name="rebate">Whether the engine is the rebate engine.</param>
    /// <returns>The results.</returns>
    public static OneAssetOptionResults FdBarrierPrice(JsonElement spec, EvaluationContext context, bool rebate)
    {
        BarrierOption option = OptionOf(
            spec.GetProperty("barrier"), spec.GetProperty("payoff"), spec.GetProperty("exercise"), context);
        var process = ProcessOf(spec.GetProperty("process"), context);
        var dividends = DividendsOf(spec.GetProperty("dividends"));
        int tGrid = spec.GetProperty("tGrid").GetInt32(), xGrid = spec.GetProperty("xGrid").GetInt32();
        int dampingSteps = spec.GetProperty("dampingSteps").GetInt32();
        var scheme = FdInputs.SchemeOf(spec.GetProperty("scheme"));
        bool localVol = spec.GetProperty("localVol").GetBoolean();
        double? overwrite = CouponInputs.NullableReal(spec.GetProperty("illegalLocalVolOverwrite"));
        return rebate
            ? FdBlackScholesRebateEngine.Calculate(
                option, context, process, dividends, tGrid, xGrid, dampingSteps, scheme, localVol, overwrite)
            : FdBlackScholesBarrierEngine.Calculate(
                option, context, process, dividends, tGrid, xGrid, dampingSteps, scheme, localVol, overwrite);
    }

    /// <summary>Compares a finite-difference pricing case: the NPV, delta, gamma and theta, or the refusal.</summary>
    /// <param name="check">The file's assertion.</param>
    /// <param name="c">The case.</param>
    /// <param name="rebate">Whether the engine is the rebate engine.</param>
    public static void CheckFdBarrierPricing(VectorAssert check, VectorCase c, bool rebate)
    {
        EvaluationContext context = c.Context();
        var priced = new Lazy<OneAssetOptionResults>(() => FdBarrierPrice(c.Input("pricing"), context, rebate));
        (string Name, Func<OneAssetOptionResults, double?> Get)[] results =
        [
            ("NPV", r => r.NPV), ("delta", r => r.Delta), ("gamma", r => r.Gamma), ("theta", r => r.Theta),
        ];
        foreach ((string name, Func<OneAssetOptionResults, double?> get) in results)
        {
            check.Output(c, name, () => get(priced.Value));
        }
    }
}
