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
/// The overnight-indexed coupon vectors, <c>tests/vectors/cashflows/overnightindexedcoupon.json</c>: coupons over the
/// conventions of <see cref="OvernightIndexedCoupon"/>, telescopic ones among them by the plan's telescopic rule, with
/// their rates, amounts, accrued amounts, date lists, fixings, effective spread and index fixing; the three paths of a
/// spread compounded daily; and legs built with <see cref="OvernightLeg"/>, with each cash flow's type, dates, nominal,
/// rate and amount.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class OvernightIndexedCouponVectorTests
{
    [Fact]
    public void OvernightIndexedCouponVector()
    {
        var file = VectorFile.Load("cashflows/overnightindexedcoupon");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Inputs.TryGetProperty("coupon", out JsonElement spec))
            {
                CheckCoupon(check, c, spec, context);
            }
            else
            {
                CheckLeg(check, c, c.Input("leg"), context);
            }
        }

        check.Complete();
    }

    private static void CheckCoupon(VectorAssert check, VectorCase c, JsonElement spec, EvaluationContext context)
    {
        OvernightIndex index = OvernightInputs.IndexOf(spec.GetProperty("index"));
        OvernightIndexedCoupon? coupon = null;
        check.Output(c, "built", () =>
        {
            coupon = OvernightInputs.CouponOf(spec, index);
            return true;
        });
        if (coupon is null)
        {
            return;
        }

        check.Output(c, "rate", () => coupon.Rate(context));
        check.Output(c, "amount", () => coupon.Amount(context));
        check.Output(
            c,
            "accruedAmount",
            c.DateInputs("accrualDates").Select(d => (Func<object?>)(() => coupon.AccruedAmount(d, context))).ToArray());
        check.Output(c, "fixingDate", coupon.FixingDate());
        check.Output(c, "fixingDates", coupon.FixingDates());
        check.Output(c, "valueDates", coupon.ValueDates());
        check.Output(c, "interestDates", coupon.InterestDates());
        check.Output(c, "dt", coupon.Dt());
        check.Output(c, "indexFixings", () => coupon.IndexFixings(context));
        check.Output(c, "effectiveSpread", () => coupon.EffectiveSpread(context));
        check.Output(c, "effectiveIndexFixing", () => coupon.EffectiveIndexFixing(context));
        check.Output(c, "rateComputationStartDate", coupon.RateComputationStartDate);
        check.Output(c, "rateComputationEndDate", coupon.RateComputationEndDate);
        check.Output(c, "averagingMethod", coupon.AveragingMethod);
        check.Output(c, "lockoutDays", coupon.LockoutDays);
        check.Output(c, "applyObservationShift", coupon.ApplyObservationShift);
        check.Output(c, "compoundSpreadDaily", coupon.CompoundSpreadDaily);
        check.Output(c, "canApplyTelescopicFormula", coupon.CanApplyTelescopicFormula);
        check.Output(c, "fixingDays", coupon.FixingDays);
    }

    private static void CheckLeg(VectorAssert check, VectorCase c, JsonElement spec, EvaluationContext context)
    {
        OvernightIndex index = OvernightInputs.IndexOf(spec.GetProperty("index"));
        ImmutableArray<CashFlow> leg = default;
        check.Output(c, "size", () =>
        {
            leg = OvernightInputs.LegBuilderOf(spec, index, context).ToLeg();
            return leg.Length;
        });
        if (leg.IsDefault)
        {
            return;
        }

        Coupon[] coupons = leg.Cast<Coupon>().ToArray();
        check.Output(c, "types", leg.Select(OvernightInputs.TypeName).ToArray());
        check.Output(c, "paymentDates", coupons.Select(x => x.Date).ToArray());
        check.Output(c, "accrualStartDates", coupons.Select(x => x.AccrualStartDate).ToArray());
        check.Output(c, "accrualEndDates", coupons.Select(x => x.AccrualEndDate).ToArray());
        check.Output(c, "referencePeriodStarts", coupons.Select(x => x.ReferencePeriodStart).ToArray());
        check.Output(c, "referencePeriodEnds", coupons.Select(x => x.ReferencePeriodEnd).ToArray());
        check.Output(c, "nominals", coupons.Select(x => x.Nominal(context)).ToArray());
        check.Output(c, "rates", coupons.Select(x => (Func<object?>)(() => x.Rate(context))).ToArray());
        check.Output(c, "amounts", coupons.Select(x => (Func<object?>)(() => x.Amount(context))).ToArray());
        check.Output(c, "rateComputationStartDates", coupons.Select(x => Underlying(x)?.RateComputationStartDate).ToArray());
        check.Output(c, "rateComputationEndDates", coupons.Select(x => Underlying(x)?.RateComputationEndDate).ToArray());
        check.Output(c, "gearings", coupons.Select(x => (x as FloatingRateCoupon)?.Gearing).ToArray());
        check.Output(c, "spreads", coupons.Select(x => (x as FloatingRateCoupon)?.Spread).ToArray());
        check.Output(c, "caps", coupons.Select(x => (x as CappedFlooredOvernightIndexedCoupon)?.Cap).ToArray());
        check.Output(c, "floors", coupons.Select(x => (x as CappedFlooredOvernightIndexedCoupon)?.Floor).ToArray());
    }

    private static OvernightIndexedCoupon? Underlying(Coupon c) => c switch
    {
        CappedFlooredOvernightIndexedCoupon cf => cf.Underlying,
        OvernightIndexedCoupon on => on,
        _ => null,
    };
}
