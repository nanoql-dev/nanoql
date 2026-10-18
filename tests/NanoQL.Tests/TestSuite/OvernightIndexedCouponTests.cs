// SPDX-FileCopyrightText: Copyright (C) 2021 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/overnightindexedcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Moved: testExCouponAccruedAmountAroundAccrualEndDate line 1936 (Divergences/TelescopicValueDatesTests.cs); testOvernightLegNPV line 1033 (Divergences/TelescopicValueDatesTests.cs)

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class OvernightIndexedCouponTests
{
    [Fact]
    public void TestPastCouponRate()
    {
        // Testing rate for past overnight-indexed coupon...

        var vars = new CommonVars();

        // coupon entirely in the past

        OvernightIndexedCoupon pastCoupon = vars.MakeCoupon(new Date(18, October, 2021), new Date(18, November, 2021));

        // expected values here and below come from manual calculations based on past dates and rates
        Rate expectedRate = 0.000987136104;
        Real expectedAmount = vars.Notional * expectedRate * 31.0 / 360;
        CheckOisCouponResult("coupon rate", pastCoupon.Rate(vars.Context), expectedRate, 1e-12);
        CheckOisCouponResult("coupon amount", pastCoupon.Amount(vars.Context), expectedAmount, 1e-8);
    }

    [Fact]
    public void TestPastSpreadedCouponRate()
    {
        // Testing rate for past overnight-indexed coupon with compounded spread...

        var vars = new CommonVars();

        // coupon entirely in the past
        OvernightIndexedCoupon pastCoupon = vars.MakeSpreadedCoupon(
            new Date(18, October, 2021), new Date(18, November, 2021), 0.0001);
        OvernightIndexedCoupon pastCouponCompoundingSpread = vars.MakeSpreadedCoupon(
            new Date(18, October, 2021), new Date(18, November, 2021), 0.0001, false);

        // expected values here and below come from manual calculations based on past dates and rates
        Rate expectedRate = 0.0010871445057780704;
        Real expectedAmount = vars.Notional * expectedRate * 31.0 / 360;
        CheckOisCouponResult("coupon rate", pastCoupon.Rate(vars.Context), expectedRate, 1e-12);
        CheckOisCouponResult("coupon amount", pastCoupon.Amount(vars.Context), expectedAmount, 1e-8);

        expectedRate = 0.0010871361040194164;
        CheckOisCouponResult("coupon rate", pastCouponCompoundingSpread.Rate(vars.Context), expectedRate, 1e-12);
    }

    // Adapted: the forecast curve linked after Sofr is built (line 384) becomes Sofr built on the curve, and the fixing
    // added for today (line 398) becomes a second store, in a second context.
    [Fact]
    public void TestCurrentCouponRate()
    {
        // Testing rate for current overnight-indexed coupon...

        var vars = new CommonVars();

        vars.LinkForecastCurve(vars.FlatRate(0.0010, new Actual360()));

        // coupon partly in the past, today not fixed

        OvernightIndexedCoupon currentCoupon = vars.MakeCoupon(new Date(10, November, 2021), new Date(10, December, 2021));

        Rate expectedRate = 0.000926701551;
        Real expectedAmount = vars.Notional * expectedRate * 30.0 / 360;
        CheckOisCouponResult("coupon rate", currentCoupon.Rate(vars.Context), expectedRate, 1e-12);
        CheckOisCouponResult("coupon amount", currentCoupon.Amount(vars.Context), expectedAmount, 1e-8);

        // coupon partly in the past, today fixed

        EvaluationContext fixedToday = vars.Context with
        {
            Fixings = vars.Context.Fixings.AddFixing(vars.Sofr, new Date(23, November, 2021), 0.0007),
        };

        expectedRate = 0.000916700760;
        expectedAmount = vars.Notional * expectedRate * 30.0 / 360;
        CheckOisCouponResult("coupon rate", currentCoupon.Rate(fixedToday), expectedRate, 1e-12);
        CheckOisCouponResult("coupon amount", currentCoupon.Amount(fixedToday), expectedAmount, 1e-8);
    }

    // Adapted: the forecast curve linked after Sofr is built (line 411) becomes Sofr built on the curve.
    [Fact]
    public void TestFutureCouponRate()
    {
        // Testing rate for future overnight-indexed coupon...

        var vars = new CommonVars();

        vars.LinkForecastCurve(vars.FlatRate(0.0010, new Actual360()));

        // coupon entirely in the future

        OvernightIndexedCoupon futureCoupon = vars.MakeCoupon(new Date(10, December, 2021), new Date(10, January, 2022));

        Rate expectedRate = 0.001000043057;
        Real expectedAmount = vars.Notional * expectedRate * 31.0 / 360;
        CheckOisCouponResult("coupon rate", futureCoupon.Rate(vars.Context), expectedRate, 1e-12);
        CheckOisCouponResult("coupon amount", futureCoupon.Amount(vars.Context), expectedAmount, 1e-8);
    }

    // Adapted: the evaluation date set after the fixture (line 429) becomes a second context, the curve built from it,
    // and the forecast curve linked after Sofr is built (line 431) becomes Sofr built on the curve.
    [Fact]
    public void TestRateWhenTodayIsHoliday()
    {
        // Testing rate for overnight-indexed coupon when today is a holiday...

        var vars = new CommonVars();

        EvaluationContext holiday = vars.Context with { EvaluationDate = new Date(20, November, 2021) };

        vars.LinkForecastCurve(FlatForward.Create(0, new NullCalendar(), 0.0010, new Actual360(), holiday));

        OvernightIndexedCoupon coupon = vars.MakeCoupon(new Date(10, November, 2021), new Date(10, December, 2021));

        Rate expectedRate = 0.000930035180;
        Real expectedAmount = vars.Notional * expectedRate * 30.0 / 360;
        CheckOisCouponResult("coupon rate", coupon.Rate(holiday), expectedRate, 1e-12);
        CheckOisCouponResult("coupon amount", coupon.Amount(holiday), expectedAmount, 1e-8);
    }

    [Fact]
    public void TestAccruedAmountInThePast()
    {
        // Testing accrued amount in the past for overnight-indexed coupon...

        var vars = new CommonVars();

        OvernightIndexedCoupon coupon = vars.MakeCoupon(new Date(18, October, 2021), new Date(18, January, 2022));

        Real expectedAmount = vars.Notional * 0.000987136104 * 31.0 / 360;
        CheckOisCouponResult(
            "coupon amount", coupon.AccruedAmount(new Date(18, November, 2021), vars.Context), expectedAmount, 1e-8);
    }

    // Adapted: the forecast curve linked after Sofr is built (line 459) becomes Sofr built on the curve, and the fixing
    // added for today (line 471) becomes a second store, in a second context.
    [Fact]
    public void TestAccruedAmountSpanningToday()
    {
        // Testing accrued amount spanning today for current overnight-indexed coupon...

        var vars = new CommonVars();

        vars.LinkForecastCurve(vars.FlatRate(0.0010, new Actual360()));

        // coupon partly in the past, today not fixed

        OvernightIndexedCoupon coupon = vars.MakeCoupon(new Date(10, November, 2021), new Date(10, January, 2022));

        Real expectedAmount = vars.Notional * 0.000926701551 * 30.0 / 360;
        CheckOisCouponResult(
            "coupon amount", coupon.AccruedAmount(new Date(10, December, 2021), vars.Context), expectedAmount, 1e-8);

        // coupon partly in the past, today fixed

        EvaluationContext fixedToday = vars.Context with
        {
            Fixings = vars.Context.Fixings.AddFixing(vars.Sofr, new Date(23, November, 2021), 0.0007),
        };

        expectedAmount = vars.Notional * 0.000916700760 * 30.0 / 360;
        CheckOisCouponResult(
            "coupon amount", coupon.AccruedAmount(new Date(10, December, 2021), fixedToday), expectedAmount, 1e-8);
    }

    // Adapted: the forecast curve linked after Sofr is built (line 482) becomes Sofr built on the curve.
    [Fact]
    public void TestAccruedAmountInTheFuture()
    {
        // Testing accrued amount in the future for overnight-indexed coupon...

        var vars = new CommonVars();

        vars.LinkForecastCurve(vars.FlatRate(0.0010, new Actual360()));

        // coupon entirely in the future

        OvernightIndexedCoupon coupon = vars.MakeCoupon(new Date(10, December, 2021), new Date(10, March, 2022));

        var accrualDate = new Date(10, January, 2022);
        Rate expectedRate = 0.001000043057;
        Real expectedAmount = vars.Notional * expectedRate * 31.0 / 360;
        CheckOisCouponResult("coupon amount", coupon.AccruedAmount(accrualDate, vars.Context), expectedAmount, 1e-8);
    }

    [Fact]
    public void TestAccruedAmountOnPastHoliday()
    {
        // Testing accrued amount on a past holiday for overnight-indexed coupon...

        var vars = new CommonVars();

        // coupon entirely in the past

        OvernightIndexedCoupon coupon = vars.MakeCoupon(new Date(18, October, 2021), new Date(18, January, 2022));

        var accrualDate = new Date(13, November, 2021);
        Real expectedAmount = vars.Notional * 0.000074724810;
        CheckOisCouponResult("coupon amount", coupon.AccruedAmount(accrualDate, vars.Context), expectedAmount, 1e-8);
    }

    // Adapted: the forecast curve linked after Sofr is built (line 514) becomes Sofr built on the curve.
    [Fact]
    public void TestAccruedAmountOnFutureHoliday()
    {
        // Testing accrued amount on a future holiday for overnight-indexed coupon...

        var vars = new CommonVars();

        vars.LinkForecastCurve(vars.FlatRate(0.0010, new Actual360()));

        // coupon entirely in the future

        OvernightIndexedCoupon coupon = vars.MakeCoupon(new Date(10, December, 2021), new Date(10, March, 2022));

        var accrualDate = new Date(15, January, 2022);
        Real expectedAmount = vars.Notional * 0.000100005012;
        CheckOisCouponResult("coupon amount", coupon.AccruedAmount(accrualDate, vars.Context), expectedAmount, 1e-8);
    }

    [Fact]
    public void TestPastCouponRateWithLookback()
    {
        // Testing rate for past overnight-indexed coupon with lookback period...

        var vars = new CommonVars();

        // coupon entirely in the past with lookback period
        // this unit test replicates an example available on NY FED website:
        // https://www.newyorkfed.org/medialibrary/Microsites/arrc/files/2020/ARRC-BWLG-Examples-Other-Lookback-Options.xlsx

        OvernightIndexedCoupon pastCoupon = vars.MakeCoupon(new Date(1, July, 2019), new Date(15, July, 2019), 5);

        // expected values here and below come from manual calculations based on past dates and rates
        Rate expectedRate = 0.024781644454;

        CheckOisCouponResult("coupon rate", pastCoupon.Rate(vars.Context), expectedRate, 1e-12);
    }

    [Fact]
    public void TestPastCouponRateWithLookbackAndObservationShift()
    {
        // Testing rate for past overnight-indexed coupon with lookback period and observation shift...

        var vars = new CommonVars();

        // coupon entirely in the past with lookback period with observation shift
        // this unit test replicates an example available on NY FED website:
        // https://www.newyorkfed.org/medialibrary/Microsites/arrc/files/2020/ARRC-BWLG-Examples-Other-Lookback-Options.xlsx

        OvernightIndexedCoupon pastCoupon =
            vars.MakeCoupon(new Date(1, July, 2019), new Date(31, July, 2019), 5, 0, true);

        // expected values here and below come from manual calculations based on past dates and rates
        Rate expectedRate = 0.024603611707;

        CheckOisCouponResult("coupon rate", pastCoupon.Rate(vars.Context), expectedRate, 1e-12);
    }

    [Fact]
    public void TestPastCouponRateWithLockout()
    {
        // Testing rate for past overnight-indexed coupon with lockout...

        var vars = new CommonVars();

        OvernightIndexedCoupon couponWithLockout =
            vars.MakeCoupon(new Date(1, July, 2019), new Date(31, July, 2019), null, 3);
        ImmutableArray<Date> fixingDates = couponWithLockout.FixingDates();
        Size n = fixingDates.Length;

        var expectedLockoutDate = new Date(25, July, 2019);
        CheckOisCouponDates("lockout date", fixingDates[n - 4], expectedLockoutDate);
        CheckOisCouponDates("day T - 2 fixing", fixingDates[n - 3], expectedLockoutDate);
        CheckOisCouponDates("day T - 1 fixing", fixingDates[n - 2], expectedLockoutDate);
        CheckOisCouponDates("day T fixing", fixingDates[n - 1], expectedLockoutDate);
    }

    [Fact]
    public void TestPastCouponRateWithLookbackObservationShiftAndLockout()
    {
        // Testing rate for past overnight-indexed coupon with lookback period, observation shift and lockout...

        var vars = new CommonVars();

        // coupon entirely in the past with lookback period with observation shift
        // and lockout this unit test replicates an example available on NY FED website:
        // https://www.newyorkfed.org/medialibrary/Microsites/arrc/files/2020/ARRC-BWLG-Examples-Other-Lookback-Options.xlsx

        OvernightIndexedCoupon pastCoupon =
            vars.MakeCoupon(new Date(1, July, 2019), new Date(31, July, 2019), 5, 3, true);

        // expected values here and below come from manual calculations based on past dates and rates
        Rate expectedRate = 0.024693783702;

        CheckOisCouponResult("coupon rate", pastCoupon.Rate(vars.Context), expectedRate, 1e-12);
    }

    [Fact]
    public void TestIncorrectNumberOfLockoutDays()
    {
        // Testing incorrect number of lockout days...

        var vars = new CommonVars();

        OvernightIndexedCoupon couponWithoutLockout =
            vars.MakeCoupon(new Date(1, July, 2019), new Date(31, July, 2019));
        Size numberOfFixings = couponWithoutLockout.FixingDates().Length;

        // Lockout days equal to the number of daily fixings
        CheckThrowsError(() =>
            vars.MakeCoupon(new Date(1, July, 2019), new Date(31, July, 2019), null, numberOfFixings));

        // Negative number of lockout days
        CheckThrowsError(() => vars.MakeCoupon(new Date(1, July, 2019), new Date(31, July, 2019), null, -1));
    }

    // Adapted: the forecast curve linked after Sofr is built (line 626) becomes Sofr built on the curve.
    [Fact]
    public void TestFutureCouponRateWithLookback()
    {
        // Testing rate for future overnight-indexed coupon with lookback period...

        var vars = new CommonVars(new Date(12, March, 2019));

        vars.LinkForecastCurve(vars.FlatRate(0.0250, new Actual360()));

        OvernightIndexedCoupon coupon8July =
            vars.MakeCoupon(new Date(1, July, 2019), new Date(8, July, 2019), 5, 0, false);
        Rate expectedRate8July = 0.0250050849311315;

        CheckOisCouponResult("coupon rate", coupon8July.Rate(vars.Context), expectedRate8July, 1e-12);

        OvernightIndexedCoupon coupon15July =
            vars.MakeCoupon(new Date(1, July, 2019), new Date(15, July, 2019), 5, 0, false);
        Rate expectedRate15July = 0.0250118464503275;

        CheckOisCouponResult("coupon rate", coupon15July.Rate(vars.Context), expectedRate15July, 1e-12);
    }

    // Adapted: the forecast curve linked after Sofr is built (line 645) becomes Sofr built on the curve.
    [Fact]
    public void TestFutureCouponRateWithLookbackAndObservationShift()
    {
        // Testing rate for future overnight-indexed coupon with lookback period and observation shift...

        var vars = new CommonVars(new Date(12, March, 2019));

        vars.LinkForecastCurve(vars.FlatRate(0.0250, new Actual360()));

        OvernightIndexedCoupon futureCoupon =
            vars.MakeCoupon(new Date(1, July, 2019), new Date(8, July, 2019), 5, 0, true);
        Rate expectedRate = 0.025003472543756455;

        CheckOisCouponResult("coupon rate", futureCoupon.Rate(vars.Context), expectedRate, 1e-12);
    }

    // Adapted: the forecast curve linked after Sofr is built (line 658) becomes Sofr built on the curve.
    [Fact]
    public void TestFutureCouponRateWithLookout()
    {
        // Testing rate for future overnight-indexed coupon with lockout...

        var vars = new CommonVars(new Date(12, March, 2019));

        YieldTermStructure forecastCurve = vars.FlatRate(0.0250, new Actual360());
        vars.LinkForecastCurve(forecastCurve);

        OvernightIndexedCoupon coupon15July =
            vars.MakeCoupon(new Date(1, July, 2019), new Date(15, July, 2019), null, 2, false);

        // expected = 0.025011784374543
        Rate lockoutFixing = vars.Sofr.Fixing(new Date(10, July, 2019), vars.Context);
        Rate expectedRate15July =
            ((forecastCurve.Discount(new Date(1, July, 2019))
              / forecastCurve.Discount(new Date(11, July, 2019))
              * (1.0 + (1.0 / 360.0 * lockoutFixing)) * (1.0 + (3.0 / 360.0 * lockoutFixing)))
             - 1.0)
            * 360.0 / 14;

        CheckOisCouponResult("coupon rate", coupon15July.Rate(vars.Context), expectedRate15July, 1e-12);
    }

    // Adapted: the forecast curve linked after Sofr is built (line 681) becomes Sofr built on the curve.
    [Fact]
    public void TestPartiallyAccruedAmountOfFutureCouponWithLookout()
    {
        // Testing partially accrued amount for future overnight-indexed coupon with lockout...

        var vars = new CommonVars(new Date(12, March, 2019));

        YieldTermStructure forecastCurve = vars.FlatRate(0.0250, new Actual360());
        vars.LinkForecastCurve(forecastCurve);

        OvernightIndexedCoupon coupon15July =
            vars.MakeCoupon(new Date(1, July, 2019), new Date(15, July, 2019), null, 2, false);

        Rate lockoutFixing = vars.Sofr.Fixing(new Date(10, July, 2019), vars.Context);
        Rate expectedRate15July =
            ((forecastCurve.Discount(new Date(1, July, 2019))
              / forecastCurve.Discount(new Date(11, July, 2019))
              * (1.0 + (1.0 / 360.0 * lockoutFixing)) * (1.0 + (2.0 / 360.0 * lockoutFixing)))
             - 1.0)
            * 360.0 / 13;

        Real expectedAccruedAmount = coupon15July.Nominal(vars.Context)
            * coupon15July.AccruedPeriod(new Date(14, July, 2019))
            * expectedRate15July;

        CheckOisCouponResult(
            "accrued amount",
            coupon15July.AccruedAmount(new Date(14, July, 2019), vars.Context),
            expectedAccruedAmount,
            1e-12);
    }

    // Adapted: the forecast curve linked after Sofr is built (line 708) becomes Sofr built on the curve.
    [Fact]
    public void TestTelescopicFormulaWhenLookbackWithObservationShiftAndNoIndexFixingDelay()
    {
        // Testing telescopic formula when lookback with observation shift is applied and the index has no fixing
        // delay...

        var vars = new CommonVars(new Date(12, March, 2019));

        YieldTermStructure forecastCurve = vars.FlatRate(0.0250, new Actual360());
        vars.LinkForecastCurve(forecastCurve);

        OvernightIndexedCoupon coupon15July =
            vars.MakeCoupon(new Date(1, July, 2019), new Date(15, July, 2019), 3, 0, true);

        Rate actualRate = coupon15July.Rate(vars.Context);

        OvernightIndexedCoupon coupon15JulyWithTelescopicDates =
            vars.MakeCoupon(new Date(1, July, 2019), new Date(15, July, 2019), 3, 0, true, true);

        CheckOisCouponResult(
            "telescopic value dates coupon rate",
            actualRate,
            coupon15JulyWithTelescopicDates.Rate(vars.Context),
            1e-12);

        Rate expectedRateTelescopicSeries =
            ((forecastCurve.Discount(new Date(26, June, 2019)) / forecastCurve.Discount(new Date(10, July, 2019))) - 1.0)
            * 360.0 / 14;

        CheckOisCouponResult("coupon rate using telescopic formula", actualRate, expectedRateTelescopicSeries, 1e-12);

        ImmutableArray<Date> fixingDates = coupon15July.FixingDates();
        ImmutableArray<double> dts = coupon15July.Dt();
        Size n = fixingDates.Length;

        Rate expectedRateIterativeFormula = 1.0;
        for (Size i = 0; i < n; ++i)
        {
            expectedRateIterativeFormula *=
                1.0 + (dts[i] * coupon15July.Index.Fixing(fixingDates[i], vars.Context));
        }

        expectedRateIterativeFormula -= 1.0;
        expectedRateIterativeFormula /= coupon15July.AccrualPeriod();

        CheckOisCouponResult("coupon rate using iterative formula", actualRate, expectedRateIterativeFormula, 1e-12);
    }

    [Fact]
    public void TestErrorWhenTelescopicValueDatesEnforcedWithLookback()
    {
        // Testing error when telescopic value dates enforced with lookback...

        var vars = new CommonVars();

        CheckThrowsError(() =>
            vars.MakeCoupon(new Date(1, July, 2019), new Date(31, July, 2019), 2, 0, false, true));
    }

    [Fact]
    public void TestErrorWhenLookbackOrLockoutAppliedForSimpleAveraging()
    {
        // Testing error when lookback or lockout is applied for simple averaging...

        var vars = new CommonVars();

        CheckThrowsError(() =>
            vars.MakeCoupon(
                new Date(1, July, 2019), new Date(31, July, 2019), 2, 0, false, false, RateAveragingType.Simple));

        CheckThrowsError(() =>
            vars.MakeCoupon(
                new Date(1, July, 2019), new Date(31, July, 2019), null, 2, false, false, RateAveragingType.Simple));

        CheckThrowsError(() =>
            vars.MakeCoupon(
                new Date(1, July, 2019), new Date(31, July, 2019), null, 0, true, false, RateAveragingType.Simple));
    }

    // Adapted: setPricer, which changes a coupon in place (lines 785, 793, 803 and 813), returns a copy holding the
    // pricer; the isCalculated() check of line 804 is dropped, since a coupon caches nothing.
    [Fact]
    public void TestBlackOvernightIndexedCouponPricerCapletFloorlet()
    {
        // Testing Black compounding overnight-indexed coupon pricer...

        var vars = new BlackONPricerVars();
        var start = new Date(1, July, 2035);
        var end = new Date(1, October, 2035);

        // Vanilla
        OvernightIndexedCoupon vanillaCoupon = vars.MakeBaseCoupon(start, end);
        Rate expectedRate = vanillaCoupon.Rate(vars.Context);

        var pricer = new BlackCompoundingOvernightIndexedCouponPricer(vars.Vol);
        vanillaCoupon = vanillaCoupon.SetPricer(pricer);

        Rate rate = vanillaCoupon.Rate(vars.Context);
        CheckOisCouponResult("Base Rate", rate, expectedRate, 1e-8);

        // Caplet
        Rate cap = 0.045;
        CappedFlooredOvernightIndexedCoupon cappedCoupon = vars.MakeCoupon(start, end, cap, null);
        cappedCoupon = cappedCoupon.SetPricer(pricer);

        rate = cappedCoupon.Rate(vars.Context);
        expectedRate = 0.036862168;
        Assert.True(rate <= cap + 1e-8); // Should not exceed cap
        CheckOisCouponResult("Capped Rate", rate, expectedRate, 1e-8);

        // Floorlet
        Rate floor = 0.035;
        CappedFlooredOvernightIndexedCoupon flooredCoupon = vars.MakeCoupon(start, end, null, floor);
        flooredCoupon = flooredCoupon.SetPricer(pricer);

        rate = flooredCoupon.Rate(vars.Context);
        expectedRate = 0.04281620;
        Assert.True(rate >= floor - 1e-8); // Should not be below floor
        CheckOisCouponResult("Floored Rate", rate, expectedRate, 1e-8);

        // Capped and Floored
        CappedFlooredOvernightIndexedCoupon cappedFlooredCoupon = vars.MakeCoupon(start, end, cap, floor);
        cappedFlooredCoupon = cappedFlooredCoupon.SetPricer(pricer);

        rate = cappedFlooredCoupon.Rate(vars.Context);
        expectedRate = 0.039473179;
        Assert.True(rate <= cap + 1e-8 && rate >= floor - 1e-8);
        CheckOisCouponResult("Capped and Floored Rate", rate, expectedRate, 1e-8);
    }

    // Adapted: setPricer, which changes a coupon in place (lines 833, 841, 851 and 860), returns a copy holding the
    // pricer.
    [Fact]
    public void TestBlackAverageONIndexedCouponPricerCapletFloorlet()
    {
        // Testing Black averaging overnight-indexed coupon pricer...

        var vars = new BlackONPricerVars();
        var start = new Date(1, July, 2035);
        var end = new Date(1, October, 2035);

        // Vanilla
        OvernightIndexedCoupon vanillaCoupon = vars.MakeBaseCoupon(start, end, RateAveragingType.Simple);
        Rate expectedRate = vanillaCoupon.Rate(vars.Context);

        var pricer = new BlackAveragingOvernightIndexedCouponPricer(vars.Vol);
        vanillaCoupon = vanillaCoupon.SetPricer(pricer);

        Rate rate = vanillaCoupon.Rate(vars.Context);
        CheckOisCouponResult("Base Rate", rate, expectedRate, 1e-8);

        // Caplet
        Rate cap = 0.045;
        CappedFlooredOvernightIndexedCoupon cappedCoupon =
            vars.MakeCoupon(start, end, cap, null, RateAveragingType.Simple);
        cappedCoupon = cappedCoupon.SetPricer(pricer);

        rate = cappedCoupon.Rate(vars.Context);
        expectedRate = 0.036745802;
        Assert.True(rate <= cap + 1e-8);
        CheckOisCouponResult("Capped Rate", rate, expectedRate, 1e-8);

        // Floorlet
        Rate floor = 0.035;
        CappedFlooredOvernightIndexedCoupon flooredCoupon =
            vars.MakeCoupon(start, end, null, floor, RateAveragingType.Simple);
        flooredCoupon = flooredCoupon.SetPricer(pricer);

        rate = flooredCoupon.Rate(vars.Context);
        expectedRate = 0.042671405;
        Assert.True(rate >= floor - 1e-8);
        CheckOisCouponResult("Capped Rate", rate, expectedRate, 1e-8);

        // Capped and Floored
        CappedFlooredOvernightIndexedCoupon cappedFlooredCoupon =
            vars.MakeCoupon(start, end, cap, floor, RateAveragingType.Simple);
        cappedFlooredCoupon = cappedFlooredCoupon.SetPricer(pricer);

        rate = cappedFlooredCoupon.Rate(vars.Context);
        expectedRate = 0.039412858;
        Assert.True(rate <= cap + 1e-8 && rate >= floor - 1e-8);
        CheckOisCouponResult("Capped and Floored Rate", rate, expectedRate, 1e-8);
    }

    // Adapted: the volatility linked after the fixture is built (line 873) becomes the fixture built on the zero
    // volatility, and setPricer, which changes a coupon in place (lines 879, 884 and 889), returns a copy holding the
    // pricer.
    [Fact]
    public void TestBlackONPricerConsistencyWithNoVol()
    {
        // Testing Black compounding pricer with zero volatility (should match vanilla pricer)...

        var vars = new BlackONPricerVars(volatility: 0.0);
        var start = new Date(1, July, 2035);
        var end = new Date(1, October, 2035);

        CappedFlooredOvernightIndexedCoupon cappedFlooredCoupon = vars.MakeCoupon(start, end, 0.045, 0.035);
        var blackPricer = new BlackCompoundingOvernightIndexedCouponPricer(vars.Vol);
        cappedFlooredCoupon = cappedFlooredCoupon.SetPricer(blackPricer);
        Rate blackRate = cappedFlooredCoupon.Rate(vars.Context);

        // Compare with standard compounding pricer
        OvernightIndexedCoupon baseONCoupon = vars.MakeBaseCoupon(start, end);
        baseONCoupon = baseONCoupon.SetPricer(new CompoundingOvernightIndexedCouponPricer());
        Rate vanillaRate = baseONCoupon.Rate(vars.Context);

        CheckOisCouponResult("Zero capped coupon rate", blackRate, vanillaRate, 1e-10);

        baseONCoupon = baseONCoupon.SetPricer(blackPricer);
        vanillaRate = baseONCoupon.Rate(vars.Context);
        CheckOisCouponResult("Zero capped coupon rate (same pricer)", blackRate, vanillaRate, 1e-10);
    }

    // Adapted: the volatility linked after the fixture is built (line 899) becomes the fixture built on the zero
    // volatility, and setPricer, which changes a coupon in place (lines 905, 910 and 915), returns a copy holding the
    // pricer.
    [Fact]
    public void TestBlackONAveragingPricerConsistencyWithNoVol()
    {
        // Testing Black averaging pricer with zero volatility (should match vanilla pricer)...

        var vars = new BlackONPricerVars(volatility: 0.0);
        var start = new Date(1, July, 2035);
        var end = new Date(1, October, 2035);

        CappedFlooredOvernightIndexedCoupon cappedFlooredCoupon =
            vars.MakeCoupon(start, end, 0.045, 0.035, RateAveragingType.Simple);
        var blackPricer = new BlackAveragingOvernightIndexedCouponPricer(vars.Vol);
        cappedFlooredCoupon = cappedFlooredCoupon.SetPricer(blackPricer);
        Rate blackRate = cappedFlooredCoupon.Rate(vars.Context);

        // Compare with standard compounding pricer
        OvernightIndexedCoupon baseONCoupon = vars.MakeBaseCoupon(start, end, RateAveragingType.Simple);
        baseONCoupon = baseONCoupon.SetPricer(new ArithmeticAveragedOvernightIndexedCouponPricer());
        Rate vanillaRate = baseONCoupon.Rate(vars.Context);

        CheckOisCouponResult("Zero capped coupon rate", blackRate, vanillaRate, 1e-10);

        baseONCoupon = baseONCoupon.SetPricer(blackPricer);
        vanillaRate = baseONCoupon.Rate(vars.Context);
        CheckOisCouponResult("Zero capped coupon rate (same pricer)", blackRate, vanillaRate, 1e-10);
    }

    // Adapted: the forecast curve linked after Sofr is built (line 924) becomes Sofr built on the curve.
    [Fact]
    public void TestOvernightLegBasicFunctionality()
    {
        // Testing basic functionality of overnight leg...

        var vars = new CommonVarsONLeg();
        vars.LinkForecastCurve(vars.FlatRate(0.0010, new Actual360()));

        ImmutableArray<CashFlow> leg = vars.MakeLeg();

        // Check that we have the expected number of coupons (monthly over 1 year = 12 coupons)
        Assert.Equal(4, leg.Length);

        // Check that all cash flows are OvernightIndexedCoupons
        foreach (CashFlow cf in leg)
        {
            var oisCoupon = cf as OvernightIndexedCoupon;
            Assert.True(oisCoupon is not null);
            if (oisCoupon is not null)
            {
                Assert.Equal(vars.Notional, oisCoupon.Nominal(vars.Context));
                Assert.Equal(RateAveragingType.Compound, oisCoupon.AveragingMethod);
                Assert.Equal(0, oisCoupon.LockoutDays);
                Assert.False(oisCoupon.ApplyObservationShift);
            }
        }
    }

    // Adapted: the forecast curve linked after Sofr is built (line 948) becomes Sofr built on the curve.
    [Fact]
    public void TestOvernightLegWithLookback()
    {
        // Testing overnight leg construction with lookback days...

        var vars = new CommonVarsONLeg();
        vars.LinkForecastCurve(vars.FlatRate(0.0010, new Actual360()));

        Natural lookbackDays = 5;
        ImmutableArray<CashFlow> leg = vars.MakeLeg(lookbackDays);

        foreach (CashFlow cf in leg)
        {
            var oisCoupon = cf as OvernightIndexedCoupon;
            Assert.True(oisCoupon is not null);
            if (oisCoupon is not null)
            {
                // The coupon should have lookback configured
                Assert.True(oisCoupon.FixingDays == lookbackDays || oisCoupon.FixingDays == oisCoupon.Index.FixingDays);
            }
        }
    }

    // Adapted: the forecast curve linked after Sofr is built (line 968) becomes Sofr built on the curve.
    [Fact]
    public void TestOvernightLegWithLockout()
    {
        // Testing overnight leg construction with lockout days...

        var vars = new CommonVarsONLeg();
        vars.LinkForecastCurve(vars.FlatRate(0.0010, new Actual360()));

        Natural lockoutDays = 3;
        ImmutableArray<CashFlow> leg = vars.MakeLeg(null, lockoutDays);

        foreach (CashFlow cf in leg)
        {
            var oisCoupon = cf as OvernightIndexedCoupon;
            Assert.True(oisCoupon is not null);
            if (oisCoupon is not null)
            {
                Assert.Equal(lockoutDays, oisCoupon.LockoutDays);
            }
        }
    }

    // Adapted: the forecast curve linked after Sofr is built (line 986) becomes Sofr built on the curve.
    [Fact]
    public void TestOvernightLegWithObservationShift()
    {
        // Testing overnight leg construction with observation shift...

        var vars = new CommonVarsONLeg();
        vars.LinkForecastCurve(vars.FlatRate(0.0010, new Actual360()));

        ImmutableArray<CashFlow> leg = vars.MakeLeg(null, 0, true);

        foreach (CashFlow cf in leg)
        {
            var oisCoupon = cf as OvernightIndexedCoupon;
            Assert.True(oisCoupon is not null);
            if (oisCoupon is not null)
            {
                Assert.True(oisCoupon.ApplyObservationShift);
            }
        }
    }

    [Fact]
    public void TestOvernightLegWithGearingsAndSpreads()
    {
        // Testing overnight leg construction with gearings and spreads...

        var vars = new CommonVarsONLeg();
        vars.SetupForecastCurve();

        Real[] gearings = [1.0, 1.25, 2.0, 0.5];
        Spread[] spreads = [0.0001, 0.0001, 0.0002, 0.0002];

        ImmutableArray<CashFlow> leg =
            vars.MakeLeg(null, 0, false, false, RateAveragingType.Compound, gearings, spreads);

        Assert.Equal(4, leg.Length);

        for (Size i = 0; i < leg.Length; ++i)
        {
            var oisCoupon = leg[i] as OvernightIndexedCoupon;
            Assert.True(oisCoupon is not null);
            if (oisCoupon is not null)
            {
                CheckClose(oisCoupon.Gearing, gearings[i], 1e-12);
                CheckClose(oisCoupon.Spread, spreads[i], 1e-12);
            }
        }
    }

    // Moved: the check of line 1033, QuantLib's NPV of the leg with a lockout and telescopic value dates,
    // 34883.949669756257, which only its shortened value dates give, moves to Divergences/TelescopicValueDatesTests.cs:
    // with every daily value date, the leg gives QuantLib's full-schedule NPV (S3R11, S5R5).
    [Fact]
    public void TestOvernightLegNPV()
    {
        // Testing overnight leg NPV...

        var vars = new CommonVarsONLeg();
        vars.SetupForecastCurve();

        ImmutableArray<CashFlow> leg = vars.MakeLeg(null, 3, false, true, RateAveragingType.Compound);

        YieldTermStructure discountCurve = vars.FlatRate(0.0015, new Actual360());

        // Calculate NPV
        Real npv = 0.0;
        foreach (CashFlow cf in leg)
        {
            npv += cf.Amount(vars.Context) * discountCurve.Discount(cf.Date);
        }

        Assert.True(double.IsFinite(npv));
    }

    [Fact]
    public void TestOvernightLegWithCapsAndFloors()
    {
        // Testing overnight leg with caps and floors...

        var vars = new CommonVarsONLeg();
        vars.SetupForecastCurve();
        YieldTermStructure discountCurve = vars.FlatRate(0.0015, new Actual360());

        Rate?[] caps = [0.0435, 0.0435, 0.04, 0.04];
        Rate?[] floors = [0.025, 0.025, 0.025, 0.025];

        ImmutableArray<CashFlow> leg =
            vars.MakeLeg(null, 0, false, false, RateAveragingType.Compound, [], [], caps, floors);

        Assert.Equal(4, leg.Length);

        Real expectedNpv = 34648.328606210489;
        Real npv = 0.0;

        for (Size i = 0; i < leg.Length; ++i)
        {
            var cappedFlooredCoupon = leg[i] as CappedFlooredOvernightIndexedCoupon;
            Assert.True(cappedFlooredCoupon is not null);
            if (cappedFlooredCoupon is not null)
            {
                CheckClose(cappedFlooredCoupon.Cap!.Value, caps[i]!.Value, 1e-12);
                CheckClose(cappedFlooredCoupon.Floor!.Value, floors[i]!.Value, 1e-12);
                Assert.True(cappedFlooredCoupon.IsCapped);
                Assert.True(cappedFlooredCoupon.IsFloored);
            }

            npv += cappedFlooredCoupon!.Amount(vars.Context) * discountCurve.Discount(cappedFlooredCoupon.Date);
        }

        CheckOisCouponResult("Capped-Floored OvernightLeg NPV", npv, expectedNpv, 1e-8);
    }

    // Adapted: the forecast curve linked after Sofr is built (line 1081) becomes Sofr built on the curve.
    [Fact]
    public void TestOvernightLegSimpleAveraging()
    {
        // Testing overnight leg construction with simple averaging...

        var vars = new CommonVarsONLeg();
        vars.LinkForecastCurve(vars.FlatRate(0.0010, new Actual360()));

        ImmutableArray<CashFlow> leg = vars.MakeLeg(null, 0, false, false, RateAveragingType.Simple);

        foreach (CashFlow cf in leg)
        {
            var oisCoupon = cf as OvernightIndexedCoupon;
            Assert.True(oisCoupon is not null);
            if (oisCoupon is not null)
            {
                Assert.Equal(RateAveragingType.Simple, oisCoupon.AveragingMethod);
            }
        }
    }

    // Adapted: the forecast curve linked after Sofr is built (line 1098) becomes Sofr built on the curve.
    [Fact]
    public void TestOvernightLegErrorConditions()
    {
        // Testing error conditions for overnight leg...

        var vars = new CommonVarsONLeg();
        vars.LinkForecastCurve(vars.FlatRate(0.0010, new Actual360()));

        // Test that lookback with simple averaging throws an error
        CheckThrowsError(() => vars.MakeLeg(5, 0, false, false, RateAveragingType.Simple));

        // Test that lockout with simple averaging throws an error
        CheckThrowsError(() => vars.MakeLeg(null, 3, false, false, RateAveragingType.Simple));

        // Test that observation shift with simple averaging throws an error
        CheckThrowsError(() => vars.MakeLeg(null, 0, true, false, RateAveragingType.Simple));
    }

    // Adapted: the forecast curve linked after Sofr is built (line 1113) becomes Sofr built on the curve.
    [Fact]
    public void TestOvernightCouponAmountRounding()
    {
        // Testing overnight coupon amount with rounded rate...

        var vars = new CommonVars();
        vars.LinkForecastCurve(vars.FlatRate(0.0010, new Actual360()));

        OvernightIndexedCoupon unrounded = vars.MakeCoupon(new Date(10, December, 2021), new Date(10, January, 2022));

        var rounded = new OvernightIndexedCoupon(
            new Date(10, January, 2022),
            vars.Notional,
            new Date(10, December, 2021),
            new Date(10, January, 2022),
            vars.Sofr,
            1.0,
            0.0,
            null,
            null,
            null,
            false,
            RateAveragingType.Compound,
            null,
            0,
            false,
            false,
            null,
            null,
            null,
            5); // rounding precision
        Rate roundedRate = new ClosestRounding(5).Evaluate(unrounded.Rate(vars.Context));

        Real expectedAmount = vars.Notional * roundedRate * unrounded.AccrualPeriod();

        Assert.NotEqual(unrounded.Amount(vars.Context), rounded.Amount(vars.Context));

        CheckClose(rounded.Amount(vars.Context), expectedAmount, 1e-10);
    }

    // Adapted: the evaluation date set at line 1162 is dropped: the coupon's constructor reads no evaluation date
    // (every daily value date is built), and the case prices nothing.
    [Fact]
    public void TestOvernightIndexedCouponPaymentBeforeAccrualEnd()
    {
        // Testing that an overnight coupon with inconsistent dates throws...

        var accrualStart = new Date(18, September, 2025);

        var h = new FlatForward(accrualStart, 0.05, new Actual365Fixed());
        OvernightIndex estr = new Estr(h);

        Calendar cal = new WeekendsOnly();
        Date accrualEnd = cal.Advance(accrualStart, new Period(6, TimeUnit.Months));
        Date paymentDate = cal.Advance(accrualEnd, new Period(-1, TimeUnit.Days));

        CheckThrowsError(() => new OvernightIndexedCoupon(paymentDate, 1.0, accrualStart, accrualEnd, estr));
    }

    // Adapted: the forecast curve linked after Sofr is built (line 1185) becomes Sofr built on the curve, and the
    // history cleared and refilled mid-case (lines 1198-1199) becomes a second store, in a second context.
    [Fact]
    public void TestInterestCalculatedAccrualDateFixingHoliday()
    {
        // Testing compounded interest when an accrual start/end date lands on a fixing holiday...
        var vars = new CommonVars(new Date(19, April, 2023));
        vars.LinkForecastCurve(vars.FlatRate(0.0432, new Actual360()));
        OvernightIndex sofr = vars.Sofr;
        Calendar fixingCal = sofr.FixingCalendar;
        Schedule sch = new MakeSchedule()
            .From(new Date(15, October, 2021))
            .To(new Date(14, April, 2023))
            .WithTenor(new Period(1, TimeUnit.Days))
            .WithCalendar(fixingCal)
            .WithConvention(sofr.BusinessDayConvention)
            .Forwards()
            .ToSchedule();
        ImmutableArray<Date> pastDates = sch.Dates;
        Rate[] pastRates = Enumerable.Repeat(0.048, pastDates.Length).ToArray();

        // Overwrite the fixings provided within CommonVars to cover interest allocation to coupons
        // which start/end on good friday, the only holiday that is in the SOFR fixing calendar
        // but not the federal reserve calendar used to generate SOFR swap coupons
        EvaluationContext context = vars.Context with
        {
            Fixings = vars.Context.Fixings.ClearFixings(sofr).AddFixings(sofr, pastDates, pastRates),
        };
        DayCounter dc = sofr.DayCounter;
        (Date StartDate, Date EndDate, Date SplitAt)[] cases =
        [
            // fixed
            (new Date(15, October, 2021), new Date(17, April, 2023), new Date(15, April, 2022)),

            // forward
            (new Date(18, October, 2024), new Date(20, April, 2026), new Date(18, April, 2025)),
        ];
        static Real CompoundFactor(Rate rate, double yearFraction) => 1.0 + (rate * yearFraction);
        Date PayDate(Date endDate) => fixingCal.Advance(endDate, 2, TimeUnit.Days);
        foreach ((Date startDate, Date endDate, Date splitAt) in cases)
        {
            Assert.True(fixingCal.IsHoliday(splitAt));
            var cpnTotal = new OvernightIndexedCoupon(PayDate(endDate), 10000000.0, startDate, endDate, sofr);
            var cpnLeft = new OvernightIndexedCoupon(PayDate(splitAt), 10000000.0, startDate, splitAt, sofr);
            var cpnRight = new OvernightIndexedCoupon(cpnTotal.Date, 10000000.0, splitAt, endDate, sofr);
            Real compounded = CompoundFactor(cpnLeft.Rate(context), cpnLeft.AccrualPeriod())
                * CompoundFactor(cpnRight.Rate(context), cpnRight.AccrualPeriod());
            Real total = CompoundFactor(cpnTotal.Rate(context), cpnTotal.AccrualPeriod());
            Date valueDate = fixingCal.Adjust(splitAt, BusinessDayConvention.Preceding);
            Date maturityDate = fixingCal.Adjust(splitAt, BusinessDayConvention.Following);
            Rate fixing = sofr.Fixing(valueDate, context);

            // check that interest is split across coupons as expected
            Real adjustment = CompoundFactor(fixing, dc.YearFraction(valueDate, maturityDate))
                / CompoundFactor(fixing, dc.YearFraction(valueDate, splitAt))
                / CompoundFactor(fixing, dc.YearFraction(splitAt, maturityDate));
            CheckOisCouponResult("compound factor", compounded * adjustment, total, 1e-12);
        }
    }

    // Adapted: the forecast curve linked after Sofr is built (line 1239) becomes Sofr built on the curve, and the
    // history cleared and refilled mid-case (lines 1329-1330) becomes a second store, in a second context.
    [Fact]
    public void TestInterestCalculatedAccrualDateFixingHolidayAccruals()
    {
        // Testing compounded interest when an accrual start/end date lands on a fixing holiday...

        var vars = new CommonVars(new Date(29, May, 2025));
        vars.LinkForecastCurve(vars.FlatRate(0.0433, new Actual360()));

        Date[] pastDates =
        [
            new Date(10, October, 2024), new Date(11, October, 2024), new Date(15, October, 2024), new Date(16, October, 2024),
            new Date(17, October, 2024), new Date(18, October, 2024), new Date(21, October, 2024), new Date(22, October, 2024),
            new Date(23, October, 2024), new Date(24, October, 2024), new Date(25, October, 2024), new Date(28, October, 2024),
            new Date(29, October, 2024), new Date(30, October, 2024), new Date(31, October, 2024), new Date(1, November, 2024),
            new Date(4, November, 2024), new Date(5, November, 2024), new Date(6, November, 2024), new Date(7, November, 2024),
            new Date(8, November, 2024), new Date(12, November, 2024), new Date(13, November, 2024), new Date(14, November, 2024),
            new Date(15, November, 2024), new Date(18, November, 2024), new Date(19, November, 2024), new Date(20, November, 2024),
            new Date(21, November, 2024), new Date(22, November, 2024), new Date(25, November, 2024), new Date(26, November, 2024),
            new Date(27, November, 2024), new Date(29, November, 2024), new Date(2, December, 2024), new Date(3, December, 2024),
            new Date(4, December, 2024), new Date(5, December, 2024), new Date(6, December, 2024), new Date(9, December, 2024),
            new Date(10, December, 2024), new Date(11, December, 2024), new Date(12, December, 2024), new Date(13, December, 2024),
            new Date(16, December, 2024), new Date(17, December, 2024), new Date(18, December, 2024), new Date(19, December, 2024),
            new Date(20, December, 2024), new Date(23, December, 2024), new Date(24, December, 2024), new Date(26, December, 2024),
            new Date(27, December, 2024), new Date(30, December, 2024), new Date(31, December, 2024), new Date(2, January, 2025),
            new Date(3, January, 2025), new Date(6, January, 2025), new Date(7, January, 2025), new Date(8, January, 2025),
            new Date(9, January, 2025), new Date(10, January, 2025), new Date(13, January, 2025), new Date(14, January, 2025),
            new Date(15, January, 2025), new Date(16, January, 2025), new Date(17, January, 2025), new Date(21, January, 2025),
            new Date(22, January, 2025), new Date(23, January, 2025), new Date(24, January, 2025), new Date(27, January, 2025),
            new Date(28, January, 2025), new Date(29, January, 2025), new Date(30, January, 2025), new Date(31, January, 2025),
            new Date(3, February, 2025), new Date(4, February, 2025), new Date(5, February, 2025), new Date(6, February, 2025),
            new Date(7, February, 2025), new Date(10, February, 2025), new Date(11, February, 2025), new Date(12, February, 2025),
            new Date(13, February, 2025), new Date(14, February, 2025), new Date(18, February, 2025), new Date(19, February, 2025),
            new Date(20, February, 2025), new Date(21, February, 2025), new Date(24, February, 2025), new Date(25, February, 2025),
            new Date(26, February, 2025), new Date(27, February, 2025), new Date(28, February, 2025), new Date(3, March, 2025),
            new Date(4, March, 2025), new Date(5, March, 2025), new Date(6, March, 2025), new Date(7, March, 2025),
            new Date(10, March, 2025), new Date(11, March, 2025), new Date(12, March, 2025), new Date(13, March, 2025),
            new Date(14, March, 2025), new Date(17, March, 2025), new Date(18, March, 2025), new Date(19, March, 2025),
            new Date(20, March, 2025), new Date(21, March, 2025), new Date(24, March, 2025), new Date(25, March, 2025),
            new Date(26, March, 2025), new Date(27, March, 2025), new Date(28, March, 2025), new Date(31, March, 2025),
            new Date(1, April, 2025), new Date(2, April, 2025), new Date(3, April, 2025), new Date(4, April, 2025),
            new Date(7, April, 2025), new Date(8, April, 2025), new Date(9, April, 2025), new Date(10, April, 2025),
            new Date(11, April, 2025), new Date(14, April, 2025), new Date(15, April, 2025), new Date(16, April, 2025),
            new Date(17, April, 2025), new Date(21, April, 2025), new Date(22, April, 2025), new Date(23, April, 2025),
            new Date(24, April, 2025), new Date(25, April, 2025), new Date(28, April, 2025), new Date(29, April, 2025),
            new Date(30, April, 2025), new Date(1, May, 2025), new Date(2, May, 2025), new Date(5, May, 2025),
            new Date(6, May, 2025), new Date(7, May, 2025), new Date(8, May, 2025), new Date(9, May, 2025),
            new Date(12, May, 2025), new Date(13, May, 2025), new Date(14, May, 2025), new Date(15, May, 2025),
            new Date(16, May, 2025), new Date(19, May, 2025), new Date(20, May, 2025), new Date(21, May, 2025),
            new Date(22, May, 2025), new Date(23, May, 2025), new Date(27, May, 2025), new Date(28, May, 2025),
            new Date(29, May, 2025),
        ];

        Rate[] pastRates =
        [
            0.0482, 0.0481, 0.0486, 0.0486, 0.0485, 0.0484, 0.0482, 0.0483,
            0.0483, 0.0483, 0.0483, 0.0482, 0.0482, 0.0481, 0.049, 0.0486,
            0.0482, 0.0482, 0.0481, 0.0482, 0.046, 0.046, 0.0459, 0.0458,
            0.0457, 0.0457, 0.0457, 0.0456, 0.0457, 0.0457, 0.0458, 0.0458,
            0.0457, 0.0459, 0.0464, 0.0464, 0.0459, 0.0459, 0.046, 0.0463,
            0.0464, 0.0462, 0.0462, 0.046, 0.0465, 0.0462, 0.0457, 0.043,
            0.043, 0.0431, 0.044, 0.0453, 0.0446, 0.0437, 0.0449, 0.044,
            0.0431, 0.0427, 0.0427, 0.0429, 0.043, 0.043, 0.0429, 0.0428,
            0.0428, 0.0429, 0.0429, 0.0429, 0.043, 0.0435, 0.0434, 0.0434,
            0.0435, 0.0435, 0.0436, 0.0438, 0.0435, 0.0433, 0.0433, 0.0436,
            0.0435, 0.0435, 0.0434, 0.0432, 0.0433, 0.0433, 0.0437, 0.0435,
            0.0433, 0.0434, 0.0434, 0.0433, 0.0433, 0.0436, 0.0439, 0.0433,
            0.0433, 0.0434, 0.0435, 0.0434, 0.0433, 0.0432, 0.0431, 0.043,
            0.043, 0.0432, 0.0431, 0.0429, 0.0429, 0.043, 0.0431, 0.0433,
            0.0435, 0.0436, 0.0434, 0.0441, 0.0439, 0.0437, 0.0439, 0.0435,
            0.0433, 0.044, 0.0442, 0.0437, 0.0433, 0.0433, 0.0436, 0.0431,
            0.0432, 0.0432, 0.043, 0.0428, 0.0429, 0.0433, 0.0436, 0.0436,
            0.0441, 0.0439, 0.0436, 0.0433, 0.0432, 0.043, 0.0429, 0.0428,
            0.0428, 0.043, 0.0429, 0.0431, 0.043, 0.0429, 0.0427, 0.0426,
            0.0426, 0.0426, 0.0431, 0.0433, 0.0433,
        ];

        TestCase[] cases =
        [
            // coupon ends on a fixing holiday, vanilla case
            new(new Date(18, October, 2024), new Date(18, April, 2025), 0, false, 0, null, null, null,
            [
                (new Date(18, October, 2024), 0.0),
                (new Date(19, October, 2024), 1344.444444444),
                (new Date(20, October, 2024), 2688.88888888),
                (new Date(21, October, 2024), 4033.333333333),
                (new Date(22, October, 2024), 5372.76224074),
                (new Date(2, March, 2025), 169984.93805796764),
                (new Date(3, March, 2025), 171224.80883054345),
                (new Date(17, April, 2025), 226499.35892772756),
                (new Date(18, April, 2025), 227726.5388507987),
                (new Date(19, April, 2025), 227726.5388507987),
                (new Date(20, April, 2025), 227726.5388507987),
                (new Date(21, April, 2025), 227726.5388507987),
                (new Date(22, April, 2025), 227726.5388507987),
                (new Date(23, April, 2025), 0.0),
            ]),
            // coupon ends on a fixing holiday with obs-shift, but no lookback
            new(new Date(18, October, 2024), new Date(18, April, 2025), 0, true, 0, null, null, null,
            [
                (new Date(18, October, 2024), 0.0),
                (new Date(19, October, 2024), 1344.444444444),
                (new Date(20, October, 2024), 2688.88888888),
                (new Date(21, October, 2024), 4033.333333333),
                (new Date(22, October, 2024), 5372.76224074),
                (new Date(2, March, 2025), 169984.93805796764),
                (new Date(3, March, 2025), 171224.80883054345),
                (new Date(17, April, 2025), 226499.35892772756),
                (new Date(18, April, 2025), 227726.5388507987),
                (new Date(19, April, 2025), 227726.5388507987),
                (new Date(20, April, 2025), 227726.5388507987),
                (new Date(21, April, 2025), 227726.5388507987),
                (new Date(22, April, 2025), 227726.5388507987),
                (new Date(23, April, 2025), 0.0),
            ]),
            // coupon ends on a fixing holiday with 4d lockout
            new(new Date(18, October, 2024), new Date(18, April, 2025), 0, false, 4, null, null, null,
            [
                (new Date(18, October, 2024), 0.0),
                (new Date(19, October, 2024), 1344.444444444),
                (new Date(20, October, 2024), 2688.88888888),
                (new Date(21, October, 2024), 4033.333333333),
                (new Date(22, October, 2024), 5372.76224074),
                (new Date(2, March, 2025), 169984.93805796764),
                (new Date(3, March, 2025), 171224.80883054345),
                (new Date(17, April, 2025), 226496.51858061668),
                (new Date(18, April, 2025), 227726.53885632323),
                (new Date(19, April, 2025), 227726.53885632323),
                (new Date(20, April, 2025), 227726.53885632323),
                (new Date(21, April, 2025), 227726.53885632323),
                (new Date(22, April, 2025), 227726.53885632323),
                (new Date(23, April, 2025), 0.0),
            ]),
            // coupon ends on a fixing holiday with 4d lockout, with obs-shift but no lookback
            new(new Date(18, October, 2024), new Date(18, April, 2025), 0, true, 4, null, null, null,
            [
                (new Date(18, October, 2024), 0.0),
                (new Date(19, October, 2024), 1344.444444444),
                (new Date(20, October, 2024), 2688.88888888),
                (new Date(21, October, 2024), 4033.333333333),
                (new Date(22, October, 2024), 5372.76224074),
                (new Date(2, March, 2025), 169984.93805796764),
                (new Date(3, March, 2025), 171224.80883054345),
                (new Date(17, April, 2025), 226496.51858061668),
                (new Date(18, April, 2025), 227726.53885632323),
                (new Date(19, April, 2025), 227726.53885632323),
                (new Date(20, April, 2025), 227726.53885632323),
                (new Date(21, April, 2025), 227726.53885632323),
                (new Date(22, April, 2025), 227726.53885632323),
                (new Date(23, April, 2025), 0.0),
            ]),
            // coupon ends on a fixing holiday with 5d lookback
            new(new Date(18, October, 2024), new Date(18, April, 2025), 5, false, 0, null, null, null,
            [
                (new Date(18, October, 2024), 0.0),
                (new Date(19, October, 2024), 1338.888888888),
                (new Date(20, October, 2024), 2677.777777777),
                (new Date(21, October, 2024), 4016.666666666),
                (new Date(22, October, 2024), 5353.314449075),
                (new Date(2, March, 2025), 171188.0732570225),
                (new Date(3, March, 2025), 172413.9709083072),
                (new Date(17, April, 2025), 227743.262731344),
                (new Date(18, April, 2025), 228984.79712184728),
                (new Date(19, April, 2025), 228984.79712184728),
                (new Date(20, April, 2025), 228984.79712184728),
                (new Date(21, April, 2025), 228984.79712184728),
                (new Date(22, April, 2025), 228984.79712184728),
                (new Date(23, April, 2025), 0.0),
            ]),
            // coupon ends on a fixing holiday with 5d lookback and obs-shift
            new(new Date(18, October, 2024), new Date(18, April, 2025), 5, true, 0, null, null, null,
            [
                (new Date(18, October, 2024), 0.0),
                (new Date(19, October, 2024), 1338.888888888),
                (new Date(20, October, 2024), 2677.777777777),
                (new Date(21, October, 2024), 4016.666666666),
                (new Date(22, October, 2024), 5347.239116048),
                (new Date(1, March, 2025), 169713.19769125758),
                (new Date(2, March, 2025), 170979.7140919386),
                (new Date(3, March, 2025), 172246.23049261962),
                (new Date(17, April, 2025), 227590.6084479198),
                (new Date(18, April, 2025), 228832.36294407956),
                (new Date(19, April, 2025), 228832.36294407956),
                (new Date(20, April, 2025), 228832.36294407956),
                (new Date(21, April, 2025), 228832.36294407956),
                (new Date(22, April, 2025), 228832.36294407956),
                (new Date(23, April, 2025), 0.0),
            ]),
            // coupon ends on a fixing holiday with 5d lookback and 4d lockout
            new(new Date(18, October, 2024), new Date(18, April, 2025), 5, false, 4, null, null, null,
            [
                (new Date(18, October, 2024), 0.0),
                (new Date(19, October, 2024), 1338.8888888887784),
                (new Date(20, October, 2024), 2677.7777777775568),
                (new Date(21, October, 2024), 4016.666666666),
                (new Date(22, October, 2024), 5353.314449074),
                (new Date(2, March, 2025), 171188.07325702257),
                (new Date(3, March, 2025), 172413.970908307),
                (new Date(17, April, 2025), 227714.85583612081),
                (new Date(18, April, 2025), 228950.70471453448),
                (new Date(19, April, 2025), 228950.70471453448),
                (new Date(20, April, 2025), 228950.70471453448),
                (new Date(21, April, 2025), 228950.70471453448),
                (new Date(22, April, 2025), 228950.70471453448),
                (new Date(23, April, 2025), 0.0),
            ]),
            // coupon ends on a fixing holiday with 5d lookback, obs-shift, and 4d lockout
            new(new Date(18, October, 2024), new Date(18, April, 2025), 5, true, 4, null, null, null,
            [
                (new Date(18, October, 2024), 0.0),
                (new Date(19, October, 2024), 1338.8888888887784),
                (new Date(20, October, 2024), 2677.7777777775568),
                (new Date(21, October, 2024), 4016.666666666),
                (new Date(22, October, 2024), 5347.2391160482857),
                (new Date(1, March, 2025), 169713.19769125758),
                (new Date(2, March, 2025), 170979.7140919386),
                (new Date(3, March, 2025), 172246.23049261962),
                (new Date(17, April, 2025), 227562.35458301706),
                (new Date(18, April, 2025), 228798.4531716943),
                (new Date(19, April, 2025), 228798.4531716943),
                (new Date(20, April, 2025), 228798.4531716943),
                (new Date(21, April, 2025), 228798.4531716943),
                (new Date(22, April, 2025), 228798.4531716943),
                (new Date(23, April, 2025), 0.0),
            ]),
            // coupon starts on a fixing holiday
            new(new Date(18, April, 2025), new Date(19, May, 2025), 0, false, 0, null, null, null,
            [
                (new Date(18, April, 2025), 0.0),
                (new Date(19, April, 2025), 1200.0),
                (new Date(20, April, 2025), 2400.0),
                (new Date(21, April, 2025), 3600.0),
                (new Date(22, April, 2025), 4800.432),
                (new Date(15, May, 2025), 32462.79161128074),
                (new Date(16, May, 2025), 33663.900351055265),
                (new Date(17, May, 2025), 34862.365761374473),
                (new Date(18, May, 2025), 36060.831171695892),
                (new Date(19, May, 2025), 37259.296582012881),
                (new Date(20, May, 2025), 37259.296582012881),
                (new Date(21, May, 2025), 37259.296582012881),
                (new Date(22, May, 2025), 0.0),
            ]),
            // test daycounter different than index with lookback
            new(new Date(18, April, 2025), new Date(19, May, 2025), 5, true, 0, new Actual365Fixed(), null, null,
            [
                (new Date(18, April, 2025), 0.0),
                (new Date(19, April, 2025), 1197.2602739727813),
                (new Date(20, April, 2025), 2394.5205479455626),
                (new Date(21, April, 2025), 3591.780821918343),
                (new Date(22, April, 2025), 4756.5963949772768),
                (new Date(15, May, 2025), 32114.584259337709),
                (new Date(16, May, 2025), 33294.244344813727),
                (new Date(17, May, 2025), 34449.672542982727),
                (new Date(18, May, 2025), 35637.592285844192),
                (new Date(19, May, 2025), 36825.512028705671),
                (new Date(20, May, 2025), 36825.512028705671),
                (new Date(21, May, 2025), 36825.512028705671),
                (new Date(22, May, 2025), 0.0),
            ]),
            // test daycounter different than index
            new(new Date(18, April, 2025), new Date(19, May, 2025), 0, false, 0, new Actual365Fixed(), null, null,
            [
                (new Date(18, April, 2025), 0.0),
                (new Date(19, April, 2025), 1183.5616438346101),
                (new Date(20, April, 2025), 2367.1232876714103),
                (new Date(21, April, 2025), 3550.6849315060199),
                (new Date(22, April, 2025), 4734.6726575326247),
                (new Date(15, May, 2025), 32018.095835783744),
                (new Date(16, May, 2025), 33202.751031177795),
                (new Date(17, May, 2025), 34384.799107109066),
                (new Date(18, May, 2025), 35566.847183042526),
                (new Date(19, May, 2025), 36748.895258971606),
                (new Date(20, May, 2025), 36748.895258971606),
                (new Date(21, May, 2025), 36748.895258971606),
                (new Date(22, May, 2025), 0.0),
            ]),
            // lookback lands on a fixing holiday without obs-shift
            new(new Date(25, April, 2025), new Date(27, May, 2025), 5, false, 0, null, null, null,
            [
                (new Date(25, April, 2025), 0.0),
                (new Date(26, April, 2025), 1200.0),
                (new Date(27, April, 2025), 2400.0),
                (new Date(28, April, 2025), 3600.0),
                (new Date(22, May, 2025), 32462.79161128074),
                (new Date(23, May, 2025), 33663.900351055265),
                (new Date(24, May, 2025), 34862.365761374473),
                (new Date(25, May, 2025), 36060.831171695892),
                (new Date(26, May, 2025), 37259.296582012881),
                (new Date(27, May, 2025), 38457.761992334308),
                (new Date(28, May, 2025), 38457.761992334308),
                (new Date(29, May, 2025), 38457.761992334308),
                (new Date(30, May, 2025), 0.0),
            ]),
            // lookback lands on a fixing holiday with obs-shift
            new(new Date(25, April, 2025), new Date(27, May, 2025), 5, true, 0, null, null, null,
            [
                (new Date(25, April, 2025), 0.0),
                (new Date(26, April, 2025), 1200.0),
                (new Date(27, April, 2025), 2400.0),
                (new Date(28, April, 2025), 3600.0),
                (new Date(22, May, 2025), 32463.887689144285),
                (new Date(23, May, 2025), 33665.178902847132),
                (new Date(24, May, 2025), 34857.396658668215),
                (new Date(25, May, 2025), 36059.3758537947),
                (new Date(26, May, 2025), 37261.355048921192),
                (new Date(27, May, 2025), 38463.334244047684),
                (new Date(28, May, 2025), 38463.334244047684),
                (new Date(29, May, 2025), 38463.334244047684),
                (new Date(30, May, 2025), 0.0),
            ]),
            // Coupon period spans fixing holiday
            new(new Date(14, April, 2025), new Date(14, May, 2025), 0, false, 0, null, null, null,
            [
                (new Date(14, April, 2025), 0.0),
                (new Date(15, April, 2025), 1202.7777777778858),
                (new Date(16, April, 2025), 2414.0345586420867),
                (new Date(17, April, 2025), 3611.5457944463355),
                (new Date(18, April, 2025), 4811.9791799416953),
                (new Date(19, April, 2025), 6012.4125654370528),
                (new Date(10, May, 2025), 31317.54145558663),
                (new Date(11, May, 2025), 32510.011880697577),
                (new Date(12, May, 2025), 33702.482305808524),
                (new Date(13, May, 2025), 34895.378045369798),
                (new Date(14, May, 2025), 36093.990548857757),
                (new Date(15, May, 2025), 36093.990548857757),
                (new Date(16, May, 2025), 36093.990548857757),
                (new Date(17, May, 2025), 0.0),
            ]),
            // Partially fixed coupon
            new(new Date(14, April, 2025), new Date(16, Jun, 2025), 0, false, 0, null, new Date(11, Jun, 2025), new Date(14, Jun, 2025),
            [
                (new Date(14, April, 2025), 0.0),
                (new Date(15, April, 2025), 1202.7777777778858),
                (new Date(16, April, 2025), 2414.0345586420867),
                (new Date(17, April, 2025), 3611.5457944463355),
                (new Date(18, April, 2025), 4811.9791799416953),
                (new Date(19, April, 2025), 6012.4125654370528),
                (new Date(28, May, 2025), 52812.944149152048),
                (new Date(29, May, 2025), 54022.074150490429),
                (new Date(30, May, 2025), 55231.349583297902),
                (new Date(31, May, 2025), 56440.988690997779),
                (new Date(1, Jun, 2025), 57650.627798697671),
                (new Date(2, Jun, 2025), 58860.266906397563),
                (new Date(3, Jun, 2025), 60070.197028672112),
                (new Date(4, Jun, 2025), 61280.272687407327),
                (new Date(5, Jun, 2025), 62490.493900104753),
                (new Date(6, Jun, 2025), 63700.860684274834),
                (new Date(7, Jun, 2025), 64911.518669761303),
                (new Date(8, Jun, 2025), 66122.176655245552),
                (new Date(9, Jun, 2025), 67332.83464073202),
                (new Date(10, Jun, 2025), 68543.783885914861),
                (new Date(11, Jun, 2025), 69754.878790140312),
                (new Date(12, Jun, 2025), 70966.119370929882),
                (new Date(13, Jun, 2025), 72177.505645807381),
                (new Date(14, Jun, 2025), 73389.183367278398),
                (new Date(15, Jun, 2025), 74600.861088749429),
                (new Date(16, Jun, 2025), 75812.538810220431),
                (new Date(17, Jun, 2025), 75812.538810220431),
                (new Date(18, Jun, 2025), 75812.538810220431),
                (new Date(19, Jun, 2025), 0.0),
            ]),
            // Partially fixed coupon with 1d lockout
            new(new Date(14, April, 2025), new Date(16, Jun, 2025), 0, false, 1, null, new Date(11, Jun, 2025), new Date(14, Jun, 2025),
            [
                (new Date(14, April, 2025), 0.0),
                (new Date(15, April, 2025), 1202.7777777778858),
                (new Date(16, April, 2025), 2414.0345586420867),
                (new Date(17, April, 2025), 3611.5457944463355),
                (new Date(18, April, 2025), 4811.9791799416953),
                (new Date(19, April, 2025), 6012.4125654370528),
                (new Date(28, May, 2025), 52812.944149152048),
                (new Date(29, May, 2025), 54022.074150490429),
                (new Date(30, May, 2025), 55231.349583297902),
                (new Date(31, May, 2025), 56440.988690997779),
                (new Date(1, Jun, 2025), 57650.627798697671),
                (new Date(2, Jun, 2025), 58860.266906397563),
                (new Date(3, Jun, 2025), 60070.197028672112),
                (new Date(4, Jun, 2025), 61280.272687407327),
                (new Date(5, Jun, 2025), 62490.493900104753),
                (new Date(6, Jun, 2025), 63700.860684274834),
                (new Date(7, Jun, 2025), 64911.518669761303),
                (new Date(8, Jun, 2025), 66122.176655245552),
                (new Date(9, Jun, 2025), 67332.83464073202),
                (new Date(10, Jun, 2025), 68543.783885914861),
                (new Date(11, Jun, 2025), 69754.878790140312),
                (new Date(12, Jun, 2025), 70966.119370929882),
                (new Date(13, Jun, 2025), 72177.505645807381),
                (new Date(14, Jun, 2025), 73389.037632296531),
                (new Date(15, Jun, 2025), 74600.569618785696),
                (new Date(16, Jun, 2025), 75812.101605274845),
                (new Date(17, Jun, 2025), 75812.101605274845),
                (new Date(18, Jun, 2025), 75812.101605274845),
                (new Date(19, Jun, 2025), 0.0),
            ]),
            // Partially fixed coupon with 5d lockout
            new(new Date(14, April, 2025), new Date(16, Jun, 2025), 0, false, 5, null, new Date(11, Jun, 2025), new Date(19, Jun, 2025),
            [
                (new Date(14, April, 2025), 0.0),
                (new Date(15, April, 2025), 1202.7777777778858),
                (new Date(16, April, 2025), 2414.0345586420867),
                (new Date(17, April, 2025), 3611.5457944463355),
                (new Date(18, April, 2025), 4811.9791799416953),
                (new Date(19, April, 2025), 6012.4125654370528),
                (new Date(28, May, 2025), 52812.944149152048),
                (new Date(29, May, 2025), 54022.074150490429),
                (new Date(30, May, 2025), 55231.349583297902),
                (new Date(31, May, 2025), 56440.988690997779),
                (new Date(1, Jun, 2025), 57650.627798697671),
                (new Date(2, Jun, 2025), 58860.266906397563),
                (new Date(3, Jun, 2025), 60070.197028672112),
                (new Date(4, Jun, 2025), 61280.272687407327),
                (new Date(5, Jun, 2025), 62490.493900104753),
                (new Date(6, Jun, 2025), 63700.860684274834),
                (new Date(7, Jun, 2025), 64911.518669761303),
                (new Date(8, Jun, 2025), 66122.176655245552),
                (new Date(9, Jun, 2025), 67332.83464073202),
                (new Date(10, Jun, 2025), 68543.929550799468),
                (new Date(11, Jun, 2025), 69755.170154954802),
                (new Date(12, Jun, 2025), 70966.556470726253),
                (new Date(13, Jun, 2025), 72178.088515641997),
                (new Date(14, Jun, 2025), 73389.766307232479),
                (new Date(15, Jun, 2025), 74601.44409882075),
                (new Date(16, Jun, 2025), 75813.121890411217),
                (new Date(17, Jun, 2025), 75813.121890411217),
                (new Date(18, Jun, 2025), 75813.121890411217),
                (new Date(19, Jun, 2025), 0.0),
            ]),
            // partially fixed 5d lookback
            new(new Date(23, April, 2025), new Date(23, Jun, 2025), 5, false, 0, null, null, null,
            [
                (new Date(23, April, 2025), 0.0),
                (new Date(24, April, 2025), 1211.111111110160),
                (new Date(25, April, 2025), 2408.478330246930),
                (new Date(26, April, 2025), 3608.767347644810),
                (new Date(27, April, 2025), 4809.056365047140),
                (new Date(28, April, 2025), 6009.345382445020),
                (new Date(28, May, 2025), 42072.0404553032),
                (new Date(29, May, 2025), 43263.1417778784),
                (new Date(30, May, 2025), 44451.5945829904),
                (new Date(31, May, 2025), 45640.1880216828),
                (new Date(8, Jun, 2025), 55246.0306584401),
                (new Date(9, Jun, 2025), 56455.3805639378),
                (new Date(10, Jun, 2025), 57665.0214144325),
                (new Date(11, Jun, 2025), 58874.8077665890),
                (new Date(12, Jun, 2025), 60084.7396379112),
                (new Date(13, Jun, 2025), 61294.8170459027),
                (new Date(14, Jun, 2025), 62505.1855855885),
                (new Date(15, Jun, 2025), 63715.5541252765),
                (new Date(16, Jun, 2025), 64925.9226649645),
                (new Date(17, Jun, 2025), 66136.5823947113),
                (new Date(18, Jun, 2025), 67347.3877486774),
                (new Date(19, Jun, 2025), 68558.3387443800),
                (new Date(20, Jun, 2025), 69769.2897400826),
                (new Date(21, Jun, 2025), 70980.5320542944),
                (new Date(22, Jun, 2025), 72191.7743685063),
                (new Date(23, Jun, 2025), 73403.0166827182),
                (new Date(24, Jun, 2025), 73403.0166827182),
                (new Date(25, Jun, 2025), 73403.0166827182),
                (new Date(26, Jun, 2025), 0.0),
            ]),
            // partially fixed 5d lookback with obsshift
            new(new Date(23, April, 2025), new Date(23, Jun, 2025), 5, true, 0, null, new Date(10, Jun, 2025), new Date(21, Jun, 2025),
            [
                (new Date(23, April, 2025), 0.0),
                (new Date(24, April, 2025), 1211.111111110160),
                (new Date(25, April, 2025), 2408.478330246930),
                (new Date(26, April, 2025), 3604.817199922560),
                (new Date(27, April, 2025), 4806.422933230080),
                (new Date(28, April, 2025), 6008.0286665376),
                (new Date(29, April, 2025), 7208.999619405480),
                (new Date(28, May, 2025), 42077.6147132629),
                (new Date(29, May, 2025), 43268.7166970069),
                (new Date(30, May, 2025), 44457.1701618179),
                (new Date(31, May, 2025), 45645.7642602892),
                (new Date(8, Jun, 2025), 55248.4485876401),
                (new Date(9, Jun, 2025), 56449.5018178061),
                (new Date(10, Jun, 2025), 57659.1082384094),
                (new Date(11, Jun, 2025), 58868.8643952469),
                (new Date(12, Jun, 2025), 60078.7700574061),
                (new Date(13, Jun, 2025), 61288.8250130815),
                (new Date(14, Jun, 2025), 62515.4436336153),
                (new Date(15, Jun, 2025), 63717.6637034925),
                (new Date(16, Jun, 2025), 64919.8837733697),
                (new Date(17, Jun, 2025), 66130.5341082391),
                (new Date(18, Jun, 2025), 67341.3329076791),
                (new Date(19, Jun, 2025), 68552.2800428873),
                (new Date(20, Jun, 2025), 69754.9516225870),
                (new Date(21, Jun, 2025), 70966.1922121385),
                (new Date(22, Jun, 2025), 72169.0090292934),
                (new Date(23, Jun, 2025), 73371.8258464483),
                (new Date(24, Jun, 2025), 73371.8258464483),
                (new Date(25, Jun, 2025), 73371.8258464483),
                (new Date(26, Jun, 2025), 0.0),
            ]),
            // partially fixed 5d lookback with 4d lockout
            new(new Date(23, April, 2025), new Date(23, Jun, 2025), 5, false, 5, null, null, null,
            [
                (new Date(23, April, 2025), 0.0),
                (new Date(24, April, 2025), 1211.111111110160),
                (new Date(25, April, 2025), 2408.478330246930),
                (new Date(26, April, 2025), 3608.767347644810),
                (new Date(27, April, 2025), 4809.056365047140),
                (new Date(28, April, 2025), 6009.345382445020),
                (new Date(28, May, 2025), 42072.0404553032),
                (new Date(29, May, 2025), 43263.1417778784),
                (new Date(30, May, 2025), 44451.5945829904),
                (new Date(31, May, 2025), 45640.1880216828),
                (new Date(8, Jun, 2025), 55246.0306584401),
                (new Date(9, Jun, 2025), 56455.3805639378),
                (new Date(10, Jun, 2025), 57665.0214144325),
                (new Date(11, Jun, 2025), 58874.8077665890),
                (new Date(12, Jun, 2025), 60084.7396379112),
                (new Date(13, Jun, 2025), 61294.8170459027),
                (new Date(14, Jun, 2025), 62505.0400080696),
                (new Date(15, Jun, 2025), 63715.2629702364),
                (new Date(16, Jun, 2025), 64925.4859324033),
                (new Date(17, Jun, 2025), 66136.1456096188),
                (new Date(18, Jun, 2025), 67346.9509110469),
                (new Date(19, Jun, 2025), 68557.9018542048),
                (new Date(20, Jun, 2025), 69768.8527973628),
                (new Date(21, Jun, 2025), 70980.0950590167),
                (new Date(22, Jun, 2025), 72191.3373206706),
                (new Date(23, Jun, 2025), 73402.5795823268),
                (new Date(24, Jun, 2025), 73402.5795823268),
                (new Date(25, Jun, 2025), 73402.5795823268),
                (new Date(26, Jun, 2025), 0.0),
            ]),
            // partially fixed 5d lookback with obsshift and 4d lockout
            new(new Date(23, April, 2025), new Date(23, Jun, 2025), 5, true, 4, null, new Date(10, Jun, 2025), new Date(26, Jun, 2025),
            [
                (new Date(23, April, 2025), 0.0),
                (new Date(24, April, 2025), 1211.111111110160),
                (new Date(25, April, 2025), 2408.478330246930),
                (new Date(26, April, 2025), 3604.817199922560),
                (new Date(27, April, 2025), 4806.422933230080),
                (new Date(28, April, 2025), 6008.0286665376),
                (new Date(29, April, 2025), 7208.999619405480),
                (new Date(28, May, 2025), 42077.6147132629),
                (new Date(29, May, 2025), 43268.7166970069),
                (new Date(30, May, 2025), 44457.1701618179),
                (new Date(31, May, 2025), 45645.7642602892),
                (new Date(8, Jun, 2025), 55248.4485876401),
                (new Date(9, Jun, 2025), 56449.5018178061),
                (new Date(10, Jun, 2025), 57659.1082384094),
                (new Date(11, Jun, 2025), 58868.8643952469),
                (new Date(12, Jun, 2025), 60078.7700574061),
                (new Date(13, Jun, 2025), 61288.8250130815),
                (new Date(14, Jun, 2025), 62515.4436336153),
                (new Date(15, Jun, 2025), 63717.6637034925),
                (new Date(16, Jun, 2025), 64919.8837733697),
                (new Date(17, Jun, 2025), 66130.6771547586),
                (new Date(18, Jun, 2025), 67341.6191264041),
                (new Date(19, Jun, 2025), 68552.7095548175),
                (new Date(20, Jun, 2025), 69755.3886698143),
                (new Date(21, Jun, 2025), 70966.7750118714),
                (new Date(22, Jun, 2025), 72169.6017069879),
                (new Date(23, Jun, 2025), 73372.4284021044),
                (new Date(24, Jun, 2025), 73372.4284021044),
                (new Date(25, Jun, 2025), 73372.4284021044),
                (new Date(26, Jun, 2025), 0.0),
            ]),
            // floating starts on holiday
            new(new Date(26, Mar, 2027), new Date(28, Jun, 2027), 0, false, 0, null, null, null,
            [
                (new Date(26, Mar, 2027), 0.0),
                (new Date(27, Mar, 2027), 1203.0671590612662), // 1d of interest
                (new Date(28, Mar, 2027), 2 * 1203.0671590612662), // 2d of interest
                (new Date(29, Mar, 2027), 3 * 1203.0671590612662), // 3d of interest
                (new Date(30, Mar, 2027), 4812.4857244213899),
                (new Date(31, Mar, 2027), 6015.914708716874),
                (new Date(25, Jun, 2027), 110054.18406154988),
                (new Date(26, Jun, 2027), 111270.41832775576),
                (new Date(27, Jun, 2027), 112486.65259396384),
                (new Date(28, Jun, 2027), 113702.88686016972),
                (new Date(29, Jun, 2027), 113702.88686016972),
                (new Date(30, Jun, 2027), 113702.88686016972),
                (new Date(1, Jul, 2027), 0.0),
            ]),
            // floating ends on holiday
            new(new Date(26, Feb, 2027), new Date(26, Mar, 2027), 0, false, 1, null, new Date(25, Mar, 2027), new Date(26, Mar, 2027),
            [
                (new Date(25, Mar, 2027), 32527.788409235647),
                (new Date(26, Mar, 2027), 33734.551129034335), // only get 1d of interest
                (new Date(31, Mar, 2027), 0.0),
            ]),
        ];

        // Overwrite the fixings provided within CommonVars to cover interest accrual for coupons which
        // start on, end on, or otherwise intersect good friday, the only holiday that is in the SOFR
        // fixing calendar but not the federal reserve calendar used to generate SOFR swap coupons
        EvaluationContext context = vars.Context with
        {
            Fixings = vars.Context.Fixings.ClearFixings(vars.Sofr).AddFixings(vars.Sofr, pastDates, pastRates),
        };
        var fedCal = new UnitedStates(UnitedStatesMarket.FederalReserve);

        OvernightIndexedCoupon MakeCoupon(TestCase tc, bool telescopicValueDates)
        {
            DayCounter dc = tc.DayCounter ?? vars.Sofr.DayCounter;
            return vars.MakeCoupon(
                tc.StartDate,
                tc.EndDate,
                tc.LookbackDays,
                tc.LockoutDays,
                tc.ObsShift,
                telescopicValueDates,
                RateAveragingType.Compound,
                fedCal.Advance(tc.EndDate, 2, TimeUnit.Days),
                10000000.0,
                dc);
        }

        // QuantLib's checkAccruals: a null cut is Date(), which every date is at or after.
        void CheckAccruals(
            OvernightIndexedCoupon cpn,
            (Date AccrualDate, double AccrualAmount)[] accruals,
            Date? telescopicLowerCut,
            Date? telescopicUpperCut)
        {
            foreach ((Date accrualDate, double accrualAmnt) in accruals)
            {
                if ((telescopicLowerCut is Date lower && accrualDate < lower)
                    || telescopicUpperCut is not Date upper
                    || accrualDate >= upper)
                {
                    CheckOisCouponResult(
                        "accrual amount", cpn.AccruedAmount(accrualDate, context), accrualAmnt, 1e-8);
                }
            }
        }

        foreach (TestCase tc in cases)
        {
            OvernightIndexedCoupon cpn = MakeCoupon(tc, false);
            CheckAccruals(cpn, tc.Accruals, null, null);
            if (cpn.CanApplyTelescopicFormula)
            {
                OvernightIndexedCoupon cpnTelescopic = MakeCoupon(tc, true);
                CheckAccruals(cpnTelescopic, tc.Accruals, tc.TelescopicLowerCut, tc.TelescopicUpperCut);
            }
        }
    }

    // Adapted: the forecast curve linked after the coupon is built (lines 1919, 1921 and 1923) becomes Sofr built on
    // the curve and the coupon built again on it, and enableExtrapolation (line 1914), which changes QuantLib's curve,
    // returns a modified copy here (playbook §2).
    [Fact]
    public void TestErrorWhenCurveNullOrTooNarrow()
    {
        // Test that the expected error is raised when the curve cannot value the coupon...

        var vars = new CommonVars(new Date(26, March, 2026));
        OvernightIndexedCoupon MakeCoupon() => vars.MakeCoupon(
            new Date(31, March, 2026),
            new Date(31, March, 2027),
            0,
            0,
            false,
            true,
            RateAveragingType.Compound,
            new Date(2, April, 2027),
            1.0);
        OvernightIndexedCoupon coupon = MakeCoupon();
        CheckException(() => coupon.Rate(vars.Context), "null term structure set to this instance");

        DiscountCurve GetCurve(Date endDate, bool extrapolate = false)
        {
            var curve = new DiscountCurve([vars.Today, endDate], [1.0, 0.9], new Actual360());
            if (extrapolate)
            {
                curve = curve.EnableExtrapolation();
            }

            return curve;
        }

        vars.LinkForecastCurve(GetCurve(coupon.AccrualEndDate - 1));
        coupon = MakeCoupon();
        CheckException(() => coupon.Rate(vars.Context), "coupon requires a range");
        vars.LinkForecastCurve(GetCurve(coupon.AccrualEndDate));
        coupon = MakeCoupon();
        _ = coupon.Rate(vars.Context);
        vars.LinkForecastCurve(GetCurve(coupon.AccrualEndDate - 1, true));
        coupon = MakeCoupon();
        _ = coupon.Rate(vars.Context);
    }

    // Adapted: the forecast curve linked after Sofr is built (line 1930) becomes Sofr built on the curve, and the check
    // of line 1936, an accrued amount on 27 March 2027, inside the dates QuantLib's telescopic coupon skips, moves to
    // Divergences/TelescopicValueDatesTests.cs: with every daily value date the coupon gives QuantLib's full-schedule
    // amount there (S3R11).
    [Fact]
    public void TestExCouponAccruedAmountAroundAccrualEndDate()
    {
        // Test ex-coupon accrued amount around the accrual end date...
        var vars = new CommonVars(new Date(26, March, 2026));
        vars.LinkForecastCurve(vars.FlatRate(0.04, new Actual360()));
        var exCpnDate = new Date(27, March, 2027);
        OvernightIndexedCoupon coupon = vars.MakeCoupon(
            new Date(31, March, 2026),
            new Date(31, March, 2027),
            0,
            0,
            false,
            true,
            RateAveragingType.Compound,
            new Date(2, April, 2027),
            100.0,
            null,
            null,
            null,
            exCpnDate);
        Assert.True(coupon.TradingExCoupon(vars.Context, exCpnDate));
        CheckOisCouponResult(
            "exCoupon accrued amount", coupon.AccruedAmount(new Date(30, March, 2027), vars.Context), -0.01134, 1e-5);
        CheckOisCouponResult(
            "exCoupon accrued amount", coupon.AccruedAmount(new Date(31, March, 2027), vars.Context), 0.0, 1e-12);
        CheckOisCouponResult(
            "exCoupon accrued amount", coupon.AccruedAmount(new Date(1, April, 2027), vars.Context), 0.0, 1e-12);
    }

    // Adapted: the forecast curve linked after Sofr is built (line 1945) becomes Sofr built on the curve.
    [Fact]
    public void TestAccruedAmountExCouponDateAfterAccrualEndDate()
    {
        // Test ex-coupon accrued amount when ex-coupon date is after the accrual end date...
        var vars = new CommonVars(new Date(26, March, 2026));
        vars.LinkForecastCurve(vars.FlatRate(0.04, new Actual360()));

        // set exCouponDate after accrualEndDate and before paymentDate
        // to ensure averageRate handles dates after the last interest date
        var exCpnDate = new Date(1, April, 2027);
        OvernightIndexedCoupon coupon = vars.MakeCoupon(
            new Date(31, March, 2026),
            new Date(31, March, 2027),
            0,
            0,
            false,
            true,
            RateAveragingType.Compound,
            new Date(2, April, 2027),
            100.0,
            null,
            null,
            null,
            exCpnDate);
        Assert.True(coupon.TradingExCoupon(vars.Context, exCpnDate));
        CheckOisCouponResult("exCoupon accrued amount", coupon.AccruedAmount(exCpnDate, vars.Context), 0.0, 1e-12);
    }

    // Adapted: the forecast curve linked after Sofr is built (line 1959) becomes Sofr built on the curve.
    [Fact]
    public void TestAccruedAmountInAdvance()
    {
        // Test rate and accrued amount for in-advance compounding SOFR swaps...
        var vars = new CommonVars(new Date(21, April, 2026));
        vars.LinkForecastCurve(vars.FlatRate(0.04, new Actual360()));
        OvernightIndexedCoupon couponAdvance = vars.MakeCoupon(
            new Date(23, April, 2027),
            new Date(23, April, 2028),
            0,
            0,
            false,
            true,
            RateAveragingType.Compound,
            new Date(25, April, 2028),
            10000000.0,
            null,
            new Date(23, April, 2026),
            new Date(23, April, 2027));
        OvernightIndexedCoupon couponArrears = vars.MakeCoupon(
            couponAdvance.RateComputationStartDate!.Value,
            couponAdvance.RateComputationEndDate!.Value,
            0,
            0,
            false,
            true,
            RateAveragingType.Compound,
            null,
            10000000.0,
            null,
            null,
            null);
        ImmutableArray<Date> advanceInterestDates = couponAdvance.InterestDates();
        ImmutableArray<Date> arrearsInterestDates = couponArrears.InterestDates();
        Assert.Equal(arrearsInterestDates, advanceInterestDates);
        ImmutableArray<Date> advanceValueDates = couponAdvance.ValueDates();
        ImmutableArray<Date> arrearsValueDates = couponArrears.ValueDates();
        Assert.Equal(arrearsValueDates, advanceValueDates);
        CheckOisCouponResult(
            "in advance swapletRate", couponAdvance.Rate(vars.Context), couponArrears.Rate(vars.Context), 1e-12);
        Real oneDayInterest = couponAdvance.Rate(vars.Context) / 360.0 * couponAdvance.Nominal(vars.Context);
        CheckOisCouponResult(
            "in advance accruedAmount", couponAdvance.AccruedAmount(vars.Today, vars.Context), 0.0, 1e-12);
        for (int i = 0; i < 4; ++i)
        {
            CheckOisCouponResult(
                $"in advance accruedAmount {i} days after accrualStartDate",
                couponAdvance.AccruedAmount(couponAdvance.AccrualStartDate + i, vars.Context),
                i * oneDayInterest,
                1e-12);
        }

        Real totalAmount = couponAdvance.Amount(vars.Context);
        CheckOisCouponResult(
            "in advance accruedAmount 1 day prior to accrualEndDate",
            couponAdvance.AccruedAmount(couponAdvance.AccrualEndDate - 1, vars.Context),
            totalAmount - oneDayInterest,
            1e-12);
        for (int i = 0; i < 3; ++i)
        {
            CheckOisCouponResult(
                $"in advance accruedAmount {i} days after accrualEndDate",
                totalAmount,
                couponAdvance.AccruedAmount(couponAdvance.AccrualEndDate + i, vars.Context),
                1e-12);
        }

        CheckOisCouponResult(
            "in advance accruedAmount day after paymentDate",
            couponAdvance.AccruedAmount(couponAdvance.Date + 1, vars.Context),
            0.0,
            1e-12);
    }

    // Adapted: the forecast curve linked after Sofr is built (line 2001) becomes Sofr built on the curve.
    [Fact]
    public void TestRateComputationStartDateFixingHoliday()
    {
        // Test forward accrued amount when the rate-computation start date lands on a fixing holiday...
        var vars = new CommonVars(new Date(16, April, 2025));
        vars.LinkForecastCurve(vars.FlatRate(0.04, new Actual360()));
        OvernightIndexedCoupon coupon = vars.MakeCoupon(
            new Date(21, April, 2025),
            new Date(21, April, 2026),
            0,
            0,
            false,
            true,
            RateAveragingType.Compound,
            new Date(23, April, 2026),
            10000000.0,
            null,
            new Date(18, April, 2025),
            new Date(20, April, 2026));
        Assert.True(vars.Sofr.FixingCalendar.IsHoliday(coupon.RateComputationStartDate!.Value));
        CheckOisCouponResult("accruedAmount", coupon.AccruedAmount(vars.Today, vars.Context), 0.0, 1e-12);
        CheckOisCouponResult(
            "accruedAmount", coupon.AccruedAmount(coupon.AccrualStartDate, vars.Context), 0.0, 1e-12);
        Real accrued = (((1.0 + (vars.Sofr.Fixing(new Date(17, April, 2025), vars.Context) * 3.0 / 360.0))
                         * (1.0 + (vars.Sofr.Fixing(new Date(21, April, 2025), vars.Context) / 360.0))) - 1.0)
            / 4.0 * coupon.Nominal(vars.Context);
        CheckOisCouponResult(
            "accruedAmount", coupon.AccruedAmount(coupon.AccrualStartDate + 1, vars.Context), accrued, 1e-8);
    }

    // Adapted: the forecast curve linked after Sofr is built (line 2018) becomes Sofr built on the curve.
    [Fact]
    public void TestOvernightLegWeekendStub()
    {
        // Testing overnight leg with weekend stub period...
        var vars = new CommonVars(new Date(16, April, 2025));
        vars.LinkForecastCurve(vars.FlatRate(0.04, new Actual360()));
        var schedule = new Schedule(
        [
            new Date(27, March, 2026), // Friday
            new Date(28, March, 2026), // Saturday
            new Date(30, March, 2026), // Monday
        ]);
        const double Notional = 10000000.0;
        ImmutableArray<CashFlow> leg = new OvernightLeg(schedule, vars.Sofr).WithNotionals(Notional).ToLeg();
        Assert.Equal(2, leg.Length);
        Real oneDayInterest = vars.Sofr.Fixing(new Date(27, March, 2026), vars.Context) / 360.0 * Notional;
        (Date ValueDate, Real AccruedAmount)[] testCases =
        [
            (new Date(27, March, 2026), 0.0),
            (new Date(28, March, 2026), oneDayInterest),
            (new Date(29, March, 2026), oneDayInterest),
            (new Date(30, March, 2026), 2.0 * oneDayInterest),
            (new Date(31, March, 2026), 0.0),
        ];
        foreach ((Date valueDate, Real accruedAmount) in testCases)
        {
            CheckOisCouponResult(
                "leg accruedAmount", CashFlows.AccruedAmount(leg, vars.Context, true, valueDate), accruedAmount, 1e-8);
        }
    }

    // CHECK_OIS_COUPON_RESULT: |calculated - expected| within the tolerance.
    private static void CheckOisCouponResult(string what, double calculated, double expected, double tolerance)
    {
        if (Math.Abs(calculated - expected) > tolerance)
        {
            Assert.Fail(
                $"Failed to reproduce {what}:\n    expected:   {expected:R}\n    calculated: {calculated:R}\n"
                + $"    error:      {Math.Abs(calculated - expected):R}");
        }
    }

    // CHECK_OIS_COUPON_DATES: the dates equal.
    private static void CheckOisCouponDates(string what, Date actual, Date expected)
    {
        if (actual != expected)
        {
            Assert.Fail($"Failed to reproduce {what}:\n    expected:   {expected}\n    actual: {actual}");
        }
    }

    // QL_CHECK_CLOSE, Boost's BOOST_CHECK_CLOSE: |actual - expected| within the tolerance, in percent, of both values.
    private static void CheckClose(double actual, double expected, double tolerancePercent)
    {
        double diff = Math.Abs(actual - expected), fraction = tolerancePercent * 0.01;
        Assert.True(
            SafeDivision(diff, Math.Abs(expected)) <= fraction && SafeDivision(diff, Math.Abs(actual)) <= fraction,
            $"difference between {actual:R} and {expected:R} exceeds {tolerancePercent:R}%");
    }

    // Boost's safe_fpt_division: f1 / f2, without overflow or underflow.
    private static double SafeDivision(double f1, double f2)
    {
        const double NormalMin = 2.2250738585072014e-308;
        if (f2 < 1.0 && f1 > f2 * double.MaxValue)
        {
            return double.MaxValue;
        }

        if (Math.Abs(f1) <= NormalMin || (f2 > 1.0 && f1 < f2 * NormalMin))
        {
            return 0.0;
        }

        return f1 / f2;
    }

    // BOOST_CHECK_THROW(..., Error): QuantLib's Error is an ArgumentException or a NanoQLException in nanoql.
    private static void CheckThrowsError(Action action)
    {
        Exception? e = Record.Exception(action);
        Assert.True(
            e is ArgumentException or NanoQLException,
            $"expected an ArgumentException or a NanoQLException, got {e?.GetType().Name ?? "no exception"}");
    }

    // BOOST_CHECK_EXCEPTION(..., Error, ExpectedErrorMessage(text)): an Error whose message holds the text.
    private static void CheckException(Action action, string text)
    {
        Exception? e = Record.Exception(action);
        Assert.True(
            e is ArgumentException or NanoQLException,
            $"expected an ArgumentException or a NanoQLException, got {e?.GetType().Name ?? "no exception"}");
        Assert.Contains(text, e!.Message, StringComparison.Ordinal);
    }

    private sealed record TestCase(
        Date StartDate,
        Date EndDate,
        Natural LookbackDays,
        bool ObsShift,
        Natural LockoutDays,
        DayCounter? DayCounter,
        Date? TelescopicLowerCut,
        Date? TelescopicUpperCut,
        (Date AccrualDate, double AccrualAmount)[] Accruals);

    // QuantLib's CommonVars. Its relinkable forecast curve becomes Sofr built on the curve (LinkForecastCurve), and its
    // fixings are a store in the fixture's context.
    private sealed class CommonVars
    {
        public CommonVars(Date evaluationDate)
        {
            Today = evaluationDate;

            Sofr = new Sofr();

            Date[] pastDates =
            [
                new Date(21, June, 2019), new Date(24, June, 2019), new Date(25, June, 2019),
                new Date(26, June, 2019), new Date(27, June, 2019), new Date(28, June, 2019),
                new Date(1, July, 2019), new Date(2, July, 2019), new Date(3, July, 2019),
                new Date(5, July, 2019), new Date(8, July, 2019), new Date(9, July, 2019),
                new Date(10, July, 2019), new Date(11, July, 2019), new Date(12, July, 2019),
                new Date(15, July, 2019), new Date(16, July, 2019), new Date(17, July, 2019),
                new Date(18, July, 2019), new Date(19, July, 2019), new Date(22, July, 2019),
                new Date(23, July, 2019), new Date(24, July, 2019), new Date(25, July, 2019),
                new Date(26, July, 2019), new Date(29, July, 2019), new Date(30, July, 2019),
                new Date(31, July, 2019), new Date(1, August, 2019), new Date(2, August, 2019),
                new Date(5, August, 2019),

                new Date(18, October, 2021), new Date(19, October, 2021), new Date(20, October, 2021),
                new Date(21, October, 2021), new Date(22, October, 2021), new Date(25, October, 2021),
                new Date(26, October, 2021), new Date(27, October, 2021), new Date(28, October, 2021),
                new Date(29, October, 2021), new Date(1, November, 2021), new Date(2, November, 2021),
                new Date(3, November, 2021), new Date(4, November, 2021), new Date(5, November, 2021),
                new Date(8, November, 2021), new Date(9, November, 2021), new Date(10, November, 2021),
                new Date(12, November, 2021), new Date(15, November, 2021), new Date(16, November, 2021),
                new Date(17, November, 2021), new Date(18, November, 2021), new Date(19, November, 2021),
                new Date(22, November, 2021),
            ];
            Rate[] pastRates =
            [
                0.0237, 0.0239, 0.0241,
                0.0243, 0.0242, 0.025,
                0.0242, 0.0251, 0.0256,
                0.0259, 0.0248, 0.0245,
                0.0246, 0.0241, 0.0236,
                0.0246, 0.0247, 0.0247,
                0.0246, 0.0241, 0.024,
                0.024, 0.0241, 0.0242,
                0.0241, 0.024, 0.0239,
                0.0255, 0.0219, 0.0219,
                0.0213,

                0.0008, 0.0009, 0.0008,
                0.0010, 0.0012, 0.0011,
                0.0013, 0.0012, 0.0012,
                0.0008, 0.0009, 0.0010,
                0.0011, 0.0014, 0.0013,
                0.0011, 0.0009, 0.0008,
                0.0007, 0.0008, 0.0008,
                0.0007, 0.0009, 0.0010,
                0.0009,
            ];

            Context = new EvaluationContext
            {
                EvaluationDate = Today,
                Fixings = IndexFixings.Empty.AddFixings(Sofr, pastDates, pastRates),
            };
        }

        public CommonVars()
            : this(new Date(23, November, 2021))
        {
        }

        public Date Today { get; }

        public Real Notional { get; } = 10000.0;

        public OvernightIndex Sofr { get; private set; }

        public EvaluationContext Context { get; }

        // forecastCurve.linkTo(curve): Sofr built on the curve; the fixings stay, stored under its name.
        public void LinkForecastCurve(YieldTermStructure curve) => Sofr = new Sofr(curve);

        // flatRate(forward, dc): a flat curve whose reference date is the evaluation date.
        public YieldTermStructure FlatRate(Rate forward, DayCounter dc) =>
            FlatForward.Create(0, new NullCalendar(), forward, dc, Context);

        public OvernightIndexedCoupon MakeCoupon(
            Date startDate,
            Date endDate,
            Natural? fixingDays = null,
            Natural lockoutDays = 0,
            bool applyObservationShift = false,
            bool telescopicValueDates = false,
            RateAveragingType averaging = RateAveragingType.Compound,
            Date? paymentDate = null,
            Real notional = 10000.0,
            DayCounter? daycounter = null,
            Date? rateComputationStartDate = null,
            Date? rateComputationEndDate = null,
            Date? exCouponDate = null) =>
            new(
                paymentDate ?? endDate,
                notional,
                startDate,
                endDate,
                Sofr,
                1.0,
                0.0,
                null,
                null,
                daycounter,
                telescopicValueDates,
                averaging,
                fixingDays,
                lockoutDays,
                applyObservationShift,
                false,
                rateComputationStartDate,
                rateComputationEndDate,
                exCouponDate);

        public OvernightIndexedCoupon MakeSpreadedCoupon(
            Date startDate,
            Date endDate,
            Spread spread = 0.0001,
            bool compoundSpreadDaily = true,
            Natural? fixingDays = null,
            Natural lockoutDays = 0,
            bool applyObservationShift = false,
            bool telescopicValueDates = false,
            RateAveragingType averaging = RateAveragingType.Compound) =>
            new(
                endDate,
                Notional,
                startDate,
                endDate,
                Sofr,
                1.0,
                spread,
                null,
                null,
                null,
                telescopicValueDates,
                averaging,
                fixingDays,
                lockoutDays,
                applyObservationShift,
                compoundSpreadDaily);
    }

    // QuantLib's BlackONPricerVars. Its relinkable volatility becomes the fixture built on the volatility.
    private sealed class BlackONPricerVars
    {
        public BlackONPricerVars(Date? evalDate = null, double volatility = 0.1)
        {
            Today = evalDate ?? new Date(1, July, 2025);
            Dc = new Actual360();
            Context = new EvaluationContext { EvaluationDate = Today };

            // Flat forward curve
            ForecastCurve = new FlatForward(Today, 0.04, Dc);
            Sofr = new Sofr(ForecastCurve);

            // Flat volatility
            Vol = new ConstantOptionletVolatility(Today, new TARGET(), BusinessDayConvention.Following, volatility, Dc);
        }

        public Date Today { get; }

        public Real Notional { get; } = 1000000.0;

        public YieldTermStructure ForecastCurve { get; }

        public OptionletVolatilityStructure Vol { get; }

        public OvernightIndex Sofr { get; }

        public DayCounter Dc { get; }

        public EvaluationContext Context { get; }

        public OvernightIndexedCoupon MakeBaseCoupon(
            Date start, Date end, RateAveragingType avgMethod = RateAveragingType.Compound)
        {
            var onCoupon = new OvernightIndexedCoupon(
                end, Notional, start, end, Sofr, 1.0, 0.0, null, null, Dc, false, avgMethod, null, 0, false, false);

            return avgMethod == RateAveragingType.Compound
                ? onCoupon.SetPricer(new CompoundingOvernightIndexedCouponPricer())
                : onCoupon.SetPricer(new ArithmeticAveragedOvernightIndexedCouponPricer());
        }

        public CappedFlooredOvernightIndexedCoupon MakeCoupon(
            Date start,
            Date end,
            Rate? cap = null,
            Rate? floor = null,
            RateAveragingType avgMethod = RateAveragingType.Compound)
        {
            OvernightIndexedCoupon onCoupon = MakeBaseCoupon(start, end, avgMethod);

            return new CappedFlooredOvernightIndexedCoupon(onCoupon, cap, floor);
        }
    }

    // QuantLib's CommonVarsONLeg. Its relinkable forecast curve becomes Sofr built on the curve, and the zero curve's
    // enableExtrapolation (line 323) returns a modified copy, the curve Sofr is built on.
    private sealed class CommonVarsONLeg
    {
        public CommonVarsONLeg(Date evaluationDate)
        {
            Today = evaluationDate;
            Dc = new Actual360();

            Sofr = new Sofr();

            // Create a quarterly schedule for testing
            Context = new EvaluationContext { EvaluationDate = Today };
            LegSchedule = new Schedule(
                new Date(1, July, 2025),
                new Date(1, July, 2026),
                new Period(3, TimeUnit.Months),
                new UnitedStates(UnitedStatesMarket.GovernmentBond),
                BusinessDayConvention.ModifiedFollowing,
                BusinessDayConvention.ModifiedFollowing,
                DateGenerationRule.Forward,
                false,
                Context);

            Date[] pastDates =
            [
                new Date(2, June, 2025), new Date(3, June, 2025), new Date(4, June, 2025), new Date(5, June, 2025),
                new Date(6, June, 2025), new Date(9, June, 2025), new Date(10, June, 2025), new Date(11, June, 2025),
                new Date(12, June, 2025), new Date(13, June, 2025), new Date(16, June, 2025), new Date(17, June, 2025),
                new Date(18, June, 2025), new Date(20, June, 2025), new Date(23, June, 2025), new Date(24, June, 2025),
                new Date(25, June, 2025), new Date(26, June, 2025), new Date(27, June, 2025), new Date(30, June, 2025),
                new Date(1, July, 2025), new Date(2, July, 2025), new Date(3, July, 2025), new Date(7, July, 2025),
                new Date(8, July, 2025), new Date(9, July, 2025), new Date(10, July, 2025), new Date(11, July, 2025),
                new Date(14, July, 2025), new Date(15, July, 2025), new Date(16, July, 2025), new Date(17, July, 2025),
                new Date(18, July, 2025), new Date(21, July, 2025), new Date(22, July, 2025), new Date(23, July, 2025),
                new Date(24, July, 2025), new Date(25, July, 2025), new Date(28, July, 2025), new Date(29, July, 2025),
                new Date(30, July, 2025), new Date(31, July, 2025), new Date(1, August, 2025),
            ];

            Rate[] pastRates =
            [
                0.0435, 0.0432, 0.0428, 0.0429, 0.0429, 0.0429, 0.0428, 0.0428, 0.0428, 0.0428,
                0.0432, 0.0431, 0.0428, 0.0429, 0.0429, 0.0430, 0.0436, 0.0440, 0.0439, 0.0445,
                0.0444, 0.0440, 0.0435, 0.0433, 0.0434, 0.0432, 0.0431, 0.0431, 0.0433, 0.0437,
                0.0434, 0.0434, 0.0430, 0.0428, 0.0428, 0.0428, 0.0430, 0.0436, 0.0436, 0.0436,
                0.0432, 0.0439, 0.0434,
            ];

            Context = Context with { Fixings = IndexFixings.Empty.AddFixings(Sofr, pastDates, pastRates) };
        }

        public CommonVarsONLeg()
            : this(new Date(1, June, 2025))
        {
        }

        public Date Today { get; }

        public Real Notional { get; } = 1000000.0;

        public OvernightIndex Sofr { get; private set; }

        public Schedule LegSchedule { get; }

        public DayCounter Dc { get; }

        public EvaluationContext Context { get; }

        // forecastCurve.linkTo(curve): Sofr built on the curve; the fixings stay, stored under its name.
        public void LinkForecastCurve(YieldTermStructure curve) => Sofr = new Sofr(curve);

        // flatRate(forward, dc): a flat curve whose reference date is the evaluation date.
        public YieldTermStructure FlatRate(Rate forward, DayCounter dc) =>
            FlatForward.Create(0, new NullCalendar(), forward, dc, Context);

        public void SetupForecastCurve()
        {
            Date[] curveDates =
            [
                Today,
                new Date(30, July, 2025),
                new Date(29, August, 2025),
                new Date(30, September, 2025),
                new Date(30, December, 2025),
                new Date(30, March, 2026),
                new Date(30, June, 2026),
            ];

            Rate[] zeroRates =
            [
                0.0434,
                0.0436,
                0.0431,
                0.0413,
                0.0390,
                0.0370,
                0.0348,
            ];

            InterpolatedZeroCurve<Cubic> zeroCurve = new InterpolatedZeroCurve<Cubic>(
                curveDates, zeroRates, Dc, new UnitedStates(UnitedStatesMarket.SOFR)).EnableExtrapolation();

            LinkForecastCurve(zeroCurve);
        }

        public OptionletVolatilityStructure ReturnRateVolTS() =>
            new ConstantOptionletVolatility(Today, new TARGET(), BusinessDayConvention.Following, 0.05, Dc);

        public ImmutableArray<CashFlow> MakeLeg(
            Natural? fixingDays = null,
            Natural lockoutDays = 0,
            bool applyObservationShift = false,
            bool telescopicValueDates = false,
            RateAveragingType averaging = RateAveragingType.Compound,
            Real[]? gearings = null,
            Spread[]? spreads = null,
            Rate?[]? caps = null,
            Rate?[]? floors = null)
        {
            OvernightLeg leg = new OvernightLeg(LegSchedule, Sofr)
                .WithNotionals(Notional)
                .WithPaymentDayCounter(Dc)
                .WithAveragingMethod(averaging)
                .WithLockoutDays(lockoutDays)
                .WithObservationShift(applyObservationShift)
                .WithTelescopicValueDates(telescopicValueDates);

            if (fixingDays is Natural lookback)
            {
                leg = leg.WithLookbackDays(lookback);
            }

            if (gearings is { Length: > 0 })
            {
                leg = leg.WithGearings(gearings);
            }

            if (spreads is { Length: > 0 })
            {
                leg = leg.WithSpreads(spreads);
            }

            if (caps is { Length: > 0 })
            {
                leg = leg.WithCaps(caps);
            }

            if (floors is { Length: > 0 })
            {
                leg = leg.WithFloors(floors);
            }

            if (caps is { Length: > 0 } || floors is { Length: > 0 })
            {
                OptionletVolatilityStructure rateVolTS = ReturnRateVolTS();
                leg = averaging == RateAveragingType.Compound
                    ? leg.WithCouponPricer(new BlackCompoundingOvernightIndexedCouponPricer(rateVolTS))
                    : leg.WithCouponPricer(new BlackAveragingOvernightIndexedCouponPricer(rateVolTS));
            }

            return leg.ToLeg();
        }
    }
}
