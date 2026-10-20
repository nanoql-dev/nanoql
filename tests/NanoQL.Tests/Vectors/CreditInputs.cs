// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The credit vectors' shared inputs, as <c>tools/vectors/gen/termstructures/defaulttermstructure.py</c> writes them:
/// calendars (WeekendsOnly beside <see cref="CashFlowInputs"/>'s), credit curves, discount curves and credit default
/// swaps.
/// </summary>
internal static class CreditInputs
{
    /// <summary>The calendar a spec describes: WeekendsOnly, or one of <see cref="CashFlowInputs"/>'s.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The calendar.</returns>
    public static Calendar CalendarOf(JsonElement spec) =>
        spec.GetProperty("type").GetString() == "WeekendsOnly" ? new WeekendsOnly() : CashFlowInputs.CalendarOf(spec);

    /// <summary>The calendar a spec describes, or <see langword="null"/>, QuantLib's empty calendar, for JSON
    /// null.</summary>
    /// <param name="spec">The spec, or JSON <c>null</c>.</param>
    /// <returns>The calendar, or <see langword="null"/>.</returns>
    public static Calendar? NullableCalendarOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null ? null : CalendarOf(spec);

    /// <summary>The day counter a spec describes: one of <see cref="CashFlowInputs"/>'s, or Actual/360 including the
    /// last day.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The day counter.</returns>
    public static DayCounter DayCounterOf(JsonElement spec) =>
        spec.GetProperty("type").GetString() == "Actual360"
        && spec.TryGetProperty("includeLastDay", out JsonElement inc) && inc.GetBoolean()
            ? new Actual360(true)
            : CashFlowInputs.DayCounterOf(spec);

    /// <summary>The credit curve a spec describes.</summary>
    /// <param name="spec">The spec: a flat hazard rate on a date or after settlement days, or an interpolated
    /// curve's nodes.</param>
    /// <param name="context">The case's evaluation context, which a settlement-days curve reads.</param>
    /// <returns>The curve.</returns>
    public static DefaultProbabilityTermStructure CurveOf(JsonElement spec, EvaluationContext context)
    {
        string type = spec.GetProperty("type").GetString()!;
        DayCounter dc = DayCounterOf(spec.GetProperty("dayCounter"));
        if (type == "flat")
        {
            return new FlatHazardRate(
                new Date(spec.GetProperty("referenceDate").GetInt32()), spec.GetProperty("hazardRate").GetDouble(), dc);
        }

        if (type == "flatSettlement")
        {
            return FlatHazardRate.Create(
                spec.GetProperty("settlementDays").GetInt32(),
                CalendarOf(spec.GetProperty("calendar")),
                spec.GetProperty("hazardRate").GetDouble(),
                dc,
                context);
        }

        Date[] dates = spec.GetProperty("dates").EnumerateArray().Select(e => new Date(e.GetInt32())).ToArray();
        double[] data = spec.GetProperty("data").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        return type switch
        {
            "HazardRateCurve" => new HazardRateCurve(dates, data, dc),
            "DefaultDensityCurve" => new DefaultDensityCurve(dates, data, dc),
            "SurvivalProbabilityCurve" => new SurvivalProbabilityCurve(dates, data, dc),
            _ => throw new InvalidOperationException($"unknown credit curve type '{type}'"),
        };
    }

