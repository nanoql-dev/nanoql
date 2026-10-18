// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The stub Ibor coupon vectors, <c>tests/vectors/cashflows/stubiborcoupon.json</c>: the irregular periods of Ibor legs
/// with a <see cref="StubIndexSelection"/> over Euribor-style tenors, under both conventions, before, on and after the
/// evaluation date, with their rate, amount, weighted index name and fixing; and <see cref="StubIborCoupon"/>'s
/// constructor with its refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class StubIborCouponVectorTests
{
    [Fact]
    public void StubIborCouponVector()
    {
        var file = VectorFile.Load("cashflows/stubiborcoupon");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Inputs.TryGetProperty("coupon", out JsonElement spec))
            {
                if (c.Expected.TryGetProperty("coupon", out _))
                {
                    check.Output(c, "coupon", () => StubCouponOf(spec, context));
                    continue;
                }

                CheckCoupon(check, c, StubCouponOf(spec, context), context);
                continue;
            }

            if (c.Expected.TryGetProperty("leg", out _))
            {
                check.Output(c, "leg", () => CouponInputs.LegOf(c.Input("leg"), context));
                continue;
            }

            ImmutableArray<CashFlow> leg = CouponInputs.LegOf(c.Input("leg"), context);
            check.Output(c, "isStub", leg.Select(cf => cf is StubIborCoupon).ToArray());
            CheckCoupon(check, c, leg.OfType<StubIborCoupon>().First(), context);
        }

        check.Complete();
    }

    private static void CheckCoupon(VectorAssert check, VectorCase c, StubIborCoupon coupon, EvaluationContext context)
    {
        check.Output(c, "accrualStartDate", coupon.AccrualStartDate);
        check.Output(c, "accrualEndDate", coupon.AccrualEndDate);
        check.Output(c, "fixingDate", coupon.FixingDate());
        check.Output(c, "hasFixed", coupon.HasFixed(context));
        check.Output(c, "rate", () => coupon.Rate(context));
        check.Output(c, "amount", () => coupon.Amount(context));
        check.Output(c, "indexName", coupon.IborIndex.Name);
        check.Output(c, "indexFixing", () => coupon.IborIndex.Fixing(coupon.FixingDate(), context));
    }

    private static StubIborCoupon StubCouponOf(JsonElement spec, EvaluationContext context) =>
        new(
            new Date(spec.GetProperty("paymentDate").GetInt32()),
            spec.GetProperty("nominal").GetDouble(),
            new Date(spec.GetProperty("startDate").GetInt32()),
            new Date(spec.GetProperty("endDate").GetInt32()),
            spec.GetProperty("fixingDays").GetInt32(),
            CouponInputs.SelectionOf(spec.GetProperty("stubIndexSelection")),
            dayCounter: CouponInputs.DayCounterOrNull(spec.GetProperty("dayCounter")),
            isInArrears: spec.GetProperty("isInArrears").GetBoolean(),
            pricer: CouponInputs.PricerOf(spec.GetProperty("pricer"), context));
}
