// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The CMS coupon vectors, <c>tests/vectors/cashflows/cmscoupon.json</c>: the structure of <see cref="CmsLeg"/>s
/// (gearings and spreads, a payment day counter, convention and fixing days, caps and floors, in arrears, amortizing
/// nominals, a zero gearing's fixed coupon, ex-coupon dates, a fixing convention and zero payments), through each
/// setter's one-value and list forms, each leg priced by an <see cref="AnalyticHaganPricer"/> on the standard model and
/// a constant 20% lognormal volatility.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CmsCouponVectorTests
{
    [Fact]
    public void CmsCouponVector()
    {
        var file = VectorFile.Load("cashflows/cmscoupon");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("leg");
            ImmutableArray<CashFlow> leg = CouponPricerFunctions.SetCouponPricer(LegOf(spec, context).ToLeg(), Pricer(context));

            Coupon[] coupons = [.. leg.Select(cf => (Coupon)cf)];
            check.Output(c, "floating", coupons.Select(x => x is FloatingRateCoupon).ToArray());
            check.Output(c, "dates", coupons.Select(x => x.Date).ToArray());
            check.Output(c, "accrualStartDates", coupons.Select(x => x.AccrualStartDate).ToArray());
            check.Output(c, "accrualEndDates", coupons.Select(x => x.AccrualEndDate).ToArray());
            check.Output(c, "referenceStarts", coupons.Select(x => x.ReferencePeriodStart).ToArray());
            check.Output(c, "referenceEnds", coupons.Select(x => x.ReferencePeriodEnd).ToArray());
            check.Output(c, "exCouponDates", coupons.Select(x => x.ExCouponDate).ToArray());
            check.Output(c, "nominals", CouponInputs.Each(coupons, x => x.Nominal(context)));
            check.Output(c, "rates", CouponInputs.Each(coupons, x => x.Rate(context)));
            check.Output(c, "amounts", CouponInputs.Each(coupons, x => x.Amount(context)));
            check.Output(c, "fixingDates", Floating(coupons, x => x.FixingDate()));
            check.Output(c, "fixingDays", Floating(coupons, x => x.FixingDays));
            check.Output(c, "gearings", Floating(coupons, x => x.Gearing));
            check.Output(c, "spreads", Floating(coupons, x => x.Spread));
            check.Output(c, "inArrears", Floating(coupons, x => x.IsInArrears));
        }

        check.Complete();
    }

    // The generator's pricer: Hagan's analytic one on the standard model, no mean reversion, a constant volatility of
    // 20% (Actual/365, TARGET, Modified Following) fixed on the evaluation date.
    private static AnalyticHaganPricer Pricer(EvaluationContext context) => new(
        new ConstantSwaptionVolatility(
            context.EvaluationDate, new TARGET(), BusinessDayConvention.ModifiedFollowing, 0.20, new Actual365Fixed()),
        GFunctionFactoryYieldCurveModel.Standard,
        0.0);

    // The builder a leg spec describes: a list of one value through the setter taking one value, as QuantLib's
    // conversion repeats the last value; a longer list through the setter taking a list.
    private static CmsLeg LegOf(JsonElement spec, EvaluationContext context)
    {
        double[] nominals = CouponInputs.Reals(spec.GetProperty("nominals"));
        int?[] fixingDays = [.. spec.GetProperty("fixingDays").EnumerateArray().Select(x => (int?)x.GetInt32())];
        double[] gearings = CouponInputs.Reals(spec.GetProperty("gearings"));
        double[] spreads = CouponInputs.Reals(spec.GetProperty("spreads"));
        double?[] caps = [.. CouponInputs.Reals(spec.GetProperty("caps")).Select(x => (double?)x)];
        double?[] floors = [.. CouponInputs.Reals(spec.GetProperty("floors")).Select(x => (double?)x)];

        CmsLeg leg = new CmsLeg(
                SwapInputs.ScheduleOf(spec.GetProperty("schedule"), context),
                SwapIndexInputs.SwapIndexOf(spec.GetProperty("index")))
            .WithPaymentDayCounter(CashFlowInputs.DayCounterOf(spec.GetProperty("paymentDayCounter")))
            .WithPaymentAdjustment((BusinessDayConvention)spec.GetProperty("paymentConvention").GetInt32())
            .InArrears(spec.GetProperty("inArrears").GetBoolean())
            .WithZeroPayments(spec.GetProperty("zeroPayments").GetBoolean())
            .WithFixingConvention((BusinessDayConvention)spec.GetProperty("fixingConvention").GetInt32());
        leg = nominals.Length == 1 ? leg.WithNotionals(nominals[0]) : leg.WithNotionals(nominals);
        leg = fixingDays.Length == 1 ? leg.WithFixingDays(fixingDays[0]) : leg.WithFixingDays(fixingDays);
        leg = gearings.Length == 1 ? leg.WithGearings(gearings[0]) : leg.WithGearings(gearings);
        leg = spreads.Length == 1 ? leg.WithSpreads(spreads[0]) : leg.WithSpreads(spreads);
        leg = caps.Length == 1 ? leg.WithCaps(caps[0]) : leg.WithCaps(caps);
        leg = floors.Length == 1 ? leg.WithFloors(floors[0]) : leg.WithFloors(floors);

        JsonElement exCoupon = spec.GetProperty("exCoupon");
        if (exCoupon.ValueKind != JsonValueKind.Null)
        {
            JsonElement period = exCoupon.GetProperty("period");
            leg = leg.WithExCouponPeriod(
                new Period(period[0].GetInt32(), (TimeUnit)period[1].GetInt32()),
                CashFlowInputs.CalendarOf(exCoupon.GetProperty("calendar")),
                (BusinessDayConvention)exCoupon.GetProperty("convention").GetInt32(),
                exCoupon.GetProperty("endOfMonth").GetBoolean());
        }

        return leg;
    }

    // A floating coupon's output, null for a fixed coupon.
    private static object?[] Floating<TOut>(Coupon[] coupons, Func<FloatingRateCoupon, TOut> f) =>
        [.. coupons.Select(x => x is FloatingRateCoupon fc ? (object?)f(fc) : null)];
}
