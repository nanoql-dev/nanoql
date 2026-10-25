// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticH1HWEngine vectors, <c>tests/vectors/pricingengines/vanilla/analytich1hwengine.json</c>: three Heston
/// models on flat and zero curves with two Hull-White models and two equity-rate correlations, under both constructors,
/// the NPVs of calls and puts at three strikes and three maturities, each priced through the engine object and through
/// the static <c>Calculate</c>; and the constructor's refusal of a negative correlation.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticH1HWEngineVectorTests
{
    [Fact]
    public void AnalyticH1HWEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/analytich1hwengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            (HestonModel heston, HullWhite hullWhite) = AnalyticHestonHullWhiteEngineVectorTests.ModelsOf(c);
            double rhoSr = c.Input("rhoSr").GetDouble();
            JsonElement spec = c.Input("engine");
            if (c.Expected.TryGetProperty("engine", out _))
            {
                check.Output(c, "engine", () =>
                {
                    _ = new AnalyticH1HWEngine(heston, hullWhite, rhoSr, spec.GetProperty("integrationOrder").GetInt32());
                    return null;
                });
                continue;
            }

            AnalyticH1HWEngine engine;
            Func<VanillaOption, OneAssetOptionResults> fresh;
            if (spec.TryGetProperty("integrationOrder", out JsonElement order))
            {
                engine = new AnalyticH1HWEngine(heston, hullWhite, rhoSr, order.GetInt32());
                fresh = o => AnalyticH1HWEngine.Calculate(o, context, heston, hullWhite, rhoSr, order.GetInt32());
            }
            else
            {
                double tolerance = spec.GetProperty("relTolerance").GetDouble();
                int evaluations = spec.GetProperty("maxEvaluations").GetInt32();
                engine = new AnalyticH1HWEngine(heston, hullWhite, rhoSr, tolerance, evaluations);
                fresh = o => AnalyticH1HWEngine.Calculate(o, context, heston, hullWhite, rhoSr, tolerance, evaluations);
            }

            check.Output(c, "npv", AnalyticHestonHullWhiteEngineVectorTests.Npvs(c, context, o =>
            {
                double? value = engine.Calculate(o, context).NPV;
                Assert.Equal(value, fresh(o).NPV);
                return value;
            }));
        }

        check.Complete();
    }
}
