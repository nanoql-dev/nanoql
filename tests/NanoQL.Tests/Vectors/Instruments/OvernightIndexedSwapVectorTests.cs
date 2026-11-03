// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The overnight-indexed swap vectors, <c>tests/vectors/instruments/overnightindexedswap.json</c>: QuantLib's four
/// constructors over the conventions the swap passes to its overnight leg, telescopic swaps among them and the
/// refusals QuantLib makes (a telescopic lookback without the shift, 17 places of rounding), with the swap's
/// inspectors, each leg's payment dates and amounts, and its results; a one-date overnight schedule; and QuantLib's
/// testNotifications swap, priced on one forecasting curve and rebuilt on another, the rebuild that replaces
/// QuantLib's relink.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class OvernightIndexedSwapVectorTests
{
    [Fact]
    public void OvernightIndexedSwapVector()
    {
        var file = VectorFile.Load("instruments/overnightindexedswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            OvernightIndexedSwap? swap = null;
            check.Output(c, "built", () =>
            {
                swap = SwapInputs.OvernightIndexedSwapOf(c.Input("swap"), context);
                return true;
            });
            if (swap is null)
            {
                continue;
            }

            check.Output(c, "paymentFrequency", () => swap.PaymentFrequency());
            check.Output(c, "overnightIndex", swap.OvernightIndex.Name);
            check.Output(c, "averagingMethod", swap.AveragingMethod);
            check.Output(c, "lookbackDays", swap.LookbackDays);
            check.Output(c, "lockoutDays", swap.LockoutDays);
            check.Output(c, "applyObservationShift", swap.ApplyObservationShift);
            check.Output(c, "paymentLag", swap.PaymentLag);
            check.Output(c, "paymentConvention", swap.PaymentConvention);
            check.Output(c, "nominal", () => swap.Nominal());
            check.Output(c, "nominals", () => swap.Nominals());
            SwapInputs.CheckLeg(check, c, "fixedLeg", swap.FixedLeg, context);
            SwapInputs.CheckLeg(check, c, "overnightLeg", swap.OvernightLeg, context);

            FlatForward discount = CashFlowInputs.CurveOf(c.Input("discountCurve"));
            bool? include = c.NullableBool("includeSettlementDateFlows");
            OvernightIndexedSwapResults? results = null;
            SwapInputs.CheckResults(
                check, c, () => results = DiscountingSwapEngine.Calculate(swap, context, discount, include));
            if (c.Expected.TryGetProperty("overnightLegBPS", out _))
            {
                check.Output(c, "overnightLegBPS", () => results!.OvernightLegBPS);
                check.Output(c, "overnightLegNPV", () => results!.OvernightLegNPV);
            }
        }

        check.Complete();
    }
}
