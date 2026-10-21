// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// CMS coupons before batch 3's CMS coupon pricers (B3.3). QuantLib's pricer visitor gives a CMS coupon, capped or not,
/// only a <c>CmsCouponPricer</c> and refuses any other pricer with "pricer not compatible with CMS coupon"
/// (<c>couponpricer.cpp:376-382</c>, <c>:392-398</c>); no pricer that exists before B3.3 is one, so every pricer is
/// refused, with QuantLib's text, and a CMS coupon without a pricer refuses its rate with QuantLib's "pricer not set".
/// </summary>
public class CmsPricerRefusalTests
{
    [Fact]
    public void EveryPricerIsRefusedAndTheRateWithoutOne()
    {
        var today = new Date(15, January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var curve = new FlatForward(today, 0.03, new Actual365Fixed());
        var swapIndex = new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), curve);
        Schedule schedule = new MakeSchedule()
            .From(today)
            .To(today + new Period(2, TimeUnit.Years))
            .WithTenor(new Period(6, TimeUnit.Months))
            .WithCalendar(new TARGET())
            .ToSchedule();

        ImmutableArray<CashFlow> plain = new CmsLeg(schedule, swapIndex).WithNotionals(100.0).ToLeg();
        ImmutableArray<CashFlow> capped = new CmsLeg(schedule, swapIndex).WithNotionals(100.0).WithCaps(0.05).ToLeg();
        Assert.IsType<CmsCoupon>(plain[0]);
        Assert.IsType<CappedFlooredCmsCoupon>(capped[0]);

        var pricer = new BlackIborCouponPricer(context);
        foreach (ImmutableArray<CashFlow> leg in new[] { plain, capped })
        {
            ArgumentException e = Assert.Throws<ArgumentException>(() => CouponPricerFunctions.SetCouponPricer(leg, pricer));
            Assert.Equal("pricer", e.ParamName);
            Assert.StartsWith("pricer not compatible with CMS coupon", e.Message, StringComparison.Ordinal);
        }

        NanoQLException noPricer = Assert.Throws<NanoQLException>(() => ((CmsCoupon)plain[0]).Rate(context));
        Assert.Equal("pricer not set", noPricer.Message);
    }
}
