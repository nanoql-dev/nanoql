// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.ExoticInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The MCPerformanceEngine vectors, <c>tests/vectors/pricingengines/cliquet/mcperformanceengine.json</c>:
/// <see cref="MCPerformanceEngine"/>'s NPV and error estimate for performance options on fixed seeds, by pseudo-random
/// and low-discrepancy numbers.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCPerformanceEngineVectorTests
{
    [Fact]
    public void MCPerformanceEngineVector()
    {
        var file = VectorFile.Load("pricingengines/cliquet/mcperformanceengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CliquetOption option = CliquetOf(c.Input("option"), context);
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            JsonElement spec = c.Input("engine");
            (_, _, bool bb, bool anti, int? samples, double? tolerance, int? max, long seed) =
                MCEuropeanEngineVectorTests.Settings(spec);
            var r = new Lazy<OneAssetOptionResults>(() => spec.GetProperty("traits").GetString() == "PseudoRandom"
                ? MCPerformanceEngine.Calculate<PseudoRandom, GeneralStatistics>(
                    option, context, process, bb, anti, samples, tolerance, max, seed)
                : MCPerformanceEngine.Calculate<LowDiscrepancy, GeneralStatistics>(
                    option, context, process, bb, anti, samples, tolerance, max, seed));
            check.Output(c, "NPV", () => r.Value.NPV);
            if (c.Expected.TryGetProperty("errorEstimate", out _))
            {
                check.Output(c, "errorEstimate", () => r.Value.ErrorEstimate);
            }
        }

        check.Complete();
    }
}
