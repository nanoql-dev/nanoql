// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the bonds, legs, schedules, rates and prices that the bond vector files describe in their inputs, as
/// <c>tools/vectors/gen/instruments/bond.py</c>'s functions build them, and checks the outputs every bond file shares.
/// </summary>
/// <remarks>
/// A bond is <c>{"type": "Bond", ...}</c> or <c>"BondFaceAmount"</c> for QuantLib's two public constructors, or one of
/// <c>"FixedRateBond"</c>, <c>"FloatingRateBond"</c>, <c>"ZeroCouponBond"</c> and <c>"AmortizingFixedRateBond"</c>,
/// with QuantLib's arguments by name; a leg is <c>{"fixedRateLeg", "coupons", "extraFlows"}</c>, its flows in that
/// order; a schedule is rule-based or <c>{"dates", "calendar", "convention"}</c>; an ex-coupon period null or
/// <c>{"period", "calendar", "convention", "endOfMonth"}</c>; a rate <c>{"rate", "dayCounter", "compounding",
/// "frequency"}</c>; a price <c>{"amount", "type"}</c>. A null calendar or day counter is QuantLib's empty one, but
/// a schedule's calendar is never null: <see cref="Schedule"/> reads a null one as QuantLib's default.
/// </remarks>
internal static class BondInputs
{
    /// <summary>The bond a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context, which a floating-rate bond's coupons read.</param>
    /// <returns>The bond.</returns>
    public static Bond BondOf(JsonElement spec, EvaluationContext context)
    {
        string type = spec.GetProperty("type").GetString()!;
        int settlementDays = spec.GetProperty("settlementDays").GetInt32();
        switch (type)
        {
            case "Bond":
                return new Bond(
                    settlementDays,
                    CalendarOf(spec.GetProperty("calendar")),
                    CashFlowInputs.NullableDate(spec.GetProperty("issueDate")),
                    LegOf(spec.GetProperty("coupons"), context));
            case "BondFaceAmount":
                return new Bond(
                    settlementDays,
                    CalendarOf(spec.GetProperty("calendar")),
                    IndexInputs.Real(spec.GetProperty("faceAmount")),
                    CashFlowInputs.NullableDate(spec.GetProperty("maturityDate")),
                    CashFlowInputs.NullableDate(spec.GetProperty("issueDate")),
                    LegOf(spec.GetProperty("cashflows"), context));
            case "FixedRateBond":
            {
                (Period period, Calendar? calendar, BusinessDayConvention convention, bool eom) =
                    ExCouponOf(spec.GetProperty("exCouponPeriod"));
                return new FixedRateBond(
                    settlementDays,
                    IndexInputs.Real(spec.GetProperty("faceAmount")),
                    ScheduleOf(spec.GetProperty("schedule"), context),
                    Doubles(spec.GetProperty("coupons")),
                    CashFlowInputs.DayCounterOf(spec.GetProperty("accrualDayCounter")),
                    (BusinessDayConvention)spec.GetProperty("paymentConvention").GetInt32(),
                    IndexInputs.Real(spec.GetProperty("redemption")),
                    CashFlowInputs.NullableDate(spec.GetProperty("issueDate")),
                    CalendarOf(spec.GetProperty("paymentCalendar")),
                    period,
                    calendar,
                    convention,
                    eom,
                    DayCounterOf(spec.GetProperty("firstPeriodDayCounter")));
            }

            case "FloatingRateBond":
            {
                (Period period, Calendar? calendar, BusinessDayConvention convention, bool eom) =
                    ExCouponOf(spec.GetProperty("exCouponPeriod"));
                return new FloatingRateBond(
                    settlementDays,
                    IndexInputs.Real(spec.GetProperty("faceAmount")),
                    ScheduleOf(spec.GetProperty("schedule"), context),
                    IndexInputs.IborIndexOf(spec.GetProperty("index")),
                    DayCounterOf(spec.GetProperty("accrualDayCounter")),
                    context,
                    (BusinessDayConvention)spec.GetProperty("paymentConvention").GetInt32(),
                    CouponInputs.NullableInt(spec.GetProperty("fixingDays")),
                    Doubles(spec.GetProperty("gearings")),
                    Doubles(spec.GetProperty("spreads")),
                    NullableDoubles(spec.GetProperty("caps")),
                    NullableDoubles(spec.GetProperty("floors")),
                    spec.GetProperty("inArrears").GetBoolean(),
                    IndexInputs.Real(spec.GetProperty("redemption")),
                    CashFlowInputs.NullableDate(spec.GetProperty("issueDate")),
                    period,
                    calendar,
                    convention,
                    eom,
                    (BusinessDayConvention)spec.GetProperty("fixingConvention").GetInt32());
            }

            case "ZeroCouponBond":
                return new ZeroCouponBond(
                    settlementDays,
                    CalendarOf(spec.GetProperty("calendar")),
                    IndexInputs.Real(spec.GetProperty("faceAmount")),
                    new Date(spec.GetProperty("maturityDate").GetInt32()),
                    (BusinessDayConvention)spec.GetProperty("paymentConvention").GetInt32(),
                    IndexInputs.Real(spec.GetProperty("redemption")),
                    CashFlowInputs.NullableDate(spec.GetProperty("issueDate")));
            case "AmortizingFixedRateBond":
            {
                (Period period, Calendar? calendar, BusinessDayConvention convention, bool eom) =
                    ExCouponOf(spec.GetProperty("exCouponPeriod"));
                return new AmortizingFixedRateBond(
                    settlementDays,
                    Doubles(spec.GetProperty("notionals")),
                    ScheduleOf(spec.GetProperty("schedule"), context),
                    Doubles(spec.GetProperty("coupons")),
                    CashFlowInputs.DayCounterOf(spec.GetProperty("accrualDayCounter")),
                    (BusinessDayConvention)spec.GetProperty("paymentConvention").GetInt32(),
                    CashFlowInputs.NullableDate(spec.GetProperty("issueDate")),
                    period,
                    calendar,
                    convention,
                    eom,
                    Doubles(spec.GetProperty("redemptions")),
                    spec.GetProperty("paymentLag").GetInt32());
            }

            default:
                throw new InvalidOperationException($"unknown bond type '{type}'");
        }
    }

