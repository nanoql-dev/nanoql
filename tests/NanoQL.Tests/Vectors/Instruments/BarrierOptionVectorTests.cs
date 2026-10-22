// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BarrierInputs;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The BarrierOption vectors, <c>tests/vectors/instruments/barrieroption.json</c>: <see cref="BarrierOption"/>'s
/// implied volatility for the four barrier types with and without a rebate on five processes, through the analytic
/// engine, and with cash dividends through the finite-difference one; the refusals. Each target is one S7R6's filter
/// kept.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BarrierOptionVectorTests
{
    [Fact]
    public void BarrierOptionVector()
    {
        var file = VectorFile.Load("instruments/barrieroption");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            BarrierOption option = OptionOf(c.Input("barrier"), c.Input("payoff"), c.Input("exercise"), context);
            var dividends = DividendsOf(c.Input("dividends"));
            double accuracy = c.Input("accuracy").GetDouble();
            int maxEvaluations = c.Input("maxEvaluations").GetInt32();
            double minVol = c.Input("minVol").GetDouble(), maxVol = c.Input("maxVol").GetDouble();
            check.Output(
                c,
                "impliedVolatility",
                c.Input("targets").EnumerateArray()
                    .Select(t => Lazy(() => option.ImpliedVolatility(
                        t.GetDouble(), process, dividends, context, accuracy, maxEvaluations, minVol, maxVol)))
                    .ToArray());
        }

        check.Complete();
    }
}
