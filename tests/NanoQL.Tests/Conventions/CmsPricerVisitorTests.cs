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
/// The pricer visitor's CMS cases (<c>couponpricer.cpp:376-382</c>, <c>:392-398</c>): a CMS coupon, capped or not,
/// takes a <see cref="CmsCouponPricer"/> and refuses any other pricer with "pricer not compatible with CMS coupon", and
/// a <see cref="CappedFlooredCoupon"/> built over a CMS coupon refuses one with "pricer not compatible with CMS
/// Coupon"; a CMS coupon without a pricer refuses its rate with QuantLib's "pricer not set". A capped leg is built of
/// capped CMS coupons.
/// </summary>
public class CmsPricerVisitorTests
{
    private static readonly Date Today = new(15, January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());
    private static readonly SwapIndex Index = new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), Curve);
    private static readonly SwaptionVolatilityStructure Vol = new ConstantSwaptionVolatility(
        Today, new TARGET(), BusinessDayConvention.ModifiedFollowing, 0.20, new Actual365Fixed());

    [Fact]
    public void ACmsPricerIsSetOnTheCmsCoupons()
    {
        (ImmutableArray<CashFlow> plain, ImmutableArray<CashFlow> capped) = Legs();
        Assert.IsType<CmsCoupon>(plain[0]);
        Assert.IsType<CappedFlooredCmsCoupon>(capped[0]);
        foreach (CmsCouponPricer pricer in new CmsCouponPricer[]
        {
            new AnalyticHaganPricer(Vol, GFunctionFactoryYieldCurveModel.Standard, 0.0),
            new NumericHaganPricer(Vol, GFunctionFactoryYieldCurveModel.ExactYield, 0.0),
            new LinearTsrPricer(Vol, 0.0),
        })
        {
            foreach (ImmutableArray<CashFlow> leg in new[] { plain, capped })
            {
                foreach (CashFlow cf in CouponPricerFunctions.SetCouponPricer(leg, pricer))
                {
                    Assert.Same(pricer, ((FloatingRateCoupon)cf).Pricer);
                }
            }

            // a capped coupon built over a CMS coupon takes it, and carries it into its underlying
            var wrapped = new CappedFlooredCoupon((CmsCoupon)plain[1], cap: 0.05);
            var set = (CappedFlooredCoupon)CouponPricerFunctions.SetCouponPricer([wrapped], pricer)[0];
            Assert.Same(pricer, set.Underlying.Pricer);
        }
    }

    [Fact]
    public void EveryOtherPricerIsRefused()
    {
        (ImmutableArray<CashFlow> plain, ImmutableArray<CashFlow> capped) = Legs();
        var wrapped = new CappedFlooredCoupon((CmsCoupon)plain[1], cap: 0.05);
        foreach (FloatingRateCouponPricer? pricer in new FloatingRateCouponPricer?[] { new BlackIborCouponPricer(Context), null })
        {
            foreach (ImmutableArray<CashFlow> leg in new[] { plain, capped })
            {
                ArgumentException e = Assert.Throws<ArgumentException>(() => CouponPricerFunctions.SetCouponPricer(leg, pricer));
                Assert.Equal("pricer", e.ParamName);
                Assert.StartsWith("pricer not compatible with CMS coupon", e.Message, StringComparison.Ordinal);
            }

            ArgumentException w = Assert.Throws<ArgumentException>(() => CouponPricerFunctions.SetCouponPricer([wrapped], pricer));
            Assert.Equal("pricer", w.ParamName);
            Assert.StartsWith("pricer not compatible with CMS Coupon", w.Message, StringComparison.Ordinal);
        }

        NanoQLException noPricer = Assert.Throws<NanoQLException>(() => ((CmsCoupon)plain[0]).Rate(Context));
        Assert.Equal("pricer not set", noPricer.Message);
    }

    private static (ImmutableArray<CashFlow> Plain, ImmutableArray<CashFlow> Capped) Legs()
    {
        Schedule schedule = new MakeSchedule()
            .From(Today + new Period(1, TimeUnit.Years))
            .To(Today + new Period(3, TimeUnit.Years))
            .WithTenor(new Period(6, TimeUnit.Months))
            .WithCalendar(new TARGET())
            .ToSchedule();
        return (new CmsLeg(schedule, Index).WithNotionals(100.0).ToLeg(),
            new CmsLeg(schedule, Index).WithNotionals(100.0).WithCaps(0.05).ToLeg());
    }
}
