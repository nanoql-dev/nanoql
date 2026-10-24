// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The CMS spread legs' vectors, <c>tests/vectors/experimental/coupons/cmsspreadcoupon.json</c>: legs on a 10Y-2Y swap
/// spread index, their coupons' terms and index fixings; their rates refuse, without a pricer. And the pricers' base's
/// correlation, as built and on a copy with another, against QuantLib's <c>correlation()</c> and <c>setCorrelation</c>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CmsSpreadCouponVectorTests
{
    [Fact]
    public void CmsSpreadCouponVector()
    {
        var file = VectorFile.Load("experimental/coupons/cmsspreadcoupon");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Inputs.TryGetProperty("pricer", out JsonElement pricerSpec))
            {
                CmsSpreadCouponPricer pricer = new BasePricer(pricerSpec.GetProperty("correlation").GetDouble());
                check.Output(c, "correlation", pricer.Correlation);
                JsonElement reset = pricerSpec.GetProperty("setCorrelation");
                if (reset.ValueKind != JsonValueKind.Null)
                {
                    check.Output(c, "correlationAfterSet", pricer.WithCorrelation(reset.GetDouble()).Correlation);
                }

                continue;
            }

            ImmutableArray<CashFlow> leg = LegOf(c.Input("leg"), context).ToLeg();

            Coupon[] coupons = [.. leg.Select(cf => (Coupon)cf)];
            check.Output(c, "floating", coupons.Select(x => x is FloatingRateCoupon).ToArray());
            check.Output(c, "dates", coupons.Select(x => x.Date).ToArray());
            check.Output(c, "accrualStartDates", coupons.Select(x => x.AccrualStartDate).ToArray());
            check.Output(c, "accrualEndDates", coupons.Select(x => x.AccrualEndDate).ToArray());
            check.Output(c, "referenceStarts", coupons.Select(x => x.ReferencePeriodStart).ToArray());
            check.Output(c, "referenceEnds", coupons.Select(x => x.ReferencePeriodEnd).ToArray());
            check.Output(c, "nominals", CouponInputs.Each(coupons, x => x.Nominal(context)));
            check.Output(c, "rates", CouponInputs.Each(coupons, x => x.Rate(context)));
            check.Output(c, "fixingDates", Floating(coupons, x => x.FixingDate()));
            check.Output(c, "fixingDays", Floating(coupons, x => x.FixingDays));
            check.Output(c, "gearings", Floating(coupons, x => x.Gearing));
            check.Output(c, "spreads", Floating(coupons, x => x.Spread));
            check.Output(c, "inArrears", Floating(coupons, x => x.IsInArrears));
            check.Output(c, "indexFixings", FloatingAttempt(coupons, x => x.IndexFixing(context)));
        }

        check.Complete();
    }

    /// <summary>The builder a leg spec describes, as CMS coupon vectors' legs: a list of one value through the setter
    /// taking one value, a longer list through the setter taking a list.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The context the schedule is built under.</param>
    /// <returns>The builder.</returns>
    internal static CmsSpreadLeg LegOf(JsonElement spec, EvaluationContext context)
    {
        double[] nominals = CouponInputs.Reals(spec.GetProperty("nominals"));
        int?[] fixingDays = [.. spec.GetProperty("fixingDays").EnumerateArray().Select(x => (int?)x.GetInt32())];
        double[] gearings = CouponInputs.Reals(spec.GetProperty("gearings"));
        double[] spreads = CouponInputs.Reals(spec.GetProperty("spreads"));
        double?[] caps = [.. CouponInputs.Reals(spec.GetProperty("caps")).Select(x => (double?)x)];
        double?[] floors = [.. CouponInputs.Reals(spec.GetProperty("floors")).Select(x => (double?)x)];
        var index = new SwapSpreadIndex(
            "CMS10Y2Y",
            SwapIndexInputs.SwapIndexOf(spec.GetProperty("index1")),
            SwapIndexInputs.SwapIndexOf(spec.GetProperty("index2")));

        CmsSpreadLeg leg = new CmsSpreadLeg(SwapInputs.ScheduleOf(spec.GetProperty("schedule"), context), index)
            .WithPaymentDayCounter(CashFlowInputs.DayCounterOf(spec.GetProperty("paymentDayCounter")))
            .WithPaymentAdjustment((BusinessDayConvention)spec.GetProperty("paymentConvention").GetInt32())
            .InArrears(spec.GetProperty("inArrears").GetBoolean());
        leg = nominals.Length == 1 ? leg.WithNotionals(nominals[0]) : leg.WithNotionals(nominals);
        leg = fixingDays.Length == 1 ? leg.WithFixingDays(fixingDays[0]) : leg.WithFixingDays(fixingDays);
        leg = gearings.Length == 1 ? leg.WithGearings(gearings[0]) : leg.WithGearings(gearings);
        leg = spreads.Length == 1 ? leg.WithSpreads(spreads[0]) : leg.WithSpreads(spreads);
        leg = caps.Length == 1 ? leg.WithCaps(caps[0]) : leg.WithCaps(caps);
        leg = floors.Length == 1 ? leg.WithFloors(floors[0]) : leg.WithFloors(floors);
        return leg;
    }

    // A floating coupon's output, null for a fixed coupon.
    private static object?[] Floating<TOut>(Coupon[] coupons, Func<FloatingRateCoupon, TOut> f) =>
        [.. coupons.Select(x => x is FloatingRateCoupon fc ? (object?)f(fc) : null)];

    // A floating coupon's output that may refuse, compared when read; null for a fixed coupon.
    private static Func<object?>[] FloatingAttempt(Coupon[] coupons, Func<FloatingRateCoupon, double> f) =>
        [.. coupons.Select(x => x is FloatingRateCoupon fc ? (Func<object?>)(() => f(fc)) : () => null)];

    // The base's members alone, which the vectors read through QuantLib's lognormal pricer, S-xrt's port.
    private sealed class BasePricer(double? correlation) : CmsSpreadCouponPricer(correlation)
    {
        public override double SwapletPrice(FloatingRateCoupon coupon, EvaluationContext context) => throw new NotSupportedException();

        public override double SwapletRate(FloatingRateCoupon coupon, EvaluationContext context) => throw new NotSupportedException();

        public override double CapletPrice(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) =>
            throw new NotSupportedException();

        public override double CapletRate(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) =>
            throw new NotSupportedException();

        public override double FloorletPrice(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) =>
            throw new NotSupportedException();

        public override double FloorletRate(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) =>
            throw new NotSupportedException();
    }
}
