// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Euler evolver vectors, <c>tests/vectors/cxx/models/marketmodels/evolvers/lognormalfwdrateeuler.json</c>:
/// <see cref="LogNormalFwdRateEuler"/>'s whole evolutions on the Mersenne-twister and Sobol generators, under the
/// terminal and money-market measures, from the first and a later step, with the model's and a set initial state:
/// each step's weight, current step, forwards, discount ratios, coterminal swap rates and Brownian increments; and the
/// refused numeraires.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LogNormalFwdRateEulerCxxVectorTests
{
    [Fact]
    public void LogNormalFwdRateEulerCxxVector() =>
        CheckEvolutions(
            "cxx/models/marketmodels/evolvers/lognormalfwdrateeuler",
            (_, m, f, numeraires, initial) => new LogNormalFwdRateEuler(m, f, numeraires, initial),
            e => ((LogNormalFwdRateEuler)e).BrowniansThisStep().ToArray());

    // An evolver file's evolutions, each path's weight and each step's reads, and its refused numeraires; the steps'
    // Brownian increments where the evolver gives them. The evolver is made from the case (its initial step 0 where the
    // case gives none), its initial state set on the case's forwards, then prepare called on it.
    internal static void CheckEvolutions(
        string vectorFile,
        Func<VectorCase, MarketModel, BrownianGeneratorFactory, int[], int, MarketModelEvolver> make,
        Func<MarketModelEvolver, double[]>? brownians = null,
        Action<VectorCase, MarketModelEvolver>? prepare = null)
    {
        var file = VectorFile.Load(vectorFile);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            MarketModel model = ModelOf(c.Input("model"));
            BrownianGeneratorFactory factory = FactoryOf(c.Input("generator"));
            if (c.String("call") == "refused")
            {
                int[][] sets = [.. c.Input("numeraires").EnumerateArray().Select(Integers)];
                check.Output(c, "refused", sets.Select(s => Lazy(() => make(c, model, factory, s, 0))).ToArray());
                continue;
            }

            int initialStep = c.Inputs.TryGetProperty("initialStep", out System.Text.Json.JsonElement i) ? i.GetInt32() : 0;
            MarketModelEvolver e = make(c, model, factory, Integers(c.Input("numeraires")), initialStep);
            if (c.Input("initialForwards").ValueKind != System.Text.Json.JsonValueKind.Null)
            {
                var cs = new LMMCurveState(model.Evolution.RateTimes.AsSpan());
                cs.SetOnForwardRates(Reals(c.Input("initialForwards")));
                e.SetInitialState(cs);
            }

            prepare?.Invoke(c, e);
            int paths = c.Int("paths"), steps = model.NumberOfSteps - initialStep;
            var pathWeights = new List<object?>();
            var reads = new Dictionary<string, List<object?>>();
            string[] keys = ["weight", "currentStep", "forwards", "discountRatios", "coterminalSwapRates", "brownians"];
            foreach (string k in keys)
            {
                reads[k] = [];
            }

            for (int p = 0; p < paths; ++p)
            {
                pathWeights.Add(e.StartNewPath());
                var path = keys.ToDictionary(k => k, _ => new List<object?>());
                for (int s = 0; s < steps; ++s)
                {
                    double w = e.AdvanceStep();
                    CurveState cs = e.CurrentState;
                    int n = cs.NumberOfRates;
                    path["weight"].Add(w);
                    path["currentStep"].Add(e.CurrentStep);
                    path["forwards"].Add(cs.ForwardRates().ToArray());
                    path["discountRatios"].Add(Enumerable.Range(0, n + 1).Select(i => cs.DiscountRatio(i, n)).ToArray());
                    path["coterminalSwapRates"].Add(cs.CoterminalSwapRates().ToArray());
                    if (brownians is not null)
                    {
                        path["brownians"].Add(brownians(e));
                    }
                }

                foreach (string k in keys)
                {
                    reads[k].Add(path[k]);
                }
            }

            check.Output(c, "numeraires", e.Numeraires);
            check.Output(c, "pathWeights", pathWeights);
            foreach (string k in keys)
            {
                if (k != "brownians" || brownians is not null)
                {
                    check.Output(c, k, reads[k]);
                }
            }
        }

        check.Complete();
    }
}
