// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the objects that the interest-rate, curve, cash-flow and swap vector files describe in their inputs, as their
/// generators build them: calendars, day counters, schedules, fixed-rate legs with the flows that follow them, flat
/// forward curves and swaps.
/// </summary>
/// <remarks>
/// A calendar is <c>{"type"}</c>, with a <c>"market"</c> integer for the US and UK calendars; a day counter is
/// <c>{"type"}</c>, with a <c>"convention"</c> integer for 30/360 and Actual/Actual. Enums and time units are
/// QuantLib's integers, dates serial numbers, and a null setting is one left at its default, but for a rule-based
/// schedule's calendar and an ex-coupon calendar, where null is QuantLib's empty calendar.
/// </remarks>
internal static class CashFlowInputs
{
    /// <summary>The calendar a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The calendar.</returns>
    public static Calendar CalendarOf(JsonElement spec)
    {
        string type = spec.GetProperty("type").GetString()!;
        return type switch
        {
            "TARGET" => new TARGET(),
            "NullCalendar" => new NullCalendar(),
            "UnitedStates" => new UnitedStates((UnitedStatesMarket)spec.GetProperty("market").GetInt32()),
            "UnitedKingdom" => new UnitedKingdom((UnitedKingdomMarket)spec.GetProperty("market").GetInt32()),
            _ => throw new InvalidOperationException($"unknown calendar type '{type}'"),
        };
    }

    /// <summary>The calendar a spec describes, or <see langword="null"/>, QuantLib's empty calendar, for JSON null.</summary>
    /// <param name="spec">The spec, or JSON <c>null</c>.</param>
    /// <returns>The calendar, or <see langword="null"/>.</returns>
    public static Calendar? NullableCalendarOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null ? null : CalendarOf(spec);

    /// <summary>The day counter a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The day counter.</returns>
    public static DayCounter DayCounterOf(JsonElement spec)
    {
        string type = spec.GetProperty("type").GetString()!;
        return type switch
        {
            "Actual360" => new Actual360(),
            "Actual365Fixed" => new Actual365Fixed(),
            "Thirty360" => new Thirty360((Thirty360Convention)spec.GetProperty("convention").GetInt32()),
            "ActualActual" => new ActualActual((ActualActualConvention)spec.GetProperty("convention").GetInt32()),
            _ => throw new InvalidOperationException($"unknown day counter type '{type}'"),
        };
    }

    /// <summary>The rule-based schedule a spec describes.</summary>
    /// <param name="spec">
    /// The spec: dates, tenor, calendar, conventions, rule, end of month, first and next-to-last dates.
    /// </param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The schedule.</returns>
    public static Schedule ScheduleOf(JsonElement spec, EvaluationContext context)
    {
        JsonElement tenor = spec.GetProperty("tenor");
        return new Schedule(
            NullableDate(spec.GetProperty("effectiveDate")),
            new Date(spec.GetProperty("terminationDate").GetInt32()),
            new Period(tenor[0].GetInt32(), (TimeUnit)tenor[1].GetInt32()),
            NullableCalendarOf(spec.GetProperty("calendar")),
            (BusinessDayConvention)spec.GetProperty("convention").GetInt32(),
            (BusinessDayConvention)spec.GetProperty("terminationDateConvention").GetInt32(),
            (DateGenerationRule)spec.GetProperty("rule").GetInt32(),
            spec.GetProperty("endOfMonth").GetBoolean(),
            context,
            NullableDate(spec.GetProperty("firstDate")),
            NullableDate(spec.GetProperty("nextToLastDate")));
    }

    /// <summary>The coupons a <c>"fixedRateLeg"</c> spec describes, built with <see cref="FixedRateLeg"/>.</summary>
    /// <param name="spec">The spec: the schedule and every builder setting.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The leg.</returns>
    public static ImmutableArray<CashFlow> FixedRateLegOf(JsonElement spec, EvaluationContext context)
    {
        FixedRateLeg builder = new FixedRateLeg(ScheduleOf(spec.GetProperty("schedule"), context))
            .WithNotionals(Doubles(spec.GetProperty("notionals")))
            .WithCouponRates(
                Doubles(spec.GetProperty("couponRates")),
                DayCounterOf(spec.GetProperty("dayCounter")),
                (Compounding)spec.GetProperty("compounding").GetInt32(),
                (Frequency)spec.GetProperty("frequency").GetInt32())
            .WithPaymentAdjustment((BusinessDayConvention)spec.GetProperty("paymentAdjustment").GetInt32())
            .WithPaymentLag(spec.GetProperty("paymentLag").GetInt32());
        if (Optional(spec, "firstPeriodDayCounter") is JsonElement firstPeriodDayCounter)
        {
            builder = builder.WithFirstPeriodDayCounter(DayCounterOf(firstPeriodDayCounter));
        }

        if (Optional(spec, "paymentCalendar") is JsonElement paymentCalendar)
        {
            builder = builder.WithPaymentCalendar(CalendarOf(paymentCalendar));
        }

        if (Optional(spec, "exCouponPeriod") is JsonElement exCoupon)
        {
            JsonElement period = exCoupon.GetProperty("period");
            builder = builder.WithExCouponPeriod(
                new Period(period[0].GetInt32(), (TimeUnit)period[1].GetInt32()),
                NullableCalendarOf(exCoupon.GetProperty("calendar")),
                (BusinessDayConvention)exCoupon.GetProperty("convention").GetInt32(),
                exCoupon.GetProperty("endOfMonth").GetBoolean());
        }

        return builder.ToLeg();
    }

