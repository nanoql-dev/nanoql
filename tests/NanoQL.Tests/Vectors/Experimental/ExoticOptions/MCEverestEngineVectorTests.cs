// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BasketInputs;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The MCEverestEngine vectors, <c>tests/vectors/experimental/exoticoptions/mceverestengine.json</c>:
/// <see cref="MCEverestEngine"/>'s NPV, and its error estimate under the pseudo-random traits, on two and three
/// correlated processes, pseudo-random and low-discrepancy on fixed seeds, by steps and by steps per year, with antithetic
/// paths and a Brownian bridge; an expired option and a zero notional, refused.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCEverestEngineVectorTests
{
    [Fact]
    public void MCEverestEngineVector()
    {
        var file = VectorFile.Load("experimental/exoticoptions/mceverestengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement o = c.Input("option");
            JsonElement e = c.Input("engine");
            StochasticProcessArray array = ArrayOf(e, context);
            int? steps = NullableInt(e.GetProperty("timeSteps"));
            int? perYear = NullableInt(e.GetProperty("timeStepsPerYear"));
            bool bridge = e.GetProperty("brownianBridge").GetBoolean();
            bool antithetic = e.GetProperty("antitheticVariate").GetBoolean();
            int samples = e.GetProperty("requiredSamples").GetInt32();
            long seed = e.GetProperty("seed").GetInt64();
            Func<EverestOption> option = () => new EverestOption(
                o.GetProperty("notional").GetDouble(),
                o.GetProperty("guarantee").GetDouble(),
                ExerciseOf(o.GetProperty("exercise"), context));
            if (e.GetProperty("traits").GetString() == "pr")
            {
                // QuantLib-Python's MCPREverestEngine forwards its settings as the builder takes them
                MakeMCEverestEngine<PseudoRandom, GeneralStatistics> builder =
                    new MakeMCEverestEngine<PseudoRandom, GeneralStatistics>(array, seed)
                        .WithBrownianBridge(bridge)
                        .WithAntitheticVariate(antithetic)
                        .WithSamples(samples);
                builder = steps is int s ? builder.WithSteps(s) : builder.WithStepsPerYear(perYear!.Value);
                Func<EverestOptionResults> price = () => builder.ToEngine()(option(), context);
                check.Output(c, "NPV", () => price().NPV);
                if (c.Expected.TryGetProperty("errorEstimate", out _))
                {
                    check.Output(c, "errorEstimate", () => price().ErrorEstimate);
                }
            }
            else
            {
                check.Output(c, "NPV", () => MCEverestEngine.Calculate<LowDiscrepancy, GeneralStatistics>(
                    option(), context, array, steps, perYear, bridge, antithetic, samples, null, null, seed).NPV);
            }
        }

        check.Complete();
    }

    // The engine spec's processes and correlation
    internal static StochasticProcessArray ArrayOf(JsonElement e, EvaluationContext context)
    {
        ImmutableArray<StochasticProcess1D> processes = [.. ProcessesOf(e.GetProperty("processes"), context)];
        return new StochasticProcessArray(processes, MatrixOf(e.GetProperty("correlation")));
    }

    private static int? NullableInt(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetInt32();
}
