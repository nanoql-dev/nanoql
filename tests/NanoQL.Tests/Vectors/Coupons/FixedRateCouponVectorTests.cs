// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The fixed-rate coupon vectors, <c>tests/vectors/cashflows/fixedratecoupon.json</c>: every coupon of legs built with
/// <see cref="FixedRateLeg"/> on five schedules and seven builder settings, with its dates, amounts, rate, accrual,
/// ex-coupon date and accrued amounts in the middle of the accrual period and late in it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FixedRateCouponVectorTests
{
    [Fact]
    public void FixedRateCouponVector()
    {
        var file = VectorFile.Load("cashflows/fixedratecoupon");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            FixedRateCoupon[] coupons = CashFlowInputs.FixedRateLegOf(c.Input("fixedRateLeg"), c.Context())
                .Cast<FixedRateCoupon>()
                .ToArray();
            check.Output(c, "date", coupons.Select(x => x.Date).ToArray());
            check.Output(c, "amount", coupons.Select(x => x.Amount()).ToArray());
            check.Output(c, "nominal", coupons.Select(x => x.Nominal()).ToArray());
            check.Output(c, "rate", coupons.Select(x => x.Rate()).ToArray());

            // The coupon's InterestRate carries the rate and the day counter the coupon reports.
            Assert.All(coupons, x => Assert.Equal(x.Rate(), x.InterestRate.Rate));
            Assert.All(coupons, x => Assert.Equal(x.DayCounter, x.InterestRate.DayCounter));
            check.Output(c, "accrualStartDate", coupons.Select(x => x.AccrualStartDate).ToArray());
            check.Output(c, "accrualEndDate", coupons.Select(x => x.AccrualEndDate).ToArray());
            check.Output(c, "referencePeriodStart", coupons.Select(x => x.ReferencePeriodStart).ToArray());
            check.Output(c, "referencePeriodEnd", coupons.Select(x => x.ReferencePeriodEnd).ToArray());
            check.Output(c, "accrualPeriod", coupons.Select(x => x.AccrualPeriod()).ToArray());
            check.Output(c, "accrualDays", coupons.Select(x => x.AccrualDays()).ToArray());
            check.Output(c, "exCouponDate", coupons.Select(x => x.ExCouponDate).ToArray());
            check.Output(c, "accruedAmountMid", Accrued(coupons, c.DateInputs("midDates")));
            check.Output(c, "accruedAmountLate", Accrued(coupons, c.DateInputs("lateDates")));
        }

        check.Complete();
    }

    // Each coupon's accrued amount at its own date, from the case's inputs.
    private static double[] Accrued(FixedRateCoupon[] coupons, Date[] dates) =>
        coupons.Zip(dates, (x, d) => x.AccruedAmount(d)).ToArray();
}
