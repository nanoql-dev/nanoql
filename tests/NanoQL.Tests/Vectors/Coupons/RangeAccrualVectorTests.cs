// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The range accrual vectors, <c>tests/vectors/cashflows/rangeaccrual.json</c>:
/// <see cref="RangeAccrualFloatersCoupon"/>'s rate, amount and price under <see cref="RangeAccrualPricerByBgm"/> on
/// each of its three routes, on flat and interpolated smiles, and <see cref="RangeAccrualLeg"/>'s coupons, QuantLib's
/// leg read past its null entries.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RangeAccrualVectorTests
{
    [Fact]
    public void RangeAccrualVector()
    {
        var file = VectorFile.Load("cashflows/rangeaccrual");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            IborIndex index = IndexInputs.IborIndexOf(c.Input("index"));
            if (c.String("kind") == "coupon")
            {
                RangeAccrualPricerByBgm pricer = PricerOf(c.Input("pricer"), context);
                RangeAccrualFloatersCoupon coupon = new RangeAccrualFloatersCoupon(
                    c.DateInput("paymentDate"),
                    c.Double("nominal"),
                    index,
                    c.DateInput("startDate"),
                    c.DateInput("endDate"),
                    c.Int("fixingDays"),
                    CashFlowInputs.DayCounterOf(c.Input("dayCounter")),
                    c.Double("gearing"),
                    c.Double("spread"),
                    c.DateInput("startDate"),
                    c.DateInput("endDate"),
                    CashFlowInputs.ScheduleOf(c.Input("observationSchedule"), context),
                    c.Double("lowerTrigger"),
                    c.Double("upperTrigger")).SetPricer(pricer);
                YieldTermStructure discount = IndexInputs.CurveOf(c.Input("discountCurve"))!;
                check.Output(c, "fixingDate", coupon.FixingDate());
                check.Output(c, "indexFixing", () => coupon.IndexFixing(context));
                check.Output(c, "rate", () => coupon.Rate(context));
                check.Output(c, "amount", () => coupon.Amount(context));
                check.Output(c, "price", () => coupon.Price(discount, context));
                check.Output(c, "accrualPeriod", coupon.AccrualPeriod());
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
                check.Output(c, "construction", () => LegOf(spec, index, context).ToLeg(context).IsDefault ? 1 : 0);
                continue;
            }

            ImmutableArray<CashFlow> leg = CouponPricerFunctions.SetCouponPricer(
                LegOf(spec, index, context).ToLeg(context), PricerOf(c.Input("pricer"), context));
            Coupon[] coupons = [.. leg.Select(f => (Coupon)f)];
            check.Output(
                c, "kinds", coupons.Select(x => x is RangeAccrualFloatersCoupon ? "RangeAccrualFloatersCoupon" : "FixedRateCoupon"));
            check.Output(c, "dates", coupons.Select(x => x.Date));
            check.Output(c, "accrualStartDates", coupons.Select(x => x.AccrualStartDate));
            check.Output(c, "accrualEndDates", coupons.Select(x => x.AccrualEndDate));
            check.Output(c, "fixingDates", coupons.Select(x => (x as RangeAccrualFloatersCoupon)?.FixingDate()));
            check.Output(c, "rates", CouponInputs.Each(coupons, x => x.Rate(context)));
            check.Output(c, "amounts", CouponInputs.Each(coupons, x => x.Amount(context)));
        }

        check.Complete();
    }

    // The pricer a spec describes, its smiles flat or interpolated sections in their floating form.
    internal static RangeAccrualPricerByBgm PricerOf(JsonElement spec, EvaluationContext context) => new(
        spec.GetProperty("correlation").GetDouble(),
        SmileOf(spec.GetProperty("smilesOnExpiry"), context),
        SmileOf(spec.GetProperty("smilesOnPayment"), context),
        spec.GetProperty("withSmile").GetBoolean(),
        spec.GetProperty("byCallSpread").GetBoolean());

    private static SmileSection SmileOf(JsonElement spec, EvaluationContext context) =>
        spec.GetProperty("type").GetString() == "FlatSmileSection"
            ? SmileSectionInputs.SectionOf(spec, context)
            : InterpolatedSmileSectionVectorTests.SectionOf(spec, context);

    // The builder a leg spec describes: a list of one value through the setter taking one value, as QuantLib's
    // conversion repeats the last value; a longer list through the setter taking a list.
    internal static RangeAccrualLeg LegOf(JsonElement spec, IborIndex index, EvaluationContext context)
    {
        JsonElement tenor = spec.GetProperty("observationTenor");
        double[] notionals = CouponInputs.Reals(spec.GetProperty("notionals"));
        int[] fixingDays = [.. spec.GetProperty("fixingDays").EnumerateArray().Select(x => x.GetInt32())];
        double[] gearings = CouponInputs.Reals(spec.GetProperty("gearings"));
        double[] spreads = CouponInputs.Reals(spec.GetProperty("spreads"));
        double[] lowers = CouponInputs.Reals(spec.GetProperty("lowerTriggers"));
        double[] uppers = CouponInputs.Reals(spec.GetProperty("upperTriggers"));
        RangeAccrualLeg leg = new RangeAccrualLeg(CashFlowInputs.ScheduleOf(spec.GetProperty("schedule"), context), index)
            .WithPaymentDayCounter(CashFlowInputs.DayCounterOf(spec.GetProperty("paymentDayCounter")))
            .WithPaymentAdjustment((BusinessDayConvention)spec.GetProperty("paymentAdjustment").GetInt32())
            .WithObservationTenor(new Period(tenor[0].GetInt32(), (TimeUnit)tenor[1].GetInt32()))
            .WithObservationConvention((BusinessDayConvention)spec.GetProperty("observationConvention").GetInt32());
        leg = notionals.Length == 1 ? leg.WithNotionals(notionals[0]) : leg.WithNotionals(notionals);
        leg = fixingDays.Length == 1 ? leg.WithFixingDays(fixingDays[0]) : leg.WithFixingDays(fixingDays);
        leg = gearings.Length == 1 ? leg.WithGearings(gearings[0]) : leg.WithGearings(gearings);
        leg = spreads.Length == 1 ? leg.WithSpreads(spreads[0]) : leg.WithSpreads(spreads);
        leg = lowers.Length == 1 ? leg.WithLowerTriggers(lowers[0]) : leg.WithLowerTriggers(lowers);
        return uppers.Length == 1 ? leg.WithUpperTriggers(uppers[0]) : leg.WithUpperTriggers(uppers);
    }
}
