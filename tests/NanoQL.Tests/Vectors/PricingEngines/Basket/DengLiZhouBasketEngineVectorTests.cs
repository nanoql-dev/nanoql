// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BasketInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The DengLiZhouBasketEngine vectors, <c>tests/vectors/pricingengines/basket/denglizhoubasketengine.json</c>:
/// <see cref="DengLiZhouBasketEngine"/>'s NPV for spread and average options on two to four processes, with one
/// positive weight or several, at positive strikes and at a negative strike with two negative weights.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DengLiZhouBasketEngineVectorTests
{
    [Fact]
    public void DengLiZhouBasketEngineVector()
    {
        var file = VectorFile.Load("pricingengines/basket/denglizhoubasketengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            BasketOption option = BasketOptionOf(c.Input("option"), context);
            JsonElement e = c.Input("engine");
            MultiAssetOptionResults r = DengLiZhouBasketEngine.Calculate(
                option, context, ProcessesOf(e.GetProperty("processes"), context), MatrixOf(e.GetProperty("rho")));
            check.Output(c, "NPV", r.NPV);
        }

        check.Complete();
    }
}
