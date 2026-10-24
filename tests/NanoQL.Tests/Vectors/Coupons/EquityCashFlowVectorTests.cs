// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The equity cash flow vectors, <c>tests/vectors/cashflows/equitycashflow.json</c>: <see cref="EquityCashFlow"/>'s
/// fixings and amount with no pricer and with <see cref="EquityQuantoCashFlowPricer"/>, QuantLib's refusals, and
/// <see cref="EquityCashFlowFunctions.SetCouponPricer"/> on a leg.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class EquityCashFlowVectorTests
{
    [Fact]
    public void EquityCashFlowVector()
    {
        var file = VectorFile.Load("cashflows/equitycashflow");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            EquityIndex index = IndexOf(c.Input("index"));
            EquityCashFlowPricer? pricer = PricerOf(c.Input("pricer"), context);
            if (c.String("kind") == "flow")
            {
                var flow = new EquityCashFlow(
                    c.Double("notional"),
                    index,
                    c.DateInput("baseDate"),
                    c.DateInput("fixingDate"),
                    c.DateInput("paymentDate"),
                    c.Input("growthOnly").GetBoolean(),
                    pricer);
                check.Output(c, "baseFixing", () => flow.BaseFixing(context));
                check.Output(c, "indexFixing", () => flow.IndexFixing(context));
                check.Output(c, "notional", flow.Notional);
                check.Output(c, "growthOnly", flow.GrowthOnly);
                check.Output(c, "amount", () => flow.Amount(context));
                continue;
            }

            JsonElement simple = c.Input("simple");
            ImmutableArray<CashFlow> leg =
            [
                .. c.Input("flows").EnumerateArray().Select(f => new EquityCashFlow(
                    f.GetProperty("notional").GetDouble(),
                    index,
                    new Date(f.GetProperty("baseDate").GetInt32()),
                    new Date(f.GetProperty("fixingDate").GetInt32()),
                    new Date(f.GetProperty("paymentDate").GetInt32()))),
                new SimpleCashFlow(simple.GetProperty("amount").GetDouble(), new Date(simple.GetProperty("date").GetInt32())),
            ];
            leg = EquityCashFlowFunctions.SetCouponPricer(leg, pricer);
            check.Output(c, "amounts", leg.Select(f => (Func<object?>)(() => f.Amount(context))).ToArray());
        }

        check.Complete();
    }

    // The generator's index: EUR, TARGET, its curves and spot each null for none.
    private static EquityIndex IndexOf(JsonElement spec)
    {
        JsonElement spot = spec.GetProperty("spot");
        return new EquityIndex(
            "eqIndex",
            new TARGET(),
            new EURCurrency(),
            CurveOf(spec.GetProperty("interest")),
            CurveOf(spec.GetProperty("dividend")),
            spot.ValueKind == JsonValueKind.Null ? null : spot.GetDouble());
    }

    // A pricer spec, null for none, each of its inputs null for QuantLib's empty handle.
    private static EquityQuantoCashFlowPricer? PricerOf(JsonElement spec, EvaluationContext context)
    {
        if (spec.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        JsonElement correlation = spec.GetProperty("correlation");
        return new EquityQuantoCashFlowPricer(
            CurveOf(spec.GetProperty("quantoCurve")),
            VolOf(spec.GetProperty("equityVolatility"), context),
            VolOf(spec.GetProperty("fxVolatility"), context),
            correlation.ValueKind == JsonValueKind.Null ? null : correlation.GetDouble());
    }

    private static BlackVolTermStructure? VolOf(JsonElement spec, EvaluationContext context) =>
        spec.ValueKind == JsonValueKind.Null ? null : EquityInputs.BlackVolOf(spec, context);

    private static YieldTermStructure? CurveOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null
            ? null
            : new FlatForward(
                new Date(spec.GetProperty("referenceDate").GetInt32()),
                spec.GetProperty("rate").GetDouble(),
                new Actual365Fixed());
}
