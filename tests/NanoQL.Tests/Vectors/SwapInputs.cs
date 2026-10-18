// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the indexes, schedules and swaps that the swap vector files describe in their inputs, as their generators
/// build them (<c>tools/vectors/gen/instruments/fixedvsfloatingswap.py</c>'s functions), and checks a swap's
/// inspectors, legs and results.
/// </summary>
/// <remarks>
/// <para>
/// An index is a spec of <see cref="IndexInputs"/>; the same with a <c>"currency"</c> code, built with that currency;
/// or a preset, <c>{"type": &lt;class&gt;, "curve"}</c>, with a <c>"tenor"</c> for <c>Euribor</c>, <c>Euribor365</c>
/// and <c>Tibor</c>. A curve is <c>null</c> or a flat forward spec, as <see cref="CashFlowInputs.CurveOf"/> reads it. A
/// schedule is a rule-based spec, as <see cref="CashFlowInputs.ScheduleOf"/> reads it, or <c>{"dates", "calendar"}</c>.
/// </para>
/// <para>
/// A vanilla swap is <c>{"kind": "VanillaSwap", "type", "nominal", "fixedSchedule", "fixedRate", "fixedDayCount",
/// "floatSchedule", "index", "spread", "floatingDayCount", "paymentConvention", "useIndexedCoupons"}</c>, built with
/// the case's context. An overnight-indexed swap is <c>{"kind": "OvernightIndexedSwap", "form", "type",
/// "fixedNominals", "fixedSchedule", "fixedRate", "fixedDC", "overnightNominals", "overnightSchedule", "index",
/// "spread", "paymentLag", "paymentAdjustment", "paymentCalendar", "telescopicValueDates", "averagingMethod",
/// "lookbackDays", "lockoutDays", "applyObservationShift", "roundingPrecision"}</c>, its form naming the constructor:
/// <c>"nominal"</c>, <c>"nominals"</c>, <c>"nominal, two schedules"</c> or <c>"nominals, two schedules"</c>. Enums are
/// QuantLib's integers, dates serial numbers, and a null setting is the constructor's default.
/// </para>
/// </remarks>
internal static class SwapInputs
{
    /// <summary>The index a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The index.</returns>
    public static IborIndex IndexOf(JsonElement spec)
    {
        string type = spec.GetProperty("type").GetString()!;
        YieldTermStructure? curve = spec.TryGetProperty("curve", out JsonElement c) ? IndexInputs.CurveOf(c) : null;
        switch (type)
        {
            case "Euribor":
                return new Euribor(Tenor(spec), curve);
            case "Euribor1W":
                return new Euribor1W(curve);
            case "Euribor1M":
                return new Euribor1M(curve);
            case "Euribor3M":
                return new Euribor3M(curve);
            case "Euribor6M":
                return new Euribor6M(curve);
            case "Euribor1Y":
                return new Euribor1Y(curve);
            case "Euribor365":
                return new Euribor365(Tenor(spec), curve);
            case "Sofr":
                return new Sofr(curve);
            case "Estr":
                return new Estr(curve);
            case "Sonia":
                return new Sonia(curve);
            case "Tonar":
                return new Tonar(curve);
            case "Tibor":
                return new Tibor(Tenor(spec), curve);
            case "Corra":
                return new Corra(curve);
            default:
                break;
        }

        if (!spec.TryGetProperty("currency", out JsonElement code))
        {
            return IndexInputs.IborIndexOf(spec);
        }

        Currency currency = CurrencyOf(code.GetString()!);
        string familyName = spec.GetProperty("familyName").GetString()!;
        int settlementDays = spec.GetProperty("settlementDays").GetInt32();
        Calendar calendar = CashFlowInputs.CalendarOf(spec.GetProperty("fixingCalendar"));
        DayCounter dayCounter = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        return type switch
        {
            "IborIndex" => new IborIndex(
                familyName,
                Tenor(spec),
                settlementDays,
                currency,
                calendar,
                (BusinessDayConvention)spec.GetProperty("convention").GetInt32(),
                spec.GetProperty("endOfMonth").GetBoolean(),
                dayCounter,
                curve),
            "OvernightIndex" => new OvernightIndex(familyName, settlementDays, currency, calendar, dayCounter, curve),
            _ => throw new InvalidOperationException($"unknown index type '{type}'"),
        };
    }