    /// <summary>
    /// The leg a spec describes: the coupons of its <c>"fixedRateLeg"</c>, if any, then its <c>"coupons"</c> given one
    /// by one, then its <c>"extraFlows"</c>.
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The leg.</returns>
    public static ImmutableArray<CashFlow> LegOf(JsonElement spec, EvaluationContext context)
    {
        ImmutableArray<CashFlow>.Builder flows = ImmutableArray.CreateBuilder<CashFlow>();
        JsonElement fixedLeg = spec.GetProperty("fixedRateLeg");
        if (fixedLeg.ValueKind != JsonValueKind.Null)
        {
            flows.AddRange(CashFlowInputs.FixedRateLegOf(fixedLeg, context));
        }

        if (spec.TryGetProperty("coupons", out JsonElement coupons))
        {
            foreach (JsonElement c in coupons.EnumerateArray())
            {
                flows.Add(new FixedRateCoupon(
                    new Date(c.GetProperty("paymentDate").GetInt32()),
                    c.GetProperty("nominal").GetDouble(),
                    c.GetProperty("rate").GetDouble(),
                    CashFlowInputs.DayCounterOf(c.GetProperty("dayCounter")),
                    new Date(c.GetProperty("accrualStartDate").GetInt32()),
                    new Date(c.GetProperty("accrualEndDate").GetInt32()),
                    CashFlowInputs.NullableDate(c.GetProperty("refPeriodStart")),
                    CashFlowInputs.NullableDate(c.GetProperty("refPeriodEnd")),
                    CashFlowInputs.NullableDate(c.GetProperty("exCouponDate"))));
            }
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

    /// <summary>The schedule a spec describes: rule-based, or built from dates with no tenor.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The schedule.</returns>
    public static Schedule ScheduleOf(JsonElement spec, EvaluationContext context)
    {
        if (spec.TryGetProperty("dates", out JsonElement dates))
        {
            return new Schedule(
                dates.EnumerateArray().Select(d => new Date(d.GetInt32())),
                CalendarOf(spec.GetProperty("calendar")),
                (BusinessDayConvention)spec.GetProperty("convention").GetInt32());
        }

        return CashFlowInputs.ScheduleOf(spec, context);
    }

    /// <summary>
    /// The bond helper a spec describes: <c>{"type": "BondHelper", "price", "bond", "priceType"}</c>, its bond a spec
    /// of <see cref="BondOf"/>, or <c>{"type": "FixedRateBondHelper", "price", "settlementDays", "faceAmount",
    /// "schedule", "coupons", "dayCounter", "paymentConv", "redemption", "issueDate", "paymentCalendar",
    /// "exCouponPeriod", "priceType"}</c>.
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The helper.</returns>
    public static BondHelper HelperOf(JsonElement spec, EvaluationContext context)
    {
        double price = spec.GetProperty("price").GetDouble();
        var priceType = (BondPriceType)spec.GetProperty("priceType").GetInt32();
        if (spec.GetProperty("type").GetString() == "BondHelper")
        {
            return new BondHelper(price, BondOf(spec.GetProperty("bond"), context), context, priceType);
        }

        (Period period, Calendar? calendar, BusinessDayConvention convention, bool eom) =
            ExCouponOf(spec.GetProperty("exCouponPeriod"));
        return new FixedRateBondHelper(
            price,
            spec.GetProperty("settlementDays").GetInt32(),
            spec.GetProperty("faceAmount").GetDouble(),
            ScheduleOf(spec.GetProperty("schedule"), context),
            [.. spec.GetProperty("coupons").EnumerateArray().Select(x => x.GetDouble())],
            CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
            context,
            (BusinessDayConvention)spec.GetProperty("paymentConv").GetInt32(),
            spec.GetProperty("redemption").GetDouble(),
            CashFlowInputs.NullableDate(spec.GetProperty("issueDate")),
            CalendarOf(spec.GetProperty("paymentCalendar")),
            period,
            calendar,
            convention,
            eom,
            priceType);
    }

    /// <summary>The interest rate a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The rate.</returns>
    public static InterestRate RateOf(JsonElement spec) => new(
        spec.GetProperty("rate").GetDouble(),
        CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
        (Compounding)spec.GetProperty("compounding").GetInt32(),
        (Frequency)spec.GetProperty("frequency").GetInt32());

    /// <summary>The bond price a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The price.</returns>
    public static BondPrice PriceOf(JsonElement spec) =>
        new(spec.GetProperty("amount").GetDouble(), (BondPriceType)spec.GetProperty("type").GetInt32());

    /// <summary>A calendar spec, or <see langword="null"/>, QuantLib's empty calendar, for JSON null.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The calendar, or <see langword="null"/>.</returns>
    public static Calendar? CalendarOf(JsonElement spec) => CashFlowInputs.NullableCalendarOf(spec);

    /// <summary>A day counter spec, or <see langword="null"/>, QuantLib's empty day counter, for JSON null.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The day counter, or <see langword="null"/>.</returns>
    public static DayCounter? DayCounterOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.DayCounterOf(spec);

    /// <summary>The dates a case's outputs are read at, null for QuantLib's null date.</summary>
    /// <param name="c">The case.</param>
    /// <param name="name">The input's name.</param>
    /// <returns>The dates.</returns>
    public static Date?[] DatesOf(VectorCase c, string name) =>
        [.. c.Input(name).EnumerateArray().Select(CashFlowInputs.NullableDate)];

    /// <summary>Checks the inspectors with no date: <c>tools/vectors/gen/instruments/bond.py</c>'s static outputs.</summary>
    /// <param name="check">The file's checker.</param>
    /// <param name="c">The case.</param>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The case's evaluation context.</param>
    public static void CheckStatic(VectorAssert check, VectorCase c, Bond bond, EvaluationContext context)
    {
        check.Output(c, "settlementDays", bond.SettlementDays);
        check.Output(c, "notionals", bond.Notionals);
        check.Output(c, "cashflowDates", bond.Cashflows.Select(cf => cf.Date));
        check.Output(c, "cashflowAmounts", Each(bond.Cashflows, cf => cf.Amount(context)));
        check.Output(c, "redemptionDates", bond.Redemptions.Select(cf => cf.Date));
        check.Output(c, "redemptionAmounts", bond.Redemptions.Select(cf => cf.Amount(context)));
        check.Output(c, "redemptionDate", () => bond.Redemption().Date);
        check.Output(c, "redemptionAmount", () => bond.Redemption().Amount(context));
        check.Output(c, "startDate", () => bond.StartDate());
        check.Output(c, "maturityDate", () => bond.MaturityDate());
        check.Output(c, "issueDate", bond.IssueDate);
        check.Output(c, "isExpired", bond.IsExpired(context));
    }

    /// <summary>
    /// Checks the members read at the case's dates: <c>tools/vectors/gen/instruments/bond.py</c>'s dated outputs, the
    /// prices for the case's rate and the yield for its price.
    /// </summary>
    /// <param name="check">The file's checker.</param>
    /// <param name="c">The case.</param>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The case's evaluation context.</param>
    public static void CheckDated(VectorAssert check, VectorCase c, Bond bond, EvaluationContext context)
    {
        Date?[] dates = DatesOf(c, "dates");
        InterestRate rate = RateOf(c.Input("rate"));
        BondPrice price = PriceOf(c.Input("price"));
        check.Output(c, "settlementDate", Each(dates, d => bond.SettlementDate(context, d)));
        check.Output(c, "notional", Each(dates, d => bond.Notional(context, d)));
        check.Output(c, "isTradable", Each(dates, d => bond.IsTradable(context, d)));
        check.Output(c, "accruedAmount", Each(dates, d => bond.AccruedAmount(context, d)));
        check.Output(c, "nextCouponRate", Each(dates, d => bond.NextCouponRate(context, d)));
        check.Output(c, "previousCouponRate", Each(dates, d => bond.PreviousCouponRate(context, d)));
        check.Output(c, "nextCashFlowDate", Each(dates, d => bond.NextCashFlowDate(context, d)));
        check.Output(c, "previousCashFlowDate", Each(dates, d => bond.PreviousCashFlowDate(context, d)));
        check.Output(c, "cleanPrice", Each(dates, d => bond.CleanPrice(
            rate.Rate, rate.DayCounter, rate.Compounding, rate.Frequency, context, d)));
        check.Output(c, "dirtyPrice", Each(dates, d => bond.DirtyPrice(
            rate.Rate, rate.DayCounter, rate.Compounding, rate.Frequency, context, d)));
        check.Output(c, "yield", Each(dates, d => bond.Yield(
            price, rate.DayCounter, rate.Compounding, rate.Frequency, context, d, 1.0e-8, 100, 0.05)));
    }

    /// <summary>
    /// Checks the bond priced by <see cref="DiscountingBondEngine"/> on the case's curve and the members that read the
    /// engine's results: <c>tools/vectors/gen/instruments/bond.py</c>'s engine outputs.
    /// </summary>
    /// <param name="check">The file's checker.</param>
    /// <param name="c">The case.</param>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The case's evaluation context.</param>
    public static void CheckEngine(VectorAssert check, VectorCase c, Bond bond, EvaluationContext context)
    {
        FlatForward curve = CashFlowInputs.CurveOf(c.Input("curve"));
        InterestRate rate = RateOf(c.Input("rate"));
        BondResults? results = null;
        Exception? error = null;
        try
        {
            results = DiscountingBondEngine.Calculate(bond, context, curve);
        }
        catch (Exception e) when (e is ArgumentException or NanoQLException)
        {
            error = e;
        }

        BondResults Results() => results ?? throw error!;
        check.Output(c, "NPV", () => Results().NPV);
        check.Output(c, "errorEstimate", () => Results().ErrorEstimate);
        check.Output(c, "valuationDate", () => Results().ValuationDate);
        check.Output(c, "settlementValue", () => Results().SettlementValue);
        check.Output(c, "cleanPriceEngine", () => bond.CleanPrice(Results(), context));
        check.Output(c, "dirtyPriceEngine", () => bond.DirtyPrice(Results(), context));
        check.Output(c, "yieldEngine", () => bond.Yield(
            Results(), rate.DayCounter, rate.Compounding, rate.Frequency, context, 1.0e-8, 100));
        check.Output(c, "settlementValueFromClean", () => bond.SettlementValue(99.0, context));
    }

    /// <summary>Each value lazily, so that a throw is checked element by element.</summary>
    /// <typeparam name="TIn">The inputs' type.</typeparam>
    /// <typeparam name="TOut">The values' type.</typeparam>
    /// <param name="xs">The inputs.</param>
    /// <param name="f">The function.</param>
    /// <returns>The values' functions.</returns>
    public static Func<object?>[] Each<TIn, TOut>(IEnumerable<TIn> xs, Func<TIn, TOut> f) => CouponInputs.Each(xs, f);

    private static (Period Period, Calendar? Calendar, BusinessDayConvention Convention, bool EndOfMonth) ExCouponOf(
        JsonElement spec)
    {
        if (spec.ValueKind == JsonValueKind.Null)
        {
            return (default, null, BusinessDayConvention.Unadjusted, false);
        }

        JsonElement period = spec.GetProperty("period");
        return (
            new Period(period[0].GetInt32(), (TimeUnit)period[1].GetInt32()),
            CalendarOf(spec.GetProperty("calendar")),
            (BusinessDayConvention)spec.GetProperty("convention").GetInt32(),
            spec.GetProperty("endOfMonth").GetBoolean());
    }

    private static double[] Doubles(JsonElement e) => [.. e.EnumerateArray().Select(IndexInputs.Real)];

    private static double?[] NullableDoubles(JsonElement e) => [.. e.EnumerateArray().Select(CouponInputs.NullableReal)];
}
