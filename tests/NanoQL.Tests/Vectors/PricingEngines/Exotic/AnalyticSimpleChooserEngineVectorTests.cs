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
/// The AnalyticSimpleChooserEngine vectors, <c>tests/vectors/pricingengines/exotic/analyticsimplechooserengine.json</c>:
/// <see cref="AnalyticSimpleChooserEngine"/>'s NPV for simple choosers on flat markets.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticSimpleChooserEngineVectorTests
{
    [Fact]
    public void AnalyticSimpleChooserEngineVector()
    {
        var file = VectorFile.Load("pricingengines/exotic/analyticsimplechooserengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement o = c.Input("option");
            var option = new SimpleChooserOption(
                DateOf(o.GetProperty("choosingDate")), o.GetProperty("strike").GetDouble(), ExerciseOf(o.GetProperty("exercise"), context));
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            check.Output(c, "NPV", () => AnalyticSimpleChooserEngine.Calculate(option, context, process).NPV);
        }

        check.Complete();
    }

    private static Date DateOf(JsonElement spec) => new(spec.GetInt32());
}