    /// <summary>The overnight index a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The index.</returns>
    public static OvernightIndex OvernightIndexOf(JsonElement spec) => (OvernightIndex)IndexOf(spec);

    /// <summary>The currency a code names.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The currency.</returns>
    public static Currency CurrencyOf(string code) => code switch
    {
        "EUR" => new EURCurrency(),
        "USD" => new USDCurrency(),
        "GBP" => new GBPCurrency(),
        "CHF" => new CHFCurrency(),
        "SEK" => new SEKCurrency(),
        "JPY" => new JPYCurrency(),
        "AUD" => new AUDCurrency(),
        "HKD" => new HKDCurrency(),
        "THB" => new THBCurrency(),
        "CAD" => new CADCurrency(),
        "NZD" => new NZDCurrency(),
        _ => throw new InvalidOperationException($"unknown currency '{code}'"),
    };

    /// <summary>The schedule a spec describes: rule-based, or a list of dates on a calendar.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The schedule.</returns>
    public static Schedule ScheduleOf(JsonElement spec, EvaluationContext context) =>
        spec.TryGetProperty("dates", out JsonElement dates)
            ? new Schedule(
                dates.EnumerateArray().Select(d => new Date(d.GetInt32())),
                CashFlowInputs.CalendarOf(spec.GetProperty("calendar")))
            : CashFlowInputs.ScheduleOf(spec, context);

    /// <summary>The swap a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context, which a vanilla swap's floating leg reads.</param>
    /// <returns>The swap.</returns>
    public static FixedVsFloatingSwap SwapOf(JsonElement spec, EvaluationContext context) =>
        spec.GetProperty("kind").GetString() switch
        {
            "VanillaSwap" => VanillaSwapOf(spec, context),
            "OvernightIndexedSwap" => OvernightIndexedSwapOf(spec, context),
            string kind => throw new InvalidOperationException($"unknown swap kind '{kind}'"),
            null => throw new InvalidOperationException("a swap spec needs its kind"),
        };

    /// <summary>The vanilla swap a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The swap.</returns>
    public static VanillaSwap VanillaSwapOf(JsonElement spec, EvaluationContext context)
    {
        JsonElement convention = spec.GetProperty("paymentConvention");
        return new VanillaSwap(
            (SwapType)spec.GetProperty("type").GetInt32(),
            IndexInputs.Real(spec.GetProperty("nominal")),
            ScheduleOf(spec.GetProperty("fixedSchedule"), context),
            IndexInputs.Real(spec.GetProperty("fixedRate")),
            CouponInputs.DayCounterOrNull(spec.GetProperty("fixedDayCount")),
            ScheduleOf(spec.GetProperty("floatSchedule"), context),
            IndexOf(spec.GetProperty("index")),
            IndexInputs.Real(spec.GetProperty("spread")),
            CouponInputs.DayCounterOrNull(spec.GetProperty("floatingDayCount")),
            context,
            convention.ValueKind == JsonValueKind.Null ? null : (BusinessDayConvention)convention.GetInt32(),
            CouponInputs.NullableBool(spec.GetProperty("useIndexedCoupons")));
    }

