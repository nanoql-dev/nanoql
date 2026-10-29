// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.CommodityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's vanilla energy swap vectors, <c>tests/vectors/cxx/experimental/commodities/energyvanillaswap.json</c>:
/// <see cref="EnergySwap.Calculate"/>'s NPV, pricing errors, secondary cost amounts, daily positions and
/// payment cash flows, on the base unit and currency, with its expiry, quantity and pay-receive flag.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class EnergyVanillaSwapCxxVectorTests
{
    [Fact]
    public void EnergyVanillaSwapCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/commodities/energyvanillaswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            EnergyVanillaSwap swap = SwapOf(c.Input("swap"), context);
            EnergyCommodityResults results = swap.Calculate(context);
            check.Output(c, "NPV", results.NPV);
            check.Output(c, "isExpired", swap.IsExpired(context));
            check.Output(c, "errors", Errors(results));
            check.Output(c, "costs", Costs(results));
            check.Output(c, "positions", Positions(results));
            check.Output(c, "cashFlows", CashFlows(results));
            check.Output(c, "quantity", Result(swap.Quantity));
            check.Output(c, "payReceive", swap.PayReceive);
        }

        check.Complete();
    }

    private static EnergyVanillaSwap SwapOf(JsonElement s, EvaluationContext context) =>
        new(
            s.GetProperty("payer").GetBoolean(),
            CashFlowInputs.CalendarOf(s.GetProperty("calendar")),
            new Money(
                CurrencyOf(s.GetProperty("fixedPrice").GetProperty("currency")),
                s.GetProperty("fixedPrice").GetProperty("amount").GetDouble()),
            UnitOf(s.GetProperty("fixedPriceUnit")),
            IndexOf(s.GetProperty("index"), context),
            CurrencyOf(s.GetProperty("payCurrency")),
            CurrencyOf(s.GetProperty("receiveCurrency")),
            PeriodsOf(s.GetProperty("periods")),
            CommodityTypeOf(s.GetProperty("commodityType")),
            SecondaryCostsOf(s.GetProperty("secondaryCosts")),
            YieldCurveOf(s.GetProperty("payLegCurve")),
            YieldCurveOf(s.GetProperty("receiveLegCurve")),
            YieldCurveOf(s.GetProperty("discountCurve")));
}