    /// <summary>The discount curve a spec describes, or <see langword="null"/>, QuantLib's empty handle, for JSON
    /// null.</summary>
    /// <param name="spec">The spec, or JSON <c>null</c>.</param>
    /// <returns>The curve, or <see langword="null"/>.</returns>
    public static YieldTermStructure? DiscountOf(JsonElement spec)
    {
        if (spec.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (spec.TryGetProperty("type", out JsonElement type) && type.GetString() == "DiscountCurve")
        {
            return new DiscountCurve(
                spec.GetProperty("dates").EnumerateArray().Select(e => new Date(e.GetInt32())),
                spec.GetProperty("dfs").EnumerateArray().Select(e => e.GetDouble()),
                DayCounterOf(spec.GetProperty("dayCounter")));
        }

        return CashFlowInputs.CurveOf(spec);
    }

    /// <summary>The CDS schedule a spec describes, rule-based on a calendar spec that may be WeekendsOnly.</summary>
    /// <param name="spec">The spec: dates, tenor, calendar, conventions and rule.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The schedule.</returns>
    public static Schedule ScheduleOf(JsonElement spec, EvaluationContext context)
    {
        JsonElement tenor = spec.GetProperty("tenor");
        return new Schedule(
            new Date(spec.GetProperty("effectiveDate").GetInt32()),
            new Date(spec.GetProperty("terminationDate").GetInt32()),
            new Period(tenor[0].GetInt32(), (TimeUnit)tenor[1].GetInt32()),
            CalendarOf(spec.GetProperty("calendar")),
            (BusinessDayConvention)spec.GetProperty("convention").GetInt32(),
            (BusinessDayConvention)spec.GetProperty("terminationDateConvention").GetInt32(),
            (DateGenerationRule)spec.GetProperty("rule").GetInt32(),
            false,
            context);
    }

    /// <summary>The credit default swap a spec describes, through its running-spread or its upfront
    /// constructor.</summary>
    /// <param name="spec">The spec: side, notional, upfront (null for the running-spread constructor), spread,
    /// schedule, conventions, flags, dates and cash settlement days.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The swap.</returns>
    public static CreditDefaultSwap SwapOf(JsonElement spec, EvaluationContext context)
    {
        var side = (ProtectionSide)spec.GetProperty("side").GetInt32();
        double notional = spec.GetProperty("notional").GetDouble();
        double spread = spec.GetProperty("spread").GetDouble();
        Schedule schedule = ScheduleOf(spec.GetProperty("schedule"), context);
        var convention = (BusinessDayConvention)spec.GetProperty("paymentConvention").GetInt32();
        DayCounter dc = DayCounterOf(spec.GetProperty("dayCounter"));
        bool settlesAccrual = spec.GetProperty("settlesAccrual").GetBoolean();
        bool paysAtDefaultTime = spec.GetProperty("paysAtDefaultTime").GetBoolean();
        Date? protectionStart = CashFlowInputs.NullableDate(spec.GetProperty("protectionStart"));
        JsonElement last = spec.GetProperty("lastPeriodDayCounter");
        DayCounter? lastPeriodDayCounter = last.ValueKind == JsonValueKind.Null ? null : DayCounterOf(last);
        bool rebatesAccrual = spec.GetProperty("rebatesAccrual").GetBoolean();
        Date? tradeDate = CashFlowInputs.NullableDate(spec.GetProperty("tradeDate"));
        int cashSettlementDays = spec.GetProperty("cashSettlementDays").GetInt32();
        JsonElement upfront = spec.GetProperty("upfront");
        if (upfront.ValueKind == JsonValueKind.Null)
        {
            return new CreditDefaultSwap(
                side,
                notional,
                spread,
                schedule,
                convention,
                dc,
                settlesAccrual,
                paysAtDefaultTime,
                protectionStart,
                null,
                lastPeriodDayCounter,
                rebatesAccrual,
                tradeDate,
                cashSettlementDays);
        }

        return new CreditDefaultSwap(
            side,
            notional,
            upfront.GetDouble(),
            spread,
            schedule,
            convention,
            dc,
            settlesAccrual,
            paysAtDefaultTime,
            protectionStart,
            CashFlowInputs.NullableDate(spec.GetProperty("upfrontDate")),
            null,
            lastPeriodDayCounter,
            rebatesAccrual,
            tradeDate,
            cashSettlementDays);
    }

    /// <summary>An interpolated credit curve's vectors: its nodes, and its values at the case's sample dates, read
    /// with extrapolation; or, for a <c>"construction"</c> case, its refusal.</summary>
    /// <typeparam name="T">The curve's type.</typeparam>
    /// <param name="file">The vector file's path under <c>tests/vectors</c>.</param>
    /// <param name="dates">The curve's node dates.</param>
    /// <param name="data">The curve's data, as its named member gives them.</param>
    public static void CheckInterpolated<T>(string file, Func<T, IEnumerable<Date>> dates, Func<T, IEnumerable<double>> data)
        where T : DefaultProbabilityTermStructure
    {
        var vectors = VectorFile.Load(file);
        var check = new VectorAssert(vectors);
        foreach (VectorCase c in vectors.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Expected.TryGetProperty("construction", out _))
            {
                check.Output(c, "construction", () => CurveOf(c.Input("curve"), context) is null ? 1 : 0);
                continue;
            }

            var curve = (T)CurveOf(c.Input("curve"), context);
            Date[] sample = c.DateInputs("sample");
            check.Output(c, "dates", dates(curve).Select(d => (object?)d));
            check.Output(c, "times", dates(curve).Select(d => (object?)curve.TimeFromReference(d)));
            check.Output(c, "data", data(curve).Select(x => (object?)x));
            check.Output(c, "maxDate", curve.MaxDate);
            check.Output(c, "survivalProbability", sample.Select(d => (object?)(Func<object?>)(() => curve.SurvivalProbability(d, true))));
            check.Output(c, "hazardRate", sample.Select(d => (object?)(Func<object?>)(() => curve.HazardRate(d, true))));
            check.Output(c, "defaultDensity", sample.Select(d => (object?)(Func<object?>)(() => curve.DefaultDensity(d, true))));
        }

        check.Complete();
    }