    /// <summary>The overnight-indexed swap a spec describes, through the constructor its form names.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context, which only the schedules read.</param>
    /// <returns>The swap.</returns>
    public static OvernightIndexedSwap OvernightIndexedSwapOf(JsonElement spec, EvaluationContext context)
    {
        var type = (SwapType)spec.GetProperty("type").GetInt32();
        double[] fixedNominals = CouponInputs.Reals(spec.GetProperty("fixedNominals"));
        double[] overnightNominals = CouponInputs.Reals(spec.GetProperty("overnightNominals"));
        Schedule fixedSchedule = ScheduleOf(spec.GetProperty("fixedSchedule"), context);
        Schedule overnightSchedule = ScheduleOf(spec.GetProperty("overnightSchedule"), context);
        double fixedRate = IndexInputs.Real(spec.GetProperty("fixedRate"));
        DayCounter? fixedDC = CouponInputs.DayCounterOrNull(spec.GetProperty("fixedDC"));
        OvernightIndex index = OvernightIndexOf(spec.GetProperty("index"));
        double spread = IndexInputs.Real(spec.GetProperty("spread"));
        int paymentLag = spec.GetProperty("paymentLag").GetInt32();
        var paymentAdjustment = (BusinessDayConvention)spec.GetProperty("paymentAdjustment").GetInt32();
        JsonElement calendar = spec.GetProperty("paymentCalendar");
        Calendar? paymentCalendar =
            calendar.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CalendarOf(calendar);
        bool telescopic = spec.GetProperty("telescopicValueDates").GetBoolean();
        var averaging = (RateAveragingType)spec.GetProperty("averagingMethod").GetInt32();
        int? lookback = CouponInputs.NullableInt(spec.GetProperty("lookbackDays"));
        int lockout = spec.GetProperty("lockoutDays").GetInt32();
        bool shift = spec.GetProperty("applyObservationShift").GetBoolean();
        int? rounding = CouponInputs.NullableInt(spec.GetProperty("roundingPrecision"));
        string form = spec.GetProperty("form").GetString()!;
        return form switch
        {
            "nominal" => new OvernightIndexedSwap(
                type, fixedNominals[0], fixedSchedule, fixedRate, fixedDC, index, spread, paymentLag,
                paymentAdjustment, paymentCalendar, telescopic, averaging, lookback, lockout, shift, rounding),
            "nominals" => new OvernightIndexedSwap(
                type, fixedNominals, fixedSchedule, fixedRate, fixedDC, index, spread, paymentLag,
                paymentAdjustment, paymentCalendar, telescopic, averaging, lookback, lockout, shift, rounding),
            "nominal, two schedules" => new OvernightIndexedSwap(
                type, fixedNominals[0], fixedSchedule, fixedRate, fixedDC, overnightSchedule, index, spread,
                paymentLag, paymentAdjustment, paymentCalendar, telescopic, averaging, lookback, lockout, shift,
                rounding),
            "nominals, two schedules" => new OvernightIndexedSwap(
                type, fixedNominals, fixedSchedule, fixedRate, fixedDC, overnightNominals, overnightSchedule, index,
                spread, paymentLag, paymentAdjustment, paymentCalendar, telescopic, averaging, lookback, lockout,
                shift, rounding),
            _ => throw new InvalidOperationException($"unknown form '{form}'"),
        };
    }

    /// <summary>Checks the inspectors of <see cref="FixedVsFloatingSwap"/>, as <c>inspectors</c> writes them.</summary>
    /// <param name="check">The file's comparison.</param>
    /// <param name="c">The case.</param>
    /// <param name="swap">The swap.</param>
    public static void CheckInspectors(VectorAssert check, VectorCase c, FixedVsFloatingSwap swap)
    {
        check.Output(c, "type", swap.Type);
        check.Output(c, "nominal", () => swap.Nominal());
        check.Output(c, "nominals", () => swap.Nominals());
        check.Output(c, "fixedNominals", swap.FixedNominals);
        check.Output(c, "floatingNominals", swap.FloatingNominals);
        check.Output(c, "fixedSchedule", swap.FixedSchedule.Dates);
        check.Output(c, "floatingSchedule", swap.FloatingSchedule.Dates);
        check.Output(c, "fixedRate", swap.FixedRate);
        check.Output(c, "fixedDayCount", swap.FixedDayCount.Name);
        check.Output(c, "spread", swap.Spread);
        check.Output(c, "floatingDayCount", swap.FloatingDayCount?.Name);
        check.Output(c, "paymentConvention", swap.PaymentConvention);
        check.Output(c, "paymentLag", swap.PaymentLag);
        check.Output(c, "paymentCalendar", swap.PaymentCalendar?.Name);
        check.Output(c, "iborIndex", swap.IborIndex.Name);
    }

