// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the overnight coupons, legs, pricers and volatilities that the overnight vector files describe in their
/// inputs, as their generators build them (<c>tools/vectors/gen/cashflows/overnightindexedcoupon.py</c>'s functions).
/// </summary>
/// <remarks>
/// <para>
/// A coupon is <c>{"paymentDate", "nominal", "startDate", "endDate", "index", "gearing", "spread", "refPeriodStart",
/// "refPeriodEnd", "dayCounter", "telescopicValueDates", "averagingMethod", "lookbackDays", "lockoutDays",
/// "applyObservationShift", "compoundSpreadDaily", "rateComputationStartDate", "rateComputationEndDate",
/// "exCouponDate", "roundingPrecision"}</c>, its index an overnight index as <see cref="IndexInputs"/> reads it, a null
/// date, day counter, lookback or precision being the constructor's default. A capped coupon is <c>{"underlying",
/// "cap", "floor", "nakedOption", "dailyCapFloor"}</c>. A pricer is <c>{"type", "vol", "effectiveVolatilityInput"}</c>,
/// the type <c>Compounding</c>, <c>ArithmeticAveraged</c> (with <c>"meanReversion"</c>, <c>"volatility"</c> and
/// <c>"byApprox"</c>), <c>BlackCompounding</c> or <c>BlackAveraging</c>. A volatility is a
/// <see cref="ConstantOptionletVolatility"/>, <c>{"referenceDate", "calendar", "convention", "volatility",
/// "dayCounter", "type", "displacement"}</c>, or null for none.
/// </para>
/// <para>
/// A leg is <c>{"schedule", "index", "notionals", "paymentDayCounter", "paymentConvention", "paymentCalendar",
/// "paymentLag", "gearings", "spreads", "telescopicValueDates", "averagingMethod", "lookbackDays", "lockoutDays",
/// "applyObservationShift", "compoundSpreadDaily", "caps", "floors", "dailyCapFloor", "inArrears", "nakedOption",
/// "paymentDates", "roundingPrecision"}</c>, which QuantLib-Python's <c>OvernightLeg</c> passes to the builder's
/// setters (a null day counter, calendar, lookback or precision leaving the builder's default); the C++ harness's legs
/// add <c>"lastRecentPeriod"</c>, <c>"lastRecentPeriodCalendar"</c> and <c>"couponPricer"</c>. A schedule is a
/// rule-based spec as <see cref="CashFlowInputs.ScheduleOf"/> reads it, or <c>{"dates", "calendar", "tenor",
/// "isRegular"}</c>.
/// </para>
/// </remarks>
internal static class OvernightInputs
{
    /// <summary>The overnight index a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The index.</returns>
    public static OvernightIndex IndexOf(JsonElement spec) =>
        (OvernightIndex)IndexInputs.IborIndexOf(spec);

    /// <summary>The coupon a spec describes, on the given index.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="index">The coupon's index, built from the spec's.</param>
    /// <returns>The coupon.</returns>
    public static OvernightIndexedCoupon CouponOf(JsonElement spec, OvernightIndex index) =>
        new(
            DateOf(spec, "paymentDate"),
            spec.GetProperty("nominal").GetDouble(),
            DateOf(spec, "startDate"),
            DateOf(spec, "endDate"),
            index,
            spec.GetProperty("gearing").GetDouble(),
            spec.GetProperty("spread").GetDouble(),
            NullableDateOf(spec, "refPeriodStart"),
            NullableDateOf(spec, "refPeriodEnd"),
            NullableDayCounterOf(spec.GetProperty("dayCounter")),
            spec.GetProperty("telescopicValueDates").GetBoolean(),
            (RateAveragingType)spec.GetProperty("averagingMethod").GetInt32(),
            NullableInt(spec.GetProperty("lookbackDays")),
            spec.GetProperty("lockoutDays").GetInt32(),
            spec.GetProperty("applyObservationShift").GetBoolean(),
            spec.GetProperty("compoundSpreadDaily").GetBoolean(),
            NullableDateOf(spec, "rateComputationStartDate"),
            NullableDateOf(spec, "rateComputationEndDate"),
            NullableDateOf(spec, "exCouponDate"),
            NullableInt(spec.GetProperty("roundingPrecision")));

    /// <summary>The coupon a spec describes, on its own index.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The coupon.</returns>
    public static OvernightIndexedCoupon CouponOf(JsonElement spec) => CouponOf(spec, IndexOf(spec.GetProperty("index")));

