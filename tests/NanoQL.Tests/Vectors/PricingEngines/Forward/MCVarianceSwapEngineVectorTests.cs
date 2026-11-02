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
/// The MCVarianceSwapEngine vectors, <c>tests/vectors/pricingengines/forward/mcvarianceswapengine.json</c>:
/// <see cref="MCVarianceSwapEngine"/>'s variance, NPV and error estimate for variance swaps on fixed seeds, by
/// pseudo-random and low-discrepancy numbers, on volatilities whose local variance is and isn't path-dependent.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCVarianceSwapEngineVectorTests
{
    [Fact]
    public void MCVarianceSwapEngineVector()
    {
        var file = VectorFile.Load("pricingengines/forward/mcvarianceswapengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            VarianceSwap swap = ReplicatingVarianceSwapEngineVectorTests.SwapOf(c.Input("swap"));
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            JsonElement spec = c.Input("engine");
            (int? steps, int? perYear, bool bb, bool anti, int? samples, double? tolerance, int? max, long seed) =
                MCEuropeanEngineVectorTests.Settings(spec);
            var r = new Lazy<VarianceSwapResults>(() => spec.GetProperty("traits").GetString() == "PseudoRandom"
                ? MCPRVarianceSwapEngine.Calculate(
                    swap, context, process, steps, perYear, bb, anti, samples, tolerance, max, seed)
                : MCLDVarianceSwapEngine.Calculate(
                    swap, context, process, steps, perYear, bb, anti, samples, tolerance, max, seed));
            check.Output(c, "variance", () => r.Value.Variance);
            check.Output(c, "NPV", () => r.Value.NPV);
            if (c.Expected.TryGetProperty("errorEstimate", out _))
            {
                check.Output(c, "errorEstimate", () => r.Value.ErrorEstimate);
            }
        }

        check.Complete();
    }
}
