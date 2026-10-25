// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticHestonHullWhiteEngine vectors,
/// <c>tests/vectors/pricingengines/vanilla/analytichestonhullwhiteengine.json</c>: three Heston models on flat and zero
/// curves with two Hull-White models, under both constructors, the NPVs of calls and puts at three strikes and three
/// maturities, each priced through the engine object and through the static <c>Calculate</c>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticHestonHullWhiteEngineVectorTests
{
    [Fact]
    public void AnalyticHestonHullWhiteEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/analytichestonhullwhiteengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            (HestonModel heston, HullWhite hullWhite) = ModelsOf(c);
            JsonElement spec = c.Input("engine");
            AnalyticHestonHullWhiteEngine engine;
            Func<VanillaOption, OneAssetOptionResults> fresh;
            if (spec.TryGetProperty("integrationOrder", out JsonElement order))
            {
                engine = new AnalyticHestonHullWhiteEngine(heston, hullWhite, order.GetInt32());
                fresh = o => AnalyticHestonHullWhiteEngine.Calculate(o, context, heston, hullWhite, order.GetInt32());
            }
            else
            {
                double tolerance = spec.GetProperty("relTolerance").GetDouble();
                int evaluations = spec.GetProperty("maxEvaluations").GetInt32();
                engine = new AnalyticHestonHullWhiteEngine(heston, hullWhite, tolerance, evaluations);
                fresh = o => AnalyticHestonHullWhiteEngine.Calculate(o, context, heston, hullWhite, tolerance, evaluations);
            }

            check.Output(c, "npv", Npvs(c, context, o =>
            {
                double? value = engine.Calculate(o, context).NPV;
                Assert.Equal(value, fresh(o).NPV);
                return value;
            }));
        }

        check.Complete();
    }

    // The case's Heston model and its Hull-White model on the risk-free curve.
    internal static (HestonModel Heston, HullWhite HullWhite) ModelsOf(VectorCase c)
    {
        var heston = new HestonModel(HestonProcessVectorTests.HestonProcessOf(c.Input("process")));
        JsonElement hw = c.Input("hullWhite");
        return (heston, new HullWhite(heston.Process.RiskFreeRate, hw.GetProperty("a").GetDouble(), hw.GetProperty("sigma").GetDouble()));
    }

    // The case's calls and puts at each maturity and strike, in the generator's order, each priced by price.
    internal static Func<object?>[] Npvs(VectorCase c, EvaluationContext context, Func<VanillaOption, double?> price)
    {
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
                    npv.Add(() => price(option));
                }
            }
        }

        return [.. npv];
    }
}
