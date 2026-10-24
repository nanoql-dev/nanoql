// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The range accrual's harness vectors, <c>tests/vectors/cxx/cashflows/rangeaccrual.json</c>:
/// <see cref="RangeAccrualFloatersCoupon"/>'s times, triggers, observations and price without optionality, the members
/// the wheel doesn't expose, on future and started coupons.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RangeAccrualCxxVectorTests
{
    [Fact]
    public void RangeAccrualCxxVector()
    {
        var file = VectorFile.Load("cxx/cashflows/rangeaccrual");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("coupon");
            Date start = new(spec.GetProperty("startDate").GetInt32()), end = new(spec.GetProperty("endDate").GetInt32());
            var coupon = new RangeAccrualFloatersCoupon(
                new Date(spec.GetProperty("paymentDate").GetInt32()),
                spec.GetProperty("nominal").GetDouble(),
                IndexInputs.IborIndexOf(spec.GetProperty("index")),
                start,
                end,
                spec.GetProperty("fixingDays").GetInt32(),
                CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
                spec.GetProperty("gearing").GetDouble(),
                spec.GetProperty("spread").GetDouble(),
                start,
                end,
                CashFlowInputs.ScheduleOf(spec.GetProperty("observationSchedule"), context),
                spec.GetProperty("lowerTrigger").GetDouble(),
                spec.GetProperty("upperTrigger").GetDouble());
            YieldTermStructure discount = IndexInputs.CurveOf(c.Input("discountCurve"))!;
            check.Output(c, "startTime", coupon.StartTime);
            check.Output(c, "endTime", coupon.EndTime);
            check.Output(c, "lowerTrigger", coupon.LowerTrigger);
            check.Output(c, "upperTrigger", coupon.UpperTrigger);
            check.Output(c, "observationsNo", coupon.ObservationsNo);
            check.Output(c, "observationDates", coupon.ObservationDates);
            check.Output(c, "observationTimes", coupon.ObservationTimes);
            check.Output(c, "observationScheduleDates", coupon.ObservationSchedule.Dates);
            check.Output(c, "priceWithoutOptionality", () => coupon.PriceWithoutOptionality(discount, context));
        }

        check.Complete();
    }
}
