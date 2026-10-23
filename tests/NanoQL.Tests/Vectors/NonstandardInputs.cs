// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The nonstandard swaps, swaptions and rebated exercises the vector generators describe: a swap is
/// <c>tools/vectors/gen/instruments/nonstandardswap.py</c>'s <c>nonstandard</c> spec, its schedules and index
/// <see cref="SwapInputs"/>'; a rebated exercise wraps an exercise spec of <see cref="OptionInputs"/> with its rebates.
/// </summary>
internal static class NonstandardInputs
{
    /// <summary>The nonstandard swap a spec describes, by its per-coupon constructor.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context, which an Ibor leg reads.</param>
    /// <returns>The swap.</returns>
    public static NonstandardSwap SwapOf(JsonElement spec, EvaluationContext context)
    {
        JsonElement calendar = spec.GetProperty("paymentCalendar");
        return new NonstandardSwap(
            (SwapType)spec.GetProperty("type").GetInt32(),
            CouponInputs.Reals(spec.GetProperty("fixedNominal")),
            CouponInputs.Reals(spec.GetProperty("floatingNominal")),
            SwapInputs.ScheduleOf(spec.GetProperty("fixedSchedule"), context),
            CouponInputs.Reals(spec.GetProperty("fixedRate")),
            CouponInputs.DayCounterOrNull(spec.GetProperty("fixedDayCount"))!,
            SwapInputs.ScheduleOf(spec.GetProperty("floatingSchedule"), context),
            SwapInputs.IndexOf(spec.GetProperty("index")),
            CouponInputs.Reals(spec.GetProperty("gearing")),
            CouponInputs.Reals(spec.GetProperty("spread")),
            CouponInputs.DayCounterOrNull(spec.GetProperty("floatingDayCount")),
            context,
            spec.GetProperty("intermediateCapitalExchange").GetBoolean(),
            spec.GetProperty("finalCapitalExchange").GetBoolean(),
            (BusinessDayConvention)spec.GetProperty("paymentConvention").GetInt32(),
            spec.GetProperty("paymentLag").GetInt32(),
            calendar.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CalendarOf(calendar));
    }

    /// <summary>The exercise a spec describes: an exercise spec, or a rebated exercise around one.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The exercise.</returns>
    public static Exercise ExerciseOf(JsonElement spec)
    {
        if (spec.GetProperty("type").GetString() != "RebatedExercise")
        {
            return OptionInputs.ExerciseOf(spec);
        }

        Exercise exercise = OptionInputs.ExerciseOf(spec.GetProperty("exercise"));
        int days = spec.GetProperty("rebateSettlementDays").GetInt32();
        Calendar calendar = CashFlowInputs.CalendarOf(spec.GetProperty("rebatePaymentCalendar"));
        var convention = (BusinessDayConvention)spec.GetProperty("rebatePaymentConvention").GetInt32();
        return spec.TryGetProperty("rebates", out JsonElement rebates)
            ? new RebatedExercise(exercise, CouponInputs.Reals(rebates), days, calendar, convention)
            : new RebatedExercise(exercise, IndexInputs.Real(spec.GetProperty("rebate")), days, calendar, convention);
    }

    /// <summary>The nonstandard swaption a spec describes: <c>{"swap", "exercise", "settlementType",
    /// "settlementMethod"}</c>.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The swaption.</returns>
    public static NonstandardSwaption SwaptionOf(JsonElement spec, EvaluationContext context) => new(
        SwapOf(spec.GetProperty("swap"), context),
        ExerciseOf(spec.GetProperty("exercise")),
        (SettlementType)spec.GetProperty("settlementType").GetInt32(),
        (SettlementMethod)spec.GetProperty("settlementMethod").GetInt32());
}
