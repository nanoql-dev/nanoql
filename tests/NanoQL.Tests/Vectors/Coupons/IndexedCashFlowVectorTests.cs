// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The indexed cash-flow vectors, <c>tests/vectors/cashflows/indexedcashflow.json</c>: <see cref="IndexedCashFlow"/>'s
/// and <see cref="ZeroInflationCashFlow"/>'s inspectors and amounts on a zero-inflation index's fixings and forecasts,
/// and QuantLib's <c>testNotifications</c> replaced: a zero-inflation cash flow under two curves.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IndexedCashFlowVectorTests
{
    [Fact]
    public void IndexedCashFlowVector()
    {
        var file = VectorFile.Load("cashflows/indexedcashflow");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            (InflationIndex index, EvaluationContext context) = InflationCashFlowInputs.IndexOf(c);
            var zii = (ZeroInflationIndex)index;
            IndexedCashFlow cf = c.String("kind") switch
            {
                "indexed" => new IndexedCashFlow(
                    c.Double("notional"),
                    zii,
                    new Date(c.Int("baseDate")),
                    new Date(c.Int("fixingDate")),
                    new Date(c.Int("paymentDate")),
                    c.Bool("growthOnly")),
                "zero" => new ZeroInflationCashFlow(
                    c.Double("notional"),
                    zii,
                    (CPIInterpolationType)c.Int("interpolation"),
                    new Date(c.Int("startDate")),
                    new Date(c.Int("endDate")),
                    InflationCashFlowInputs.PeriodOf(c.Input("observationLag")),
                    new Date(c.Int("paymentDate")),
                    c.Bool("growthOnly")),
                _ => throw new InvalidOperationException($"unknown case kind in {c.Name}"),
            };
            check.Output(c, "notional", cf.Notional);
            check.Output(c, "baseDate", cf.BaseDate());
            check.Output(c, "fixingDate", cf.FixingDate);
            check.Output(c, "date", cf.Date);
            check.Output(c, "growthOnly", cf.GrowthOnly);
            if (c.String("kind") == "indexed")
            {
                check.Output(c, "baseFixing", () => cf.BaseFixing(context));
                check.Output(c, "indexFixing", () => cf.IndexFixing(context));
            }

            check.Output(c, "amount", () => cf.Amount(context));
        }

        check.Complete();
    }
}
