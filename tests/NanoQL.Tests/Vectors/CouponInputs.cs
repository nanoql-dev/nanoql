// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the volatilities, pricers, floating-rate coupons and Ibor legs that the coupon vector files describe in their
/// inputs, as their generators build them (<c>tools/vectors/gen/cashflows/iborcoupon.py</c>'s functions), and checks a
/// leg's per-cash-flow outputs.
/// </summary>
/// <remarks>
/// <para>
/// A volatility is <c>null</c> or a <see cref="ConstantOptionletVolatility"/> with a fixed reference date. A pricer is
/// <c>{"type": "BlackIborCouponPricer", "volatility", "timingAdjustment", "correlation", "useIndexedCoupon"}</c>, built
/// with the case's context when <c>useIndexedCoupon</c> is null; the C++ harness's files add <c>{"type":
/// "IndexFixingPricer", "adjustment"}</c>, a pricer of any floating-rate coupon. A coupon is an <c>"IborCoupon"</c>, a
/// <c>"CappedFlooredIborCoupon"</c>, a <c>"CappedFlooredCoupon"</c> over another coupon, or, in the harness's files, a
/// <c>"FloatingRateCoupon"</c>, its pricer set once it is built. A leg is an <see cref="IborLeg"/> spec, its pricer, if
/// any, set on it once it is built. Enums are QuantLib's integers, dates serial numbers, and a null setting is one left
/// at its default.
/// </para>
/// </remarks>
internal static class CouponInputs
{
    /// <summary>The caplet volatility a spec describes, or <see langword="null"/>.</summary>
    /// <param name="spec">The spec, or JSON <c>null</c>.</param>
    /// <returns>The volatility, or <see langword="null"/>.</returns>
    public static OptionletVolatilityStructure? VolatilityOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null
            ? null
            : new ConstantOptionletVolatility(
                new Date(spec.GetProperty("referenceDate").GetInt32()),
                CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
                (BusinessDayConvention)spec.GetProperty("convention").GetInt32(),
                spec.GetProperty("volatility").GetDouble(),
                CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
                (VolatilityType)spec.GetProperty("volatilityType").GetInt32(),
                spec.GetProperty("displacement").GetDouble());

