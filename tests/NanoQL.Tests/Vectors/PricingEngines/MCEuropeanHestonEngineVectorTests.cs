// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The MCEuropeanHestonEngine vectors, <c>tests/vectors/pricingengines/vanilla/mceuropeanhestonengine.json</c>:
/// European calls and puts by Monte Carlo on three Heston processes and a Bates process under four engine settings,
/// their NPVs and error estimates on fixed seeds, and the refusal of a cash-or-nothing payoff.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCEuropeanHestonEngineVectorTests
{
    [Fact]
    public void MCEuropeanHestonEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/mceuropeanhestonengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement processSpec = c.Input("process");
            HestonProcess process = processSpec.TryGetProperty("lambda", out _)
                ? BatesProcessVectorTests.BatesProcessOf(processSpec)
                : HestonProcessVectorTests.HestonProcessOf(processSpec);
            Exercise exercise = ExerciseOf(c.Input("exercise"), context);
            JsonElement spec = c.Input("engine");
            Func<OneAssetOptionResults>[] priced = c.Input("payoffs").EnumerateArray()
                .Select(p =>
                {
                    var results = new Lazy<OneAssetOptionResults>(
                        () => Price(new VanillaOption(PayoffOf(p), exercise), context, process, spec));
                    return (Func<OneAssetOptionResults>)(() => results.Value);
                })
                .ToArray();
            check.Output(c, "NPV", priced.Select(f => Lazy(() => f().NPV)).ToArray());
            if (c.Expected.TryGetProperty("errorEstimate", out _))
            {
                check.Output(c, "errorEstimate", priced.Select(f => Lazy(() => f().ErrorEstimate)).ToArray());
            }
        }

        check.Complete();
    }

    // The engine a spec describes, on the case's option.
    private static OneAssetOptionResults Price(
        VanillaOption option, EvaluationContext context, HestonProcess process, JsonElement spec)
    {
        int? steps = MCEuropeanEngineVectorTests.NullableInt(spec, "timeSteps");
        int? perYear = MCEuropeanEngineVectorTests.NullableInt(spec, "timeStepsPerYear");
        bool anti = spec.GetProperty("antitheticVariate").GetBoolean();
        int? samples = MCEuropeanEngineVectorTests.NullableInt(spec, "requiredSamples");
        double? tolerance = spec.GetProperty("requiredTolerance") is { ValueKind: JsonValueKind.Number } t ? t.GetDouble() : null;
        int? max = MCEuropeanEngineVectorTests.NullableInt(spec, "maxSamples");
        long seed = spec.GetProperty("seed").GetInt64();
        return spec.GetProperty("traits").GetString() == "PseudoRandom"
            ? MCPREuropeanHestonEngine.Calculate(
                option, context, process, steps, perYear, anti, samples, tolerance, max, seed)
            : MCLDEuropeanHestonEngine.Calculate(
                option, context, process, steps, perYear, anti, samples, tolerance, max, seed);
    }
}
