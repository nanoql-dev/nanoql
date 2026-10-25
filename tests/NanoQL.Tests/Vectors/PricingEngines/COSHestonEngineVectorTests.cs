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
/// The COSHestonEngine vectors, <c>tests/vectors/pricingengines/vanilla/coshestonengine.json</c>: three Heston models on
/// flat and zero curves, the NPVs of calls and puts at five strikes and three maturities at three truncations and
/// orders, each priced through the static <c>Calculate</c> and through the engine object.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class COSHestonEngineVectorTests
{
    [Fact]
    public void COSHestonEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/coshestonengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var model = new HestonModel(HestonProcessVectorTests.HestonProcessOf(c.Input("process")));
            JsonElement spec = c.Input("engine");
            double l = spec.GetProperty("L").GetDouble();
            int n = spec.GetProperty("N").GetInt32();
            var engine = new COSHestonEngine(model, l, n);
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
                            Assert.Equal(value, COSHestonEngine.Calculate(option, context, model, l, n).NPV);
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
