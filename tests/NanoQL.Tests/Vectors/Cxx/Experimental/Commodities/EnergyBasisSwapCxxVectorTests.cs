// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.CommodityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's energy basis swap vectors, <c>tests/vectors/cxx/experimental/commodities/energybasisswap.json</c>:
/// <see cref="EnergyBasisSwap"/>'s NPV, pricing errors, secondary cost amounts, daily positions and payment cash flows,
/// on the base unit and currency, with its expiry and quantity.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class EnergyBasisSwapCxxVectorTests
{
    [Fact]
    public void EnergyBasisSwapCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/commodities/energybasisswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            EnergyBasisSwap swap = SwapOf(c.Input("swap"), context);
            EnergyCommodityResults results = swap.Calculate(context);
            check.Output(c, "NPV", results.NPV);
            check.Output(c, "isExpired", swap.IsExpired(context));
            check.Output(c, "errors", Errors(results));
            check.Output(c, "costs", Costs(results));
            check.Output(c, "positions", Positions(results));
            check.Output(c, "cashFlows", CashFlows(results));
            check.Output(c, "quantity", Result(swap.Quantity));
        }

        check.Complete();
    }

    private static EnergyBasisSwap SwapOf(JsonElement s, EvaluationContext context)
    {
        JsonElement spread = s.GetProperty("spreadIndex");
        return new EnergyBasisSwap(
            CashFlowInputs.CalendarOf(s.GetProperty("calendar")),
            spread.ValueKind == JsonValueKind.Null ? null : IndexOf(spread, context),
            IndexOf(s.GetProperty("payIndex"), context),
            IndexOf(s.GetProperty("receiveIndex"), context),
            s.GetProperty("spreadToPayLeg").GetBoolean(),
            CurrencyOf(s.GetProperty("payCurrency")),
            CurrencyOf(s.GetProperty("receiveCurrency")),
            PeriodsOf(s.GetProperty("periods")),
            UnitCostOf(s.GetProperty("basis")),
            CommodityTypeOf(s.GetProperty("commodityType")),
            SecondaryCostsOf(s.GetProperty("secondaryCosts")),
            YieldCurveOf(s.GetProperty("payLegCurve")),
            YieldCurveOf(s.GetProperty("receiveLegCurve")),
            YieldCurveOf(s.GetProperty("discountCurve")));
    }
}
