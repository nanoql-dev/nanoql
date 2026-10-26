// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BasketInputs;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticEuropeanMargrabeEngine vectors,
/// <c>tests/vectors/pricingengines/exotic/analyticeuropeanmargrabeengine.json</c>:
/// <see cref="AnalyticEuropeanMargrabeEngine"/>'s NPV, deltas, gammas, theta and rho for European Margrabe options, the
/// gammas and the theta at quantities of 1 only; and an expired option's NPV, theta and rho.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticEuropeanMargrabeEngineVectorTests
{
    private static readonly (string Name, Func<MargrabeOptionResults, double?> Get)[] Results =
    [
        ("NPV", r => r.NPV), ("delta1", r => r.Delta1), ("delta2", r => r.Delta2), ("gamma1", r => r.Gamma1),
        ("gamma2", r => r.Gamma2), ("theta", r => r.Theta), ("rho", r => r.Rho),
    ];

    [Fact]
    public void AnalyticEuropeanMargrabeEngineVector()
    {
        var file = VectorFile.Load("pricingengines/exotic/analyticeuropeanmargrabeengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement o = c.Input("option");
            var option = new MargrabeOption(
                o.GetProperty("Q1").GetInt32(), o.GetProperty("Q2").GetInt32(), ExerciseOf(o.GetProperty("exercise"), context));
            JsonElement e = c.Input("engine");
            ImmutableArray<GeneralizedBlackScholesProcess> p = ProcessesOf(e.GetProperty("processes"), context);
            double rho = e.GetProperty("correlation").GetDouble();
            var results = new Lazy<MargrabeOptionResults>(
                () => AnalyticEuropeanMargrabeEngine.Calculate(option, context, p[0], p[1], rho));
            foreach ((string name, Func<MargrabeOptionResults, double?> get) in Results)
            {
                if (c.Expected.TryGetProperty(name, out _))
                {
                    check.Output(c, name, () => get(results.Value));
                }
            }
        }

        check.Complete();
    }
}
