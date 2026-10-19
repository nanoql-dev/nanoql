// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="BachelierCapFloorEngine"/>'s vectors,
/// <c>tests/vectors/pricingengines/capfloor/bacheliercapfloorengine.json</c>: caps, floors and collars priced on a
/// constant normal volatility and on a normal optionlet volatility structure, their NPVs, vegas and per-optionlet
/// results, and the structures the engine refuses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BachelierCapFloorEngineVectorTests
{
    [Fact]
    public void BachelierCapFloorEngineVector()
    {
        var file = VectorFile.Load("pricingengines/capfloor/bacheliercapfloorengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CapFloor capFloor = CapFloorInputs.CapFloorOf(c.Input("capFloor"), context);
            YieldTermStructure curve = CashFlowInputs.CurveOf(c.Input("discountCurve"));
            CapFloorInputs.CheckPerEngine(check, c, e => CapFloorInputs.BachelierOf(capFloor, context, curve, e));
        }

        check.Complete();
    }
}
