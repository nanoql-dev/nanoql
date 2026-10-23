// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="G2SwaptionEngine"/>'s vectors, <c>tests/vectors/pricingengines/swaption/g2swaptionengine.json</c>: European
/// swaptions on fixed legs on the curve's day counter, payers and receivers in, at and out of the money, a floating
/// spread, an overnight-indexed swap and an expired one, on two G2 models with two integrations; and the cash-settled
/// refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class G2SwaptionEngineVectorTests
{
    [Fact]
    public void G2SwaptionEngineVector()
    {
        var file = VectorFile.Load("pricingengines/swaption/g2swaptionengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            YieldTermStructure curve = CashFlowInputs.CurveOf(c.Input("discountCurve"));
            check.Output(c, "NPV", c.Input("engines").EnumerateArray().Select(e => (Func<object?>)(() =>
            {
                Swaption swaption = SwaptionInputs.SwaptionOf(c.Input("swaption"), context);
                var model = new G2(
                    curve,
                    e.GetProperty("a").GetDouble(),
                    e.GetProperty("sigma").GetDouble(),
                    e.GetProperty("b").GetDouble(),
                    e.GetProperty("eta").GetDouble(),
                    e.GetProperty("rho").GetDouble());
                return G2SwaptionEngine.Calculate(
                    swaption, context, model, e.GetProperty("range").GetDouble(), e.GetProperty("intervals").GetInt32()).NPV;
            })));
        }

        check.Complete();
    }
}
