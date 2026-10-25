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
/// The AnalyticPDFHestonEngine vectors, <c>tests/vectors/pricingengines/vanilla/analyticpdfhestonengine.json</c>: two
/// Heston models on flat and zero curves, the NPVs of plain calls and puts, cash-or-nothing calls and asset-or-nothing
/// puts at five strikes and two maturities at two accuracies, each priced through the static <c>Calculate</c> and
/// through the engine object. The output is the window's value, QuantLib's price: the NPV less the tails nanoql adds
/// beyond the window (the code review's C2-B1-2), which <c>Divergences/AnalyticPdfHestonTailsTests.cs</c> checks.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticPDFHestonEngineVectorTests
{
    [Fact]
    public void AnalyticPDFHestonEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/analyticpdfhestonengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var model = new HestonModel(HestonProcessVectorTests.HestonProcessOf(c.Input("process")));
            JsonElement spec = c.Input("engine");
            double eps = spec.GetProperty("eps").GetDouble();
            int maxIterations = spec.GetProperty("maxIterations").GetInt32();
            var engine = new AnalyticPDFHestonEngine(model, eps, maxIterations);
            double[] strikes = Doubles(c.Input("strikes"));
            var npv = new List<Func<object?>>();
            foreach (int days in c.Ints("maturities"))
            {
                var exercise = new EuropeanExercise(context.EvaluationDate + days);
                foreach (JsonElement p in c.Input("payoffs").EnumerateArray())
                {
                    string kind = p[0].GetString()!;
                    var type = (OptionType)p[1].GetInt32();
                    foreach (double k in strikes)
                    {
                        var option = new VanillaOption(PayoffOf(kind, type, k), exercise);
                        npv.Add(() =>
                        {
                            OneAssetOptionResults results = engine.Calculate(option, context);
                            Assert.Equal(results.NPV, AnalyticPDFHestonEngine.Calculate(option, context, model, eps, maxIterations).NPV);
                            return results.NPV - (results.AdditionalResults.TryGetValue("tailsValue", out object? tails) ? (double)tails : 0.0);
                        });
                    }
                }
            }

            check.Output(c, "npv", npv.ToArray());
        }

        check.Complete();
    }

    private static StrikedTypePayoff PayoffOf(string kind, OptionType type, double strike) => kind switch
    {
        "PlainVanilla" => new PlainVanillaPayoff(type, strike),
        "CashOrNothing" => new CashOrNothingPayoff(type, strike, 1.0),
        _ => new AssetOrNothingPayoff(type, strike),
    };
}
