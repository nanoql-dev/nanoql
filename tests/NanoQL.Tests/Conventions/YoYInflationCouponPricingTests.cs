// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A year-on-year coupon whose fixing is past, priced by each pricer built on a nominal curve alone (QuantLib's
/// one-argument constructors, which leave the volatility empty), and the pricer setter's swap and bond forms. With no
/// cap or floor no optionlet is priced, so each pricer gives the coupon its fixing: on <see cref="InflationFixture"/>'s
/// market, the coupon from 1 July 2023 to 1 July 2024, observed three months back, has QuantLib's index fixing and rate,
/// 0.033084673723716485, and accrues 17,947.30245834483 by 15 January 2024 and nothing at its start, under each of the
/// wheel's pricers given an empty volatility (the wheel binds no one-argument constructor). The setter's forms return a
/// copy of the swap or bond whose inflation coupons hold the pricer, the original's unchanged.
/// </summary>
public class YoYInflationCouponPricingTests
{
    [Fact]
    public void EachPricerPricesAPastFixingAtTheFixing()
    {
        EvaluationContext context = InflationFixture.Context();
        YoYInflationCoupon coupon = Coupon(new Date(1, Month.July, 2023), new Date(1, Month.July, 2024));
        InflationCouponPricer[] pricers =
        [
            new YoYInflationCouponPricer(InflationFixture.Nominal()),
            new BlackYoYInflationCouponPricer(InflationFixture.Nominal()),
            new UnitDisplacedBlackYoYInflationCouponPricer(InflationFixture.Nominal()),
            new BachelierYoYInflationCouponPricer(InflationFixture.Nominal()),
        ];

        foreach (InflationCouponPricer pricer in pricers)
        {
            var priced = (YoYInflationCoupon)InflationCouponPricerFunctions.SetCouponPricer([coupon], pricer)[0];
            double fixing = priced.IndexFixing(context);
            double rate = priced.Rate(context);
            double accrued = priced.AccruedAmount(new Date(15, Month.January, 2024), context);

            // QuantLib's, on the wheel
            Assert.True(Tolerance.Within(0.033084673723716485, fixing), $"{pricer.GetType().Name}: {fixing}");
            Assert.True(Tolerance.Within(0.033084673723716485, rate), $"{pricer.GetType().Name}: {rate}");
            Assert.True(Tolerance.Within(17947.30245834483, accrued, 1e6), $"{pricer.GetType().Name}: {accrued}");
            Assert.Equal(0.0, priced.AccruedAmount(new Date(1, Month.July, 2023), context));
        }
    }

    [Fact]
    public void TheSwapAndBondFormsSetThePricerOnACopy()
    {
        var pricer = new BlackYoYInflationCouponPricer(InflationFixture.Nominal());
        var swap = new YearOnYearInflationSwap(
            SwapType.Payer,
            1e6,
            Schedule(),
            0.03,
            new Actual365Fixed(),
            Schedule(),
            Index(),
            new Period(3, TimeUnit.Months),
            CPIInterpolationType.Flat,
            0.0,
            new Actual365Fixed(),
            new UnitedKingdom());

        YearOnYearInflationSwap set = InflationCouponPricerFunctions.SetCouponPricer(swap, 1, pricer);

        Assert.Equal(swap.Legs[0], set.Legs[0]);
        Assert.All(set.Legs[1], f => Assert.Same(pricer, Assert.IsType<YoYInflationCoupon>(f).Pricer));
        Assert.All(swap.Legs[1], f => Assert.IsType<YoYInflationCouponPricer>(((YoYInflationCoupon)f).Pricer));
        Assert.Throws<ArgumentException>(() => InflationCouponPricerFunctions.SetCouponPricer(swap, 2, pricer));

        var cpiPricer = new CPICouponPricer(InflationFixture.Nominal());
        var bond = new CPIBond(
            2,
            100.0,
            300.0,
            new Period(3, TimeUnit.Months),
            new UKRPI(),
            CPIInterpolationType.Flat,
            Schedule(),
            [0.01],
            new Actual365Fixed());

        CPIBond setBond = InflationCouponPricerFunctions.SetCouponPricer(bond, cpiPricer);

        Assert.All(setBond.Cashflows.OfType<CPICoupon>(), c => Assert.Same(cpiPricer, c.Pricer));
        Assert.Equal(bond.Cashflows.Length, setBond.Cashflows.Length);
        Assert.All(bond.Cashflows.OfType<CPICoupon>(), c => Assert.NotSame(cpiPricer, c.Pricer));
    }

    private static YoYInflationIndex Index() =>
        new(new UKRPI(), InflationFixture.YoYCurve([new(1, Month.April, 2024), new(1, Month.April, 2034)], [0.03, 0.03]));

    private static YoYInflationCoupon Coupon(Date start, Date end) =>
        new(end, 1e6, start, end, 0, Index(), new Period(3, TimeUnit.Months), CPIInterpolationType.Flat, new Actual365Fixed());

    private static Schedule Schedule() =>
        new(
            new Date(15, Month.June, 2024),
            new Date(15, Month.June, 2027),
            new Period(1, TimeUnit.Years),
            new UnitedKingdom(),
            BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Forward,
            false,
            InflationFixture.Context());
}
