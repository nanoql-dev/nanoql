// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticRoughHestonEngine vectors, <c>tests/vectors/pricingengines/vanilla/analyticroughhestonengine.json</c>: two
/// rough Heston models on flat and zero curves, the NPVs of calls and puts at five strikes and three maturities by the
/// Adams, Padé and lifted routes and three variants, each priced through the static <c>Calculate</c> and through the
/// engine object.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticRoughHestonEngineVectorTests
{
    [Fact]
    public void AnalyticRoughHestonEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/analyticroughhestonengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            RoughHestonModel model = RoughHestonModelVectorTests.ModelOf(c.Input("model"));
            JsonElement spec = c.Input("engine");
            AnalyticRoughHestonEngine engine = EngineOf(model, spec);
            double[] strikes = Doubles(c.Input("strikes"));
            var npv = new List<Func<object?>>();
            foreach (int days in c.Ints("maturities"))
            {
                var exercise = new EuropeanExercise(context.EvaluationDate + days);
                foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
                {
                    foreach (double k in strikes)
                    {
                        var option = new VanillaOption(new PlainVanillaPayoff(type, k), exercise);
                        npv.Add(() =>
                        {
                            double? value = engine.Calculate(option, context).NPV;
                            Assert.Equal(value, Calculated(model, spec, option, context).NPV);
                            return value;
                        });
                    }
                }
            }

            check.Output(c, "npv", npv.ToArray());
        }

        check.Complete();
    }

    private static AnalyticRoughHestonEngine EngineOf(RoughHestonModel model, JsonElement spec)
    {
        var approximation = (AnalyticRoughHestonEngineApproximation)spec.GetProperty("approximation").GetInt32();
        int timeSteps = spec.GetProperty("timeSteps").GetInt32();
        int nFactors = spec.GetProperty("nFactors").GetInt32();
        if (spec.TryGetProperty("order", out JsonElement order))
        {
            return new AnalyticRoughHestonEngine(model, order.GetInt32(), timeSteps, approximation, nFactors);
        }

        JsonElement lobatto = spec.GetProperty("lobatto");
        return new AnalyticRoughHestonEngine(
            model,
            FourierIntegration.GaussLobatto(lobatto[0].GetDouble(), null, lobatto[1].GetInt32()),
            timeSteps,
            spec.GetProperty("epsilon").GetDouble(),
            spec.GetProperty("alpha").GetDouble(),
            approximation,
            nFactors);
    }

    private static OneAssetOptionResults Calculated(RoughHestonModel model, JsonElement spec, VanillaOption option, EvaluationContext context)
    {
        var approximation = (AnalyticRoughHestonEngineApproximation)spec.GetProperty("approximation").GetInt32();
        int timeSteps = spec.GetProperty("timeSteps").GetInt32();
        int nFactors = spec.GetProperty("nFactors").GetInt32();
        if (spec.TryGetProperty("order", out JsonElement order))
        {
            return AnalyticRoughHestonEngine.Calculate(option, context, model, order.GetInt32(), timeSteps, approximation, nFactors);
        }

        JsonElement lobatto = spec.GetProperty("lobatto");
        return AnalyticRoughHestonEngine.Calculate(
            option,
            context,
            model,
            FourierIntegration.GaussLobatto(lobatto[0].GetDouble(), null, lobatto[1].GetInt32()),
            timeSteps,
            spec.GetProperty("epsilon").GetDouble(),
            spec.GetProperty("alpha").GetDouble(),
            approximation,
            nFactors);
    }
}
