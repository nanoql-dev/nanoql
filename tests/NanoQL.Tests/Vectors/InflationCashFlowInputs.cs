// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The inflation cash-flow, swap, bond and cap vectors' shared inputs, as
/// <c>tools/vectors/gen/cashflows/indexedcashflow.py</c> defines them: a case's index, from its <c>"index"</c> spec
/// (<see cref="InflationInputs.IndexOf"/>), with its <c>"fixings"</c>, <c>[[serial, value], ...]</c>, added through its
/// store's owner one by one; a lag, <c>[length, units]</c>; and the C++ harness's coupon and pricer specs, as
/// <c>tools/cxx/src/cashflows/inflationspec.hpp</c> reads them.
/// </summary>
internal static class InflationCashFlowInputs
{
    /// <summary>The case's index and its evaluation context, the index's fixings added through its store's
    /// owner.</summary>
    /// <param name="c">The case.</param>
    /// <returns>The index and the context.</returns>
    public static (InflationIndex Index, EvaluationContext Context) IndexOf(VectorCase c)
    {
        InflationIndex index = InflationInputs.IndexOf(c.Input("index"));
        return (index, WithFixings(c.Context(), index, c.Input("fixings")));
    }

    /// <summary>A context with fixings added through an index's store owner: a ratio's underlying, or the
    /// index.</summary>
    /// <param name="context">The context.</param>
    /// <param name="index">The index.</param>
    /// <param name="fixings">The fixings, <c>[[serial, value], ...]</c>.</param>
    /// <returns>The context with the fixings.</returns>
    public static EvaluationContext WithFixings(EvaluationContext context, InflationIndex index, JsonElement fixings)
    {
        FinancialIndex owner = index is YoYInflationIndex { UnderlyingIndex: { } underlying } ? underlying : index;
        foreach (JsonElement f in fixings.EnumerateArray())
        {
            context = context with
            {
                Fixings = context.Fixings.AddFixing(owner, new Date(f[0].GetInt32()), f[1].GetDouble()),
            };
        }

        return context;
    }

    /// <summary>A period, <c>[length, units]</c>.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The period.</returns>
    public static Period PeriodOf(JsonElement spec) => new(spec[0].GetInt32(), (TimeUnit)spec[1].GetInt32());

    /// <summary>A date, or <see langword="null"/> for JSON null, QuantLib's <c>Date()</c>.</summary>
    /// <param name="spec">The serial number, or JSON null.</param>
    /// <returns>The date, or <see langword="null"/>.</returns>
    public static Date? NullableDateOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null ? null : new Date(spec.GetInt32());

    /// <summary>A number, or <see langword="null"/> for JSON null, QuantLib's <c>Null&lt;Real&gt;()</c>.</summary>
    /// <param name="spec">The number, or JSON null.</param>
    /// <returns>The number, or <see langword="null"/>.</returns>
    public static double? NullableDoubleOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null ? null : spec.GetDouble();

    /// <summary>A year-on-year coupon, the harness's spec.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="index">The index.</param>
    /// <returns>The coupon, with no pricer.</returns>
    public static YoYInflationCoupon YoYCouponOf(JsonElement spec, YoYInflationIndex index) =>
        new(
            new Date(spec.GetProperty("paymentDate").GetInt32()),
            spec.GetProperty("nominal").GetDouble(),
            new Date(spec.GetProperty("startDate").GetInt32()),
            new Date(spec.GetProperty("endDate").GetInt32()),
            spec.GetProperty("fixingDays").GetInt32(),
            index,
            PeriodOf(spec.GetProperty("observationLag")),
            (CPIInterpolationType)spec.GetProperty("interpolation").GetInt32(),
            CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
            spec.GetProperty("gearing").GetDouble(),
            spec.GetProperty("spread").GetDouble());

    /// <summary>A CPI coupon, the harness's spec, built with the constructor taking both bases.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="index">The index.</param>
    /// <returns>The coupon, with no pricer.</returns>
    public static CPICoupon CPICouponOf(JsonElement spec, ZeroInflationIndex index) =>
        new(
            NullableDoubleOf(spec.GetProperty("baseCPI")),
            NullableDateOf(spec.GetProperty("baseDate")),
            new Date(spec.GetProperty("paymentDate").GetInt32()),
            spec.GetProperty("nominal").GetDouble(),
            new Date(spec.GetProperty("startDate").GetInt32()),
            new Date(spec.GetProperty("endDate").GetInt32()),
            index,
            PeriodOf(spec.GetProperty("observationLag")),
            (CPIInterpolationType)spec.GetProperty("interpolation").GetInt32(),
            CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
            spec.GetProperty("fixedRate").GetDouble());

    /// <summary>A pricer, the harness's spec: its type, volatility and nominal curve, each null for none.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context, from which the volatility's reference date is.</param>
    /// <returns>The pricer.</returns>
    public static InflationCouponPricer PricerOf(JsonElement spec, EvaluationContext context)
    {
        JsonElement vol = spec.GetProperty("volatility");
        bool hasVol = vol.ValueKind != JsonValueKind.Null;
        YieldTermStructure? nominal = NullableCurveOf(spec.GetProperty("nominalCurve"));
        string type = spec.GetProperty("type").GetString()!;
        if (type == "CPICouponPricer")
        {
            return new CPICouponPricer(hasVol ? InflationInputs.CPIVolatilityOf(vol, context) : null, nominal);
        }

        YoYOptionletVolatilitySurface? yoyVol = hasVol ? InflationInputs.YoYVolatilityOf(vol, context) : null;
        return type switch
        {
            "YoYInflationCouponPricer" => new YoYInflationCouponPricer(yoyVol, nominal),
            "BlackYoYInflationCouponPricer" => new BlackYoYInflationCouponPricer(yoyVol, nominal),
            "UnitDisplacedBlackYoYInflationCouponPricer" => new UnitDisplacedBlackYoYInflationCouponPricer(yoyVol, nominal),
            "BachelierYoYInflationCouponPricer" => new BachelierYoYInflationCouponPricer(yoyVol, nominal),
            _ => throw new InvalidOperationException($"unknown pricer '{type}'"),
        };
    }

    /// <summary>A nominal curve, <see cref="CashFlowInputs.CurveOf"/>'s spec, or null for none.</summary>
    /// <param name="spec">The spec, or JSON null.</param>
    /// <returns>The curve, or <see langword="null"/>.</returns>
    public static YieldTermStructure? NullableCurveOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CurveOf(spec);
}
