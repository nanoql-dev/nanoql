// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Ibor coupon vectors, <c>tests/vectors/cxx/cashflows/iborcoupon.json</c>: the five quantities an
/// <see cref="IborCoupon"/> computes from its pricer, under both settings of the at-par switch and with explicit
/// choices, which the wheel doesn't bind; and <see cref="IborLeg"/>s built with
/// <see cref="IborLeg.WithFixingConvention"/> and <see cref="IborLeg.WithZeroPayments"/>, which the wheel's leg doesn't
/// pass.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IborCouponCxxVectorTests
{
    [Fact]
    public void IborCouponCxxVector()
    {
        var file = VectorFile.Load("cxx/cashflows/iborcoupon");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Inputs.TryGetProperty("coupon", out var spec))
            {
                var coupon = (IborCoupon)CouponInputs.CouponOf(spec, context);
                check.Output(c, "fixingValueDate", () => coupon.FixingValueDate());
                check.Output(c, "fixingEndDate", () => coupon.FixingEndDate());
                check.Output(c, "fixingMaturityDate", () => coupon.FixingMaturityDate());
                check.Output(c, "spanningTime", () => coupon.SpanningTime());
                check.Output(c, "spanningTimeIndexMaturity", () => coupon.SpanningTimeIndexMaturity());
                continue;
            }

            if (c.Expected.TryGetProperty("leg", out _))
            {
                check.Output(c, "leg", () => CouponInputs.LegOf(c.Input("leg"), context));
                continue;
            }

            ImmutableArray<CashFlow> leg = CouponInputs.LegOf(c.Input("leg"), context);
            Coupon[] coupons = [.. leg.Select(cf => (Coupon)cf)];
            check.Output(c, "date", coupons.Select(x => x.Date).ToArray());
            check.Output(c, "nominal", coupons.Select(x => x.Nominal(context)).ToArray());
            check.Output(c, "accrualStartDate", coupons.Select(x => x.AccrualStartDate).ToArray());
            check.Output(c, "accrualEndDate", coupons.Select(x => x.AccrualEndDate).ToArray());
            check.Output(c, "referencePeriodStart", coupons.Select(x => x.ReferencePeriodStart).ToArray());
            check.Output(c, "referencePeriodEnd", coupons.Select(x => x.ReferencePeriodEnd).ToArray());
            check.Output(c, "exCouponDate", coupons.Select(x => x.ExCouponDate).ToArray());
            check.Output(c, "fixingDate", coupons.Select(x => (x as FloatingRateCoupon)?.FixingDate()).ToArray());
            check.Output(c, "rate", CouponInputs.Each(coupons, x => x.Rate(context)));
            check.Output(c, "amount", CouponInputs.Each(coupons, x => x.Amount(context)));
        }

        check.Complete();
    }
}
