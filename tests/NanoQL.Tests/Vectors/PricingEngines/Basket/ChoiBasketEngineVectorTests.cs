// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BasketInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The ChoiBasketEngine vectors, <c>tests/vectors/pricingengines/basket/choibasketengine.json</c>:
/// <see cref="ChoiBasketEngine"/>'s NPV for spread and average options on two to four processes, at several scales,
/// with integration-step limits, the forward deltas and their control variate.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ChoiBasketEngineVectorTests
{
    [Fact]
    public void ChoiBasketEngineVector()
    {
        var file = VectorFile.Load("pricingengines/basket/choibasketengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            BasketOption option = BasketOptionOf(c.Input("option"), context);
            JsonElement e = c.Input("engine");
            JsonElement max = e.GetProperty("maxNrIntegrationSteps");
            MultiAssetOptionResults r = ChoiBasketEngine.Calculate(
                option,
                context,
                ProcessesOf(e.GetProperty("processes"), context),
                MatrixOf(e.GetProperty("rho")),
                e.GetProperty("lambda").GetDouble(),
                max.ValueKind == JsonValueKind.Null ? null : max.GetInt32(),
                e.GetProperty("calcFwdDelta").GetBoolean(),
                e.GetProperty("controlVariate").GetBoolean());
            check.Output(c, "NPV", r.NPV);
        }

        check.Complete();
    }
}
