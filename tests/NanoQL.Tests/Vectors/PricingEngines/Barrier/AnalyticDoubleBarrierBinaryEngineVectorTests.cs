// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BarrierInputs;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticDoubleBarrierBinaryEngine vectors,
/// <c>tests/vectors/pricingengines/barrier/analyticdoublebarrierbinaryengine.json</c>: cash-or-nothing double
/// knock-outs and knock-ins over three corridors and expiries on seven processes, KIKO and KOKI on markets where
/// QuantLib's sums converge, the degenerate branches with their Greeks, an expired option and the refusals. The outputs
/// nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticDoubleBarrierBinaryEngineVectorTests
{
    private static readonly (string Name, Func<OneAssetOptionResults, double?> Get)[] Greeks =
    [
        ("delta", r => r.Delta), ("gamma", r => r.Gamma), ("vega", r => r.Vega), ("rho", r => r.Rho),
    ];

    [Fact]
    public void AnalyticDoubleBarrierBinaryEngineVector()
    {
        var file = VectorFile.Load("pricingengines/barrier/analyticdoublebarrierbinaryengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            var priced = new Lazy<OneAssetOptionResults>(() => AnalyticDoubleBarrierBinaryEngine.Calculate(
                DoubleOptionOf(c.Input("barrier"), c.Input("payoff"), c.Input("exercise"), context), context, process));
            check.Output(c, "NPV", Lazy(() => priced.Value.NPV));
            foreach ((string name, Func<OneAssetOptionResults, double?> get) in Greeks)
            {
                if (c.Expected.TryGetProperty(name, out _))
                {
                    check.Output(c, name, Lazy(() => get(priced.Value)));
                }
            }
        }

        check.Complete();
    }
}
