// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The ReplicatingVarianceSwapEngine vectors,
/// <c>tests/vectors/pricingengines/forward/replicatingvarianceswapengine.json</c>: long and short variance swaps over
/// four processes at a zero dividend yield and three strike settings, their fair variances and NPVs, an expired swap
/// and the refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ReplicatingVarianceSwapEngineVectorTests
{
    [Fact]
    public void ReplicatingVarianceSwapEngineVector()
    {
        var file = VectorFile.Load("pricingengines/forward/replicatingvarianceswapengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            VarianceSwap swap = SwapOf(c.Input("swap"));
            JsonElement engine = c.Input("engine");
            var results = new Lazy<VarianceSwapResults>(() => ReplicatingVarianceSwapEngine.Calculate(
                swap,
                context,
                process,
                engine.GetProperty("dk").GetDouble(),
                Doubles(engine.GetProperty("callStrikes")),
                Doubles(engine.GetProperty("putStrikes"))));
            check.Output(c, "variance", Lazy(() => results.Value.Variance));
            check.Output(c, "NPV", Lazy(() => results.Value.NPV));
        }

        check.Complete();
    }

    // A swap spec, its position QuantLib's integer.
    internal static VarianceSwap SwapOf(JsonElement spec) =>
        new(
            (PositionType)spec.GetProperty("position").GetInt32(),
            spec.GetProperty("strike").GetDouble(),
            spec.GetProperty("notional").GetDouble(),
            new Date(spec.GetProperty("startDate").GetInt32()),
            new Date(spec.GetProperty("maturityDate").GetInt32()));
}