    /// <summary>
    /// The leg a <c>"leg"</c> spec describes: the coupons of its <c>"fixedRateLeg"</c>, if any, followed by its
    /// <c>"extraFlows"</c> in order, each a <see cref="Redemption"/> or an <see cref="AmortizingPayment"/>.
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The leg; empty when the spec has neither coupons nor flows.</returns>
    public static ImmutableArray<CashFlow> LegOf(JsonElement spec, EvaluationContext context)
    {
        ImmutableArray<CashFlow>.Builder flows = ImmutableArray.CreateBuilder<CashFlow>();
        if (Optional(spec, "fixedRateLeg") is JsonElement coupons)
        {
            flows.AddRange(FixedRateLegOf(coupons, context));
        }

        foreach (JsonElement flow in spec.GetProperty("extraFlows").EnumerateArray())
        {
            string type = flow.GetProperty("type").GetString()!;
            double amount = flow.GetProperty("amount").GetDouble();
            var date = new Date(flow.GetProperty("date").GetInt32());
            flows.Add(type switch
            {
                "Redemption" => new Redemption(amount, date),
                "AmortizingPayment" => new AmortizingPayment(amount, date),
                _ => throw new InvalidOperationException($"unknown cash flow type '{type}'"),
            });
        }

        return flows.ToImmutable();
    }

    /// <summary>The flat forward curve, with a fixed reference date, that a <c>"curve"</c> spec describes.</summary>
    /// <param name="spec">The spec: reference date, forward rate, day counter, compounding and frequency.</param>
    /// <returns>The curve.</returns>
    public static FlatForward CurveOf(JsonElement spec) =>
        new(
            new Date(spec.GetProperty("referenceDate").GetInt32()),
            spec.GetProperty("forward").GetDouble(),
            DayCounterOf(spec.GetProperty("dayCounter")),
            (Compounding)spec.GetProperty("compounding").GetInt32(),
            (Frequency)spec.GetProperty("frequency").GetInt32());

    /// <summary>
    /// The swap a <c>"swap"</c> spec describes: <c>"pair"</c> for the constructor taking a paid and a received leg,
    /// <c>"legs"</c> for the one taking legs and payer flags.
    /// </summary>
    /// <param name="spec">The spec: the form, the legs and the payer flags.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The swap.</returns>
    public static Swap SwapOf(JsonElement spec, EvaluationContext context)
    {
        ImmutableArray<CashFlow>[] legs =
            spec.GetProperty("legs").EnumerateArray().Select(l => LegOf(l, context)).ToArray();
        bool[] payer = spec.GetProperty("payer").EnumerateArray().Select(p => p.GetBoolean()).ToArray();
        string form = spec.GetProperty("form").GetString()!;
        switch (form)
        {
            case "pair":
                if (legs.Length != 2 || !payer.SequenceEqual([true, false]))
                {
                    throw new InvalidOperationException("a pair swap pays its first leg and receives its second");
                }

                return new Swap(legs[0], legs[1]);
            case "legs":
                return new Swap(legs, payer);
            default:
                throw new InvalidOperationException($"unknown swap form '{form}'");
        }
    }

    /// <summary>A date written as its serial number, or null.</summary>
    /// <param name="e">The JSON value.</param>
    /// <returns>The date, or <see langword="null"/>.</returns>
    public static Date? NullableDate(JsonElement e) =>
        e.ValueKind == JsonValueKind.Null ? null : new Date(e.GetInt32());

    private static double[] Doubles(JsonElement e) => e.EnumerateArray().Select(x => x.GetDouble()).ToArray();

    private static JsonElement? Optional(JsonElement spec, string name)
    {
        JsonElement e = spec.GetProperty(name);
        return e.ValueKind == JsonValueKind.Null ? null : e;
    }
}
