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
/// The AnalyticPerformanceEngine vectors, <c>tests/vectors/pricingengines/cliquet/analyticperformanceengine.json</c>:
/// <see cref="AnalyticPerformanceEngine"/>'s NPV and Greeks for performance calls and puts on flat markets and on zero
/// curves (its theta left out there).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticPerformanceEngineVectorTests
{
    [Fact]
    public void AnalyticPerformanceEngineVector()
    {
        var file = VectorFile.Load("pricingengines/cliquet/analyticperformanceengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CliquetOption option = CliquetOf(c.Input("option"), context);
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            var r = new Lazy<OneAssetOptionResults>(() => AnalyticPerformanceEngine.Calculate(option, context, process));
            AnalyticCliquetEngineVectorTests.CheckGreeks(check, c, r);
        }

        check.Complete();
    }
}
