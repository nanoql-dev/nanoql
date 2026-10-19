// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Jamshidian engine vectors, <c>tests/vectors/pricingengines/swaption/jamshidianswaptionengine.json</c>: European
/// swaptions on vanilla and overnight-indexed swaps priced on Hull-White models, on Vasicek models with the engine's
/// curve, and on a Hull-White model with another curve given, which it ignores; and the engine's refusals, in
/// QuantLib's order.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class JamshidianSwaptionEngineVectorTests
{
    [Fact]
    public void JamshidianSwaptionEngineVector()
    {
        var file = VectorFile.Load("pricingengines/swaption/jamshidianswaptionengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Swaption swaption = SwaptionInputs.SwaptionOf(c.Input("swaption"), context);
            YieldTermStructure discountCurve = CashFlowInputs.CurveOf(c.Input("discountCurve"));
            if (c.Inputs.TryGetProperty("model", out JsonElement kind))
            {
                YieldTermStructure? engineCurve = IndexInputs.CurveOf(c.Input("engineCurve"));
                OneFactorAffineModel model = kind.GetString() == "HullWhite"
                    ? CalibrationInputs.HullWhiteOf(c.Input("hullWhite"), discountCurve)
                    : CalibrationInputs.VasicekOf(c.Input("vasicek"));
                check.Output(c, "NPV", () => JamshidianSwaptionEngine.Calculate(swaption, context, model, engineCurve).NPV);
                continue;
            }

            check.Output(c, "hullWhite", c.Input("hullWhite").EnumerateArray().Select(m => (Func<object?>)(() =>
                JamshidianSwaptionEngine.Calculate(swaption, context, CalibrationInputs.HullWhiteOf(m, discountCurve)).NPV)));
            check.Output(c, "vasicek", c.Input("vasicek").EnumerateArray().Select(m => (Func<object?>)(() =>
                JamshidianSwaptionEngine.Calculate(swaption, context, CalibrationInputs.VasicekOf(m), discountCurve).NPV)));
            HullWhite first = CalibrationInputs.HullWhiteOf(c.Input("hullWhite")[0], discountCurve);
            check.Output(
                c,
                "hullWhiteOtherCurve",
                () => JamshidianSwaptionEngine.Calculate(swaption, context, first, CashFlowInputs.CurveOf(c.Input("otherCurve"))).NPV);
        }

        check.Complete();
    }
}
