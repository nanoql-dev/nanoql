// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BasketInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The MCEuropeanBasketEngine vectors, <c>tests/vectors/pricingengines/basket/mceuropeanbasketengine.json</c>:
/// <see cref="MCEuropeanBasketEngine"/>'s NPV, and its error estimate under the pseudo-random traits, on two and three
/// correlated processes, pseudo-random and low-discrepancy on fixed seeds, by steps and by steps per year, with
/// antithetic paths, and a Brownian bridge, refused.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCEuropeanBasketEngineVectorTests
{
    [Fact]
    public void MCEuropeanBasketEngineVector()
    {
        var file = VectorFile.Load("pricingengines/basket/mceuropeanbasketengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            BasketOption option = BasketOptionOf(c.Input("option"), context);
            JsonElement e = c.Input("engine");
            ImmutableArray<StochasticProcess1D> processes =
                [.. ProcessesOf(e.GetProperty("processes"), context)];
            var array = new StochasticProcessArray(processes, MatrixOf(e.GetProperty("correlation")));
            int? steps = NullableInt(e.GetProperty("timeSteps"));
            int? perYear = NullableInt(e.GetProperty("timeStepsPerYear"));
            bool bridge = e.GetProperty("brownianBridge").GetBoolean();
            bool antithetic = e.GetProperty("antitheticVariate").GetBoolean();
            int samples = e.GetProperty("requiredSamples").GetInt32();
            long seed = e.GetProperty("seed").GetInt64();
            if (e.GetProperty("traits").GetString() == "pr")
            {
                // QuantLib-Python's MCEuropeanBasketEngine forwards its settings as the builder takes them
                MakeMCEuropeanBasketEngine<PseudoRandom, GeneralStatistics> builder =
                    new MakeMCEuropeanBasketEngine<PseudoRandom, GeneralStatistics>(array, seed)
                        .WithBrownianBridge(bridge)
                        .WithAntitheticVariate(antithetic)
                        .WithSamples(samples);
                builder = steps is int s ? builder.WithSteps(s) : builder.WithStepsPerYear(perYear!.Value);
                Func<MultiAssetOptionResults> price = () => builder.ToEngine()(option, context);
                check.Output(c, "NPV", () => price().NPV);
                check.Output(c, "errorEstimate", () => price().ErrorEstimate);
            }
            else
            {
                check.Output(c, "NPV", () => MCEuropeanBasketEngine.Calculate<LowDiscrepancy, GeneralStatistics>(
                    option, context, array, steps, perYear, bridge, antithetic, samples, null, null, seed).NPV);
            }
        }

        check.Complete();
    }

    private static int? NullableInt(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetInt32();
}
