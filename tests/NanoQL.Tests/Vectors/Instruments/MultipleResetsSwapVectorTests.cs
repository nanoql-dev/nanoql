// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The multiple-resets swap vectors, <c>tests/vectors/instruments/multipleresetsswap.json</c>:
/// <see cref="MultipleResetsSwap"/>'s inspectors, legs and results on <see cref="DiscountingSwapEngine"/>, its fair
/// spread on the averaging legs.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MultipleResetsSwapVectorTests
{
    [Fact]
    public void MultipleResetsSwapVector()
    {
        var file = VectorFile.Load("instruments/multipleresetsswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            MultipleResetsSwap swap = SwapOf(c.Input("swap"), context);
            YieldTermStructure discount = IndexInputs.CurveOf(c.Input("discountCurve"))!;
            SwapInputs.CheckInspectors(check, c, swap);
            check.Output(c, "fullResetSchedule", swap.FullResetSchedule.Dates);
            check.Output(c, "resetsPerCoupon", swap.ResetsPerCoupon);
            check.Output(c, "averagingMethod", (int)swap.AveragingMethod);
            SwapInputs.CheckLeg(check, c, "fixedLeg", swap.FixedLeg, context);
            SwapInputs.CheckLeg(check, c, "floatingLeg", swap.FloatingLeg, context);

            FixedVsFloatingSwapResults results = DiscountingSwapEngine.Calculate(swap, context, discount);
            check.Output(c, "NPV", results.NPV);
            check.Output(c, "legNPV", results.LegNPV);
            check.Output(c, "legBPS", results.LegBPS);
            check.Output(c, "fairRate", results.FairRate);
            if (c.Expected.TryGetProperty("fairSpread", out _))
            {
                check.Output(c, "fairSpread", results.FairSpread);
            }

            check.Output(c, "fixedLegBPS", results.FixedLegBPS);
            check.Output(c, "fixedLegNPV", results.FixedLegNPV);
            check.Output(c, "floatingLegBPS", results.FloatingLegBPS);
            check.Output(c, "floatingLegNPV", results.FloatingLegNPV);
        }

        check.Complete();
    }

    /// <summary>The swap a <c>"swap"</c> spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The swap.</returns>
    internal static MultipleResetsSwap SwapOf(JsonElement spec, EvaluationContext context)
    {
        JsonElement convention = spec.GetProperty("paymentConvention");
        JsonElement calendar = spec.GetProperty("paymentCalendar");
        return new MultipleResetsSwap(
            (SwapType)spec.GetProperty("type").GetInt32(),
            spec.GetProperty("nominal").GetDouble(),
            CashFlowInputs.ScheduleOf(spec.GetProperty("fixedSchedule"), context),
            spec.GetProperty("fixedRate").GetDouble(),
            CashFlowInputs.DayCounterOf(spec.GetProperty("fixedDayCount")),
            CashFlowInputs.ScheduleOf(spec.GetProperty("fullResetSchedule"), context),
            IndexInputs.IborIndexOf(spec.GetProperty("index")),
            spec.GetProperty("resetsPerCoupon").GetInt32(),
            spec.GetProperty("spread").GetDouble(),
            spec.GetProperty("averaging").GetString() == "averaging" ? RateAveragingType.Simple : RateAveragingType.Compound,
            convention.ValueKind == JsonValueKind.Null ? null : (BusinessDayConvention)convention.GetInt32(),
            spec.GetProperty("paymentLag").GetInt32(),
            calendar.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CalendarOf(calendar));
    }
}
