// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="CPICouponPricer"/>'s optionlet once its fixing date is past. QuantLib then pays <c>max(indexFixing -
/// strike, 0)</c>, a CPI level against a strike on the index ratio (<c>cpicouponpricer.cpp:84-94</c>), while before it
/// the forward is <c>indexRatio(accrualEnd)</c> (<c>:104</c>; QuantLib's defect). nanoql's fixed branch reads the
/// ratio, as the live one does. QuantLib's check is its <c>blackFormula</c> at zero deviation, the live branch's own
/// formula at zero variance, on the coupon's <c>indexRatio</c>, which the wheel binds. On
/// <see cref="InflationFixture"/>'s market, for a coupon of 1,000,000 at 3% on a base CPI of 301 from 1 October 2023 to
/// 1 April 2024, lagged two months and fixed at 309.80326478740244, its ratio 1.0292467268684466: the caplet rate
/// struck at 1.01 is 9.263797943622073 from QuantLib's pricer (the C++ harness) and 0.0005774018060533992 from
/// <c>blackFormula</c> and nanoql; the floorlet rate struck at 1.05 is 0.0 from QuantLib's pricer and
/// 0.0006225981939466019 from <c>blackFormula</c> and nanoql.
/// </summary>
public class CpiPricerFixedOptionletTests
{
    [Fact]
    public void AFixedOptionletPaysOnTheIndexRatio()
    {
        EvaluationContext context = InflationFixture.Context();
        var coupon = new CPICoupon(
            301.0,
            new Date(4, Month.April, 2024),
            1e6,
            new Date(1, Month.October, 2023),
            new Date(1, Month.April, 2024),
            new UKRPI(InflationFixture.ZeroCurve()),
            new Period(2, TimeUnit.Months),
            CPIInterpolationType.Flat,
            new Actual365Fixed(),
            0.03);
        var pricer = new CPICouponPricer();

        Assert.Equal(1.0292467268684466, coupon.IndexRatio(coupon.AccrualEndDate, context));

        // QuantLib's: 9.263797943622073 and 0.0
        Assert.Equal(0.0005774018060533992, pricer.CapletRate(coupon, 1.01, context), 1e-15);
        Assert.Equal(0.0006225981939466019, pricer.FloorletRate(coupon, 1.05, context), 1e-15);
    }
}
