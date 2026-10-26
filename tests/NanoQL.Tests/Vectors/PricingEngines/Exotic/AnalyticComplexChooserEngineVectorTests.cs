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
/// The AnalyticComplexChooserEngine vectors, <c>tests/vectors/pricingengines/exotic/analyticcomplexchooserengine.json</c>:
/// <see cref="AnalyticComplexChooserEngine"/>'s NPV for complex choosers on flat markets, QuantLib's fixed engine (the
/// S-exo plan's ER6 (ii)) within Drezner's accuracy (ER7).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticComplexChooserEngineVectorTests
{
    [Fact]
    public void AnalyticComplexChooserEngineVector()
    {
        var file = VectorFile.Load("pricingengines/exotic/analyticcomplexchooserengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement o = c.Input("option");
            var option = new ComplexChooserOption(
                DateOf(o.GetProperty("choosingDate")),
                o.GetProperty("strikeCall").GetDouble(),
                o.GetProperty("strikePut").GetDouble(),
                ExerciseOf(o.GetProperty("exerciseCall"), context),
                ExerciseOf(o.GetProperty("exercisePut"), context));
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            check.Output(c, "NPV", () => AnalyticComplexChooserEngine.Calculate(option, context, process).NPV);
        }

        check.Complete();
    }

    private static Date DateOf(JsonElement spec) => new(spec.GetInt32());
}
