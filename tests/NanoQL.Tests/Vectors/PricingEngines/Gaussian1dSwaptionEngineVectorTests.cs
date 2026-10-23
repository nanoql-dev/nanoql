// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Gaussian1d swaption engine vectors, <c>tests/vectors/pricingengines/swaption/gaussian1dswaptionengine.json</c>:
/// European and Bermudan swaptions on vanilla and overnight-indexed swaps priced on three GSR models
/// (<see cref="Gaussian1dInputs"/>), the first also under each of the engine's settings; and the engine's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Gaussian1dSwaptionEngineVectorTests
{
    [Fact]
    public void Gaussian1dSwaptionEngineVector()
    {
        var file = VectorFile.Load("pricingengines/swaption/gaussian1dswaptionengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Swaption swaption = SwaptionInputs.SwaptionOf(c.Input("swaption"), context);
            if (c.Inputs.TryGetProperty("model", out JsonElement single))
            {
                check.Output(c, "NPV", () => Gaussian1dInputs.Price(swaption, context, Gaussian1dInputs.GsrOf(single)));
                continue;
            }

            Gsr[] models = [.. c.Input("models").EnumerateArray().Select(Gaussian1dInputs.GsrOf)];
            check.Output(c, "gsr", models.Select(m => (Func<object?>)(() => Gaussian1dInputs.Price(swaption, context, m))));
            check.Output(
                c,
                "settings",
                c.Input("engineSettings").EnumerateArray().Select(s => (Func<object?>)(() => Gaussian1dInputs.Price(swaption, context, models[0], s))));
        }

        check.Complete();
    }
}
