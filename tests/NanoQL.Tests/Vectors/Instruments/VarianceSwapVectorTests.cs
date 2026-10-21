// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The VarianceSwap vectors, <c>tests/vectors/instruments/varianceswap.json</c>: long and short swaps' inspectors, and
/// their expiry under evaluation dates on either side of the maturity and on it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class VarianceSwapVectorTests
{
    [Fact]
    public void VarianceSwapVector()
    {
        var file = VectorFile.Load("instruments/varianceswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            VarianceSwap swap = ReplicatingVarianceSwapEngineVectorTests.SwapOf(c.Input("swap"));
            check.Output(c, "position", (int)swap.Position);
            check.Output(c, "strike", swap.Strike);
            check.Output(c, "notional", swap.Notional);
            check.Output(c, "startDate", swap.StartDate.SerialNumber);
            check.Output(c, "maturityDate", swap.MaturityDate.SerialNumber);
            check.Output(c, "isExpired", c.Input("settings").EnumerateArray().Select(s => (object)swap.IsExpired(ContextOf(s))).ToArray());
        }

        check.Complete();
    }

    private static EvaluationContext ContextOf(JsonElement s) =>
        new()
        {
            EvaluationDate = new Date(s.GetProperty("evaluationDate").GetInt32()),
            IncludeReferenceDateEvents = s.GetProperty("includeReferenceDateEvents").GetBoolean(),
        };
}