    /// <summary>
    /// Checks a leg's outputs, as <c>leg_outputs</c> writes them: per cash flow its amount, and its payment date, its
    /// accrual dates, nominal and rate when the case holds them.
    /// </summary>
    /// <param name="check">The file's comparison.</param>
    /// <param name="c">The case.</param>
    /// <param name="prefix">The outputs' prefix.</param>
    /// <param name="leg">The leg.</param>
    /// <param name="context">The case's evaluation context.</param>
    public static void CheckLeg(
        VectorAssert check, VectorCase c, string prefix, ImmutableArray<CashFlow> leg, EvaluationContext context)
    {
        Coupon[] coupons = [.. leg.Select(cf => (Coupon)cf)];
        if (c.Expected.TryGetProperty(prefix + "Dates", out _))
        {
            check.Output(c, prefix + "Dates", coupons.Select(x => x.Date).ToArray());
        }

        if (c.Expected.TryGetProperty(prefix + "Rates", out _))
        {
            check.Output(c, prefix + "AccrualStartDates", coupons.Select(x => x.AccrualStartDate).ToArray());
            check.Output(c, prefix + "AccrualEndDates", coupons.Select(x => x.AccrualEndDate).ToArray());
            check.Output(c, prefix + "Nominals", coupons.Select(x => x.Nominal(context)).ToArray());
            check.Output(c, prefix + "Rates", CouponInputs.Each(coupons, x => x.Rate(context)));
        }

        check.Output(c, prefix + "Amounts", CouponInputs.Each(coupons, x => x.Amount(context)));
    }

    /// <summary>
    /// Checks a swap's results, as <c>results</c> writes them: the NPV, the per-leg NPV and BPS, the fair rate and
    /// spread and, when the case holds them, the four leg accessors; or a refused pricing, the one output
    /// <c>calculate</c>.
    /// </summary>
    /// <param name="check">The file's comparison.</param>
    /// <param name="c">The case.</param>
    /// <param name="price">Prices the swap.</param>
    /// <param name="prefix">The outputs' prefix, empty for none.</param>
    /// <returns>The results, or <see langword="null"/> when the pricing was refused.</returns>
    public static FixedVsFloatingSwapResults? CheckResults(
        VectorAssert check, VectorCase c, Func<FixedVsFloatingSwapResults> price, string prefix = "")
    {
        string Name(string n) => prefix.Length == 0 ? n : prefix + char.ToUpperInvariant(n[0]) + n[1..];
        if (c.Expected.TryGetProperty(Name("calculate"), out _))
        {
            check.Output(c, Name("calculate"), () => price());
            return null;
        }

        // Priced once; a throw is reported on every output.
        FixedVsFloatingSwapResults? results = null;
        Exception? error = null;
        try
        {
            results = price();
        }
        catch (Exception e) when (e is ArgumentException or NanoQLException)
        {
            error = e;
        }

        FixedVsFloatingSwapResults Results() => results ?? throw error!;
        check.Output(c, Name("NPV"), () => Results().NPV);
        check.Output(c, Name("legNPV"), () => Results().LegNPV);
        check.Output(c, Name("legBPS"), () => Results().LegBPS);
        check.Output(c, Name("fairRate"), () => Results().FairRate);
        check.Output(c, Name("fairSpread"), () => Results().FairSpread);
        if (c.Expected.TryGetProperty(Name("fixedLegBPS"), out _))
        {
            check.Output(c, Name("fixedLegBPS"), () => Results().FixedLegBPS);
            check.Output(c, Name("fixedLegNPV"), () => Results().FixedLegNPV);
            check.Output(c, Name("floatingLegBPS"), () => Results().FloatingLegBPS);
            check.Output(c, Name("floatingLegNPV"), () => Results().FloatingLegNPV);
        }

        return results;
    }

