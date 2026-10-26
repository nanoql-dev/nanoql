// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BasketInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The StulzEngine vectors, <c>tests/vectors/pricingengines/basket/stulzengine.json</c>:
/// <see cref="StulzEngine"/>'s NPV for European calls and puts on the minimum and the maximum of two processes.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class StulzEngineVectorTests
{
    [Fact]
    public void StulzEngineVector()
    {
        var file = VectorFile.Load("pricingengines/basket/stulzengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            BasketOption option = BasketOptionOf(c.Input("option"), context);
            JsonElement e = c.Input("engine");
            ImmutableArray<GeneralizedBlackScholesProcess> p = ProcessesOf(e.GetProperty("processes"), context);
            double rho = e.GetProperty("correlation").GetDouble();
            check.Output(c, "NPV", StulzEngine.Calculate(option, context, p[0], p[1], rho).NPV);
        }

        check.Complete();
    }
}
