// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The MCAmericanEngine vectors, <c>tests/vectors/pricingengines/vanilla/mcamericanengine.json</c>: American and
/// Bermudan options by Longstaff-Schwartz Monte Carlo on three processes under six engine settings, their NPVs and error
/// estimates on fixed seeds, every calibration seed given; and the refusals of a European exercise, of the Legendre
/// basis and of a payoff at expiry.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCAmericanEngineVectorTests
{
    [Fact]
    public void MCAmericanEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/mcamericanengine");
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
        (int? steps, int? perYear, _, bool anti, int? samples, double? tolerance, int? max, long seed) =
            MCEuropeanEngineVectorTests.Settings(spec);
        bool cv = spec.GetProperty("controlVariate").GetBoolean();
        int order = spec.GetProperty("polynomialOrder").GetInt32();
        var type = (LsmBasisSystemPolynomialType)spec.GetProperty("polynomialType").GetInt32();
        int? nCalibration = MCEuropeanEngineVectorTests.NullableInt(spec, "nCalibrationSamples");
        bool? antiCalibration = spec.GetProperty("antitheticVariateCalibration") is { ValueKind: JsonValueKind.True or JsonValueKind.False } a
            ? a.GetBoolean()
            : null;
        long seedCalibration = spec.GetProperty("seedCalibration").GetInt64();
        return spec.GetProperty("traits").GetString() == "PseudoRandom"
            ? MCPRAmericanEngine.Calculate(
                option, context, process, steps, perYear, anti, cv, samples, tolerance, max, seed, order, type, nCalibration,
                antiCalibration, seedCalibration)
            : MCLDAmericanEngine.Calculate(
                option, context, process, steps, perYear, anti, cv, samples, tolerance, max, seed, order, type, nCalibration,
                antiCalibration, seedCalibration);
    }
}
