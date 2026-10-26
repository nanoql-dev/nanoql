// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticContinuousFloatingLookbackEngine vectors,
/// <c>tests/vectors/pricingengines/lookback/analyticcontinuousfloatinglookback.json</c>: calls and puts at two running
/// extrema each, over three expiries on ten processes; an expired option and the refusal of a plain-vanilla payoff. The
/// outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticContinuousFloatingLookbackEngineVectorTests
{
    [Fact]
    public void AnalyticContinuousFloatingLookbackEngineVector()
    {
        var file = VectorFile.Load("pricingengines/lookback/analyticcontinuousfloatinglookback");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            TypePayoff payoff = c.Inputs.TryGetProperty("payoff", out var p) ? PayoffOf(p) : new FloatingTypePayoff((OptionType)c.Int("optionType"));
            check.Output(
                c,
                "NPV",
                c.Input("minmaxes").EnumerateArray()
                    .Select(m => Lazy(() => AnalyticContinuousFloatingLookbackEngine.Calculate(
                        new ContinuousFloatingLookbackOption(m.GetDouble(), payoff, ExerciseOf(c.Input("exercise"), context)),
                        context,
                        process).NPV))
                    .ToArray());
        }

        check.Complete();
    }
}
