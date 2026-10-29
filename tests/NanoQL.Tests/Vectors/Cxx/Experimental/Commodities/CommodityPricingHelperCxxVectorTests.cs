// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.CommodityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's pricing helper vectors, <c>tests/vectors/cxx/experimental/commodities/commoditypricinghelpers.json</c>:
/// <see cref="CommodityPricingHelperFunctions.CreatePricingPeriods"/>'s monthly periods and refusals, and
/// <see cref="CommodityPricingHelperFunctions.CalculateUnitCost"/> on the base unit.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CommodityPricingHelperCxxVectorTests
{
    [Fact]
    public void CommodityPricingHelperCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/commodities/commoditypricinghelpers");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Inputs.TryGetProperty("paymentTerm", out System.Text.Json.JsonElement term))
            {
                var paymentTerm = new PaymentTerm(
                    term.GetProperty("name").GetString()!,
                    (PaymentTermEventType)term.GetProperty("eventType").GetInt32(),
                    term.GetProperty("offsetDays").GetInt32(),
                    CashFlowInputs.CalendarOf(term.GetProperty("calendar")));
                var other = new PaymentTerm(
                    paymentTerm.Name + " (other)", paymentTerm.EventType, paymentTerm.OffsetDays, paymentTerm.Calendar);
                var itself = new PaymentTerm(
                    paymentTerm.Name, paymentTerm.EventType, paymentTerm.OffsetDays, paymentTerm.Calendar);
                check.Output(c, "term", new object?[]
                {
                    paymentTerm.ToString(), paymentTerm == itself, paymentTerm != other, paymentTerm == other,
                });
                check.Output(c, "periods", () =>
                {
                    ImmutableArray<PricingPeriod> periods = CommodityPricingHelperFunctions.CreatePricingPeriods(
                        new Date(c.Input("startDate").GetInt32()),
                        new Date(c.Input("endDate").GetInt32()),
                        QuantityOf(c.Input("quantity")),
                        (EnergyCommodityDeliverySchedule)c.Input("deliverySchedule").GetInt32(),
                        (EnergyCommodityQuantityPeriodicity)c.Input("qtyPeriodicity").GetInt32(),
                        paymentTerm);
                    return periods.Select(p => (object?)new object?[]
                    {
                        p.StartDate, p.EndDate, p.PaymentDate, Result(p.Quantity),
                    }).ToArray();
                });
                continue;
            }

            check.Output(c, "text", UnitCostOf(c.Input("unitCost")).ToString());
            check.Output(c, "unitCost", () => CommodityPricingHelperFunctions.CalculateUnitCost(
                CommodityTypeOf(c.Input("commodityType")),
                UnitCostOf(c.Input("unitCost")),
                CurrencyOf(c.Input("baseCurrency")),
                UnitOf(c.Input("baseUnit")),
                context.EvaluationDate,
                context));
        }

        check.Complete();
    }
}
