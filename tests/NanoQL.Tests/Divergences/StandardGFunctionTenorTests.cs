// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The standard g-function's swap length: QuantLib passes the swap index's tenor as its length in the tenor's own units
/// (<c>conundrumpricer.cpp:130</c>, <c>swapTenor_.length()</c> into a <c>Size</c>), so an 18-month index reads as 18
/// years: on the wheel, on a flat 3% curve and a 20% volatility, a 1-year coupon fixing in 10 years on an 18-month
/// index with an annual fixed leg prices 0.030653808259814307 on the analytic Hagan pricer's standard model, a
/// convexity adjustment of 3.04e-4 against the exact-yield model's 7.13e-5. Read in years the length doesn't repair it:
/// that swap starts with a 6-month stub, and the payment's delay δ is counted in the first period's length
/// (<c>:126</c>), where the model presumes equal periods, so the adjustment comes out −3.25e-4. nanoql reads the length
/// in years where the tenor is a whole number of fixed-leg periods (18 months on a semiannual leg, 3 periods), and
/// refuses the standard model otherwise, naming the exact-yield model, which reads the schedule. QuantLib's checks: its
/// own whole-year tenors, and its exact-yield model on the same swap.
/// </summary>
public class StandardGFunctionTenorTests
{
    [Fact]
    public void WholeYearTenorsKeepQuantLibsValues()
    {
        foreach (Period tenor in new[] { new Period(2, TimeUnit.Years), new Period(24, TimeUnit.Months) })
        {
            CmsCoupon c = CmsInputs.Coupon(CmsInputs.SwapIndex(tenor));
            AssertClose(0.030673308809592435, Rate(c, Analytic(GFunctionFactoryYieldCurveModel.Standard)), 1e-15);
            AssertClose(0.0306674465008249, Rate(c, new NumericHaganPricer(CmsInputs.Volatility(), GFunctionFactoryYieldCurveModel.Standard, 0.0)), 1e-14);
        }
    }

    [Fact]
    public void WholePeriodsAreReadInYears()
    {
        // 18 months on a semiannual leg: 3 periods, the payment two thirds into the swap, where both models' slopes
        // nearly vanish (the exact-yield adjustment 1.0e-6)
        SwapIndex index = CmsInputs.SwapIndex(new Period(18, TimeUnit.Months), new Period(6, TimeUnit.Months));
        CmsCoupon c = CmsInputs.Coupon(index);
        double fixing = index.Fixing(c.FixingDate(), CmsInputs.Context);
        double standard = Rate(c, Analytic(GFunctionFactoryYieldCurveModel.Standard)) - fixing;
        double exact = Rate(c, Analytic(GFunctionFactoryYieldCurveModel.ExactYield)) - fixing;
        Assert.True(Math.Abs(standard - exact) <= 5e-6, $"{standard} against {exact}");
    }

    [Fact]
    public void AStubSwapIsRefused()
    {
        CmsCoupon c = CmsInputs.Coupon(CmsInputs.SwapIndex(new Period(18, TimeUnit.Months)));
        foreach (CmsCouponPricer pricer in new CmsCouponPricer[]
        {
            Analytic(GFunctionFactoryYieldCurveModel.Standard),
            new NumericHaganPricer(CmsInputs.Volatility(), GFunctionFactoryYieldCurveModel.Standard, 0.0),
        })
        {
            NanoQLException e = Assert.Throws<NanoQLException>(() => Rate(c, pricer));
            Assert.Equal(
                "the Standard g-function needs a swap of whole fixed-leg periods, not 18M on a 1Y fixed leg; the "
                + "ExactYield model reads the swap's schedule",
                e.Message);
        }

        // the exact-yield model prices it; QuantLib's standard model, 4.3 times its adjustment
        double fixing = c.SwapIndex.Fixing(c.FixingDate(), CmsInputs.Context);
        AssertClose(0.030349543873054746, fixing, 1e-15);
        AssertClose(0.03042081416699042, Rate(c, Analytic(GFunctionFactoryYieldCurveModel.ExactYield)), 1e-15);
        Assert.True((0.030653808259814307 - fixing) / (0.03042081416699042 - fixing) > 4.2);
    }

    private static AnalyticHaganPricer Analytic(GFunctionFactoryYieldCurveModel model) => new(CmsInputs.Volatility(), model, 0.0);

    private static double Rate(CmsCoupon c, CmsCouponPricer pricer) => c.SetPricer(pricer).Rate(CmsInputs.Context);

    private static void AssertClose(double expected, double actual, double relative) =>
        Assert.True(Math.Abs(actual - expected) <= relative * Math.Abs(expected), $"{actual} against QuantLib's {expected}");
}
