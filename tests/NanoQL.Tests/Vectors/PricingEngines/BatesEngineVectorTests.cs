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
/// The BatesEngine vectors, <c>tests/vectors/pricingengines/vanilla/batesengine.json</c>: three Bates models on flat
/// and zero curves, the NPVs of calls and puts at five strikes and three maturities by the Gauss-Laguerre constructor
/// at orders 144 and 64 and by the Gauss-Lobatto constructor, each priced through the static <c>Calculate</c> and
/// through the engine object.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BatesEngineVectorTests
{
    [Fact]
    public void BatesEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/batesengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var model = new BatesModel(BatesProcessVectorTests.BatesProcessOf(c.Input("process")));
            JsonElement spec = c.Input("engine");
            BatesEngine engine = EngineOf(model, spec);
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

    private static BatesEngine EngineOf(BatesModel model, JsonElement spec) =>
        spec.TryGetProperty("order", out JsonElement order)
            ? new BatesEngine(model, order.GetInt32())
            : new BatesEngine(model, spec.GetProperty("relTolerance").GetDouble(), spec.GetProperty("maxEvaluations").GetInt32());

    private static OneAssetOptionResults Calculated(BatesModel model, JsonElement spec, VanillaOption option, EvaluationContext context) =>
        spec.TryGetProperty("order", out JsonElement order)
            ? BatesEngine.Calculate(option, context, model, order.GetInt32())
            : BatesEngine.Calculate(
                option, context, model, spec.GetProperty("relTolerance").GetDouble(), spec.GetProperty("maxEvaluations").GetInt32());
}
