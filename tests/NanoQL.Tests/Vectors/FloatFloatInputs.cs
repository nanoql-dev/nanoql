// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The float-float swaps and swaptions the vector generators describe: a swap is
/// <c>tools/vectors/gen/instruments/floatfloatswap.py</c>'s spec, its schedules <see cref="SwapInputs"/>', an index
/// <c>{"ibor": …}</c> or <c>{"swap": …}</c>; a swaption wraps one with an exercise of <see cref="NonstandardInputs"/>.
/// </summary>
internal static class FloatFloatInputs
{
    /// <summary>The float-float swap a spec describes, by its per-coupon constructor.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context, which the legs read.</param>
    /// <returns>The swap.</returns>
    public static FloatFloatSwap SwapOf(JsonElement spec, EvaluationContext context) => new(
        (SwapType)spec.GetProperty("type").GetInt32(),
        Reals(spec.GetProperty("nominal1")),
        Reals(spec.GetProperty("nominal2")),
        SwapInputs.ScheduleOf(spec.GetProperty("schedule1"), context),
        IndexOf(spec.GetProperty("index1")),
        CashFlowInputs.DayCounterOf(spec.GetProperty("dayCount1")),
        SwapInputs.ScheduleOf(spec.GetProperty("schedule2"), context),
        IndexOf(spec.GetProperty("index2")),
        CashFlowInputs.DayCounterOf(spec.GetProperty("dayCount2")),
        context,
        spec.GetProperty("intermediateCapitalExchange").GetBoolean(),
        spec.GetProperty("finalCapitalExchange").GetBoolean(),
        Reals(spec.GetProperty("gearing1")),
        Reals(spec.GetProperty("spread1")),
        NullableReals(spec.GetProperty("cappedRate1")),
        NullableReals(spec.GetProperty("flooredRate1")),
        Reals(spec.GetProperty("gearing2")),
        Reals(spec.GetProperty("spread2")),
        NullableReals(spec.GetProperty("cappedRate2")),
        NullableReals(spec.GetProperty("flooredRate2")),
        (BusinessDayConvention)spec.GetProperty("paymentConvention1").GetInt32(),
        (BusinessDayConvention)spec.GetProperty("paymentConvention2").GetInt32());

    /// <summary>The float-float swaption a spec describes: <c>{"swap", "exercise", "settlementType",
    /// "settlementMethod"}</c>.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The swaption.</returns>
    public static FloatFloatSwaption SwaptionOf(JsonElement spec, EvaluationContext context) => new(
        SwapOf(spec.GetProperty("swap"), context),
        NonstandardInputs.ExerciseOf(spec.GetProperty("exercise")),
        (SettlementType)spec.GetProperty("settlementType").GetInt32(),
        (SettlementMethod)spec.GetProperty("settlementMethod").GetInt32());

    private static InterestRateIndex IndexOf(JsonElement spec) =>
        spec.TryGetProperty("ibor", out JsonElement ibor)
            ? SwapInputs.IndexOf(ibor)
            : SwapIndexInputs.SwapIndexOf(spec.GetProperty("swap"));

    private static double[] Reals(JsonElement e) => [.. e.EnumerateArray().Select(x => x.GetDouble())];

    private static double?[] NullableReals(JsonElement e) =>
        [.. e.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.Null ? (double?)null : x.GetDouble())];
}
