// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The fixed-vs-floating swap vectors, <c>tests/vectors/instruments/fixedvsfloatingswap.json</c>: vanilla and
/// overnight-indexed swaps read as <see cref="FixedVsFloatingSwap"/>s, with the base's inspectors, each leg's payment
/// dates and amounts, and the results of <see cref="DiscountingSwapEngine"/>: the fair rate and spread its results
/// derive, and the same results once the swap has expired.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FixedVsFloatingSwapVectorTests
{
    [Fact]
    public void FixedVsFloatingSwapVector()
    {
        var file = VectorFile.Load("instruments/fixedvsfloatingswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            FixedVsFloatingSwap swap = SwapInputs.SwapOf(c.Input("swap"), context);
            SwapInputs.CheckInspectors(check, c, swap);
            SwapInputs.CheckLeg(check, c, "fixedLeg", swap.FixedLeg, context);
            SwapInputs.CheckLeg(check, c, "floatingLeg", swap.FloatingLeg, context);

            FlatForward discount = CashFlowInputs.CurveOf(c.Input("discountCurve"));
            SwapInputs.CheckResults(check, c, () => DiscountingSwapEngine.Calculate(swap, context, discount));

            EvaluationContext expired = context with { EvaluationDate = c.DateInput("expiredOn") };
            check.Output(c, "expiredIsExpired", swap.IsExpired(expired));
            SwapInputs.CheckResults(
                check, c, () => DiscountingSwapEngine.Calculate(swap, expired, discount), "expired");
        }

        check.Complete();
    }
}
