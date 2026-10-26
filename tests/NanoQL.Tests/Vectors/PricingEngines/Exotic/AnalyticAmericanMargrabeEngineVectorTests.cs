// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BasketInputs;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticAmericanMargrabeEngine vectors,
/// <c>tests/vectors/pricingengines/exotic/analyticamericanmargrabeengine.json</c>:
/// <see cref="AnalyticAmericanMargrabeEngine"/>'s NPV for American Margrabe options.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticAmericanMargrabeEngineVectorTests
{
    [Fact]
    public void AnalyticAmericanMargrabeEngineVector()
    {
        var file = VectorFile.Load("pricingengines/exotic/analyticamericanmargrabeengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement o = c.Input("option");
            var option = new MargrabeOption(
                o.GetProperty("Q1").GetInt32(), o.GetProperty("Q2").GetInt32(), ExerciseOf(o.GetProperty("exercise"), context));
            JsonElement e = c.Input("engine");
            ImmutableArray<GeneralizedBlackScholesProcess> p = ProcessesOf(e.GetProperty("processes"), context);
            double rho = e.GetProperty("correlation").GetDouble();
            check.Output(c, "NPV", () => AnalyticAmericanMargrabeEngine.Calculate(option, context, p[0], p[1], rho).NPV);
        }

        check.Complete();
    }
}
