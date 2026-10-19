// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's swaption engine vectors, <c>tests/vectors/cxx/pricingengines/swaption/blackswaptionengine.json</c>:
/// the Black and Bachelier engines on a volatility, the overloads QuantLib-Python doesn't bind, with both cash annuity
/// models; the NPV and the eleven additional results. The Bachelier engine's model is QuantLib's: its constructor on a
/// volatility keeps <see cref="CashAnnuityModel.DiscountCurve"/> whatever the model given.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackSwaptionEngineCxxVectorTests
{
    [Fact]
    public void BlackSwaptionEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/swaption/blackswaptionengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Swaption swaption = SwaptionInputs.SwaptionOf(c.Input("swaption"), context);
            YieldTermStructure discountCurve = CashFlowInputs.CurveOf(c.Input("discountCurve"));
            string[] tags = [.. c.Input("tags").EnumerateArray().Select(t => t.GetString()!)];
            check.Output(
                c,
                "values",
                c.Input("engines").EnumerateArray().Select(e => Values(swaption, context, discountCurve, e, tags)).ToArray());
        }

        check.Complete();
    }

    // The engine's NPV and results at the tags, each a throw where the pricing or the tag's result is refused.
    private static Func<object?>[] Values(
        Swaption swaption, EvaluationContext context, YieldTermStructure discountCurve, JsonElement engine, string[] tags)
    {
        Func<InstrumentResults> price = engine.GetProperty("type").GetString() == "BlackSwaptionEngine"
            ? () => SwaptionInputs.Black(swaption, context, discountCurve, engine)
            : () => SwaptionInputs.Bachelier(swaption, context, discountCurve, engine);
        return [.. tags.Select(t => (Func<object?>)(() => t == "NPV" ? price().NPV : price().Result<double>(t)))];
    }
}