    /// <summary>
    /// The builder a <c>"build"</c> spec describes, as QuantLib-Python's <c>MakeVanillaSwap</c> builds it: the fixed
    /// rate and the forward start first, then each setter in order.
    /// </summary>
    /// <param name="spec">The spec: <c>{"swapTenor", "index", "fixedRate", "forwardStart", "setters"}</c>.</param>
    /// <returns>The builder.</returns>
    public static MakeVanillaSwap MakeVanillaSwapOf(JsonElement spec)
    {
        MakeVanillaSwap b = new MakeVanillaSwap(
                PeriodOf(spec.GetProperty("swapTenor")), IndexOf(spec.GetProperty("index")))
            .WithFixedRate(CouponInputs.NullableReal(spec.GetProperty("fixedRate")))
            .WithForwardStart(PeriodOf(spec.GetProperty("forwardStart")));
        foreach (JsonElement setter in spec.GetProperty("setters").EnumerateArray())
        {
            JsonElement x = setter[1];
            b = setter[0].GetString() switch
            {
                "receiveFixed" => b.ReceiveFixed(x.GetBoolean()),
                "swapType" => b.WithType((SwapType)x.GetInt32()),
                "nominal" => b.WithNominal(IndexInputs.Real(x)),
                "settlementDays" => b.WithSettlementDays(x.GetInt32()),
                "settlementCalendar" => b.WithSettlementCalendar(CashFlowInputs.CalendarOf(x)),
                "effectiveDate" => b.WithEffectiveDate(new Date(x.GetInt32())),
                "terminationDate" => b.WithTerminationDate(new Date(x.GetInt32())),
                "dateGenerationRule" => b.WithRule((DateGenerationRule)x.GetInt32()),
                "paymentConvention" => b.WithPaymentConvention((BusinessDayConvention)x.GetInt32()),
                "fixedLegTenor" => b.WithFixedLegTenor(PeriodOf(x)),
                "fixedLegCalendar" => b.WithFixedLegCalendar(CashFlowInputs.CalendarOf(x)),
                "fixedLegConvention" => b.WithFixedLegConvention((BusinessDayConvention)x.GetInt32()),
                "fixedLegTerminationDateConvention" =>
                    b.WithFixedLegTerminationDateConvention((BusinessDayConvention)x.GetInt32()),
                "fixedLegDateGenRule" => b.WithFixedLegRule((DateGenerationRule)x.GetInt32()),
                "fixedLegEndOfMonth" => b.WithFixedLegEndOfMonth(x.GetBoolean()),
                "fixedLegFirstDate" => b.WithFixedLegFirstDate(new Date(x.GetInt32())),
                "fixedLegNextToLastDate" => b.WithFixedLegNextToLastDate(new Date(x.GetInt32())),
                "fixedLegDayCount" => b.WithFixedLegDayCount(CashFlowInputs.DayCounterOf(x)),
                "floatingLegTenor" => b.WithFloatingLegTenor(PeriodOf(x)),
                "floatingLegCalendar" => b.WithFloatingLegCalendar(CashFlowInputs.CalendarOf(x)),
                "floatingLegConvention" => b.WithFloatingLegConvention((BusinessDayConvention)x.GetInt32()),
                "floatingLegTerminationDateConvention" =>
                    b.WithFloatingLegTerminationDateConvention((BusinessDayConvention)x.GetInt32()),
                "floatingLegDateGenRule" => b.WithFloatingLegRule((DateGenerationRule)x.GetInt32()),
                "floatingLegEndOfMonth" => b.WithFloatingLegEndOfMonth(x.GetBoolean()),
                "floatingLegFirstDate" => b.WithFloatingLegFirstDate(new Date(x.GetInt32())),
                "floatingLegNextToLastDate" => b.WithFloatingLegNextToLastDate(new Date(x.GetInt32())),
                "floatingLegDayCount" => b.WithFloatingLegDayCount(CashFlowInputs.DayCounterOf(x)),
                "floatingLegSpread" => b.WithFloatingLegSpread(IndexInputs.Real(x)),
                "discountingTermStructure" => b.WithDiscountingTermStructure(CashFlowInputs.CurveOf(x)),
                "pricingEngine" => b.WithPricingEngine(EngineOf(x)),
                "withIndexedCoupons" => b.WithIndexedCoupons(x.GetBoolean()),
                "atParCoupons" => b.WithAtParCoupons(x.GetBoolean()),
                "maturityEndOfMonth" => b.WithMaturityEndOfMonth(x.GetBoolean()),
                string name => throw new InvalidOperationException($"unknown MakeVanillaSwap setter '{name}'"),
                null => throw new InvalidOperationException("a setter needs its name"),
            };
        }

        return b;
    }

