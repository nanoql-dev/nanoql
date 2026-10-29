// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The commodity specs the C++ harness's commodities vectors use (<c>tools/cxx/src/experimental/commodities/
/// commodityspec.hpp</c>, written by <c>tools/vectors/gen/cxx/experimental/commodities/commodityindex.py</c>), and the
/// results nanoql gives in the harness's shapes.
/// </summary>
internal static class CommodityInputs
{
    /// <summary>One of QuantLib's preset units, by code.</summary>
    /// <param name="spec">The code.</param>
    /// <returns>The unit.</returns>
    public static UnitOfMeasure UnitOf(JsonElement spec) => spec.GetString() switch
    {
        "BBL" => new BarrelUnitOfMeasure(),
        "MT" => new MTUnitOfMeasure(),
        "MB" => new MBUnitOfMeasure(),
        "GAL" => new GallonUnitOfMeasure(),
        "l" => new LitreUnitOfMeasure(),
        "kl" => new KilolitreUnitOfMeasure(),
        "KL_tk" => new TokyoKilolitreUnitOfMeasure(),
        "Lot" => new LotUnitOfMeasure(),
        string code => throw new InvalidOperationException($"unknown unit '{code}'"),
        _ => throw new InvalidOperationException("a unit is its code"),
    };

    /// <summary>A commodity type: <c>{name, code}</c>, or <c>"&lt;NULL&gt;"</c>.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The type.</returns>
    public static CommodityType CommodityTypeOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.String
            ? new NullCommodityType()
            : new CommodityType(spec.GetProperty("name").GetString()!, spec.GetProperty("code").GetString()!);

    /// <summary>A currency, by code: USD or EUR.</summary>
    /// <param name="spec">The code.</param>
    /// <returns>The currency.</returns>
    public static Currency CurrencyOf(JsonElement spec) =>
        spec.GetString() == "USD" ? new USDCurrency() : new EURCurrency();

    /// <summary>A quantity: <c>{commodityType, unit, amount}</c>.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The quantity.</returns>
    public static Quantity QuantityOf(JsonElement spec) =>
        new(CommodityTypeOf(spec.GetProperty("commodityType")), UnitOf(spec.GetProperty("unit")),
            spec.GetProperty("amount").GetDouble());

    /// <summary>A conversion: <c>{commodityType, source, target, factor}</c>.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The conversion.</returns>
    public static UnitOfMeasureConversion ConversionOf(JsonElement spec) =>
        new(CommodityTypeOf(spec.GetProperty("commodityType")), UnitOf(spec.GetProperty("source")),
            UnitOf(spec.GetProperty("target")), spec.GetProperty("factor").GetDouble());

