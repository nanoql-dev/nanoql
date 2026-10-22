// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BarrierInputs;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticBarrierEngine vectors, <c>tests/vectors/pricingengines/barrier/analyticbarrierengine.json</c>: the four
/// barrier types at two barriers each, with and without a rebate, over three expiries on ten processes, calls and puts
/// at three strikes; an expired option and the refusals. The outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticBarrierEngineVectorTests
{
    [Fact]
    public void AnalyticBarrierEngineVector()
    {
        var file = VectorFile.Load("pricingengines/barrier/analyticbarrierengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            check.Output(
                c,
                "NPV",
                c.Input("payoffs").EnumerateArray()
                    .Select(p => Lazy(() => AnalyticBarrierEngine.Calculate(
                        OptionOf(c.Input("barrier"), p, c.Input("exercise"), context), context, process).NPV))
                    .ToArray());
        }

        check.Complete();
    }
}
