// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the indexes, legs, swaps and engine arguments the cross-currency vector files describe in their inputs, as
/// <c>tools/vectors/gen/instruments/constnotionalcrosscurrencyswap.py</c>'s functions build them, and checks the
/// outputs the cross-currency swap files share.
/// </summary>
/// <remarks>
/// An index is a preset of <see cref="SwapInputs.IndexOf"/>, or <c>{"type": "USDLibor" or "GBPLibor", "tenor",
/// "curve"}</c>; a currency a code, or null for QuantLib's empty currency. A leg is <c>{"kind": "fixed", "schedule",
/// "notional", "rate", "dayCounter", "paymentLag", "exchanges"}</c> or <c>{"kind": "ibor", "schedule", "notional",
/// "index", "spread", "dayCounter", "paymentLag", "exchanges"}</c>; with <c>"exchanges"</c> the notional is paid on
/// the schedule's first date before the coupons and repaid on the last coupon's payment date after them. An engine is
/// <c>{"domesticCcy", "domesticCurve", "foreignCcy", "foreignCurve", "spotFX", "includeSettlementDateFlows",
/// "settlementDate", "npvDate", "spotFXSettleDate"}</c>, a null flag or date the engine's default.
/// </remarks>
internal static class CrossCurrencyInputs
{
    /// <summary>The index a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The index.</returns>
    public static IborIndex IndexOf(JsonElement spec)
    {
        string type = spec.GetProperty("type").GetString()!;
        if (type is not ("USDLibor" or "GBPLibor"))
        {
            return SwapInputs.IndexOf(spec);
        }

        Period tenor = SwapInputs.PeriodOf(spec.GetProperty("tenor"));
        YieldTermStructure? curve = IndexInputs.CurveOf(spec.GetProperty("curve"));
        return type == "USDLibor" ? new USDLibor(tenor, curve) : new GBPLibor(tenor, curve);
    }

    /// <summary>The currency a code names, or <see langword="null"/> for a null code.</summary>
    /// <param name="e">The code.</param>
    /// <returns>The currency.</returns>
    public static Currency? CurrencyOf(JsonElement e) =>
        e.ValueKind == JsonValueKind.Null ? null : SwapInputs.CurrencyOf(e.GetString()!);

    /// <summary>
    /// The stub index selection a spec describes, <c>{"convention", "indices"}</c>; the empty selection for null.
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The selection.</returns>
    public static StubIndexSelection StubOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null
            ? new StubIndexSelection()
            : new StubIndexSelection(
                (StubIndexSelectionConvention)spec.GetProperty("convention").GetInt32(),
                spec.GetProperty("indices").EnumerateArray().Select(IndexOf));

    /// <summary>The leg a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The leg.</returns>
    public static ImmutableArray<CashFlow> LegOf(JsonElement spec, EvaluationContext context)
    {
        Schedule schedule = SwapInputs.ScheduleOf(spec.GetProperty("schedule"), context);
        double notional = spec.GetProperty("notional").GetDouble();
        DayCounter dayCounter = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        int paymentLag = spec.GetProperty("paymentLag").GetInt32();
        ImmutableArray<CashFlow> coupons = spec.GetProperty("kind").GetString() switch
        {
            "fixed" => new FixedRateLeg(schedule)
                .WithNotionals(notional)
                .WithCouponRates(spec.GetProperty("rate").GetDouble(), dayCounter)
                .WithPaymentLag(paymentLag)
                .ToLeg(),
            "ibor" => new IborLeg(schedule, IndexOf(spec.GetProperty("index")))
                .WithNotionals(notional)
                .WithPaymentDayCounter(dayCounter)
                .WithSpreads(spec.GetProperty("spread").GetDouble())
                .WithPaymentLag(paymentLag)
                .ToLeg(context),
            var k => throw new InvalidOperationException($"unknown leg kind '{k}'"),
        };
        if (!spec.GetProperty("exchanges").GetBoolean())
        {
            return coupons;
        }

        return
        [
            new SimpleCashFlow(-notional, schedule.StartDate()),
            .. coupons,
            new SimpleCashFlow(notional, coupons[^1].Date),
        ];
    }

