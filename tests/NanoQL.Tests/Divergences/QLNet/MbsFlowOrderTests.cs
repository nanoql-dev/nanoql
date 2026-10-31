// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Extensions.Coupons;
using NanoQL.Extensions.Instruments;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="MBSFixedRateBond"/>'s flows on a date are its coupon, then its scheduled principal, then its prepayment,
/// as a bond's redemptions follow its coupons on a date (QuantLib's <c>bond.cpp:325-328</c>). QLNet's are the
/// prepayment, the scheduled principal and the coupon (<c>MBSFixedRateBond.cs:70-72</c>). The amounts and dates are
/// QLNet's bit for bit (the vectors); a sum over a date's flows, as <c>PVDifference</c> takes, adds them in this order.
/// </summary>
public class MbsFlowOrderTests
{
    [Fact]
    public void ADatesCouponComesFirst()
    {
        MBSFixedRateBond pool = MbsPools.Fabozzi();

        // QLNet: VoluntaryPrepay, AmortizingPayment, FixedRateCoupon.
        Assert.IsType<FixedRateCoupon>(pool.ExpectedCashflows[0]);
        Assert.IsType<AmortizingPayment>(pool.ExpectedCashflows[1]);
        Assert.IsType<VoluntaryPrepay>(pool.ExpectedCashflows[2]);
        Assert.Equal(pool.ExpectedCashflows[0].Date, pool.ExpectedCashflows[2].Date);
        Assert.Equal(pool.Redemptions, [.. pool.ExpectedCashflows.Where(c => c is not Coupon)]);
    }
}
