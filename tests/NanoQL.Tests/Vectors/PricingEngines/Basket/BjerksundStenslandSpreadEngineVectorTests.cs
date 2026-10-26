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
/// The BjerksundStenslandSpreadEngine vectors,
/// <c>tests/vectors/pricingengines/basket/bjerksundstenslandspreadengine.json</c>:
/// <see cref="BjerksundStenslandSpreadEngine"/>'s NPV for European spread calls and puts on two processes.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BjerksundStenslandSpreadEngineVectorTests
{
    [Fact]
    public void BjerksundStenslandSpreadEngineVector()
    {
        var file = VectorFile.Load("pricingengines/basket/bjerksundstenslandspreadengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            BasketOption option = BasketOptionOf(c.Input("option"), context);
            JsonElement e = c.Input("engine");
            ImmutableArray<GeneralizedBlackScholesProcess> p = ProcessesOf(e.GetProperty("processes"), context);
            double rho = e.GetProperty("correlation").GetDouble();
            check.Output(c, "NPV", BjerksundStenslandSpreadEngine.Calculate(option, context, p[0], p[1], rho).NPV);
        }

        check.Complete();
    }
}
