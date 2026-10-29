// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.CommodityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's energy future vectors, <c>tests/vectors/cxx/experimental/commodities/energyfuture.json</c>:
/// <see cref="EnergyFuture.Calculate"/>'s NPV, pricing errors and secondary cost amounts at one lot of the base unit,
/// off today's fixing and off the forward curve.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class EnergyFutureCxxVectorTests
{
    [Fact]
    public void EnergyFutureCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/commodities/energyfuture");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement f = c.Input("future");
            var future = new EnergyFuture(
                f.GetProperty("buySell").GetInt32(),
                QuantityOf(f.GetProperty("quantity")),
                UnitCostOf(f.GetProperty("tradePrice")),
                IndexOf(f.GetProperty("index"), context),
                CommodityTypeOf(f.GetProperty("commodityType")),
                SecondaryCostsOf(f.GetProperty("secondaryCosts")));
            EnergyCommodityResults results = future.Calculate(context);
            check.Output(c, "NPV", results.NPV);
            check.Output(c, "isExpired", future.IsExpired(context));
            check.Output(c, "errors", Errors(results));
            check.Output(c, "costs", Costs(results));
            check.Output(c, "quantity", Result(future.Quantity));
        }

        check.Complete();
    }
}