    /// <summary>
    /// The builder a <c>"build"</c> spec describes, as QuantLib-Python's <c>MakeOIS</c> builds it: the fixed rate and
    /// the forward start first, then each setter in order.
    /// </summary>
    /// <param name="spec">The spec: <c>{"swapTenor", "index", "fixedRate", "forwardStart", "setters"}</c>.</param>
    /// <returns>The builder.</returns>
    public static MakeOIS MakeOISOf(JsonElement spec)
    {
        MakeOIS b = new MakeOIS(PeriodOf(spec.GetProperty("swapTenor")), OvernightIndexOf(spec.GetProperty("index")))
            .WithFixedRate(CouponInputs.NullableReal(spec.GetProperty("fixedRate")))
            .WithForwardStart(PeriodOf(spec.GetProperty("forwardStart")));
        foreach (JsonElement setter in spec.GetProperty("setters").EnumerateArray())
        {
            JsonElement x = setter[1];
            b = setter[0].GetString() switch
            {
                "receiveFixed" => b.ReceiveFixed(x.GetBoolean()),
                "swapType" => b.WithType((SwapType)x.GetInt32()),
                "nominal" => b.WithNominal(IndexInputs.Real(x)),
                "settlementDays" => b.WithSettlementDays(x.GetInt32()),
                "settlementCalendar" => b.WithSettlementCalendar(CashFlowInputs.CalendarOf(x)),
                "effectiveDate" => b.WithEffectiveDate(new Date(x.GetInt32())),
                "terminationDate" => b.WithTerminationDate(new Date(x.GetInt32())),
                "dateGenerationRule" => b.WithRule((DateGenerationRule)x.GetInt32()),
                "fixedLegRule" => b.WithFixedLegRule((DateGenerationRule)x.GetInt32()),
                "overnightLegRule" => b.WithOvernightLegRule((DateGenerationRule)x.GetInt32()),
                "paymentFrequency" => b.WithPaymentFrequency((Frequency)x.GetInt32()),
                "fixedLegPaymentFrequency" => b.WithFixedLegPaymentFrequency((Frequency)x.GetInt32()),
                "overnightLegPaymentFrequency" => b.WithOvernightLegPaymentFrequency((Frequency)x.GetInt32()),
                "paymentAdjustmentConvention" => b.WithPaymentAdjustment((BusinessDayConvention)x.GetInt32()),
                "paymentLag" => b.WithPaymentLag(x.GetInt32()),
                "paymentCalendar" => b.WithPaymentCalendar(CashFlowInputs.CalendarOf(x)),
                "calendar" => b.WithCalendar(CashFlowInputs.CalendarOf(x)),
                "fixedLegCalendar" => b.WithFixedLegCalendar(CashFlowInputs.CalendarOf(x)),
                "overnightLegCalendar" => b.WithOvernightLegCalendar(CashFlowInputs.CalendarOf(x)),
                "convention" => b.WithConvention((BusinessDayConvention)x.GetInt32()),
                "fixedLegConvention" => b.WithFixedLegConvention((BusinessDayConvention)x.GetInt32()),
                "overnightLegConvention" => b.WithOvernightLegConvention((BusinessDayConvention)x.GetInt32()),
                "terminationDateConvention" =>
                    b.WithTerminationDateConvention((BusinessDayConvention)x.GetInt32()),
                "fixedLegTerminationDateConvention" =>
                    b.WithFixedLegTerminationDateConvention((BusinessDayConvention)x.GetInt32()),
                "overnightLegTerminationDateConvention" =>
                    b.WithOvernightLegTerminationDateConvention((BusinessDayConvention)x.GetInt32()),
                "endOfMonth" => b.WithEndOfMonth(x.GetBoolean()),
                "maturityEndOfMonth" => b.WithMaturityEndOfMonth(x.GetBoolean()),
                "fixedLegEndOfMonth" => b.WithFixedLegEndOfMonth(x.GetBoolean()),
                "overnightLegEndOfMonth" => b.WithOvernightLegEndOfMonth(x.GetBoolean()),
                "fixedLegDayCount" => b.WithFixedLegDayCount(CashFlowInputs.DayCounterOf(x)),
                "overnightLegSpread" => b.WithOvernightLegSpread(IndexInputs.Real(x)),
                "discountingTermStructure" => b.WithDiscountingTermStructure(CashFlowInputs.CurveOf(x)),
                "telescopicValueDates" => b.WithTelescopicValueDates(x.GetBoolean()),
                "averagingMethod" => b.WithAveragingMethod((RateAveragingType)x.GetInt32()),
                "lookbackDays" => b.WithLookbackDays(x.GetInt32()),
                "lockoutDays" => b.WithLockoutDays(x.GetInt32()),
                "applyObservationShift" => b.WithObservationShift(x.GetBoolean()),
                "roundingPrecision" => b.WithRoundingPrecision(x.GetInt32()),
                "pricingEngine" => b.WithPricingEngine(EngineOf(x)),
                string name => throw new InvalidOperationException($"unknown MakeOIS setter '{name}'"),
                null => throw new InvalidOperationException("a setter needs its name"),
            };
        }

        return b;
    }