    /// <summary>The capped or floored coupon a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The coupon, with no pricer.</returns>
    public static CappedFlooredOvernightIndexedCoupon CappedOf(JsonElement spec) =>
        new(
            CouponOf(spec.GetProperty("underlying")),
            NullableDouble(spec.GetProperty("cap")),
            NullableDouble(spec.GetProperty("floor")),
            spec.GetProperty("nakedOption").GetBoolean(),
            spec.GetProperty("dailyCapFloor").GetBoolean());

    /// <summary>The pricer a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The pricer.</returns>
    public static OvernightIndexedCouponPricer PricerOf(JsonElement spec)
    {
        OptionletVolatilityStructure? vol = VolatilityOf(spec.GetProperty("vol"));
        bool effective = spec.GetProperty("effectiveVolatilityInput").GetBoolean();
        string type = spec.GetProperty("type").GetString()!;
        return type switch
        {
            "Compounding" => new CompoundingOvernightIndexedCouponPricer(vol, effective),
            "ArithmeticAveraged" => new ArithmeticAveragedOvernightIndexedCouponPricer(
                spec.GetProperty("meanReversion").GetDouble(),
                spec.GetProperty("volatility").GetDouble(),
                spec.GetProperty("byApprox").GetBoolean(),
                vol,
                effective),
            "BlackCompounding" => new BlackCompoundingOvernightIndexedCouponPricer(vol, effective),
            "BlackAveraging" => new BlackAveragingOvernightIndexedCouponPricer(vol, effective),
            _ => throw new InvalidOperationException($"unknown pricer type '{type}'"),
        };
    }

    /// <summary>The volatility a spec describes, or <see langword="null"/> for none.</summary>
    /// <param name="spec">The spec, or JSON <c>null</c>.</param>
    /// <returns>The volatility.</returns>
    public static OptionletVolatilityStructure? VolatilityOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null
            ? null
            : new ConstantOptionletVolatility(
                DateOf(spec, "referenceDate"),
                CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
                (BusinessDayConvention)spec.GetProperty("convention").GetInt32(),
                spec.GetProperty("volatility").GetDouble(),
                CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
                (VolatilityType)spec.GetProperty("type").GetInt32(),
                spec.GetProperty("displacement").GetDouble());

    /// <summary>The schedule a spec describes: rule-based, or from a list of dates.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The schedule.</returns>
    public static Schedule ScheduleOf(JsonElement spec, EvaluationContext context)
    {
        if (!spec.TryGetProperty("dates", out JsonElement dates))
        {
            return CashFlowInputs.ScheduleOf(spec, context);
        }

        JsonElement calendar = spec.GetProperty("calendar");
        JsonElement tenor = spec.GetProperty("tenor");
        return new Schedule(
            dates.EnumerateArray().Select(d => new Date(d.GetInt32())),
            calendar.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CalendarOf(calendar),
            BusinessDayConvention.Unadjusted,
            null,
            tenor.ValueKind == JsonValueKind.Null ? null : new Period(tenor[0].GetInt32(), (TimeUnit)tenor[1].GetInt32()),
            null,
            null,
            spec.GetProperty("isRegular").EnumerateArray().Select(b => b.GetBoolean()));
    }

