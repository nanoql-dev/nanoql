// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticWriterExtensibleOptionEngine vectors, <c>tests/vectors/pricingengines/exotic/analyticwriterextensibleoptionengine.json</c>:
/// <see cref="AnalyticWriterExtensibleOptionEngine"/>'s NPV for writer-extensible calls and puts on flat markets.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticWriterExtensibleOptionEngineVectorTests
{
    [Fact]
    public void AnalyticWriterExtensibleOptionEngineVector()
    {
        var file = VectorFile.Load("pricingengines/exotic/analyticwriterextensibleoptionengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement o = c.Input("option");
            var option = new WriterExtensibleOption(
                (PlainVanillaPayoff)PayoffOf(o.GetProperty("payoff1")),
                ExerciseOf(o.GetProperty("exercise1"), context),
                (PlainVanillaPayoff)PayoffOf(o.GetProperty("payoff2")),
                ExerciseOf(o.GetProperty("exercise2"), context));
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            check.Output(c, "NPV", () => AnalyticWriterExtensibleOptionEngine.Calculate(option, context, process).NPV);
        }

        check.Complete();
    }

    private static Date DateOf(JsonElement spec) => new(spec.GetInt32());
}
