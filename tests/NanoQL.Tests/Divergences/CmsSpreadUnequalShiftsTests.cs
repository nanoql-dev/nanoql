// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The lognormal CMS spread pricer with unequal shifts, as QuantLib's master after 1.44 (#2874, taken ahead of the pin:
/// the S-xrt plan's XRR2 and XRR5 (xvii)). QuantLib 1.44's <c>optionletPrice</c> branches on the strike where the
/// integrand's sign turns on the displaced strike k + g₁s₁ + g₂s₂, and its negative branch shifts the second rate by
/// the first's shift. On QuantLib 1.44 (the S-xrt planner's <c>xp_c3.cpp</c>: #2874's set-up below, ρ 0.5) its caplets
/// and floorlets are NaN at strikes 0.008 and 0.002 on shifts 0 and 0.01, and at −0.008 and −0.002 on shifts 0.01 and 0;
/// the cap at −0.002 is 0.003275576228 on shifts 0 and 0.01, where #2874's case caches 0.0080778180, and put-call
/// parity breaks by 2.032e-2 there and 2.040e-2 on shifts 0.01 and 0 at −0.012. nanoql's is #2874's: every price finite,
/// parity to 1e-12, and #2874's two cached caplets to 1e-8. Equal shifts take the same branch either way, so QuantLib's
/// <c>testCouponPricing</c> keeps 1.44's bits.
/// </summary>
public class CmsSpreadUnequalShiftsTests
{
    // #2874's testShiftedLognormalDifferentShifts (QuantLib master a212050f3).
    [Theory]
    [InlineData(0.01, 0.01)]
    [InlineData(0.0, 0.01)]
    [InlineData(0.01, 0.0)]
    public void TestShiftedLognormalDifferentShifts(double shift1, double shift2)
    {
        var refDate = new Date(21, Month.September, 2026);
        var context = new EvaluationContext { EvaluationDate = refDate };
        DayCounter curveDayCounter = new Actual365Fixed();
        Calendar calendar = new NullCalendar();
        YieldTermStructure curve = new FlatForward(refDate, 0.03, curveDayCounter);
        var ibor = new IborIndex(
            "I6M", new Period(6, TimeUnit.Months), 0, new USDCurrency(), calendar, BusinessDayConvention.Unadjusted,
            false, new Actual360(), curve);
        SwapIndex SwapIndexOf(int years) => new(
            "S", new Period(years, TimeUnit.Years), 0, new USDCurrency(), calendar, new Period(1, TimeUnit.Years),
            BusinessDayConvention.Unadjusted, new Thirty360(Thirty360Convention.European), ibor);
        SwapIndex swap10y = SwapIndexOf(10);
        SwapIndex swap2y = SwapIndexOf(2);
        var spreadIndex = new SwapSpreadIndex("SP", swap10y, swap2y, 1.0, -1.0);
        Date fixingDate = refDate + new Period(5, TimeUnit.Years);
        Date paymentDate = fixingDate + new Period(1, TimeUnit.Years);

        var vols = new Matrix(2, 2, 0.20);
        var shifts = new Matrix(2, 2);
        shifts[0, 0] = shifts[1, 0] = shift2;
        shifts[0, 1] = shifts[1, 1] = shift1;
        SwaptionVolatilityMatrix vol = new SwaptionVolatilityMatrix(
            refDate,
            calendar,
            BusinessDayConvention.Following,
            [new Period(1, TimeUnit.Years), new Period(10, TimeUnit.Years)],
            [new Period(2, TimeUnit.Years), new Period(10, TimeUnit.Years)],
            vols,
            curveDayCounter,
            false,
            VolatilityType.ShiftedLognormal,
            shifts).EnableExtrapolation();
        var cmsPricer = new LinearTsrPricer(vol, 0.0);
        var spreadPricer = new LognormalCmsSpreadPricer(cmsPricer, 0.5);
        CmsSpreadCoupon coupon = new CmsSpreadCoupon(
            paymentDate, 1e6, fixingDate, paymentDate, 0, spreadIndex, 1.0, 0.0, null, null, new Actual360(), false)
            .SetPricer(spreadPricer);
        double swaplet = coupon.Rate(context);

        double displacedStrikeBoundary = shift2 - shift1;
        List<double> strikes = [displacedStrikeBoundary - 0.002, displacedStrikeBoundary, displacedStrikeBoundary + 0.002];
        if (shift1 == 0.0 && shift2 == 0.01)
        {
            strikes.Add(-0.002);
            strikes.Add(0.002);
        }

        foreach (double strike in strikes)
        {
            double cap = spreadPricer.CapletRate(coupon, strike, context);
            double floor = spreadPricer.FloorletRate(coupon, strike, context);
            Assert.True(double.IsFinite(cap));
            Assert.True(double.IsFinite(floor));
            Assert.True(Math.Abs(cap - floor - (swaplet - strike)) <= 1e-12);

            if (shift1 == 0.0 && shift2 == 0.01)
            {
                if (Math.Abs(strike + 0.002) < 1e-12)
                {
                    Assert.True(Math.Abs(cap - 0.0080778180) <= 1e-8);
                }

                if (Math.Abs(strike - 0.002) < 1e-12)
                {
                    Assert.True(Math.Abs(cap - 0.0058457681) <= 1e-8);
                }
            }
        }
    }
}
