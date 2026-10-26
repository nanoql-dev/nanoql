// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.ExoticInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticCliquetEngine vectors, <c>tests/vectors/pricingengines/cliquet/analyticcliquetengine.json</c>:
/// <see cref="AnalyticCliquetEngine"/>'s NPV and Greeks for cliquet calls and puts on flat markets and on zero curves
/// (its theta left out there, the S-exo plan's ER5 (xxii)).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticCliquetEngineVectorTests
{
    [Fact]
    public void AnalyticCliquetEngineVector()
    {
        var file = VectorFile.Load("pricingengines/cliquet/analyticcliquetengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CliquetOption option = CliquetOf(c.Input("option"), context);
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            var r = new Lazy<OneAssetOptionResults>(() => AnalyticCliquetEngine.Calculate(option, context, process));
            CheckGreeks(check, c, r);
        }

        check.Complete();
    }

    // The NPV and the Greeks a case lists.
    internal static void CheckGreeks(VectorAssert check, VectorCase c, Lazy<OneAssetOptionResults> r)
    {
        check.Output(c, "NPV", () => r.Value.NPV);
        foreach ((string name, Func<OneAssetOptionResults, double?> read) in new (string, Func<OneAssetOptionResults, double?>)[]
        {
            ("delta", x => x.Delta), ("gamma", x => x.Gamma), ("theta", x => x.Theta), ("vega", x => x.Vega),
            ("rho", x => x.Rho), ("dividendRho", x => x.DividendRho),
        })
        {
            if (c.Expected.TryGetProperty(name, out _))
            {
                check.Output(c, name, () => read(r.Value));
            }
        }
    }
}
