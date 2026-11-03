// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticTwoAssetBarrierEngine vectors,
/// <c>tests/vectors/pricingengines/barrier/analytictwoassetbarrierengine.json</c>: the four barrier types at two
/// barriers each on the second asset, over two expiries on process pairs sharing a risk-free curve, at three
/// correlations, calls and puts on the first at three strikes; an expired option and the refusals. Every value is within
/// Drezner's accuracy of QuantLib's. The outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticTwoAssetBarrierEngineVectorTests
{
    [Fact]
    public void AnalyticTwoAssetBarrierEngineVector()
    {
        var file = VectorFile.Load("pricingengines/barrier/analytictwoassetbarrierengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process1 = ProcessOf(c.Input("process1"), context);
            GeneralizedBlackScholesProcess process2 = ProcessOf(c.Input("process2"), context);
            double rho = c.Double("correlation");
            var barrier = c.Input("barrier");
            check.Output(
                c,
                "NPV",
                c.Input("payoffs").EnumerateArray()
                    .Select(p => Lazy(() => AnalyticTwoAssetBarrierEngine.Calculate(
                        new TwoAssetBarrierOption(
                            (BarrierType)barrier.GetProperty("barrierType").GetInt32(),
                            barrier.GetProperty("barrier").GetDouble(),
                            PayoffOf(p),
                            ExerciseOf(c.Input("exercise"), context)),
                        context,
                        process1,
                        process2,
                        rho).NPV))
                    .ToArray());
        }

        check.Complete();
    }
}
