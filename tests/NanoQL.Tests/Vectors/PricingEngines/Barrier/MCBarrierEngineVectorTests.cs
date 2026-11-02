// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BarrierInputs;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The MCBarrierEngine vectors, <c>tests/vectors/pricingengines/barrier/mcbarrierengine.json</c>: the four barrier types
/// with and without a rebate on two processes, pseudo-random paths at two seeds and low-discrepancy paths at seed 5, by
/// the Brownian-bridge and the biased pricers: the NPV and the error estimate; an expired option and the refusals. The
/// outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCBarrierEngineVectorTests
{
    [Fact]
    public void MCBarrierEngineVector()
    {
        var file = VectorFile.Load("pricingengines/barrier/mcbarrierengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            JsonElement mc = c.Input("mc");
            Lazy<OneAssetOptionResults>[] priced =
            [
                .. c.Input("payoffs").EnumerateArray().Select(p => new Lazy<OneAssetOptionResults>(
                    () => Price(OptionOf(c.Input("barrier"), p, c.Input("exercise"), context), context, process, mc))),
            ];
            check.Output(c, "NPV", priced.Select(r => Lazy(() => r.Value.NPV)).ToArray());
            if (c.Expected.TryGetProperty("errorEstimate", out _))
            {
                check.Output(c, "errorEstimate", priced.Select(r => Lazy(() => r.Value.ErrorEstimate)).ToArray());
            }
        }

        check.Complete();
    }

    private static OneAssetOptionResults Price(
        BarrierOption option, EvaluationContext context, GeneralizedBlackScholesProcess process, JsonElement mc)
    {
        int steps = mc.GetProperty("timeSteps").GetInt32();
        bool bridge = mc.GetProperty("brownianBridge").GetBoolean();
        bool antithetic = mc.GetProperty("antitheticVariate").GetBoolean();
        int samples = mc.GetProperty("requiredSamples").GetInt32();
        bool biased = mc.GetProperty("isBiased").GetBoolean();
        long seed = mc.GetProperty("seed").GetInt64();
        return mc.GetProperty("traits").GetString() switch
        {
            "pseudorandom" => MCPRBarrierEngine.Calculate(
                option, context, process, steps, null, bridge, antithetic, samples, null, null, biased, seed),
            "lowdiscrepancy" => MCLDBarrierEngine.Calculate(
                option, context, process, steps, null, bridge, antithetic, samples, null, null, biased, seed),
            string s => throw new InvalidOperationException($"unknown traits '{s}'"),
            null => throw new InvalidOperationException("no traits"),
        };
    }
}
