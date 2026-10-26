// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticSoftBarrierEngine vectors, <c>tests/vectors/pricingengines/barrier/analyticsoftbarrierengine.json</c>:
/// down-and-in and down-and-out calls and up-and-in and up-and-out puts in Haug's domain, two bands each over three
/// expiries on the processes clear of the form's singular carries, at three strikes; the degenerate branches, an
/// expired option and the refusals; and the option's implied volatility. The outputs nanoql fixes are left out of the
/// file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticSoftBarrierEngineVectorTests
{
    [Fact]
    public void AnalyticSoftBarrierEngineVector()
    {
        var file = VectorFile.Load("pricingengines/barrier/analyticsoftbarrierengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            var barrier = c.Input("barrier");
            SoftBarrierOption OptionOf(JsonElement payoff) => new(
                (BarrierType)barrier.GetProperty("barrierType").GetInt32(),
                barrier.GetProperty("barrierLo").GetDouble(),
                barrier.GetProperty("barrierHi").GetDouble(),
                PayoffOf(payoff),
                ExerciseOf(c.Input("exercise"), context));
            if (c.Inputs.TryGetProperty("impliedVolatility", out JsonElement iv))
            {
                check.Output(c, "impliedVolatility", Lazy(() => OptionOf(c.Input("payoff")).ImpliedVolatility(
                    iv.GetProperty("target").GetDouble(),
                    process,
                    context,
                    iv.GetProperty("accuracy").GetDouble(),
                    iv.GetProperty("maxEvaluations").GetInt32(),
                    iv.GetProperty("minVol").GetDouble(),
                    iv.GetProperty("maxVol").GetDouble())));
                continue;
            }

            check.Output(
                c,
                "NPV",
                c.Input("payoffs").EnumerateArray()
                    .Select(p => Lazy(() => AnalyticSoftBarrierEngine.Calculate(OptionOf(p), context, process).NPV))
                    .ToArray());
        }

        check.Complete();
    }
}
