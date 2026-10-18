// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the rate helpers that the bootstrap vector files describe in their inputs, as their generators build them
/// (<c>tools/vectors/gen/termstructures/yield/ratehelpers.py</c>'s functions), and checks a helper's five dates.
/// </summary>
/// <remarks>
/// A helper is <c>{"kind": "DepositRateHelper", "form": "tenor", "rate", "tenor", "fixingDays", "calendar",
/// "convention", "endOfMonth", "dayCounter"}</c>, <c>{"kind": "DepositRateHelper", "form": "index", "rate",
/// "index"}</c>, <c>{"kind": "DepositRateHelper", "form": "fixingDate", "rate", "fixingDate", "index"}</c>, or
/// <c>{"kind": "SwapRateHelper", "form": "tenor", "rate", "tenor", "calendar", "fixedFrequency", "fixedConvention",
/// "fixedDayCount", "index", "spread", "fwdStart", "discountingCurve", "settlementDays", "pillar", "customPillarDate",
/// "endOfMonth", "useIndexedCoupons", "floatConvention"}</c>, the same with <c>"form": "dates"</c>, <c>"startDate"</c>
/// and <c>"endDate"</c> for <c>"tenor"</c> and without <c>"fwdStart"</c> and <c>"settlementDays"</c>; the C++
/// harness's files may add a <c>"couponPricer"</c>, a pricer spec of <see cref="CouponInputs"/>. A period is
/// <c>[length, units]</c>; a calendar and a day counter are specs of <see cref="CashFlowInputs"/>, <c>null</c> for
/// QuantLib's empty one; an index is a spec of <see cref="SwapInputs"/>; the discounting curve is <c>null</c> or a flat
/// forward spec. A null settlement-day count, custom pillar date, coupon choice or floating convention is the
/// constructor's default; enums are QuantLib's integers.
/// </remarks>
internal static class RateHelperInputs
{
    /// <summary>The helper a spec describes, built in a context.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The evaluation context the helper is built in.</param>
    /// <returns>The helper.</returns>
    public static BootstrapHelper<YieldTermStructure> HelperOf(JsonElement spec, EvaluationContext context)
    {
        string kind = spec.GetProperty("kind").GetString()!;
        string form = spec.GetProperty("form").GetString()!;
        double rate = spec.GetProperty("rate").GetDouble();
        switch (kind, form)
        {
            case ("DepositRateHelper", "tenor"):
                return new DepositRateHelper(
                    rate,
                    SwapInputs.PeriodOf(spec.GetProperty("tenor")),
                    spec.GetProperty("fixingDays").GetInt32(),
                    CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
                    (BusinessDayConvention)spec.GetProperty("convention").GetInt32(),
                    spec.GetProperty("endOfMonth").GetBoolean(),
                    CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
                    context);
            case ("DepositRateHelper", "index"):
                return new DepositRateHelper(rate, SwapInputs.IndexOf(spec.GetProperty("index")), context);
            case ("DepositRateHelper", "fixingDate"):
                return new DepositRateHelper(
                    rate,
                    new Date(spec.GetProperty("fixingDate").GetInt32()),
                    SwapInputs.IndexOf(spec.GetProperty("index")));
            case ("SwapRateHelper", "tenor"):
                return new SwapRateHelper(
                    rate,
                    SwapInputs.PeriodOf(spec.GetProperty("tenor")),
                    CalendarOrNull(spec.GetProperty("calendar")),
                    (Frequency)spec.GetProperty("fixedFrequency").GetInt32(),
                    (BusinessDayConvention)spec.GetProperty("fixedConvention").GetInt32(),
                    CouponInputs.DayCounterOrNull(spec.GetProperty("fixedDayCount")),
                    SwapInputs.IndexOf(spec.GetProperty("index")),
                    context,
                    spec.GetProperty("spread").GetDouble(),
                    SwapInputs.PeriodOf(spec.GetProperty("fwdStart")),
                    IndexInputs.CurveOf(spec.GetProperty("discountingCurve")),
                    CouponInputs.NullableInt(spec.GetProperty("settlementDays")),
                    (PillarChoice)spec.GetProperty("pillar").GetInt32(),
                    CashFlowInputs.NullableDate(spec.GetProperty("customPillarDate")),
                    spec.GetProperty("endOfMonth").GetBoolean(),
                    CouponInputs.NullableBool(spec.GetProperty("useIndexedCoupons")),
                    FloatConvention(spec),
                    PricerOf(spec, context));
            case ("SwapRateHelper", "dates"):
                return new SwapRateHelper(
                    rate,
                    new Date(spec.GetProperty("startDate").GetInt32()),
                    new Date(spec.GetProperty("endDate").GetInt32()),
                    CalendarOrNull(spec.GetProperty("calendar")),
                    (Frequency)spec.GetProperty("fixedFrequency").GetInt32(),
                    (BusinessDayConvention)spec.GetProperty("fixedConvention").GetInt32(),
                    CouponInputs.DayCounterOrNull(spec.GetProperty("fixedDayCount")),
                    SwapInputs.IndexOf(spec.GetProperty("index")),
                    context,
                    spec.GetProperty("spread").GetDouble(),
                    IndexInputs.CurveOf(spec.GetProperty("discountingCurve")),
                    (PillarChoice)spec.GetProperty("pillar").GetInt32(),
                    CashFlowInputs.NullableDate(spec.GetProperty("customPillarDate")),
                    spec.GetProperty("endOfMonth").GetBoolean(),
                    CouponInputs.NullableBool(spec.GetProperty("useIndexedCoupons")),
                    FloatConvention(spec),
                    PricerOf(spec, context));
            default:
                throw new InvalidOperationException($"unknown helper {kind} ({form})");
        }
    }

    /// <summary>The helpers a list of specs describes, each built in the context.</summary>
    /// <param name="specs">The specs.</param>
    /// <param name="context">The evaluation context the helpers are built in.</param>
    /// <returns>The helpers, in order.</returns>
    public static BootstrapHelper<YieldTermStructure>[] HelpersOf(JsonElement specs, EvaluationContext context) =>
        [.. specs.EnumerateArray().Select(s => HelperOf(s, context))];

    /// <summary>Compares a helper's five dates with the case's outputs of the same names.</summary>
    /// <param name="check">The file's comparison.</param>
    /// <param name="c">The case.</param>
    /// <param name="h">The helper.</param>
    public static void CheckDates(VectorAssert check, VectorCase c, BootstrapHelper<YieldTermStructure> h)
    {
        check.Output(c, "earliestDate", h.EarliestDate);
        check.Output(c, "maturityDate", h.MaturityDate);
        check.Output(c, "latestRelevantDate", h.LatestRelevantDate);
        check.Output(c, "pillarDate", h.PillarDate);
        check.Output(c, "latestDate", h.LatestDate);
    }

    private static Calendar? CalendarOrNull(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CalendarOf(spec);

    private static BusinessDayConvention? FloatConvention(JsonElement spec) =>
        CouponInputs.NullableInt(spec.GetProperty("floatConvention")) is int c ? (BusinessDayConvention)c : null;

    private static FloatingRateCouponPricer? PricerOf(JsonElement spec, EvaluationContext context) =>
        spec.TryGetProperty("couponPricer", out JsonElement pricer) ? CouponInputs.PricerOf(pricer, context) : null;
}