    /// <summary>The leg builder a spec describes, every setting passed as QuantLib-Python's <c>OvernightLeg</c> passes it.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="index">The leg's index, built from the spec's.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The builder.</returns>
    public static OvernightLeg LegBuilderOf(JsonElement spec, OvernightIndex index, EvaluationContext context)
    {
        // A list of one value goes through the setter taking one value, which QuantLib defines as that list.
        double[] notionals = Doubles(spec.GetProperty("notionals"));
        double[] gearings = Doubles(spec.GetProperty("gearings"));
        double[] spreads = Doubles(spec.GetProperty("spreads"));
        double?[] caps = NullableDoubles(spec.GetProperty("caps"));
        double?[] floors = NullableDoubles(spec.GetProperty("floors"));
        var leg = new OvernightLeg(ScheduleOf(spec.GetProperty("schedule"), context), index);
        leg = notionals.Length == 1 ? leg.WithNotionals(notionals[0]) : leg.WithNotionals(notionals);
        leg = gearings.Length == 1 ? leg.WithGearings(gearings[0]) : leg.WithGearings(gearings);
        leg = spreads.Length == 1 ? leg.WithSpreads(spreads[0]) : leg.WithSpreads(spreads);
        leg = caps is [double cap] ? leg.WithCaps(cap) : leg.WithCaps(caps);
        leg = floors is [double floor] ? leg.WithFloors(floor) : leg.WithFloors(floors);
        leg = leg
            .WithPaymentAdjustment((BusinessDayConvention)spec.GetProperty("paymentConvention").GetInt32())
            .WithPaymentLag(spec.GetProperty("paymentLag").GetInt32())
            .WithTelescopicValueDates(spec.GetProperty("telescopicValueDates").GetBoolean())
            .WithAveragingMethod((RateAveragingType)spec.GetProperty("averagingMethod").GetInt32())
            .WithLockoutDays(spec.GetProperty("lockoutDays").GetInt32())
            .WithObservationShift(spec.GetProperty("applyObservationShift").GetBoolean())
            .CompoundingSpreadDaily(spec.GetProperty("compoundSpreadDaily").GetBoolean())
            .WithDailyCapFloor(spec.GetProperty("dailyCapFloor").GetBoolean())
            .InArrears(spec.GetProperty("inArrears").GetBoolean())
            .WithNakedOption(spec.GetProperty("nakedOption").GetBoolean())
            .WithPaymentDates(spec.GetProperty("paymentDates").EnumerateArray().Select(d => new Date(d.GetInt32())));
        if (NullableDayCounterOf(spec.GetProperty("paymentDayCounter")) is DayCounter dc)
        {
            leg = leg.WithPaymentDayCounter(dc);
        }

        JsonElement calendar = spec.GetProperty("paymentCalendar");
        if (calendar.ValueKind != JsonValueKind.Null)
        {
            leg = leg.WithPaymentCalendar(CashFlowInputs.CalendarOf(calendar));
        }

        if (NullableInt(spec.GetProperty("lookbackDays")) is int lookback)
        {
            leg = leg.WithLookbackDays(lookback);
        }

        if (NullableInt(spec.GetProperty("roundingPrecision")) is int precision)
        {
            leg = leg.WithRoundingPrecision(precision);
        }

        if (spec.TryGetProperty("lastRecentPeriod", out JsonElement lastRecent))
        {
            leg = leg.WithLastRecentPeriod(
                lastRecent.ValueKind == JsonValueKind.Null
                    ? null
                    : new Period(lastRecent[0].GetInt32(), (TimeUnit)lastRecent[1].GetInt32()));
        }

        if (spec.TryGetProperty("lastRecentPeriodCalendar", out JsonElement lastRecentCalendar)
            && lastRecentCalendar.ValueKind != JsonValueKind.Null)
        {
            leg = leg.WithLastRecentPeriodCalendar(CashFlowInputs.CalendarOf(lastRecentCalendar));
        }

        if (spec.TryGetProperty("couponPricer", out JsonElement pricer) && pricer.ValueKind != JsonValueKind.Null)
        {
            leg = leg.WithCouponPricer(PricerOf(pricer));
        }

        return leg;
    }

    /// <summary>A cash flow's type name, as the overnight generators write it.</summary>
    /// <param name="cf">The cash flow.</param>
    /// <returns>The name.</returns>
    public static string TypeName(CashFlow cf) => cf switch
    {
        CappedFlooredOvernightIndexedCoupon => "CappedFlooredOvernightIndexedCoupon",
        OvernightIndexedCoupon => "OvernightIndexedCoupon",
        FixedRateCoupon => "FixedRateCoupon",
        _ => "?",
    };

    /// <summary>A date input of a spec, written as its serial number.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="name">The key.</param>
    /// <returns>The date.</returns>
    public static Date DateOf(JsonElement spec, string name) => new(spec.GetProperty(name).GetInt32());

    /// <summary>A date input of a spec that may be null.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="name">The key.</param>
    /// <returns>The date, or <see langword="null"/>.</returns>
    public static Date? NullableDateOf(JsonElement spec, string name) =>
        CashFlowInputs.NullableDate(spec.GetProperty(name));

    /// <summary>A real that may be null.</summary>
    /// <param name="e">The JSON value.</param>
    /// <returns>The real, or <see langword="null"/>.</returns>
    public static double? NullableDouble(JsonElement e) =>
        e.ValueKind == JsonValueKind.Null ? null : IndexInputs.Real(e);

    /// <summary>A list of reals.</summary>
    /// <param name="e">The JSON array.</param>
    /// <returns>The reals.</returns>
    public static double[] Doubles(JsonElement e) => e.EnumerateArray().Select(IndexInputs.Real).ToArray();

    private static double?[] NullableDoubles(JsonElement e) => e.EnumerateArray().Select(NullableDouble).ToArray();

    private static int? NullableInt(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetInt32();

    private static DayCounter? NullableDayCounterOf(JsonElement e) =>
        e.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.DayCounterOf(e);
}
