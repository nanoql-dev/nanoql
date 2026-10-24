// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The zero-coupon swap vectors, <c>tests/vectors/instruments/zerocouponswap.json</c>: <see cref="ZeroCouponSwap"/>s
/// built from a fixed payment or a fixed rate, priced by <see cref="DiscountingSwapEngine"/>: their NPVs, legs and fair
/// payments and rates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ZeroCouponSwapVectorTests
{
    [Fact]
    public void ZeroCouponSwapVector()
    {
        var file = VectorFile.Load("instruments/zerocouponswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            ZeroCouponSwap swap = SwapOf(c);
            YieldTermStructure discount = IndexInputs.CurveOf(c.Input("discountCurve"))!;
            ZeroCouponSwapResults results = DiscountingSwapEngine.Calculate(swap, context, discount);
            CashFlow fixedFlow = swap.FixedLeg[0], floatingFlow = swap.FloatingLeg[0];
            check.Output(c, "NPV", results.NPV);
            check.Output(c, "fixedLegNPV", results.FixedLegNPV);
            check.Output(c, "floatingLegNPV", results.FloatingLegNPV);
            check.Output(c, "fixedPayment", swap.FixedPayment(context));
            check.Output(c, "fixedDate", fixedFlow.Date);
            check.Output(c, "fixedAmount", () => fixedFlow.Amount(context));
            check.Output(c, "floatingDate", floatingFlow.Date);
            check.Output(c, "floatingAmount", () => floatingFlow.Amount(context));
            check.Output(c, "startDate", swap.StartDate());
            check.Output(c, "maturityDate", swap.MaturityDate());
            if (c.Expected.TryGetProperty("fairFixedPayment", out _))
            {
                check.Output(c, "fairFixedPayment", results.FairFixedPayment);
                check.Output(c, "fairFixedRateA365", () => swap.FairFixedRate(results, new Actual365Fixed()));
                check.Output(c, "fairFixedRate30360", () => swap.FairFixedRate(results, new Thirty360(Thirty360Convention.BondBasis)));
            }
        }

        check.Complete();
    }

    /// <summary>The swap a case describes, from its fixed payment or its fixed rate.</summary>
    /// <param name="c">The case.</param>
    /// <returns>The swap.</returns>
    internal static ZeroCouponSwap SwapOf(VectorCase c)
    {
        var type = (SwapType)c.Int("type");
        IborIndex index = IndexInputs.IborIndexOf(c.Input("index"));
        Calendar calendar = CashFlowInputs.CalendarOf(c.Input("paymentCalendar"));
        var convention = (BusinessDayConvention)c.Int("paymentConvention");
        return c.Inputs.TryGetProperty("fixedPayment", out _)
            ? new ZeroCouponSwap(
                type,
                c.Double("baseNominal"),
                c.DateInput("startDate"),
                c.DateInput("maturityDate"),
                c.Double("fixedPayment"),
                index,
                calendar,
                convention,
                c.Int("paymentDelay"))
            : new ZeroCouponSwap(
                type,
                c.Double("baseNominal"),
                c.DateInput("startDate"),
                c.DateInput("maturityDate"),
                c.Double("fixedRate"),
                CashFlowInputs.DayCounterOf(c.Input("fixedDayCounter")),
                index,
                calendar,
                convention,
                c.Int("paymentDelay"));
    }
}
