// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Heston expansion vectors, <c>tests/vectors/cxx/pricingengines/vanilla/hestonexpansionengine.json</c>:
/// the implied volatilities of LPP2, LPP3 and Forde's expansions at seven strikes and three or four terms on two
/// parameter sets, and the expansion engine's NPVs of calls and puts at five strikes and two maturities on two Heston
/// models, every κt at or above the expansions' cuts.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HestonExpansionEngineCxxVectorTests
{
    [Fact]
    public void HestonExpansionEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/vanilla/hestonexpansionengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var formula = (HestonExpansionEngineHestonExpansionFormula)c.Input("formula").GetInt32();
            if (c.Input("kind").GetString() == "expansion")
            {
                double kappa = c.Double("kappa"), theta = c.Double("theta"), sigma = c.Double("sigma"), v0 = c.Double("v0");
                double rho = c.Double("rho"), term = c.Double("term"), forward = c.Double("forward");
                HestonExpansion expansion = formula switch
                {
                    HestonExpansionEngineHestonExpansionFormula.LPP2 => new LPP2HestonExpansion(kappa, theta, sigma, v0, rho, term),
                    HestonExpansionEngineHestonExpansionFormula.LPP3 => new LPP3HestonExpansion(kappa, theta, sigma, v0, rho, term),
                    _ => new FordeHestonExpansion(kappa, theta, sigma, v0, rho, term),
                };
                check.Output(c, "impliedVolatility", c.Doubles("strikes").Select(k => Lazy(() => expansion.ImpliedVolatility(k, forward))).ToArray());
                continue;
            }

            EvaluationContext context = c.Context();
            var model = new HestonModel(HestonCxxInputs.ProcessOf(c.Input("process")));
            double[] strikes = c.Doubles("strikes");
            var npv = new List<Func<object?>>();
            foreach (int days in c.Ints("maturities"))
            {
                var exercise = new EuropeanExercise(context.EvaluationDate + days);
                foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
                {
                    foreach (double k in strikes)
                    {
                        var option = new VanillaOption(new PlainVanillaPayoff(type, k), exercise);
                        npv.Add(() => HestonExpansionEngine.Calculate(option, context, model, formula).NPV);
                    }
                }
            }

            check.Output(c, "npv", npv.ToArray());
        }

        check.Complete();
    }
}