    /// <summary>
    /// The engine QuantLib's builder attaches to the swap it builds: the last one given, as an engine or a discounting
    /// term structure, or, with neither, a <see cref="DiscountingSwapEngine"/> on the index's forecasting curve without
    /// the settlement date's flows, which prices nothing when the index has no curve.
    /// </summary>
    /// <param name="spec">The <c>"build"</c> spec.</param>
    /// <param name="index">The index the swap was built on.</param>
    /// <returns>The engine.</returns>
    public static Func<FixedVsFloatingSwap, EvaluationContext, FixedVsFloatingSwapResults> AttachedEngineOf(
        JsonElement spec, IborIndex index)
    {
        Func<FixedVsFloatingSwap, EvaluationContext, FixedVsFloatingSwapResults>? chosen = null;
        foreach (JsonElement setter in spec.GetProperty("setters").EnumerateArray())
        {
            switch (setter[0].GetString())
            {
                case "discountingTermStructure":
                    FlatForward curve = CashFlowInputs.CurveOf(setter[1]);
                    chosen = (s, c) => DiscountingSwapEngine.Calculate(s, c, curve, includeSettlementDateFlows: false);
                    break;
                case "pricingEngine":
                    chosen = EngineOf(setter[1]);
                    break;
                default:
                    break;
            }
        }

        return chosen ?? ((s, c) => DiscountingSwapEngine.Calculate(
            s,
            c,
            index.ForwardingTermStructure
                ?? throw new NanoQLException("discounting term structure handle is empty"),
            includeSettlementDateFlows: false));
    }

    /// <summary>
    /// The engine an engine spec describes, <c>{"type": "DiscountingSwapEngine", "curve",
    /// "includeSettlementDateFlows"}</c>, as a builder's <c>WithPricingEngine</c> takes it.
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The engine.</returns>
    public static Func<FixedVsFloatingSwap, EvaluationContext, FixedVsFloatingSwapResults> EngineOf(JsonElement spec)
    {
        if (spec.GetProperty("type").GetString() != "DiscountingSwapEngine")
        {
            throw new InvalidOperationException($"unknown engine '{spec.GetProperty("type").GetString()}'");
        }

        FlatForward curve = CashFlowInputs.CurveOf(spec.GetProperty("curve"));
        bool? include = CouponInputs.NullableBool(spec.GetProperty("includeSettlementDateFlows"));
        return (s, c) => DiscountingSwapEngine.Calculate(s, c, curve, include);
    }

    /// <summary>A period written as <c>[length, units]</c>.</summary>
    /// <param name="e">The JSON value.</param>
    /// <returns>The period.</returns>
    public static Period PeriodOf(JsonElement e) => new(e[0].GetInt32(), (TimeUnit)e[1].GetInt32());

    private static Period Tenor(JsonElement spec) => PeriodOf(spec.GetProperty("tenor"));
}
