// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="BlackCapFloorEngine"/>'s vectors, <c>tests/vectors/pricingengines/capfloor/blackcapfloorengine.json</c>:
/// caps, floors and collars priced on a constant volatility and on an optionlet volatility structure, their NPVs, vegas
/// and per-optionlet results, and the structures the engine refuses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackCapFloorEngineVectorTests
{
    [Fact]
    public void BlackCapFloorEngineVector()
    {
        var file = VectorFile.Load("pricingengines/capfloor/blackcapfloorengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CapFloor capFloor = CapFloorInputs.CapFloorOf(c.Input("capFloor"), context);
            YieldTermStructure curve = CashFlowInputs.CurveOf(c.Input("discountCurve"));
            CapFloorInputs.CheckPerEngine(check, c, e => CapFloorInputs.BlackOf(capFloor, context, curve, e));
        }

        check.Complete();
    }
}
