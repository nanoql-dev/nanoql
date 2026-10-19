// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
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
/// harness's files may add a <c>"couponPricer"</c>, a pricer spec of <see cref="CouponInputs"/>. An FRA, futures, FX
/// swap or OIS helper is <c>{"kind": "FraRateHelper", "form": "months" | "periodLength" | "monthsIndex" | "periodIndex"
/// | "imm" | "dates", …}</c>, <c>{"kind": "FuturesRateHelper", "form": "length" | "dates" | "index", …}</c>,
/// <c>{"kind": "FxSwapRateHelper", "form": "tenor" | "dates", …}</c> or <c>{"kind": "OISRateHelper", "form": "tenor" |
/// "dates", …}</c>, with the inputs of the constructor its form names, under QuantLib's parameter names, the quote
/// always <c>"rate"</c> (a futures price, an FX swap's forward points); the generator's docstring lists them. A futures
/// spec's <c>"quotes"</c>, the harness's choice of QuantLib's quote or value forms, collapses here. A swap helper on a
/// swap index is <c>{"kind": "SwapRateHelper", "form": "swapIndex", "rate", "swapIndex", "spread", "fwdStart",
/// "discountingCurve", "pillar", "customPillarDate", "endOfMonth", "useIndexedCoupons"}</c>, its swap index a spec of
/// <see cref="SwapIndexInputs"/>; a BMA swap helper is <c>{"kind": "BMASwapRateHelper", "form": "tenor", "rate",
/// "tenor", "settlementDays", "calendar", "bmaPeriod", "bmaConvention", "bmaDayCount", "index"}</c>, on a
/// <see cref="BMAIndex"/> with no curve. A period is
/// <c>[length, units]</c>; a calendar and a day counter are specs of <see cref="CashFlowInputs"/>, <c>null</c> for
/// QuantLib's empty one; an index is a spec of <see cref="SwapInputs"/>; the discounting curve is <c>null</c>, a flat
/// forward spec, or <c>{"handle": name}</c>, a curve the caller names (a multicurve's member). A null settlement-day
/// count, custom pillar date, coupon choice or floating convention is the constructor's default; enums are QuantLib's
/// integers.
/// </remarks>
internal static class RateHelperInputs
{
    /// <summary>The helper a spec describes, built in a context.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The evaluation context the helper is built in.</param>
    /// <param name="handle">The curve a <c>{"handle": name}</c> discounting curve names.</param>
    /// <returns>The helper.</returns>
    public static BootstrapHelper<YieldTermStructure> HelperOf(
        JsonElement spec, EvaluationContext context, Func<string, YieldTermStructure>? handle = null)
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
                    DiscountingCurveOf(spec.GetProperty("discountingCurve"), handle),
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
                    DiscountingCurveOf(spec.GetProperty("discountingCurve"), handle),
                    (PillarChoice)spec.GetProperty("pillar").GetInt32(),
                    CashFlowInputs.NullableDate(spec.GetProperty("customPillarDate")),
                    spec.GetProperty("endOfMonth").GetBoolean(),
                    CouponInputs.NullableBool(spec.GetProperty("useIndexedCoupons")),
                    FloatConvention(spec),
                    PricerOf(spec, context));
            case ("FraRateHelper", _):
                return FraOf(spec, form, rate, context);
            case ("FuturesRateHelper", _):
                return FuturesOf(spec, form, rate);
            case ("FxSwapRateHelper", _):
                return FxSwapOf(spec, form, rate, context);
            case ("OISRateHelper", _):
                return OisOf(spec, form, rate, context);
            case ("SwapRateHelper", "swapIndex"):
                return new SwapRateHelper(
                    rate,
                    SwapIndexInputs.SwapIndexOf(spec.GetProperty("swapIndex")),
                    context,
                    spec.GetProperty("spread").GetDouble(),
                    SwapInputs.PeriodOf(spec.GetProperty("fwdStart")),
                    IndexInputs.CurveOf(spec.GetProperty("discountingCurve")),
                    (PillarChoice)spec.GetProperty("pillar").GetInt32(),
                    CashFlowInputs.NullableDate(spec.GetProperty("customPillarDate")),
                    spec.GetProperty("endOfMonth").GetBoolean(),
                    CouponInputs.NullableBool(spec.GetProperty("useIndexedCoupons")),
                    PricerOf(spec, context));
            case ("BMASwapRateHelper", "tenor"):
                return new BMASwapRateHelper(
                    rate,
                    SwapInputs.PeriodOf(spec.GetProperty("tenor")),
                    spec.GetProperty("settlementDays").GetInt32(),
                    CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
                    SwapInputs.PeriodOf(spec.GetProperty("bmaPeriod")),
                    (BusinessDayConvention)spec.GetProperty("bmaConvention").GetInt32(),
                    CouponInputs.DayCounterOrNull(spec.GetProperty("bmaDayCount")),
                    new BMAIndex(),
                    SwapInputs.IndexOf(spec.GetProperty("index")),
                    context);
            default:
                throw new InvalidOperationException($"unknown helper {kind} ({form})");
        }
    }

    /// <summary>The helpers a list of specs describes, each built in the context.</summary>
    /// <param name="specs">The specs.</param>
    /// <param name="context">The evaluation context the helpers are built in.</param>
    /// <param name="handle">The curve a <c>{"handle": name}</c> discounting curve names.</param>
    /// <returns>The helpers, in order.</returns>
    public static BootstrapHelper<YieldTermStructure>[] HelpersOf(
        JsonElement specs, EvaluationContext context, Func<string, YieldTermStructure>? handle = null) =>
        [.. specs.EnumerateArray().Select(s => HelperOf(s, context, handle))];

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

    private static YieldTermStructure? DiscountingCurveOf(JsonElement spec, Func<string, YieldTermStructure>? handle) =>
        spec.ValueKind == JsonValueKind.Object && spec.TryGetProperty("handle", out JsonElement name)
            ? handle!(name.GetString()!)
            : IndexInputs.CurveOf(spec);

    private static FraRateHelper FraOf(JsonElement spec, string form, double rate, EvaluationContext context)
    {
        var pillar = (PillarChoice)spec.GetProperty("pillar").GetInt32();
        Date? custom = CashFlowInputs.NullableDate(spec.GetProperty("customPillarDate"));
        bool indexed = spec.GetProperty("useIndexedCoupon").GetBoolean();
        return form switch
        {
            "months" => new FraRateHelper(
                rate,
                spec.GetProperty("monthsToStart").GetInt32(),
                spec.GetProperty("monthsToEnd").GetInt32(),
                spec.GetProperty("fixingDays").GetInt32(),
                CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
                (BusinessDayConvention)spec.GetProperty("convention").GetInt32(),
                spec.GetProperty("endOfMonth").GetBoolean(),
                CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
                context,
                pillar,
                custom,
                indexed),
            "periodLength" => new FraRateHelper(
                rate,
                SwapInputs.PeriodOf(spec.GetProperty("periodToStart")),
                spec.GetProperty("lengthInMonths").GetInt32(),
                spec.GetProperty("fixingDays").GetInt32(),
                CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
                (BusinessDayConvention)spec.GetProperty("convention").GetInt32(),
                spec.GetProperty("endOfMonth").GetBoolean(),
                CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
                context,
                pillar,
                custom,
                indexed),
            "monthsIndex" => new FraRateHelper(
                rate,
                spec.GetProperty("monthsToStart").GetInt32(),
                SwapInputs.IndexOf(spec.GetProperty("index")),
                context,
                pillar,
                custom,
                indexed),
            "periodIndex" => new FraRateHelper(
                rate,
                SwapInputs.PeriodOf(spec.GetProperty("periodToStart")),
                SwapInputs.IndexOf(spec.GetProperty("index")),
                context,
                pillar,
                custom,
                indexed),
            "imm" => new FraRateHelper(
                rate,
                spec.GetProperty("immOffsetStart").GetInt32(),
                spec.GetProperty("immOffsetEnd").GetInt32(),
                SwapInputs.IndexOf(spec.GetProperty("index")),
                context,
                pillar,
                custom,
                indexed),
            "dates" => new FraRateHelper(
                rate,
                new Date(spec.GetProperty("startDate").GetInt32()),
                new Date(spec.GetProperty("endDate").GetInt32()),
                SwapInputs.IndexOf(spec.GetProperty("index")),
                pillar,
                custom,
                indexed),
            _ => throw new InvalidOperationException($"unknown FRA helper ({form})"),
        };
    }

    private static FuturesRateHelper FuturesOf(JsonElement spec, string form, double price)
    {
        var start = new Date(spec.GetProperty("iborStartDate").GetInt32());
        double convexity = spec.GetProperty("convexityAdjustment").GetDouble();
        var type = (FuturesType)spec.GetProperty("type").GetInt32();
        return form switch
        {
            "length" => new FuturesRateHelper(
                price,
                start,
                spec.GetProperty("lengthInMonths").GetInt32(),
                CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
                (BusinessDayConvention)spec.GetProperty("convention").GetInt32(),
                spec.GetProperty("endOfMonth").GetBoolean(),
                CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
                convexity,
                type),
            "dates" => new FuturesRateHelper(
                price,
                start,
                CashFlowInputs.NullableDate(spec.GetProperty("iborEndDate")),
                CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
                convexity,
                type),
            "index" => new FuturesRateHelper(price, start, SwapInputs.IndexOf(spec.GetProperty("index")), convexity, type),
            _ => throw new InvalidOperationException($"unknown futures helper ({form})"),
        };
    }

    private static FxSwapRateHelper FxSwapOf(JsonElement spec, string form, double points, EvaluationContext context)
    {
        double spot = spec.GetProperty("spotFx").GetDouble();
        bool isBase = spec.GetProperty("isFxBaseCurrencyCollateralCurrency").GetBoolean();
        YieldTermStructure? collateral = IndexInputs.CurveOf(spec.GetProperty("collateralCurve"));
        return form switch
        {
            "tenor" => new FxSwapRateHelper(
                points,
                spot,
                SwapInputs.PeriodOf(spec.GetProperty("tenor")),
                spec.GetProperty("fixingDays").GetInt32(),
                CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
                (BusinessDayConvention)spec.GetProperty("convention").GetInt32(),
                spec.GetProperty("endOfMonth").GetBoolean(),
                isBase,
                collateral,
                context,
                CashFlowInputs.NullableCalendarOf(spec.GetProperty("tradingCalendar"))),
            "dates" => new FxSwapRateHelper(
                points,
                spot,
                new Date(spec.GetProperty("startDate").GetInt32()),
                new Date(spec.GetProperty("endDate").GetInt32()),
                isBase,
                collateral),
            _ => throw new InvalidOperationException($"unknown FX swap helper ({form})"),
        };
    }

    private static OISRateHelper OisOf(JsonElement spec, string form, double rate, EvaluationContext context)
    {
        OvernightIndex index = SwapInputs.OvernightIndexOf(spec.GetProperty("index"));
        YieldTermStructure? discount = IndexInputs.CurveOf(spec.GetProperty("discountingCurve"));
        bool telescopic = spec.GetProperty("telescopicValueDates").GetBoolean();
        int paymentLag = spec.GetProperty("paymentLag").GetInt32();
        var paymentConvention = (BusinessDayConvention)spec.GetProperty("paymentConvention").GetInt32();
        var paymentFrequency = (Frequency)spec.GetProperty("paymentFrequency").GetInt32();
        Calendar? paymentCalendar = CashFlowInputs.NullableCalendarOf(spec.GetProperty("paymentCalendar"));
        double spread = spec.GetProperty("overnightSpread").GetDouble();
        var pillar = (PillarChoice)spec.GetProperty("pillar").GetInt32();
        Date? custom = CashFlowInputs.NullableDate(spec.GetProperty("customPillarDate"));
        var averaging = (RateAveragingType)spec.GetProperty("averagingMethod").GetInt32();
        bool? endOfMonth = CouponInputs.NullableBool(spec.GetProperty("endOfMonth"));
        Frequency? fixedFrequency =
            CouponInputs.NullableInt(spec.GetProperty("fixedPaymentFrequency")) is int f ? (Frequency)f : null;
        Calendar? fixedCalendar = CashFlowInputs.NullableCalendarOf(spec.GetProperty("fixedCalendar"));
        int? lookbackDays = CouponInputs.NullableInt(spec.GetProperty("lookbackDays"));
        int lockoutDays = spec.GetProperty("lockoutDays").GetInt32();
        bool shift = spec.GetProperty("applyObservationShift").GetBoolean();
        JsonElement pricerSpec = spec.GetProperty("pricer");
        FloatingRateCouponPricer? pricer =
            pricerSpec.ValueKind == JsonValueKind.Null ? null : OvernightInputs.PricerOf(pricerSpec);
        var rule = (DateGenerationRule)spec.GetProperty("rule").GetInt32();
        Calendar? overnightCalendar = CashFlowInputs.NullableCalendarOf(spec.GetProperty("overnightCalendar"));
        var convention = (BusinessDayConvention)spec.GetProperty("convention").GetInt32();
        DayCounter? fixedDayCount = CouponInputs.DayCounterOrNull(spec.GetProperty("fixedDayCount"));
        return form switch
        {
            "tenor" => new OISRateHelper(
                spec.GetProperty("settlementDays").GetInt32(),
                SwapInputs.PeriodOf(spec.GetProperty("tenor")),
                rate,
                index,
                context,
                discount,
                telescopic,
                paymentLag,
                paymentConvention,
                paymentFrequency,
                paymentCalendar,
                SwapInputs.PeriodOf(spec.GetProperty("forwardStart")),
                spread,
                pillar,
                custom,
                averaging,
                endOfMonth,
                fixedFrequency,
                fixedCalendar,
                lookbackDays,
                lockoutDays,
                shift,
                pricer,
                rule,
                overnightCalendar,
                convention,
                fixedDayCount),
            "dates" => new OISRateHelper(
                new Date(spec.GetProperty("startDate").GetInt32()),
                new Date(spec.GetProperty("endDate").GetInt32()),
                rate,
                index,
                context,
                discount,
                telescopic,
                paymentLag,
                paymentConvention,
                paymentFrequency,
                paymentCalendar,
                spread,
                pillar,
                custom,
                averaging,
                endOfMonth,
                fixedFrequency,
                fixedCalendar,
                lookbackDays,
                lockoutDays,
                shift,
                pricer,
                rule,
                overnightCalendar,
                convention,
                fixedDayCount),
            _ => throw new InvalidOperationException($"unknown OIS helper ({form})"),
        };
    }

    private static Calendar? CalendarOrNull(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CalendarOf(spec);

    private static BusinessDayConvention? FloatConvention(JsonElement spec) =>
        CouponInputs.NullableInt(spec.GetProperty("floatConvention")) is int c ? (BusinessDayConvention)c : null;

    private static FloatingRateCouponPricer? PricerOf(JsonElement spec, EvaluationContext context) =>
        spec.TryGetProperty("couponPricer", out JsonElement pricer) ? CouponInputs.PricerOf(pricer, context) : null;
}
