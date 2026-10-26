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
/// The OperatorSplittingSpreadEngine vectors,
/// <c>tests/vectors/pricingengines/basket/operatorsplittingspreadengine.json</c>:
/// <see cref="OperatorSplittingSpreadEngine"/>'s NPV for European spread calls and puts on two processes.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class OperatorSplittingSpreadEngineVectorTests
{
    [Fact]
    public void OperatorSplittingSpreadEngineVector()
    {
        var file = VectorFile.Load("pricingengines/basket/operatorsplittingspreadengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            BasketOption option = BasketOptionOf(c.Input("option"), context);
            JsonElement e = c.Input("engine");
            ImmutableArray<GeneralizedBlackScholesProcess> p = ProcessesOf(e.GetProperty("processes"), context);
            double rho = e.GetProperty("correlation").GetDouble();
            check.Output(c, "NPV", OperatorSplittingSpreadEngine.Calculate(
                option,
                context,
                p[0],
                p[1],
                rho,
                Enum.Parse<OperatorSplittingSpreadEngineOrder>(e.GetProperty("order").GetString()!)).NPV);
        }

        check.Complete();
    }
}
