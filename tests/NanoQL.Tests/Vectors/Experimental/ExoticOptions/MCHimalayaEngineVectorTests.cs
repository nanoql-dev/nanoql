// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The MCHimalayaEngine vectors, <c>tests/vectors/experimental/exoticoptions/mchimalayaengine.json</c>:
/// <see cref="MCHimalayaEngine"/>'s NPV, and its error estimate under the pseudo-random traits, on two and three
/// correlated processes, pseudo-random and low-discrepancy on fixed seeds, with antithetic paths and a Brownian bridge,
/// on fewer fixings than assets, as many and more; an expired option, a past fixing and unsorted fixings, refused; the
/// pseudo-random cases through the builder and through <see cref="MCPRHimalayaEngine"/>, the low-discrepancy ones
/// through <see cref="MCLDHimalayaEngine"/>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCHimalayaEngineVectorTests
{
    [Fact]
    public void MCHimalayaEngineVector()
    {
        var file = VectorFile.Load("experimental/exoticoptions/mchimalayaengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement o = c.Input("option");
            JsonElement e = c.Input("engine");
            StochasticProcessArray array = MCEverestEngineVectorTests.ArrayOf(e, context);
            bool bridge = e.GetProperty("brownianBridge").GetBoolean();
            bool antithetic = e.GetProperty("antitheticVariate").GetBoolean();
            int samples = e.GetProperty("requiredSamples").GetInt32();
            long seed = e.GetProperty("seed").GetInt64();
            Func<HimalayaOption> option = () => new HimalayaOption(
                o.GetProperty("fixingDates").EnumerateArray().Select(d => new Date(d.GetInt32())),
                o.GetProperty("strike").GetDouble());
            if (e.GetProperty("traits").GetString() == "pr")
            {
                // QuantLib-Python's MCPRHimalayaEngine forwards its settings as the builder takes them
                Func<MultiAssetOptionResults> price = () => new MakeMCHimalayaEngine<PseudoRandom, GeneralStatistics>(array, seed)
                    .WithBrownianBridge(bridge)
                    .WithAntitheticVariate(antithetic)
                    .WithSamples(samples)
                    .ToEngine()(option(), context);
                check.Output(c, "NPV", () => price().NPV);
                if (c.Expected.TryGetProperty("errorEstimate", out _))
                {
                    check.Output(c, "errorEstimate", () => price().ErrorEstimate);
                }
            }
            else
            {
                check.Output(c, "NPV", () => MCLDHimalayaEngine.Calculate(
                    option(), context, array, bridge, antithetic, samples, null, null, seed).NPV);
            }
        }

        check.Complete();
    }

    [Fact]
    public void MCPRHimalayaEngineVector()
    {
        // QuantLib-Python's MCPRHimalayaEngine on the pseudo-random cases, which the fact above prices through the
        // builder: QuantLib's same closing, so its same outputs
        var file = VectorFile.Load("experimental/exoticoptions/mchimalayaengine");
        var check = new VectorAssert(file);
        VectorCase[] cases = [.. file.Cases.Where(c => c.Input("engine").GetProperty("traits").GetString() == "pr")];
        foreach (VectorCase c in cases)
        {
            EvaluationContext context = c.Context();
            JsonElement o = c.Input("option");
            JsonElement e = c.Input("engine");
            StochasticProcessArray array = MCEverestEngineVectorTests.ArrayOf(e, context);
            var priced = new Lazy<MultiAssetOptionResults>(() => MCPRHimalayaEngine.Calculate(
                new HimalayaOption(
                    o.GetProperty("fixingDates").EnumerateArray().Select(d => new Date(d.GetInt32())), o.GetProperty("strike").GetDouble()),
                context,
                array,
                e.GetProperty("brownianBridge").GetBoolean(),
                e.GetProperty("antitheticVariate").GetBoolean(),
                e.GetProperty("requiredSamples").GetInt32(),
                null,
                null,
                e.GetProperty("seed").GetInt64()));
            check.Output(c, "NPV", () => priced.Value.NPV);
            if (c.Expected.TryGetProperty("errorEstimate", out _))
            {
                check.Output(c, "errorEstimate", () => priced.Value.ErrorEstimate);
            }
        }

        Assert.Empty(check.Mismatches);
        Assert.Equal(cases.Sum(c => c.Expected.EnumerateObject().Count()), check.Compared);
    }
}
