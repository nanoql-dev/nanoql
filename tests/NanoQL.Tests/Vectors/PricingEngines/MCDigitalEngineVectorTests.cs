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
/// The MCDigitalEngine vectors, <c>tests/vectors/pricingengines/vanilla/mcdigitalengine.json</c>: American
/// cash-or-nothing options at the hit and at expiry by Monte Carlo on two processes under five engine settings, their
/// NPVs and error estimates on fixed seeds; and the refusals of a plain vanilla payoff and of a European exercise.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCDigitalEngineVectorTests
{
    [Fact]
    public void MCDigitalEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/mcdigitalengine");
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

    private static OneAssetOptionResults Price(
        VanillaOption option, EvaluationContext context, GeneralizedBlackScholesProcess process, JsonElement spec)
    {
        (int? steps, int? perYear, bool bb, bool anti, int? samples, double? tolerance, int? max, long seed) =
            MCEuropeanEngineVectorTests.Settings(spec);
        return spec.GetProperty("traits").GetString() == "PseudoRandom"
            ? MCPRDigitalEngine.Calculate(
                option, context, process, steps, perYear, bb, anti, samples, tolerance, max, seed)
            : MCLDDigitalEngine.Calculate(
                option, context, process, steps, perYear, bb, anti, samples, tolerance, max, seed);
    }
}
