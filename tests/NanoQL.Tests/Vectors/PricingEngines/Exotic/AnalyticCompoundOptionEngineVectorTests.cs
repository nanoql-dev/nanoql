// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticCompoundOptionEngine vectors, <c>tests/vectors/pricingengines/exotic/analyticcompoundoptionengine.json</c>:
/// <see cref="AnalyticCompoundOptionEngine"/>'s NPV and Greeks for compound options on flat markets, the value, delta
/// and theta within Drezner's accuracy of QuantLib's (the S-exo plan's ER7).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticCompoundOptionEngineVectorTests
{
    [Fact]
    public void AnalyticCompoundOptionEngineVector()
    {
        var file = VectorFile.Load("pricingengines/exotic/analyticcompoundoptionengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement o = c.Input("option");
            var option = new CompoundOption(
                PayoffOf(o.GetProperty("motherPayoff")),
                ExerciseOf(o.GetProperty("motherExercise"), context),
                PayoffOf(o.GetProperty("daughterPayoff")),
                ExerciseOf(o.GetProperty("daughterExercise"), context));
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            var r = new Lazy<OneAssetOptionResults>(() => AnalyticCompoundOptionEngine.Calculate(option, context, process));
            AnalyticCliquetEngineVectorTests.CheckGreeks(check, c, r);
        }

        check.Complete();
    }
}
