// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The swaption vectors, <c>tests/vectors/instruments/swaption.json</c>: swaptions on vanilla and overnight-indexed
/// swaps, their settlement terms and expiry, and their NPV and results on the Black and Bachelier engines on a
/// volatility, including the swaption of QuantLib's <c>testBlackEngineCaching</c>, whose point, the engine's caching,
/// has no nanoql counterpart (an engine is a function called afresh); and implied volatilities, spot and forward, of both
/// types.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SwaptionVectorTests
{
    [Fact]
    public void SwaptionVector()
    {
        var file = VectorFile.Load("instruments/swaption");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Swaption swaption = SwaptionInputs.SwaptionOf(c.Input("swaption"), context);
            YieldTermStructure discountCurve = CashFlowInputs.CurveOf(c.Input("discountCurve"));
            if (c.Inputs.TryGetProperty("impliedVolatility", out JsonElement implied))
            {
                check.Output(c, "impliedVolatility", () => swaption.ImpliedVolatility(
                    IndexInputs.Real(implied.GetProperty("price")),
                    discountCurve,
                    IndexInputs.Real(implied.GetProperty("guess")),
                    context,
                    IndexInputs.Real(implied.GetProperty("accuracy")),
                    implied.GetProperty("maxEvaluations").GetInt32(),
                    IndexInputs.Real(implied.GetProperty("minVol")),
                    IndexInputs.Real(implied.GetProperty("maxVol")),
                    (VolatilityType)implied.GetProperty("type").GetInt32(),
                    IndexInputs.Real(implied.GetProperty("displacement")),
                    (SwaptionPriceType)implied.GetProperty("priceType").GetInt32()));
                continue;
            }

            check.Output(c, "settlementType", swaption.SettlementType);
            check.Output(c, "settlementMethod", swaption.SettlementMethod);
            check.Output(c, "type", swaption.Type);
            check.Output(c, "isExpired", swaption.IsExpired(context));
            SwaptionInputs.CheckPriced(
                check, c, "black", () => SwaptionInputs.Black(swaption, context, discountCurve, c.Input("black")));
            SwaptionInputs.CheckPriced(
                check,
                c,
                "bachelier",
                () => SwaptionInputs.Bachelier(swaption, context, discountCurve, c.Input("bachelier")));
        }

        check.Complete();
    }
}
