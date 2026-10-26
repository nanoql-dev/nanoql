// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdHestonBarrierEngineVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdHestonRebateEngine vectors, <c>tests/vectors/pricingengines/barrier/fdhestonrebateengine.json</c>: the NPV,
/// delta, gamma and theta of the rebate of the four barrier types on three Heston parameter sets, and on the first:
/// QuantLib's default grid, damping steps, a scheme, cash dividends, a leverage function, a mixing factor and an expired
/// option. The outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdHestonRebateEngineVectorTests
{
    [Fact]
    public void FdHestonRebateEngineVector()
    {
        var file = VectorFile.Load("pricingengines/barrier/fdhestonrebateengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("pricing");
            var priced = new Lazy<OneAssetOptionResults>(() =>
            {
                FdHestonPricing p = PricingOf(spec, context);
                JsonElement barrier = spec.GetProperty("barrier");
                var option = new BarrierOption(
                    (BarrierType)barrier.GetProperty("barrierType").GetInt32(),
                    barrier.GetProperty("barrier").GetDouble(),
                    barrier.GetProperty("rebate").GetDouble(),
                    p.Payoff,
                    p.Exercise);
                return FdHestonRebateEngine.Calculate(
                    option, context, p.Model, p.Dividends, p.TGrid, p.XGrid, p.VGrid, p.DampingSteps, p.Scheme, p.Leverage,
                    p.MixingFactor);
            });
            CheckResults(check, c, priced);
        }

        check.Complete();
    }
}
