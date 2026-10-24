// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The multiple-resets coupon vectors, <c>tests/vectors/cashflows/multipleresetscoupon.json</c>:
/// <see cref="MultipleResetsCoupon"/>'s sub-periods, rate, amount and accrued amount under the compounding and the
/// averaging pricers, future, in progress and past, and <see cref="MultipleResetsLeg"/>'s coupons.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MultipleResetsCouponVectorTests
{
    [Fact]
    public void MultipleResetsCouponVector()
    {
        var file = VectorFile.Load("cashflows/multipleresetscoupon");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            IborIndex index = IndexInputs.IborIndexOf(c.Input("index"));
            if (c.String("kind") == "coupon")
            {
                JsonElement dc = c.Input("dayCounter");
                MultipleResetsPricer pricer = PricerOf(c.String("pricer"));
                MultipleResetsCoupon coupon = new MultipleResetsCoupon(
                    c.DateInput("paymentDate"),
                    c.Double("nominal"),
                    CashFlowInputs.ScheduleOf(c.Input("resetSchedule"), context),
                    c.Int("fixingDays"),
                    index,
                    c.Double("gearing"),
                    c.Double("couponSpread"),
                    c.Double("rateSpread"),
                    c.NullableDateInput("refPeriodStart"),
                    c.NullableDateInput("refPeriodEnd"),
                    dc.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.DayCounterOf(dc),
                    c.NullableDateInput("exCouponDate")).SetPricer(pricer);
                Date accrualDate = c.DateInput("accrualDate");
                check.Output(c, "fixingDate", coupon.FixingDate());
                check.Output(c, "fixingDates", coupon.FixingDates);
                check.Output(c, "valueDates", coupon.ValueDates);
                check.Output(c, "dt", coupon.Dt);
                check.Output(c, "rate", () => coupon.Rate(context));
                check.Output(c, "amount", () => coupon.Amount(context));
                check.Output(c, "accrualPeriod", coupon.AccrualPeriod());
                check.Output(c, "gearing", coupon.Gearing);
                check.Output(c, "spread", coupon.Spread);
                check.Output(c, "rateSpread", coupon.RateSpread);
                check.Output(c, "exCouponDate", coupon.ExCouponDate);
                check.Output(c, "accruedAmount", () => coupon.AccruedAmount(accrualDate, context));
                check.Output(c, "swapletPrice", () => pricer.SwapletPrice(coupon, context));
                check.Output(c, "capletPrice", () => pricer.CapletPrice(coupon, 0.03, context));
                check.Output(c, "capletRate", () => pricer.CapletRate(coupon, 0.03, context));
                check.Output(c, "floorletPrice", () => pricer.FloorletPrice(coupon, 0.03, context));
                check.Output(c, "floorletRate", () => pricer.FloorletRate(coupon, 0.03, context));
                continue;
            }

            JsonElement spec = c.Input("leg");
            if (c.Expected.TryGetProperty("construction", out _))
            {
                check.Output(c, "construction", () => LegOf(spec, index, context).IsDefault ? 1 : 0);
                continue;
            }

            MultipleResetsCoupon[] coupons = [.. LegOf(spec, index, context).Cast<MultipleResetsCoupon>()];
            check.Output(c, "dates", coupons.Select(x => x.Date));
            check.Output(c, "accrualStartDates", coupons.Select(x => x.AccrualStartDate));
            check.Output(c, "accrualEndDates", coupons.Select(x => x.AccrualEndDate));
            check.Output(c, "exCouponDates", coupons.Select(x => x.ExCouponDate));
            check.Output(c, "resets", coupons.Select(x => x.FixingDates.Length));
            check.Output(c, "fixingDates", coupons.Select(x => x.FixingDates.ToArray()));
            check.Output(c, "rates", CouponInputs.Each(coupons, x => x.Rate(context)));
            check.Output(c, "amounts", CouponInputs.Each(coupons, x => x.Amount(context)));
        }

        check.Complete();
    }

    private static MultipleResetsPricer PricerOf(string kind) => kind switch
    {
        "compounding" => new CompoundingMultipleResetsPricer(),
        "averaging" => new AveragingMultipleResetsPricer(),
        _ => throw new InvalidOperationException($"unknown pricer '{kind}'"),
    };

    // The leg a spec describes, every setting given as a list; built inside the call, where QuantLib's builder checks
    // the resets per coupon.
    private static ImmutableArray<CashFlow> LegOf(JsonElement spec, IborIndex index, EvaluationContext context)
    {
        MultipleResetsLeg leg = new MultipleResetsLeg(
                CashFlowInputs.ScheduleOf(spec.GetProperty("schedule"), context),
                index,
                spec.GetProperty("resetsPerCoupon").GetInt32())
            .WithNotionals(CouponInputs.Reals(spec.GetProperty("notionals")))
            .WithPaymentDayCounter(CashFlowInputs.DayCounterOf(spec.GetProperty("paymentDayCounter")))
            .WithPaymentAdjustment((BusinessDayConvention)spec.GetProperty("paymentConvention").GetInt32())
            .WithPaymentCalendar(CashFlowInputs.CalendarOf(spec.GetProperty("paymentCalendar")))
            .WithPaymentLag(spec.GetProperty("paymentLag").GetInt32())
            .WithFixingDays(spec.GetProperty("fixingDays").EnumerateArray().Select(x => x.GetInt32()))
            .WithGearings(CouponInputs.Reals(spec.GetProperty("gearings")))
            .WithCouponSpreads(CouponInputs.Reals(spec.GetProperty("couponSpreads")))
            .WithRateSpreads(CouponInputs.Reals(spec.GetProperty("rateSpreads")))
            .WithAveragingMethod(spec.GetProperty("averaging").GetString() == "averaging"
                ? RateAveragingType.Simple
                : RateAveragingType.Compound);
        JsonElement ex = spec.GetProperty("exCoupon");
        if (ex.ValueKind != JsonValueKind.Null)
        {
            JsonElement period = ex.GetProperty("period");
            JsonElement cal = ex.GetProperty("calendar");
            leg = leg.WithExCouponPeriod(
                new Period(period[0].GetInt32(), (TimeUnit)period[1].GetInt32()),
                cal.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CalendarOf(cal),
                (BusinessDayConvention)ex.GetProperty("convention").GetInt32(),
                ex.GetProperty("endOfMonth").GetBoolean());
        }

        return leg.ToLeg();
    }
}