    /// <summary>
    /// The swap a spec describes: <c>{"form": "pair" or "legs", "legs", "payer", "currencies"}</c>, the pair's first
    /// leg paid.
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The swap.</returns>
    public static ConstNotionalCrossCurrencySwap SwapOf(JsonElement spec, EvaluationContext context)
    {
        ImmutableArray<CashFlow>[] legs = [.. spec.GetProperty("legs").EnumerateArray().Select(l => LegOf(l, context))];
        Currency?[] currencies = [.. spec.GetProperty("currencies").EnumerateArray().Select(CurrencyOf)];
        return spec.GetProperty("form").GetString() == "pair"
            ? new ConstNotionalCrossCurrencySwap(legs[0], currencies[0], legs[1], currencies[1])
            : new ConstNotionalCrossCurrencySwap(
                legs, spec.GetProperty("payer").EnumerateArray().Select(p => p.GetBoolean()), currencies);
    }

    /// <summary>The engine's arguments a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The arguments.</returns>
    public static EngineArguments EngineOf(JsonElement spec) => new(
        CurrencyOf(spec.GetProperty("domesticCcy")),
        CashFlowInputs.CurveOf(spec.GetProperty("domesticCurve")),
        CurrencyOf(spec.GetProperty("foreignCcy")),
        CashFlowInputs.CurveOf(spec.GetProperty("foreignCurve")),
        spec.GetProperty("spotFX").GetDouble(),
        CouponInputs.NullableBool(spec.GetProperty("includeSettlementDateFlows")),
        CashFlowInputs.NullableDate(spec.GetProperty("settlementDate")),
        CashFlowInputs.NullableDate(spec.GetProperty("npvDate")),
        CashFlowInputs.NullableDate(spec.GetProperty("spotFXSettleDate")));

    /// <summary>Checks each leg's payment dates and amounts, as <c>"leg1Dates"</c>, <c>"leg1Amounts"</c>, ….</summary>
    /// <param name="check">The checker.</param>
    /// <param name="c">The case.</param>
    /// <param name="legs">The legs.</param>
    /// <param name="context">The case's evaluation context.</param>
    public static void CheckLegs(
        VectorAssert check, VectorCase c, ImmutableArray<ImmutableArray<CashFlow>> legs, EvaluationContext context)
    {
        for (int j = 0; j < legs.Length; ++j)
        {
            check.Output(c, $"leg{j + 1}Dates", legs[j].Select(cf => cf.Date).ToArray());
            check.Output(
                c, $"leg{j + 1}Amounts", legs[j].Select(cf => (Func<object?>)(() => cf.Amount(context))).ToArray());
        }
    }

    /// <summary>Checks a cross-currency swap's results.</summary>
    /// <param name="check">The checker.</param>
    /// <param name="c">The case.</param>
    /// <param name="results">The results.</param>
    /// <param name="inCcyLegBps">Whether the vectors hold the in-currency BPS, which the wheel's MtM swap doesn't
    /// bind.</param>
    public static void CheckResults(
        VectorAssert check, VectorCase c, CrossCurrencySwapResults results, bool inCcyLegBps = true)
    {
        check.Output(c, "NPV", results.NPV);
        check.Output(c, "legNPV", results.LegNPV);
        check.Output(c, "legBPS", results.LegBPS);
        check.Output(c, "inCcyLegNPV", results.InCcyLegNPV);
        if (inCcyLegBps)
        {
            check.Output(c, "inCcyLegBPS", results.InCcyLegBPS);
        }

        check.Output(c, "startDiscounts", results.StartDiscounts);
        check.Output(c, "endDiscounts", results.EndDiscounts);
    }

    /// <summary>A cross-currency engine's arguments, as the engines' <c>Calculate</c> overloads take them.</summary>
    /// <param name="DomesticCcy">The domestic currency.</param>
    /// <param name="DomesticCurve">The domestic currency's discount curve.</param>
    /// <param name="ForeignCcy">The foreign currency.</param>
    /// <param name="ForeignCurve">The foreign currency's discount curve.</param>
    /// <param name="SpotFX">The spot rate.</param>
    /// <param name="IncludeSettlementDateFlows">Whether flows on the settlement date count.</param>
    /// <param name="SettlementDate">The settlement date.</param>
    /// <param name="NpvDate">The NPV date.</param>
    /// <param name="SpotFXSettleDate">The spot rate's settlement date.</param>
    public sealed record EngineArguments(
        Currency? DomesticCcy,
        YieldTermStructure DomesticCurve,
        Currency? ForeignCcy,
        YieldTermStructure ForeignCurve,
        double SpotFX,
        bool? IncludeSettlementDateFlows,
        Date? SettlementDate,
        Date? NpvDate,
        Date? SpotFXSettleDate);
}
