// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ExoticInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The MCForwardEuropeanHestonEngine vectors,
/// <c>tests/vectors/pricingengines/forward/mcforwardeuropeanhestonengine.json</c>:
/// <see cref="MCForwardEuropeanHestonEngine"/>'s NPV and error estimate for forward-start options on Heston processes
/// on fixed seeds, by pseudo-random numbers with and without the control variate and by low-discrepancy numbers.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCForwardEuropeanHestonEngineVectorTests
{
    [Fact]
    public void MCForwardEuropeanHestonEngineVector()
    {
        var file = VectorFile.Load("pricingengines/forward/mcforwardeuropeanhestonengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            ForwardVanillaOption option = ForwardOf(c.Input("option"), context);
            HestonProcess process = HestonProcessVectorTests.HestonProcessOf(c.Input("process"));
            JsonElement spec = c.Input("engine");
            (int? steps, int? perYear, _, bool anti, int? samples, double? tolerance, int? max, long seed) =
                MCEuropeanEngineVectorTests.Settings(spec);
            bool control = spec.GetProperty("controlVariate").GetBoolean();
            var r = new Lazy<OneAssetOptionResults>(() => spec.GetProperty("traits").GetString() == "PseudoRandom"
                ? MCForwardEuropeanHestonEngine.Calculate<PseudoRandom, GeneralStatistics>(
                    option, context, process, steps, perYear, anti, samples, tolerance, max, seed, control)
                : MCForwardEuropeanHestonEngine.Calculate<LowDiscrepancy, GeneralStatistics>(
                    option, context, process, steps, perYear, anti, samples, tolerance, max, seed, control));
            check.Output(c, "NPV", () => r.Value.NPV);
            if (c.Expected.TryGetProperty("errorEstimate", out _))
            {
                check.Output(c, "errorEstimate", () => r.Value.ErrorEstimate);
            }
        }

        check.Complete();
    }
}