    /// <summary>The CDS helper a spec describes, quoted at its own quote.</summary>
    /// <param name="spec">The spec, as <c>tools/vectors/gen/termstructures/credit/defaultprobabilityhelpers.py</c>
    /// writes it.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The helper.</returns>
    public static CdsHelper HelperOf(JsonElement spec, EvaluationContext context)
    {
        JsonElement tenor = spec.GetProperty("tenor");
        var period = new Period(tenor[0].GetInt32(), (TimeUnit)tenor[1].GetInt32());
        int settlementDays = spec.GetProperty("settlementDays").GetInt32();
        Calendar calendar = CalendarOf(spec.GetProperty("calendar"));
        var frequency = (Frequency)spec.GetProperty("frequency").GetInt32();
        var convention = (BusinessDayConvention)spec.GetProperty("convention").GetInt32();
        var rule = (DateGenerationRule)spec.GetProperty("rule").GetInt32();
        DayCounter dc = DayCounterOf(spec.GetProperty("dayCounter"));
        double recovery = spec.GetProperty("recoveryRate").GetDouble();
        YieldTermStructure? discount = DiscountOf(spec.GetProperty("discount"));
        bool settles = spec.GetProperty("settlesAccrual").GetBoolean();
        bool pays = spec.GetProperty("paysAtDefaultTime").GetBoolean();
        Date? start = CashFlowInputs.NullableDate(spec.GetProperty("startDate"));
        DayCounter last = DayCounterOf(spec.GetProperty("lastPeriodDayCounter"));
        bool rebates = spec.GetProperty("rebatesAccrual").GetBoolean();
        var model = (CreditDefaultSwapPricingModel)spec.GetProperty("model").GetInt32();
        Date? tradeDate = CashFlowInputs.NullableDate(spec.GetProperty("tradeDate"));
        double quote = spec.GetProperty("quote").GetDouble();
        if (spec.GetProperty("kind").GetString() == "spread")
        {
            return new SpreadCdsHelper(
                quote, period, settlementDays, calendar, frequency, convention, rule, dc, recovery, discount, context,
                settles, pays, start, last, rebates, model, tradeDate);
        }

        return new UpfrontCdsHelper(
            quote,
            spec.GetProperty("runningSpread").GetDouble(),
            period,
            settlementDays,
            calendar,
            frequency,
            convention,
            rule,
            dc,
            recovery,
            discount,
            context,
            spec.GetProperty("upfrontSettlementDays").GetInt32(),
            settles,
            pays,
            start,
            last,
            rebates,
            model,
            tradeDate);
    }

