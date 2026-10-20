// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The year-on-year inflation swap vectors, <c>tests/vectors/instruments/yearonyearinflationswap.json</c>:
/// <see cref="YearOnYearInflationSwap"/>'s legs and the discounting engine's results, its fair rate and spread among
/// them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class YearOnYearInflationSwapVectorTests
{
    [Fact]
    public void YearOnYearInflationSwapVector()
    {
        var file = VectorFile.Load("instruments/yearonyearinflationswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            (InflationIndex index, EvaluationContext context) = InflationCashFlowInputs.IndexOf(c);
            YearOnYearInflationSwap swap = SwapOf(c, (YoYInflationIndex)index, context);
            YieldTermStructure nominal = CashFlowInputs.CurveOf(c.Input("nominalCurve"));
            YearOnYearInflationSwapResults results = DiscountingSwapEngine.Calculate(swap, context, nominal);
            check.Output(c, "fixedDates", swap.FixedLeg.Select(f => f.Date));
            check.Output(c, "fixedAmounts", swap.FixedLeg.Select(f => (Func<object?>)(() => f.Amount(context))).ToArray());
            check.Output(c, "yoyDates", swap.YoYLeg.Select(f => f.Date));
            check.Output(c, "yoyAmounts", swap.YoYLeg.Select(f => (Func<object?>)(() => f.Amount(context))).ToArray());
            check.Output(c, "NPV", results.NPV);
            check.Output(c, "legNPV", results.LegNPV.Select(x => (object?)x));
            check.Output(c, "legBPS", results.LegBPS.Select(x => (object?)x));
            check.Output(c, "startDiscounts", results.StartDiscounts.Select(x => (object?)x));
            check.Output(c, "endDiscounts", results.EndDiscounts.Select(x => (object?)x));
            check.Output(c, "npvDateDiscount", results.NpvDateDiscount);
            check.Output(c, "fixedLegNPV", results.FixedLegNPV);
            check.Output(c, "yoyLegNPV", results.YoYLegNPV);
            check.Output(c, "fairRate", results.FairRate);
            check.Output(c, "fairSpread", results.FairSpread);
        }

        check.Complete();
    }

    /// <summary>The swap a case's arguments describe.</summary>
    /// <param name="c">The case.</param>
    /// <param name="index">The year-on-year inflation index.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The swap.</returns>
    internal static YearOnYearInflationSwap SwapOf(VectorCase c, YoYInflationIndex index, EvaluationContext context) =>
        new(
            (SwapType)c.Int("type"),
            c.Double("nominal"),
            CashFlowInputs.ScheduleOf(c.Input("fixedSchedule"), context),
            c.Double("fixedRate"),
            CashFlowInputs.DayCounterOf(c.Input("fixedDayCount")),
            CashFlowInputs.ScheduleOf(c.Input("yoySchedule"), context),
            index,
            InflationCashFlowInputs.PeriodOf(c.Input("observationLag")),
            (CPIInterpolationType)c.Int("interpolation"),
            c.Double("spread"),
            CashFlowInputs.DayCounterOf(c.Input("yoyDayCount")),
            CashFlowInputs.CalendarOf(c.Input("paymentCalendar")),
            (BusinessDayConvention)c.Int("paymentConvention"));
}
