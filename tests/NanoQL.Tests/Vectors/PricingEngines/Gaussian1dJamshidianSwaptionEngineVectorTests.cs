// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Gaussian1d Jamshidian engine vectors,
/// <c>tests/vectors/pricingengines/swaption/gaussian1djamshidianswaptionengine.json</c>: European swaptions on vanilla
/// and overnight-indexed swaps priced on three GSR models (<see cref="Gaussian1dInputs"/>); and the engine's refusals,
/// in QuantLib's order.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Gaussian1dJamshidianSwaptionEngineVectorTests
{
    [Fact]
    public void Gaussian1dJamshidianSwaptionEngineVector()
    {
        var file = VectorFile.Load("pricingengines/swaption/gaussian1djamshidianswaptionengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Swaption swaption = SwaptionInputs.SwaptionOf(c.Input("swaption"), context);
            if (c.Inputs.TryGetProperty("model", out JsonElement single))
            {
                check.Output(c, "NPV", () => Gaussian1dJamshidianSwaptionEngine.Calculate(swaption, context, Gaussian1dInputs.GsrOf(single)).NPV);
                continue;
            }

            check.Output(c, "gsr", c.Input("models").EnumerateArray().Select(m => (Func<object?>)(() =>
                Gaussian1dJamshidianSwaptionEngine.Calculate(swaption, context, Gaussian1dInputs.GsrOf(m)).NPV)));
        }

        check.Complete();
    }
}
