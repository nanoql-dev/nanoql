// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdHestonBarrierEngineVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdHestonDoubleBarrierEngine vectors, <c>tests/vectors/pricingengines/barrier/fdhestondoublebarrierengine.json</c>:
/// the NPV, delta, gamma and theta of double knock-outs on two corridors with and without a rebate on three Heston
/// parameter sets, and on the first: QuantLib's default grid, damping steps, a scheme, a leverage function, a mixing
/// factor, an expired option and the refusal of the KIKO type. The outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdHestonDoubleBarrierEngineVectorTests
{
    [Fact]
    public void FdHestonDoubleBarrierEngineVector()
    {
        var file = VectorFile.Load("pricingengines/barrier/fdhestondoublebarrierengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("pricing");
            var priced = new Lazy<OneAssetOptionResults>(() =>
            {
                FdHestonPricing p = PricingOf(spec, context);
                JsonElement barrier = spec.GetProperty("barrier");
                var option = new DoubleBarrierOption(
                    (DoubleBarrierType)barrier.GetProperty("barrierType").GetInt32(),
                    barrier.GetProperty("barrierLo").GetDouble(),
                    barrier.GetProperty("barrierHi").GetDouble(),
                    barrier.GetProperty("rebate").GetDouble(),
                    p.Payoff,
                    p.Exercise);
                return FdHestonDoubleBarrierEngine.Calculate(
                    option, context, p.Model, p.TGrid, p.XGrid, p.VGrid, p.DampingSteps, p.Scheme, p.Leverage, p.MixingFactor);
            });
            CheckResults(check, c, priced);
        }

        check.Complete();
    }
}
