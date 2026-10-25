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
/// The AnalyticPTDHestonEngine vectors, <c>tests/vectors/pricingengines/vanilla/analyticptdhestonengine.json</c>: three
/// piecewise time-dependent Heston models on flat and zero curves, the NPVs of calls and puts at five strikes and three
/// maturities by Gatheral's form at two Gauss-Laguerre orders and on Gauss-Lobatto and by Andersen and Piterbarg's on
/// two integrations, each priced through the static <c>Calculate</c> and through the engine object.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticPTDHestonEngineVectorTests
{
    [Fact]
    public void AnalyticPTDHestonEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/analyticptdhestonengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            PiecewiseTimeDependentHestonModel model = PiecewiseTimeDependentHestonModelVectorTests.ModelOf(c.Input("model"));
            JsonElement spec = c.Input("engine");
            AnalyticPTDHestonEngine engine = EngineOf(model, spec);
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

    private static FourierIntegration IntegrationOf(JsonElement spec) =>
        spec[0].GetString() == "laguerre"
            ? FourierIntegration.GaussLaguerre(spec[1].GetInt32())
            : FourierIntegration.DiscreteTrapezoid(spec[1].GetInt32());

    private static AnalyticPTDHestonEngine EngineOf(PiecewiseTimeDependentHestonModel model, JsonElement spec)
    {
        if (spec.TryGetProperty("order", out JsonElement order))
        {
            return new AnalyticPTDHestonEngine(model, order.GetInt32());
        }

        if (spec.TryGetProperty("relTolerance", out JsonElement relTolerance))
        {
            return new AnalyticPTDHestonEngine(model, relTolerance.GetDouble(), spec.GetProperty("maxEvaluations").GetInt32());
        }

        return new AnalyticPTDHestonEngine(
            model,
            (AnalyticPTDHestonEngineComplexLogFormula)spec.GetProperty("cpxLog").GetInt32(),
            IntegrationOf(spec.GetProperty("integration")),
            spec.GetProperty("epsilon").GetDouble());
    }

    private static OneAssetOptionResults Calculated(
        PiecewiseTimeDependentHestonModel model, JsonElement spec, VanillaOption option, EvaluationContext context)
    {
        if (spec.TryGetProperty("order", out JsonElement order))
        {
            return AnalyticPTDHestonEngine.Calculate(option, context, model, order.GetInt32());
        }

        if (spec.TryGetProperty("relTolerance", out JsonElement relTolerance))
        {
            return AnalyticPTDHestonEngine.Calculate(
                option, context, model, relTolerance.GetDouble(), spec.GetProperty("maxEvaluations").GetInt32());
        }

        return AnalyticPTDHestonEngine.Calculate(
            option,
            context,
            model,
            (AnalyticPTDHestonEngineComplexLogFormula)spec.GetProperty("cpxLog").GetInt32(),
            IntegrationOf(spec.GetProperty("integration")),
            spec.GetProperty("epsilon").GetDouble());
    }
}
