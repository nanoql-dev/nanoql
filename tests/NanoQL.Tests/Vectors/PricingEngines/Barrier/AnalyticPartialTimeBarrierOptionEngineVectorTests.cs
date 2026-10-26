// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticPartialTimeBarrierOptionEngine vectors,
/// <c>tests/vectors/pricingengines/barrier/analyticpartialtimebarrieroptionengine.json</c>: knock-outs in each window
/// and start-window knock-ins at two barriers each, over three expiries on ten processes, calls and puts at three
/// strikes; an expired option and the refusals. Every value is within Drezner's accuracy of QuantLib's (the S-bar plan's
/// BR4 (e)). The outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticPartialTimeBarrierOptionEngineVectorTests
{
    [Fact]
    public void AnalyticPartialTimeBarrierOptionEngineVector()
    {
        var file = VectorFile.Load("pricingengines/barrier/analyticpartialtimebarrieroptionengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            var barrier = c.Input("barrier");
            var range = (PartialBarrierRange)c.Int("barrierRange");
            Date cover = c.DateInput("coverEventDate");
            check.Output(
                c,
                "NPV",
                c.Input("payoffs").EnumerateArray()
                    .Select(p => Lazy(() => AnalyticPartialTimeBarrierOptionEngine.Calculate(
                        new PartialTimeBarrierOption(
                            (BarrierType)barrier.GetProperty("barrierType").GetInt32(),
                            range,
                            barrier.GetProperty("barrier").GetDouble(),
                            barrier.GetProperty("rebate").GetDouble(),
                            cover,
                            PayoffOf(p),
                            ExerciseOf(c.Input("exercise"), context)),
                        context,
                        process).NPV))
                    .ToArray());
        }

        check.Complete();
    }
}