    /// <summary>A curve from its dates, with its basis curve where the spec has one.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's context, whose conversions give a basis curve's factor.</param>
    /// <returns>The curve.</returns>
    public static CommodityCurve CurveOf(JsonElement spec, EvaluationContext context)
    {
        var curve = new CommodityCurve(
            spec.GetProperty("name").GetString()!,
            CommodityTypeOf(spec.GetProperty("commodityType")),
            CurrencyOf(spec.GetProperty("currency")),
            UnitOf(spec.GetProperty("unit")),
            CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
            Dates(spec.GetProperty("dates")),
            Doubles(spec.GetProperty("prices")),
            CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")));
        return spec.TryGetProperty("basis", out JsonElement basis)
            ? curve.SetBasisOfCurve(CurveOf(basis, context), context)
            : curve;
    }

    /// <summary>
    /// The same curve through <see cref="CommodityCurve.Create"/> and <see cref="CommodityCurve.SetPrices"/>, its
    /// reference date the context's evaluation date adjusted, with its basis curve's built the same way.
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's context.</param>
    /// <returns>The curve.</returns>
    public static CommodityCurve MovingCurveOf(JsonElement spec, EvaluationContext context)
    {
        Date[] dates = Dates(spec.GetProperty("dates"));
        double[] prices = Doubles(spec.GetProperty("prices"));
        CommodityCurve curve = CommodityCurve.Create(
                spec.GetProperty("name").GetString()!,
                CommodityTypeOf(spec.GetProperty("commodityType")),
                CurrencyOf(spec.GetProperty("currency")),
                UnitOf(spec.GetProperty("unit")),
                CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
                context,
                CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")))
            .SetPrices(dates.Zip(prices).ToDictionary(p => p.First, p => p.Second));
        return spec.TryGetProperty("basis", out JsonElement basis)
            ? curve.SetBasisOfCurve(MovingCurveOf(basis, context), context)
            : curve;
    }

    /// <summary>Exchange contracts by expiration date, or <see langword="null"/>.</summary>
    /// <param name="spec">The spec, or JSON null.</param>
    /// <returns>The contracts.</returns>
    public static ImmutableSortedDictionary<Date, ExchangeContract>? ContractsOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null
            ? null
            : spec.EnumerateArray()
                .Select(c => new ExchangeContract(
                    c.GetProperty("code").GetString()!,
                    new Date(c.GetProperty("expirationDate").GetInt32()),
                    new Date(c.GetProperty("underlyingStartDate").GetInt32()),
                    new Date(c.GetProperty("underlyingEndDate").GetInt32())))
                .ToImmutableSortedDictionary(c => c.ExpirationDate, c => c);

    /// <summary>An index, its curve built from dates.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's context.</param>
    /// <returns>The index.</returns>
    public static CommodityIndex IndexOf(JsonElement spec, EvaluationContext context)
    {
        JsonElement curve = spec.GetProperty("curve");
        return new CommodityIndex(
            spec.GetProperty("name").GetString()!,
            CommodityTypeOf(spec.GetProperty("commodityType")),
            CurrencyOf(spec.GetProperty("currency")),
            UnitOf(spec.GetProperty("unit")),
            CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
            spec.GetProperty("lotQuantity").GetDouble(),
            curve.ValueKind == JsonValueKind.Null ? null : CurveOf(curve, context),
            ContractsOf(spec.GetProperty("contracts")),
            spec.GetProperty("nearbyOffset").GetInt32());
    }

    /// <summary>A unit cost: <c>{amount, currency, unit}</c>.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The cost.</returns>
    public static CommodityUnitCost UnitCostOf(JsonElement spec) =>
        new(new Money(CurrencyOf(spec.GetProperty("currency")), spec.GetProperty("amount").GetDouble()),
            UnitOf(spec.GetProperty("unit")));

    /// <summary>Secondary costs: <c>{name: {unitCost} or {money}}</c>, or <see langword="null"/>.</summary>
    /// <param name="spec">The spec, or JSON null.</param>
    /// <returns>The costs.</returns>
    public static Dictionary<string, object>? SecondaryCostsOf(JsonElement spec)
    {
        if (spec.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var costs = new Dictionary<string, object>();
        foreach (JsonProperty c in spec.EnumerateObject())
        {
            costs[c.Name] = c.Value.TryGetProperty("unitCost", out JsonElement cost)
                ? UnitCostOf(cost)
                : new Money(
                    CurrencyOf(c.Value.GetProperty("money").GetProperty("currency")),
                    c.Value.GetProperty("money").GetProperty("amount").GetDouble());
        }

        return costs;
    }

    /// <summary>Pricing periods.</summary>
    /// <param name="spec">The specs.</param>
    /// <returns>The periods.</returns>
    public static PricingPeriod[] PeriodsOf(JsonElement spec) =>
        [.. spec.EnumerateArray().Select(p => new PricingPeriod(
            new Date(p.GetProperty("startDate").GetInt32()),
            new Date(p.GetProperty("endDate").GetInt32()),
            new Date(p.GetProperty("paymentDate").GetInt32()),
            QuantityOf(p.GetProperty("quantity"))))];

    /// <summary>A flat forward yield curve, <c>indexes::curve</c>'s spec.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The curve.</returns>
    public static YieldTermStructure YieldCurveOf(JsonElement spec) => CashFlowInputs.CurveOf(spec);

    /// <summary>A quantity in the harness's shape, <c>[unit, amount]</c>.</summary>
    /// <param name="q">The quantity.</param>
    /// <returns>The shape.</returns>
    public static object?[] Result(Quantity q) => [q.UnitOfMeasure!.Code, q.Amount];

    /// <summary>A calculation's pricing errors in the harness's shape, each <c>[level, error, detail, text]</c>.</summary>
    /// <param name="results">The results.</param>
    /// <returns>The shape.</returns>
    public static object?[] Errors(EnergyCommodityResults results) =>
        [.. results.PricingErrors.Select(e => (object?)new object?[] { e.ErrorLevel, e.Error, e.Detail, e.ToString() })];

    /// <summary>The secondary cost amounts in the harness's shape, <c>[[name, amount], ...]</c>.</summary>
    /// <param name="results">The results.</param>
    /// <returns>The shape.</returns>
    public static object?[] Costs(EnergyCommodityResults results) =>
        [.. results.SecondaryCostAmounts.Select(kv => (object?)new object?[] { kv.Key, kv.Value.Value })];

    /// <summary>An energy swap's daily positions in the harness's shape.</summary>
    /// <param name="results">The results.</param>
    /// <returns>The shape.</returns>
    public static object?[] Positions(EnergyCommodityResults results) =>
        [.. results.DailyPositions.Values.Select(p => (object?)new object?[]
        {
            p.Date, p.PayLegPrice, p.ReceiveLegPrice, p.QuantityAmount, p.RiskDelta, p.Unrealized,
        })];

    /// <summary>An energy swap's payment cash flows in the harness's shape.</summary>
    /// <param name="results">The results.</param>
    /// <returns>The shape.</returns>
    public static object?[] CashFlows(EnergyCommodityResults results) =>
        [.. results.PaymentCashFlows.Values.Select(c => (object?)new object?[]
        {
            c.Date, c.Currency!.Code, c.DiscountedAmount.Value, c.UndiscountedAmount.Value, c.DiscountedPaymentAmount.Value,
            c.UndiscountedPaymentAmount.Value, c.DiscountedPaymentAmount.Currency!.Code, c.DiscountFactor,
            c.PaymentDiscountFactor, c.Finalized,
        })];

    /// <summary>Dates written as serials.</summary>
    /// <param name="e">The array.</param>
    /// <returns>The dates.</returns>
    public static Date[] Dates(JsonElement e) => [.. e.EnumerateArray().Select(d => new Date(d.GetInt32()))];

    private static double[] Doubles(JsonElement e) => [.. e.EnumerateArray().Select(d => d.GetDouble())];
}
