// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's analytic rough Heston engine vectors,
/// <c>tests/vectors/cxx/pricingengines/vanilla/analyticroughhestonengine.json</c>: the normalized characteristic
/// function, its logarithm and the fractional Riccati solution at five complex arguments and three times, and the time
/// form's prices, by the Adams, Padé and lifted routes on two rough Heston models.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticRoughHestonEngineCxxVectorTests
{
    [Fact]
    public void AnalyticRoughHestonEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/vanilla/analyticroughhestonengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var model = new RoughHestonModel(HestonCxxInputs.ProcessOf(c.Input("process")), c.Double("hurst"));
            var engine = new AnalyticRoughHestonEngine(
                model,
                c.Input("order").GetInt32(),
                c.Input("timeSteps").GetInt32(),
                (AnalyticRoughHestonEngineApproximation)c.Input("approximation").GetInt32(),
                c.Input("nFactors").GetInt32());
            Complex[] zs = [.. c.Input("zs").EnumerateArray().Select(AnalyticPTDHestonEngineCxxVectorTests.ComplexOf)];
            double[] ts = c.Doubles("ts");
            check.Output(c, "chF", zs.SelectMany(z => ts.Select(t => Lazy(() => COSHestonEngineCxxVectorTests.Parts(engine.ChF(z, t))))).ToArray());
            check.Output(
                c, "lnChF", zs.SelectMany(z => ts.Select(t => Lazy(() => COSHestonEngineCxxVectorTests.Parts(engine.LnChF(z, t))))).ToArray());
            check.Output(
                c,
                "riccatiSolution",
                zs.SelectMany(z => ts.Select(t => Lazy(() => COSHestonEngineCxxVectorTests.Parts(engine.RiccatiSolution(z, t))))).ToArray());
            check.Output(
                c,
                "priceVanillaPayoff",
                c.Input("payoffs").EnumerateArray().Select(p => Lazy(() => engine.PriceVanillaPayoff(
                    new PlainVanillaPayoff((OptionType)p[0].GetInt32(), p[1].GetDouble()), p[2].GetDouble()))).ToArray());
        }

        check.Complete();
    }
}
