// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The zero-coupon inflation swap vectors, <c>tests/vectors/instruments/zerocouponinflationswap.json</c>:
/// <see cref="ZeroCouponInflationSwap"/>'s legs, the discounting engine's results and the results-dependent members,
/// and its construction refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ZeroCouponInflationSwapVectorTests
{
    [Fact]
    public void ZeroCouponInflationSwapVector()
    {
        var file = VectorFile.Load("instruments/zerocouponinflationswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            (InflationIndex index, EvaluationContext context) = InflationCashFlowInputs.IndexOf(c);
            if (c.String("kind") == "construction")
            {
                check.Output(c, "construction", () => SwapOf(c, (ZeroInflationIndex)index) is null ? 1 : 0);
                continue;
            }

            ZeroCouponInflationSwap swap = SwapOf(c, (ZeroInflationIndex)index);
            YieldTermStructure nominal = CashFlowInputs.CurveOf(c.Input("nominalCurve"));
            SwapResults results = DiscountingSwapEngine.Calculate(swap, context, nominal);
            CashFlow fixedFlow = swap.FixedLeg[0];
            var inflationFlow = (IndexedCashFlow)swap.InflationLeg[0];
            check.Output(c, "startDate", swap.StartDate());
            check.Output(c, "maturityDate", swap.MaturityDate());
            check.Output(c, "fixedDate", fixedFlow.Date);
            check.Output(c, "fixedAmount", () => fixedFlow.Amount(context));
            check.Output(c, "inflationDate", inflationFlow.Date);
            check.Output(c, "inflationAmount", () => inflationFlow.Amount(context));
            check.Output(c, "NPV", results.NPV);
            check.Output(c, "legNPV", results.LegNPV.Select(x => (object?)x));
            check.Output(c, "fixedLegNPV", ZeroCouponInflationSwap.FixedLegNPV(results));
            check.Output(c, "inflationLegNPV", ZeroCouponInflationSwap.InflationLegNPV(results));
            check.Output(c, "endDiscounts", results.EndDiscounts.Select(x => (object?)x));
            check.Output(c, "fixedLegBPS", () => swap.FixedLegBPS(results));
            check.Output(c, "inflationBaseDate", inflationFlow.BaseDate());
            check.Output(c, "inflationFixingDate", inflationFlow.FixingDate);
            if (c.Expected.TryGetProperty("fairRate", out _))
            {
                check.Output(c, "fairRate", () => swap.FairRate(context, results));
            }
        }

        check.Complete();
    }

    /// <summary>The swap a case's arguments describe.</summary>
    /// <param name="c">The case.</param>
    /// <param name="index">The zero-inflation index.</param>
    /// <param name="fixedRate">A fixed rate in place of the case's, or <see langword="null"/> for the case's.</param>
    /// <returns>The swap.</returns>
    internal static ZeroCouponInflationSwap SwapOf(VectorCase c, ZeroInflationIndex index, double? fixedRate = null)
    {
        JsonElement infConvention = c.Input("infConvention");
        return new ZeroCouponInflationSwap(
            (SwapType)c.Int("type"),
            c.Double("nominal"),
            c.DateInput("startDate"),
            c.DateInput("maturity"),
            CashFlowInputs.NullableCalendarOf(c.Input("fixCalendar")),
            (BusinessDayConvention)c.Int("fixConvention"),
            CashFlowInputs.DayCounterOf(c.Input("dayCounter")),
            fixedRate ?? c.Double("fixedRate"),
            index,
            InflationCashFlowInputs.PeriodOf(c.Input("observationLag")),
            (CPIInterpolationType)c.Int("interpolation"),
            false,
            CashFlowInputs.NullableCalendarOf(c.Input("infCalendar")),
            infConvention.ValueKind == JsonValueKind.Null ? null : (BusinessDayConvention)infConvention.GetInt32());
    }
}
