// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A capped or floored coupon copies its underlying's terms when it is built, which has no context, so it reads the
/// nominal the underlying was built with, <see cref="Coupon.ContractNominal"/>, not
/// <see cref="Coupon.Nominal(EvaluationContext)"/>, which pricing calls and which a coupon may compute from market data.
/// </summary>
public class ContractNominalCopyTests
{
    [Fact]
    public void CappedCouponsCopyTheUnderlyingsContractNominal()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2025) };
        var index = new Euribor6M();
        var start = new Date(17, Month.July, 2025);
        var end = new Date(19, Month.January, 2026);

        var underlying = new ResetNominalCoupon(end, 1e6, start, end, index);
        Assert.Equal(2e6, underlying.Nominal(context));

        var capped = new CappedFlooredCoupon(underlying, 0.04, 0.01);
        Assert.Equal(1e6, capped.ContractNominal);
        Assert.Equal(1e6, capped.Nominal(context));

        var ibor = new CappedFlooredIborCoupon(end, 3e6, start, end, 2, index, floor: 0.0);
        Assert.Equal(3e6, ibor.ContractNominal);
        Assert.Equal(3e6, ibor.Underlying.ContractNominal);
    }

    // A coupon whose nominal for pricing differs from the one it was built with, as an FX-reset coupon's does.
    private sealed class ResetNominalCoupon(Date payment, double nominal, Date start, Date end, IborIndex index)
        : FloatingRateCoupon(payment, nominal, start, end, null, index)
    {
        public override double Nominal(EvaluationContext context) => 2.0 * ContractNominal;
    }
}
