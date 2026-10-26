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
/// The GaussianCopulaSpreadEngine vectors, <c>tests/vectors/pricingengines/basket/gaussiancopulaspreadengine.json</c>:
/// <see cref="GaussianCopulaSpreadEngine"/>'s NPV for European spread calls and puts on two processes, the second
/// rebuilt on the first's risk-free curve, which the engine requires to be one object.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GaussianCopulaSpreadEngineVectorTests
{
    [Fact]
    public void GaussianCopulaSpreadEngineVector()
    {
        var file = VectorFile.Load("pricingengines/basket/gaussiancopulaspreadengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            BasketOption option = BasketOptionOf(c.Input("option"), context);
            JsonElement e = c.Input("engine");
            ImmutableArray<GeneralizedBlackScholesProcess> p = ProcessesOf(e.GetProperty("processes"), context);
            double rho = e.GetProperty("correlation").GetDouble();
            var shared = new GeneralizedBlackScholesProcess(p[1].X0, p[1].DividendYield, p[0].RiskFreeRate, p[1].BlackVolatility);
            check.Output(c, "NPV", GaussianCopulaSpreadEngine.Calculate(option, context, p[0], shared, rho).NPV);
        }

        check.Complete();
    }
}
