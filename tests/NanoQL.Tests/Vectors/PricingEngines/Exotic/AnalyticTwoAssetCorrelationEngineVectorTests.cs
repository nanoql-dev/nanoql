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
/// The AnalyticTwoAssetCorrelationEngine vectors,
/// <c>tests/vectors/pricingengines/exotic/analytictwoassetcorrelationengine.json</c>:
/// <see cref="AnalyticTwoAssetCorrelationEngine"/>'s NPV for European two-asset correlation calls and puts, every value
/// within Drezner's accuracy of QuantLib's (the S-exo plan's ER7).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticTwoAssetCorrelationEngineVectorTests
{
    [Fact]
    public void AnalyticTwoAssetCorrelationEngineVector()
    {
        var file = VectorFile.Load("pricingengines/exotic/analytictwoassetcorrelationengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement o = c.Input("option");
            var option = new TwoAssetCorrelationOption(
                (OptionType)o.GetProperty("type").GetInt32(),
                o.GetProperty("strike1").GetDouble(),
                o.GetProperty("strike2").GetDouble(),
                ExerciseOf(o.GetProperty("exercise"), context));
            JsonElement e = c.Input("engine");
            ImmutableArray<GeneralizedBlackScholesProcess> p = ProcessesOf(e.GetProperty("processes"), context);
            double rho = e.GetProperty("correlation").GetDouble();
            check.Output(c, "NPV", () => AnalyticTwoAssetCorrelationEngine.Calculate(option, context, p[0], p[1], rho).NPV);
        }

        check.Complete();
    }
}
