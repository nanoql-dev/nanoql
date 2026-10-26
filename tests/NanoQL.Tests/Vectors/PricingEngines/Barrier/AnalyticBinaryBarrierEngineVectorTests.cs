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
/// The AnalyticBinaryBarrierEngine vectors, <c>tests/vectors/pricingengines/barrier/analyticbinarybarrierengine.json</c>:
/// cash-or-nothing and asset-or-nothing payoffs knocked in or out at two barriers of each type, over three expiries on
/// ten processes; the degenerate branches with their Greeks; a plain-vanilla payoff, an expired option and the
/// refusals. The outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticBinaryBarrierEngineVectorTests
{
    private static readonly (string Name, Func<OneAssetOptionResults, double?> Get)[] Greeks =
    [
        ("delta", r => r.Delta), ("gamma", r => r.Gamma), ("vega", r => r.Vega), ("theta", r => r.Theta),
        ("rho", r => r.Rho), ("dividendRho", r => r.DividendRho),
    ];

    [Fact]
    public void AnalyticBinaryBarrierEngineVector()
    {
        var file = VectorFile.Load("pricingengines/barrier/analyticbinarybarrierengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            Lazy<OneAssetOptionResults>[] priced =
            [
                .. c.Input("payoffs").EnumerateArray().Select(p => new Lazy<OneAssetOptionResults>(
                    () => AnalyticBinaryBarrierEngine.Calculate(
                        OptionOf(c.Input("barrier"), p, c.Input("exercise"), context), context, process))),
            ];
            check.Output(c, "NPV", priced.Select(r => Lazy(() => r.Value.NPV)).ToArray());
            foreach ((string name, Func<OneAssetOptionResults, double?> get) in Greeks)
            {
                if (c.Expected.TryGetProperty(name, out _))
                {
                    check.Output(c, name, priced.Select(r => Lazy(() => get(r.Value))).ToArray());
                }
            }
        }

        check.Complete();
    }
}