    /// <summary>The piecewise flat hazard-rate curve a spec describes, bootstrapped on the given helpers.</summary>
    /// <param name="spec">The curve spec: a reference date, or settlement days on a calendar; and a day
    /// counter.</param>
    /// <param name="helpers">The helpers.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The curve.</returns>
    public static PiecewiseDefaultCurve<HazardRate, Numerics.BackwardFlat, IterativeBootstrap> PiecewiseOf(
        JsonElement spec, IEnumerable<CdsHelper> helpers, EvaluationContext context)
    {
        DayCounter dc = DayCounterOf(spec.GetProperty("dayCounter"));
        JsonElement reference = spec.GetProperty("referenceDate");
        return reference.ValueKind != JsonValueKind.Null
            ? new PiecewiseFlatHazardRate(new Date(reference.GetInt32()), helpers, dc, context)
            : PiecewiseFlatHazardRate.Create(
                spec.GetProperty("settlementDays").GetInt32(), CalendarOf(spec.GetProperty("calendar")), helpers, dc, context);
    }

    /// <summary>The outputs the stability filter kept, a dropped one null.</summary>
    /// <param name="c">The case, whose <c>"dropped"</c> input lists each output's dropped indexes.</param>
    /// <param name="name">The output.</param>
    /// <param name="values">The values, each computed when compared.</param>
    /// <returns>The values, the dropped ones null.</returns>
    public static IEnumerable<object?> Kept(VectorCase c, string name, IEnumerable<Func<object?>> values)
    {
        int[] dropped = [.. c.Input("dropped").GetProperty(name).EnumerateArray().Select(e => e.GetInt32())];
        return values.Select((f, i) => dropped.Contains(i) ? null : (object?)f);
    }

    /// <summary>A case's credit-default-swap inspectors, as the vectors write them, against the swap's.</summary>
    /// <param name="check">The checker.</param>
    /// <param name="c">The case.</param>
    /// <param name="cds">The swap.</param>
    /// <param name="context">The case's evaluation context.</param>
    public static void CheckInspectors(VectorAssert check, VectorCase c, CreditDefaultSwap cds, EvaluationContext context)
    {
        check.Output(c, "protectionStartDate", cds.ProtectionStartDate);
        check.Output(c, "protectionEndDate", cds.ProtectionEndDate);
        check.Output(c, "tradeDate", cds.TradeDate);
        check.Output(c, "cashSettlementDays", cds.CashSettlementDays);
        check.Output(c, "upfront", cds.Upfront);
        check.Output(c, "upfrontDate", cds.UpfrontPayment.Date);
        check.Output(c, "upfrontAmount", cds.UpfrontPayment.Amount(context));
        check.Output(c, "rebateDate", cds.AccrualRebate?.Date);
        check.Output(c, "rebateAmount", cds.AccrualRebate?.Amount(context));
        check.Output(c, "couponDates", cds.Coupons.Select(f => (object?)f.Date));
        check.Output(c, "accrualStartDates", cds.Coupons.Select(f => (object?)((Coupons.Coupon)f).AccrualStartDate));
        check.Output(c, "accrualEndDates", cds.Coupons.Select(f => (object?)((Coupons.Coupon)f).AccrualEndDate));
        check.Output(c, "couponAmounts", cds.Coupons.Select(f => (object?)f.Amount(context)));
        check.Output(c, "isExpired", cds.IsExpired(context));
    }

    /// <summary>A case's credit-default-swap results, as the vectors write them, against the engine's; an expired
    /// swap's accrual-rebate NPV, which QuantLib leaves unset, isn't written.</summary>
    /// <param name="check">The checker.</param>
    /// <param name="c">The case.</param>
    /// <param name="results">The results.</param>
    public static void CheckResults(VectorAssert check, VectorCase c, CreditDefaultSwapResults results)
    {
        check.Output(c, "NPV", results.NPV);
        check.Output(c, "couponLegNPV", results.CouponLegNPV);
        check.Output(c, "defaultLegNPV", results.DefaultLegNPV);
        check.Output(c, "upfrontNPV", results.UpfrontNPV);
        if (c.Expected.TryGetProperty("accrualRebateNPV", out _))
        {
            check.Output(c, "accrualRebateNPV", results.AccrualRebateNPV);
        }
        check.Output(c, "fairSpread", results.FairSpread);
        check.Output(c, "fairUpfront", results.FairUpfront);
        check.Output(c, "couponLegBPS", results.CouponLegBPS);
        check.Output(c, "upfrontBPS", results.UpfrontBPS);
    }
}
