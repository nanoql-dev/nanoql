// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The swap vectors, <c>tests/vectors/instruments/swap.json</c>: six swaps of fixed-rate legs under eighteen evaluation
/// contexts, with whether each has expired, its number of legs, its payer flags, and its start and maturity dates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SwapVectorTests
{
    [Fact]
    public void SwapVector()
    {
        var file = VectorFile.Load("instruments/swap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Swap swap = CashFlowInputs.SwapOf(c.Input("swap"), context);
            check.Output(c, "isExpired", swap.IsExpired(context));
            check.Output(c, "numberOfLegs", swap.NumberOfLegs);
            check.Output(c, "payer", Enumerable.Range(0, swap.NumberOfLegs).Select(swap.Payer).ToArray());
            check.Output(c, "startDate", () => swap.StartDate());
            check.Output(c, "maturityDate", () => swap.MaturityDate());

            // Leg(j) is the leg Legs holds.
            for (int j = 0; j < swap.NumberOfLegs; ++j)
            {
                Assert.Equal(swap.Legs[j], swap.Leg(j));
            }
        }

        check.Complete();
    }
}
