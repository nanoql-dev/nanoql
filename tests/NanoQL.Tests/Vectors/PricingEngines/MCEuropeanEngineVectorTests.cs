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
/// The MCEuropeanEngine vectors, <c>tests/vectors/pricingengines/vanilla/mceuropeanengine.json</c>: European calls
/// and puts by Monte Carlo on four processes under five engine settings, their NPVs and error estimates on fixed seeds,
/// and the refusal of a cash-or-nothing payoff.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCEuropeanEngineVectorTests
{
    [Fact]
    public void MCEuropeanEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/mceuropeanengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
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
    internal static OneAssetOptionResults Price(
        VanillaOption option, EvaluationContext context, GeneralizedBlackScholesProcess process, JsonElement spec)
    {
        (int? steps, int? perYear, bool bb, bool anti, int? samples, double? tolerance, int? max, long seed) = Settings(spec);
        return spec.GetProperty("traits").GetString() == "PseudoRandom"
            ? MCPREuropeanEngine.Calculate(
                option, context, process, steps, perYear, bb, anti, samples, tolerance, max, seed)
            : MCLDEuropeanEngine.Calculate(
                option, context, process, steps, perYear, bb, anti, samples, tolerance, max, seed);
    }

    // An engine spec's settings: {"timeSteps", "timeStepsPerYear", "brownianBridge", "antitheticVariate",
    // "requiredSamples", "requiredTolerance", "maxSamples", "seed"}, nulls for QuantLib's Null.
    internal static (int? Steps, int? PerYear, bool Bb, bool Anti, int? Samples, double? Tolerance, int? Max, long Seed)
        Settings(JsonElement spec) =>
        (
            NullableInt(spec, "timeSteps"),
            NullableInt(spec, "timeStepsPerYear"),
            spec.TryGetProperty("brownianBridge", out JsonElement bb) && bb.GetBoolean(),
            spec.GetProperty("antitheticVariate").GetBoolean(),
            NullableInt(spec, "requiredSamples"),
            spec.GetProperty("requiredTolerance") is { ValueKind: JsonValueKind.Number } t ? t.GetDouble() : null,
            NullableInt(spec, "maxSamples"),
            spec.GetProperty("seed").GetInt64());

    internal static int? NullableInt(JsonElement spec, string name) =>
        spec.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.Number ? e.GetInt32() : null;
}
