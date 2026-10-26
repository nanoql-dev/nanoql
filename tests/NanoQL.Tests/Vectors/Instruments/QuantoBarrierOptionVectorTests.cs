// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The QuantoBarrierOption vectors, <c>tests/vectors/instruments/quantobarrieroption.json</c>: the four barrier types
/// with and without a rebate over two expiries on four processes under two quanto specs, a call and a put at the spot,
/// priced by the quanto engine over the analytic barrier engine: the NPV and the Greeks, which that engine leaves unset;
/// an expired option and the refusal. The outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class QuantoBarrierOptionVectorTests
{
    private static readonly (string Name, Func<OneAssetOptionResults, double?> Get)[] Greeks =
    [
        ("delta", r => r.Delta), ("gamma", r => r.Gamma), ("theta", r => r.Theta), ("vega", r => r.Vega),
        ("rho", r => r.Rho), ("dividendRho", r => r.DividendRho),
    ];

    [Fact]
    public void QuantoBarrierOptionVector()
    {
        var file = VectorFile.Load("instruments/quantobarrieroption");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            JsonElement q = c.Input("quanto");
            YieldTermStructure foreign = CurveOf(q.GetProperty("foreign"));
            BlackVolTermStructure fxVol = BlackVolOf(q.GetProperty("fxVolatility"), context);
            double rho = q.GetProperty("correlation").GetDouble();
            JsonElement barrier = c.Input("barrier");
            Lazy<OneAssetOptionResults>[] priced =
            [
                .. c.Input("payoffs").EnumerateArray().Select(p => new Lazy<OneAssetOptionResults>(
                    () => QuantoEngine.Calculate(
                        new QuantoBarrierOption(
                            (BarrierType)barrier.GetProperty("barrierType").GetInt32(),
                            barrier.GetProperty("barrier").GetDouble(),
                            barrier.GetProperty("rebate").GetDouble(),
                            PayoffOf(p),
                            ExerciseOf(c.Input("exercise"), context)),
                        context,
                        process,
                        foreign,
                        fxVol,
                        rho,
                        AnalyticBarrierEngine.Calculate))),
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
