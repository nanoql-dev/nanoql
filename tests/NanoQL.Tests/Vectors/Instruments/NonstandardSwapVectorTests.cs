// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The wheel's nonstandard swap vectors, <c>tests/vectors/instruments/nonstandardswap.json</c>: on swaps of constant
/// terms, amortizing nominals with step-up rates, gearings and spreads (a zero gearing among them), the capital
/// exchanges, other floating nominals, a semiannual fixed leg and an overnight index paid late, the inspectors, each
/// leg's cash flows and the discounting engine's NPV, leg NPVs and BPSs.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class NonstandardSwapVectorTests
{
    [Fact]
    public void NonstandardSwapVector()
    {
        var file = VectorFile.Load("instruments/nonstandardswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            NonstandardSwap swap = NonstandardInputs.SwapOf(c.Input("swap"), context);
            YieldTermStructure discount = CashFlowInputs.CurveOf(c.Input("discount"));
            check.Output(c, "type", swap.Type);
            check.Output(c, "fixedNominal", swap.FixedNominal);
            check.Output(c, "floatingNominal", swap.FloatingNominal);
            check.Output(c, "fixedRate", swap.FixedRate);
            check.Output(c, "gearings", swap.Gearings);
            check.Output(c, "spreads", swap.Spreads);
            check.Output(c, "paymentConvention", swap.PaymentConvention);
            check.Output(c, "paymentLag", swap.PaymentLag);
            check.Output(c, "fixedDates", swap.FixedLeg.Select(cf => cf.Date).ToArray());
            check.Output(c, "fixedAmounts", swap.FixedLeg.Select(cf => cf.Amount(context)).ToArray());
            check.Output(c, "floatingDates", swap.FloatingLeg.Select(cf => cf.Date).ToArray());
            check.Output(c, "floatingAmounts", swap.FloatingLeg.Select(cf => cf.Amount(context)).ToArray());
            SwapResults r = DiscountingSwapEngine.Calculate(swap, context, discount);
            check.Output(c, "NPV", r.NPV);
            check.Output(c, "legNPV", r.LegNPV);
            check.Output(c, "legBPS", r.LegBPS);
        }

        check.Complete();
    }
}
