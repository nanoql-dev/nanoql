// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="FdHullWhiteSwaptionEngine"/>'s vectors, <c>tests/vectors/pricingengines/swaption/fdhullwhiteswaptionengine.json</c>:
/// European and Bermudan swaptions, a floating spread, an overnight-indexed swap and an expired one, on two Hull-White
/// models at two grids under seven schemes; and the refusals of a forwarding curve on another day counter and of a past
/// exercise date.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdHullWhiteSwaptionEngineVectorTests
{
    [Fact]
    public void FdHullWhiteSwaptionEngineVector()
    {
        var file = VectorFile.Load("pricingengines/swaption/fdhullwhiteswaptionengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            check.Output(c, "NPV", c.Input("engines").EnumerateArray().Select(e => (Func<object?>)(() =>
            {
                Swaption swaption = SwaptionInputs.SwaptionOf(c.Input("swaption"), context);
                return Priced(swaption, context, e).NPV;
            })));
        }

        check.Complete();
    }

    // An engine spec of tools/vectors/gen/pricingengines/swaption/fdhullwhiteswaptionengine.py, on its model's type.
    internal static InstrumentResults Priced(Swaption swaption, EvaluationContext context, JsonElement e)
    {
        ShortRateModel model = ShortRateFdInputs.ModelOf(e.GetProperty("model"));
        int Int(string name) => e.GetProperty(name).GetInt32();
        double invEps = e.GetProperty("invEps").GetDouble();
        return model is G2 g2
            ? FdG2SwaptionEngine.Calculate(
                swaption, context, g2, Int("tGrid"), Int("xGrid"), Int("yGrid"), Int("dampingSteps"), invEps, SchemeOf(e.GetProperty("scheme")))
            : FdHullWhiteSwaptionEngine.Calculate(
                swaption, context, (HullWhite)model, Int("tGrid"), Int("xGrid"), Int("dampingSteps"), invEps, SchemeOf(e.GetProperty("scheme")));
    }
}
