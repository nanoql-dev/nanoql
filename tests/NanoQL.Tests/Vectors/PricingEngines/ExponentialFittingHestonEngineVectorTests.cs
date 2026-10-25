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
/// The ExponentialFittingHestonEngine vectors, <c>tests/vectors/pricingengines/vanilla/exponentialfittinghestonengine.json</c>:
/// three Heston models on flat and zero curves, the NPVs of calls and puts at seven strikes and three maturities by four
/// control variates, a scaling and a damping exponent, each priced through the static <c>Calculate</c> and through the
/// engine object.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ExponentialFittingHestonEngineVectorTests
{
    [Fact]
    public void ExponentialFittingHestonEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/exponentialfittinghestonengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var model = new HestonModel(HestonProcessVectorTests.HestonProcessOf(c.Input("process")));
            JsonElement spec = c.Input("engine");
            var cv = (AnalyticHestonEngineComplexLogFormula)spec.GetProperty("cv").GetInt32();
            JsonElement s = spec.GetProperty("scaling");
            double? scaling = s.ValueKind == JsonValueKind.Null ? null : s.GetDouble();
            double alpha = spec.GetProperty("alpha").GetDouble();
            var engine = new ExponentialFittingHestonEngine(model, cv, scaling, alpha);
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
                            Assert.Equal(value, ExponentialFittingHestonEngine.Calculate(option, context, model, cv, scaling, alpha).NPV);
                            return value;
                        });
                    }
                }
            }

            check.Output(c, "npv", npv.ToArray());
        }

        check.Complete();
    }
}
