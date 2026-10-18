// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Ibor coupon vectors, <c>tests/vectors/cashflows/iborcoupon.json</c>: <see cref="IborCoupon.HasFixed"/> and the
/// rate over scenarios of fixing date, enforce flag and stored fixing, and the cash flows of <see cref="IborLeg"/>s
/// (stubs, vectors, caps and floors, in arrears, ex-coupon dates, payment conventions, indexed and at-par coupons, zero
/// gearings), with the conversions QuantLib refuses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IborCouponVectorTests
{
    [Fact]
    public void IborCouponVector()
    {
        var file = VectorFile.Load("cashflows/iborcoupon");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Inputs.TryGetProperty("coupon", out var spec))
            {
                var coupon = (IborCoupon)CouponInputs.CouponOf(spec, context);
                check.Output(c, "fixingDate", coupon.FixingDate());
                check.Output(c, "hasFixed", coupon.HasFixed(context));
                check.Output(c, "rate", () => coupon.Rate(context));
                continue;
            }

            if (c.Expected.TryGetProperty("leg", out _))
            {
                check.Output(c, "leg", () => CouponInputs.LegOf(c.Input("leg"), context));
                continue;
            }

            // A leg built under one at-par switch and priced under the other keeps the choice it was built with.
            EvaluationContext built = c.Inputs.TryGetProperty("builtWithAtParCoupons", out var atPar)
                ? context with { UseAtParCoupons = atPar.GetBoolean() }
                : context;
            ImmutableArray<CashFlow> leg = CouponInputs.LegOf(c.Input("leg"), built);
            CouponInputs.CheckLeg(check, c, leg, context);
        }

        check.Complete();
    }
}
