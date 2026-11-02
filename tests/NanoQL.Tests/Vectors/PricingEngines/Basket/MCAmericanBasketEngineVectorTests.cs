// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BasketInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The MCAmericanBasketEngine vectors, <c>tests/vectors/pricingengines/basket/mcamericanbasketengine.json</c>:
/// <see cref="MCAmericanBasketEngine"/>'s NPV and error estimate for American basket options on fixed seeds through
/// <see cref="MCPRAmericanBasketEngine"/>, and its NPV through <see cref="MCLDAmericanBasketEngine"/> on
/// low-discrepancy paths, the calibration seed the wheel's effective one, 2147483647, which QuantLib's engine takes
/// from its base's narrowed null (the S-van plan's VR4 (v)).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCAmericanBasketEngineVectorTests
{
    // QuantLib's calibration seed through the wheel
    private const long WheelCalibrationSeed = 2147483647;

    [Fact]
    public void MCAmericanBasketEngineVector()
    {
        var file = VectorFile.Load("pricingengines/basket/mcamericanbasketengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            BasketOption option = BasketOptionOf(c.Input("option"), context);
            JsonElement e = c.Input("engine");
            var array = new StochasticProcessArray(
                [.. ProcessesOf(e.GetProperty("processes"), context)], MatrixOf(e.GetProperty("rho")));
            int steps = e.GetProperty("timeSteps").GetInt32();
            bool antithetic = e.GetProperty("antitheticVariate").GetBoolean();
            int samples = e.GetProperty("requiredSamples").GetInt32();
            long seed = e.GetProperty("seed").GetInt64();
            int calibration = e.GetProperty("nCalibrationSamples").GetInt32();
            int order = e.GetProperty("polynomialOrder").GetInt32();
            var basis = Enum.Parse<LsmBasisSystemPolynomialType>(e.GetProperty("polynomialType").GetString()!);
            if (e.TryGetProperty("traits", out JsonElement traits) && traits.GetString() == "ld")
            {
                check.Output(c, "NPV", MCLDAmericanBasketEngine.Calculate(
                    option, context, array, steps, null, false, antithetic, samples, null, null, seed, calibration, order, basis,
                    WheelCalibrationSeed).NPV);
                continue;
            }

            MultiAssetOptionResults r = MCPRAmericanBasketEngine.Calculate(
                option, context, array, steps, null, false, antithetic, samples, null, null, seed, calibration, order, basis,
                WheelCalibrationSeed);
            check.Output(c, "NPV", r.NPV);
            check.Output(c, "errorEstimate", r.ErrorEstimate);
        }

        check.Complete();
    }
}