    /// <summary>The pricer a spec describes, or <see langword="null"/>.</summary>
    /// <param name="spec">The spec, or JSON <c>null</c>.</param>
    /// <param name="context">The case's evaluation context, whose at-par switch a pricer without an explicit choice
    /// reads.</param>
    /// <returns>The pricer, or <see langword="null"/>.</returns>
    public static FloatingRateCouponPricer? PricerOf(JsonElement spec, EvaluationContext context)
    {
        if (spec.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        string type = spec.GetProperty("type").GetString()!;
        switch (type)
        {
            case "BlackIborCouponPricer":
                OptionletVolatilityStructure? vol = VolatilityOf(spec.GetProperty("volatility"));
                var timing = (BlackIborCouponPricerTimingAdjustment)spec.GetProperty("timingAdjustment").GetInt32();
                double correlation = spec.GetProperty("correlation").GetDouble();
                JsonElement indexed = spec.GetProperty("useIndexedCoupon");
                return indexed.ValueKind == JsonValueKind.Null
                    ? new BlackIborCouponPricer(context, vol, timing, correlation)
                    : new BlackIborCouponPricer(vol, timing, correlation, indexed.GetBoolean());
            case "IndexFixingPricer":
                return new IndexFixingPricer(spec.GetProperty("adjustment").GetDouble());
            default:
                throw new InvalidOperationException($"unknown pricer type '{type}'");
        }
    }

    /// <summary>The coupon a spec describes, with its pricer set.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The coupon.</returns>
    public static FloatingRateCoupon CouponOf(JsonElement spec, EvaluationContext context)
    {
        string type = spec.GetProperty("type").GetString()!;
        FloatingRateCoupon c;
        if (type == "CappedFlooredCoupon")
        {
            c = new CappedFlooredCoupon(
                CouponOf(spec.GetProperty("underlying"), context),
                NullableReal(spec.GetProperty("cap")),
                NullableReal(spec.GetProperty("floor")));
        }
        else
        {
            var paymentDate = new Date(spec.GetProperty("paymentDate").GetInt32());
            double nominal = IndexInputs.Real(spec.GetProperty("nominal"));
            var startDate = new Date(spec.GetProperty("startDate").GetInt32());
            var endDate = new Date(spec.GetProperty("endDate").GetInt32());
            int? fixingDays = NullableInt(spec.GetProperty("fixingDays"));
            IborIndex index = IndexInputs.IborIndexOf(spec.GetProperty("index"));
            double gearing = IndexInputs.Real(spec.GetProperty("gearing"));
            double spread = IndexInputs.Real(spec.GetProperty("spread"));
            Date? refStart = CashFlowInputs.NullableDate(spec.GetProperty("refPeriodStart"));
            Date? refEnd = CashFlowInputs.NullableDate(spec.GetProperty("refPeriodEnd"));
            DayCounter? dc = DayCounterOrNull(spec.GetProperty("dayCounter"));
            bool inArrears = spec.GetProperty("isInArrears").GetBoolean();
            Date? exCoupon = CashFlowInputs.NullableDate(spec.GetProperty("exCouponDate"));
            var fixingConvention = (BusinessDayConvention)spec.GetProperty("fixingConvention").GetInt32();
            c = type switch
            {
                "IborCoupon" => new IborCoupon(
                    paymentDate, nominal, startDate, endDate, fixingDays, index, gearing, spread, refStart, refEnd, dc,
                    inArrears, exCoupon, fixingConvention),
                "CappedFlooredIborCoupon" => new CappedFlooredIborCoupon(
                    paymentDate, nominal, startDate, endDate, fixingDays, index, gearing, spread,
                    NullableReal(spec.GetProperty("cap")), NullableReal(spec.GetProperty("floor")), refStart, refEnd,
                    dc, inArrears, exCoupon, fixingConvention),
                "FloatingRateCoupon" => new FloatingRateCoupon(
                    paymentDate, nominal, startDate, endDate, fixingDays, index, gearing, spread, refStart, refEnd, dc,
                    inArrears, exCoupon, fixingConvention),
                _ => throw new InvalidOperationException($"unknown coupon type '{type}'"),
            };
        }

        FloatingRateCouponPricer? pricer = PricerOf(spec.GetProperty("pricer"), context);
        return pricer is null ? c : c.SetPricer(pricer);
    }

    /// <summary>The schedule a leg spec describes: a dates schedule with no regularity information, or a rule-based one.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The schedule.</returns>
    public static Schedule ScheduleOf(JsonElement spec, EvaluationContext context) =>
        spec.TryGetProperty("dates", out JsonElement dates)
            ? new Schedule(dates.EnumerateArray().Select(d => new Date(d.GetInt32())))
            : CashFlowInputs.ScheduleOf(spec, context);

    /// <summary>The stub index selection a spec describes; the empty selection for JSON <c>null</c>.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The selection.</returns>
    public static StubIndexSelection SelectionOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null
            ? new StubIndexSelection()
            : new StubIndexSelection(
                (StubIndexSelectionConvention)spec.GetProperty("convention").GetInt32(),
                spec.GetProperty("indices").EnumerateArray().Select(IndexInputs.IborIndexOf));

    /// <summary>The leg an <see cref="IborLeg"/> spec describes, with the spec's pricer set on it.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The leg.</returns>
    public static ImmutableArray<CashFlow> LegOf(JsonElement spec, EvaluationContext context)
    {
        // A one-element setting goes through the single-value setter, which QuantLib's stores as that vector.
        double[] notionals = Reals(spec.GetProperty("notionals"));
        int?[] fixingDays = [.. spec.GetProperty("fixingDays").EnumerateArray().Select(NullableInt)];
        double[] gearings = Reals(spec.GetProperty("gearings"));
        double[] spreads = Reals(spec.GetProperty("spreads"));
        double?[] caps = [.. spec.GetProperty("caps").EnumerateArray().Select(NullableReal)];
        double?[] floors = [.. spec.GetProperty("floors").EnumerateArray().Select(NullableReal)];
        IborLeg builder = new IborLeg(
                ScheduleOf(spec.GetProperty("schedule"), context),
                IndexInputs.IborIndexOf(spec.GetProperty("index")))
            .WithPaymentAdjustment((BusinessDayConvention)spec.GetProperty("paymentConvention").GetInt32());
        builder = notionals.Length == 1 ? builder.WithNotionals(notionals[0]) : builder.WithNotionals(notionals);
        builder = fixingDays.Length == 1 ? builder.WithFixingDays(fixingDays[0]) : builder.WithFixingDays(fixingDays);
        builder = gearings.Length == 1 ? builder.WithGearings(gearings[0]) : builder.WithGearings(gearings);
        builder = spreads.Length == 1 ? builder.WithSpreads(spreads[0]) : builder.WithSpreads(spreads);
        builder = caps is [double cap] ? builder.WithCaps(cap) : builder.WithCaps(caps);
        builder = floors is [double floor] ? builder.WithFloors(floor) : builder.WithFloors(floors);
        builder = builder
            .InArrears(spec.GetProperty("isInArrears").GetBoolean())
            .WithPaymentLag(spec.GetProperty("paymentLag").GetInt32())
            .WithIndexedCoupons(NullableBool(spec.GetProperty("withIndexedCoupons")))
            .WithStubIndexSelection(SelectionOf(spec.GetProperty("stubIndexSelection")));
        if (DayCounterOrNull(spec.GetProperty("paymentDayCounter")) is DayCounter dc)
        {
            builder = builder.WithPaymentDayCounter(dc);
        }

        if (spec.GetProperty("paymentCalendar") is { ValueKind: not JsonValueKind.Null } cal)
        {
            builder = builder.WithPaymentCalendar(CashFlowInputs.CalendarOf(cal));
        }

        if (spec.GetProperty("exCouponPeriod") is { ValueKind: not JsonValueKind.Null } ex)
        {
            JsonElement period = ex.GetProperty("period");
            builder = builder.WithExCouponPeriod(
                new Period(period[0].GetInt32(), (TimeUnit)period[1].GetInt32()),
                CashFlowInputs.CalendarOf(ex.GetProperty("calendar")),
                (BusinessDayConvention)ex.GetProperty("convention").GetInt32(),
                ex.GetProperty("endOfMonth").GetBoolean());
        }

        if (spec.TryGetProperty("zeroPayments", out JsonElement zero))
        {
            builder = builder.WithZeroPayments(zero.GetBoolean());
        }

        if (spec.TryGetProperty("fixingConvention", out JsonElement fixingConvention))
        {
            builder = builder.WithFixingConvention((BusinessDayConvention)fixingConvention.GetInt32());
        }

        if (spec.TryGetProperty("atParCoupons", out JsonElement atParCoupons))
        {
            builder = builder.WithAtParCoupons(atParCoupons.GetBoolean());
        }

        ImmutableArray<CashFlow> leg = builder.ToLeg(context);
        FloatingRateCouponPricer? pricer = PricerOf(spec.GetProperty("pricer"), context);
        return pricer is null ? leg : CouponPricerFunctions.SetCouponPricer(leg, pricer);
    }

    /// <summary>
    /// Compares a leg's per-cash-flow outputs, as <c>leg_outputs</c> writes them: dates, nominal, rate and amount, and a
    /// floating coupon's fixing date, fixing days, gearing, spread and whether it holds a pricer (null for a fixed-rate
    /// coupon).
    /// </summary>
    /// <param name="check">The file's comparison.</param>
    /// <param name="c">The case.</param>
    /// <param name="leg">The leg.</param>
    /// <param name="context">The case's evaluation context.</param>
    public static void CheckLeg(VectorAssert check, VectorCase c, ImmutableArray<CashFlow> leg, EvaluationContext context)
    {
        Coupon[] coupons = [.. leg.Select(cf => (Coupon)cf)];
        check.Output(c, "date", Each(coupons, x => x.Date));
        check.Output(c, "nominal", Each(coupons, x => x.Nominal(context)));
        check.Output(c, "accrualStartDate", Each(coupons, x => x.AccrualStartDate));
        check.Output(c, "accrualEndDate", Each(coupons, x => x.AccrualEndDate));
        check.Output(c, "referencePeriodStart", Each(coupons, x => x.ReferencePeriodStart));
        check.Output(c, "referencePeriodEnd", Each(coupons, x => x.ReferencePeriodEnd));
        check.Output(c, "exCouponDate", Each(coupons, x => x.ExCouponDate));
        check.Output(c, "fixingDate", EachFloating(coupons, x => x.FixingDate()));
        check.Output(c, "fixingDays", EachFloating(coupons, x => x.FixingDays));
        check.Output(c, "gearing", EachFloating(coupons, x => x.Gearing));
        check.Output(c, "spread", EachFloating(coupons, x => x.Spread));
        check.Output(c, "hasPricer", EachFloating(coupons, x => x.Pricer is not null));
        check.Output(c, "rate", Each(coupons, x => x.Rate(context)));
        check.Output(c, "amount", Each(coupons, x => x.Amount(context)));
    }

    /// <summary>One lazily computed output per coupon, so that a throw is checked per element.</summary>
    /// <typeparam name="TIn">The element type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="xs">The elements.</param>
    /// <param name="f">The output of an element.</param>
    /// <returns>The outputs.</returns>
    public static Func<object?>[] Each<TIn, TOut>(IEnumerable<TIn> xs, Func<TIn, TOut> f) =>
        xs.Select(x => (Func<object?>)(() => f(x))).ToArray();

    /// <summary>A JSON integer, or <see langword="null"/>.</summary>
    /// <param name="e">The JSON value.</param>
    /// <returns>The integer, or <see langword="null"/>.</returns>
    public static int? NullableInt(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetInt32();

    /// <summary>A JSON Boolean, or <see langword="null"/>.</summary>
    /// <param name="e">The JSON value.</param>
    /// <returns>The Boolean, or <see langword="null"/>.</returns>
    public static bool? NullableBool(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetBoolean();

    /// <summary>A real, written as a number or a named literal, or <see langword="null"/>.</summary>
    /// <param name="e">The JSON value.</param>
    /// <returns>The real, or <see langword="null"/>.</returns>
    public static double? NullableReal(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : IndexInputs.Real(e);

    /// <summary>An array of reals, each a number or a named literal.</summary>
    /// <param name="e">The JSON array.</param>
    /// <returns>The reals.</returns>
    public static double[] Reals(JsonElement e) => e.EnumerateArray().Select(IndexInputs.Real).ToArray();

    /// <summary>A day counter, or <see langword="null"/>, QuantLib's empty one.</summary>
    /// <param name="spec">The spec, or JSON <c>null</c>.</param>
    /// <returns>The day counter, or <see langword="null"/>.</returns>
    public static DayCounter? DayCounterOrNull(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.DayCounterOf(spec);

    private static Func<object?>[] EachFloating<TOut>(Coupon[] coupons, Func<FloatingRateCoupon, TOut> f) =>
        coupons.Select(x => (Func<object?>)(() => x is FloatingRateCoupon fc ? f(fc) : null)).ToArray();

    /// <summary>
    /// The C++ harness's pricer of any floating-rate coupon: the geared index fixing, shifted by a constant
    /// adjustment, plus the spread; its caplet and floorlet rates are their intrinsic values on the index fixing, and
    /// its prices the rates times the accrual period, undiscounted.
    /// </summary>
    /// <param name="adjustment">The adjustment added to the index fixing.</param>
    private sealed class IndexFixingPricer(double adjustment) : FloatingRateCouponPricer
    {
        public override double SwapletPrice(FloatingRateCoupon coupon, EvaluationContext context) =>
            SwapletRate(coupon, context) * coupon.AccrualPeriod();

        public override double SwapletRate(FloatingRateCoupon coupon, EvaluationContext context) =>
            (coupon.Gearing * (coupon.IndexFixing(context) + adjustment)) + coupon.Spread;

        public override double CapletPrice(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) =>
            CapletRate(coupon, effectiveCap, context) * coupon.AccrualPeriod();

        public override double CapletRate(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) =>
            coupon.Gearing * Positive(coupon.IndexFixing(context) - effectiveCap);

        public override double FloorletPrice(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) =>
            FloorletRate(coupon, effectiveFloor, context) * coupon.AccrualPeriod();

        public override double FloorletRate(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) =>
            coupon.Gearing * Positive(effectiveFloor - coupon.IndexFixing(context));

        // std::max(x, 0.0)
        private static double Positive(double x) => x < 0.0 ? 0.0 : x;
    }
}
