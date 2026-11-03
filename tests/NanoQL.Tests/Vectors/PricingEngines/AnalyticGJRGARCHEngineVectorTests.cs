// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticGJRGARCHEngine vectors, <c>tests/vectors/pricingengines/vanilla/analyticgjrgarchengine.json</c>: four
/// GJR-GARCH models on a flat and a zero risk-free curve, the NPVs of calls and puts at six strikes and five maturities,
/// each priced through one engine object, its moments' memo warm, and through the static <c>Calculate</c>, a fresh
/// engine, bit for bit.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticGJRGARCHEngineVectorTests
{
    [Fact]
    public void AnalyticGJRGARCHEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/analyticgjrgarchengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var model = new GJRGARCHModel(GJRGARCHProcessVectorTests.GJRGARCHProcessOf(c.Input("process")));
            var engine = new AnalyticGJRGARCHEngine(model);
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
                            Assert.Equal(value, AnalyticGJRGARCHEngine.Calculate(option, context, model).NPV);
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
