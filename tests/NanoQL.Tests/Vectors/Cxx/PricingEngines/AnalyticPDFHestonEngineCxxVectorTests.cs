// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's analytic PDF engine vectors, <c>tests/vectors/cxx/pricingengines/vanilla/analyticpdfhestonengine.json</c>:
/// the density of the log spot and the distribution of the spot at five points and three times, on two Heston models
/// at two accuracies.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticPDFHestonEngineCxxVectorTests
{
    [Fact]
    public void AnalyticPDFHestonEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/vanilla/analyticpdfhestonengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var engine = new AnalyticPDFHestonEngine(
                new HestonModel(HestonCxxInputs.ProcessOf(c.Input("process"))), c.Double("eps"), c.Input("maxIterations").GetInt32());
            double[] xs = c.Doubles("xs"), ss = c.Doubles("ss"), ts = c.Doubles("ts");
            check.Output(c, "pv", xs.SelectMany(x => ts.Select(t => Lazy(() => engine.Pv(x, t)))).ToArray());
            check.Output(c, "cdf", ss.SelectMany(s => ts.Select(t => Lazy(() => engine.Cdf(s, t)))).ToArray());
        }

        check.Complete();
    }
}
