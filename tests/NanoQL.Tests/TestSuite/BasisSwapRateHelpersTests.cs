// SPDX-FileCopyrightText: Copyright (C) 2021 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/basisswapratehelpers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Moved: testOvernightIborAveragingAndTelescopicValueDates line 767 (Divergences/TelescopicValueDatesTests.cs); testOvernightOvernightTelescopicValueDatesWithSimpleAveraging line 920 (Divergences/TelescopicValueDatesTests.cs)

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

// QuantLib's quotes are the helpers' values, and its relinkable handles the curves passed: a case that links one after
// building the curve builds the curve with it. The curves bootstrap in their constructors, where QuantLib's bootstrap
// when a curve is first read. A helper's impliedQuote() after the bootstrap is read from the copy the bootstrap bound
// to the curve, which a test-local bootstrap keeps (CapturingBootstrap). The global at-par switch is the context's.
[Trait("Oracle", "QuantLib")]
public class BasisSwapRateHelpersTests
{
    // QuantLib's test runner's evaluation date (quantlibglobalfixture.cpp:62-63)
    private static readonly Date RunnerDate = new(16, Month.September, 2015);

    [Fact]
    public void TestIborIborBaseCurveBootstrap()
    {
        // Testing IBOR-IBOR basis-swap rate helpers (base curve bootstrap)...
        TestIborIborBootstrap(true);
    }

    [Fact]
    public void TestIborIborOtherCurveBootstrap()
    {
        // Testing IBOR-IBOR basis-swap rate helpers (other curve bootstrap)...
        TestIborIborBootstrap(false);
    }

    [Fact]
    public void TestIborIborBootstrapWithPaymentLag()
    {
        // Testing IBOR-IBOR basis-swap rate helpers with payment lag...
        TestIborIborBootstrap(true, 2);
        TestIborIborBootstrap(false, 2);
    }

    [Fact]
    public void TestBasisSwapRateHelpersRejectNullIndexes()
    {
        // Testing basis-swap rate helpers with null indexes...

        EvaluationContext context = Context();
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        YieldTermStructure curve = FlatRate(0.01, new Actual365Fixed(), context);
        double basis = 0.0010;
        var overnightIndex = new Sofr(curve);
        var iborIndex = new USDLibor(new Period(3, TimeUnit.Months), curve);

        IborIborBasisSwapRateHelper MakeIborIborHelper(IborIndex baseIndex, IborIndex otherIndex) =>
            new(basis, new Period(2, TimeUnit.Years), 2, calendar, BusinessDayConvention.Following, false, baseIndex,
                otherIndex, curve, false, context);
        OvernightIborBasisSwapRateHelper MakeOvernightIborHelper(OvernightIndex baseIndex, IborIndex otherIndex) =>
            new(basis, new Period(2, TimeUnit.Years), 2, calendar, BusinessDayConvention.Following, false, baseIndex,
                otherIndex, context, curve, false);

        Assert.Throws<ArgumentNullException>(() => MakeIborIborHelper(null!, iborIndex));
        Assert.Throws<ArgumentNullException>(() => MakeIborIborHelper(iborIndex, null!));
        Assert.Throws<ArgumentNullException>(() => MakeOvernightIborHelper(null!, iborIndex));
        Assert.Throws<ArgumentNullException>(() => MakeOvernightIborHelper(overnightIndex, null!));
    }

    // Adapted: each helper is built under a context whose at-par switch is the opposite of its override, where QuantLib
    // sets the global switch before building it and restores it afterwards.
    [Fact]
    public void TestIborIborIndexedCouponOverride()
    {
        // Testing the indexed-coupon override for IBOR-IBOR basis-swap rate helpers...

        // spot is the last UK business day of the month, so the end-of-month rule
        // of the index makes the two dates below differ; other dates may not
        var context = new EvaluationContext { EvaluationDate = new Date(27, Month.January, 2010) };
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        YieldTermStructure curve = FlatRate(0.01, new Actual365Fixed(), context);
        var baseIndex = new USDLibor(new Period(3, TimeUnit.Months), curve);
        var otherIndex = new USDLibor(new Period(6, TimeUnit.Months), curve);
        double basis = 0.0010;

        // each helper is built against the opposite global setting, so the checks
        // below can only pass if the override wins
        var indexedHelper = new IborIborBasisSwapRateHelper(
            basis, new Period(2, TimeUnit.Years), 2, calendar, BusinessDayConvention.Following, false, baseIndex,
            otherIndex, curve, false, context with { UseAtParCoupons = true }, true);

        var atParHelper = new IborIborBasisSwapRateHelper(
            basis, new Period(2, TimeUnit.Years), 2, calendar, BusinessDayConvention.Following, false, baseIndex,
            otherIndex, curve, false, context with { UseAtParCoupons = false }, false);

        for (int i = 0; i < 2; ++i)
        {
            var indexedCoupon = Assert.IsAssignableFrom<IborCoupon>(indexedHelper.Swap.Leg(i)[0]);
            var atParCoupon = Assert.IsAssignableFrom<IborCoupon>(atParHelper.Swap.Leg(i)[0]);
            Assert.Equal(indexedCoupon.FixingEndDate(), indexedCoupon.FixingMaturityDate());
            Assert.NotEqual(atParCoupon.FixingEndDate(), atParCoupon.FixingMaturityDate());
        }
    }

    [Fact]
    public void TestOvernightIborDifferentPaymentFrequencies()
    {
        // Testing overnight-IBOR basis-swap rate helpers with different payment frequencies...

        var context = new EvaluationContext { EvaluationDate = new Date(27, Month.January, 2010) };
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        YieldTermStructure curve = FlatRate(0.01, new Actual365Fixed(), context);
        var overnightIndex = new Sofr(curve);
        var iborIndex = new USDLibor(new Period(3, TimeUnit.Months), curve);

        // the overnight leg pays annually, the ibor leg at the tenor of its index
        var helper = new OvernightIborBasisSwapRateHelper(
            0.0010, new Period(2, TimeUnit.Years), 2, calendar, BusinessDayConvention.Following, false, overnightIndex,
            iborIndex, context, curve, false, 0, Frequency.Annual);
        var zeroCouponHelper = new OvernightIborBasisSwapRateHelper(
            0.0010, new Period(2, TimeUnit.Years), 2, calendar, BusinessDayConvention.Following, false, overnightIndex,
            iborIndex, context, curve, false, 0, Frequency.NoFrequency);

        Assert.Equal(2, helper.Swap.Leg(0).Length);
        Assert.Equal(8, helper.Swap.Leg(1).Length);
        Assert.Single(zeroCouponHelper.Swap.Leg(0));
        Assert.Equal(8, zeroCouponHelper.Swap.Leg(1).Length);
    }

    // Adapted: as TestIborIborIndexedCouponOverride.
    [Fact]
    public void TestOvernightIborIndexedCouponOverride()
    {
        // Testing the indexed-coupon override for overnight-IBOR basis-swap rate helpers...

        // see testIborIborIndexedCouponOverride for the choice of date
        var context = new EvaluationContext { EvaluationDate = new Date(27, Month.January, 2010) };
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        YieldTermStructure curve = FlatRate(0.01, new Actual365Fixed(), context);
        var overnightIndex = new Sofr(curve);
        var iborIndex = new USDLibor(new Period(3, TimeUnit.Months), curve);
        double basis = 0.0010;

        var indexedHelper = new OvernightIborBasisSwapRateHelper(
            basis, new Period(2, TimeUnit.Years), 2, calendar, BusinessDayConvention.Following, false, overnightIndex,
            iborIndex, context with { UseAtParCoupons = true }, curve, false, 0, Frequency.Annual, true);

        var atParHelper = new OvernightIborBasisSwapRateHelper(
            basis, new Period(2, TimeUnit.Years), 2, calendar, BusinessDayConvention.Following, false, overnightIndex,
            iborIndex, context with { UseAtParCoupons = false }, curve, false, 0, Frequency.Annual, false);

        var indexedCoupon = Assert.IsAssignableFrom<IborCoupon>(indexedHelper.Swap.Leg(1)[0]);
        var atParCoupon = Assert.IsAssignableFrom<IborCoupon>(atParHelper.Swap.Leg(1)[0]);
        Assert.Equal(indexedCoupon.FixingEndDate(), indexedCoupon.FixingMaturityDate());
        Assert.NotEqual(atParCoupon.FixingEndDate(), atParCoupon.FixingMaturityDate());
    }

    [Fact]
    public void TestOvernightIborDateGenerationRule()
    {
        // Testing the date-generation rule of overnight-IBOR basis-swap rate helpers...

        var context = new EvaluationContext { EvaluationDate = new Date(27, Month.January, 2010) };
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        YieldTermStructure curve = FlatRate(0.01, new Actual365Fixed(), context);
        var overnightIndex = new Sofr(curve);

        // an annual index, so that both legs have the same stub
        var iborIndex = new USDLibor(new Period(1, TimeUnit.Years), curve);
        double basis = 0.0010;

        Date spot = calendar.Advance(context.EvaluationDate, 2, TimeUnit.Days);

        // 18M against an annual frequency leaves a six-month stub
        var backwardHelper = new OvernightIborBasisSwapRateHelper(
            basis, new Period(18, TimeUnit.Months), 2, calendar, BusinessDayConvention.Following, false, overnightIndex,
            iborIndex, context, curve, false, 0, Frequency.Annual, null, DateGenerationRule.Backward);

        var forwardHelper = new OvernightIborBasisSwapRateHelper(
            basis, new Period(18, TimeUnit.Months), 2, calendar, BusinessDayConvention.Following, false, overnightIndex,
            iborIndex, context, curve, false, 0, Frequency.Annual, null, DateGenerationRule.Forward);

        static Date FirstAccrualEnd(OvernightIborBasisSwapRateHelper h, int leg) =>
            ((Coupon)h.Swap.Leg(leg)[0]).AccrualEndDate;

        for (int i = 0; i < 2; ++i)
        {
            Assert.Equal(2, backwardHelper.Swap.Leg(i).Length);
            Assert.Equal(2, forwardHelper.Swap.Leg(i).Length);
            Assert.Equal(
                calendar.Adjust(spot + new Period(6, TimeUnit.Months), BusinessDayConvention.Following),
                FirstAccrualEnd(backwardHelper, i));
            Assert.Equal(
                calendar.Adjust(spot + new Period(1, TimeUnit.Years), BusinessDayConvention.Following),
                FirstAccrualEnd(forwardHelper, i));
        }

        Date expectedMaturity = calendar.Adjust(spot + new Period(18, TimeUnit.Months), BusinessDayConvention.Following);
        Assert.Equal(expectedMaturity, backwardHelper.MaturityDate);
        Assert.Equal(expectedMaturity, forwardHelper.MaturityDate);
    }

    [Fact]
    public void TestOvernightIborBootstrapWithoutDiscountCurve()
    {
        // Testing overnight-IBOR basis-swap rate helpers...
        TestOvernightIborBootstrap(false);
    }

    [Fact]
    public void TestOvernightIborBootstrapWithDiscountCurve()
    {
        // Testing overnight-IBOR basis-swap rate helpers with external discount curve...
        TestOvernightIborBootstrap(true);
    }

    // Adapted: QuantLib links the discount handle after building the curve, and its helpers discount on it when the
    // curve bootstraps again; here the curve is built with it.
    [Fact]
    public void TestOvernightIborBootstrapWithLateLinkedDiscountCurve()
    {
        // Testing overnight-IBOR basis-swap rate helpers with a late-linked discount curve...
        TestOvernightIborBootstrap(true, false, 0, true);
    }

    [Fact]
    public void TestOvernightIborBaseCurveBootstrapWithoutDiscountCurve()
    {
        // Testing overnight-IBOR basis-swap rate helpers (base curve bootstrap)...
        TestOvernightIborBootstrap(false, true);
    }

    [Fact]
    public void TestOvernightIborBaseCurveBootstrapWithDiscountCurve()
    {
        // Testing overnight-IBOR basis-swap rate helpers (base curve bootstrap with external discount curve)...
        TestOvernightIborBootstrap(true, true);
    }

    [Fact]
    public void TestOvernightIborBootstrapWithPaymentLag()
    {
        // Testing overnight-IBOR basis-swap rate helpers with payment lag...
        TestOvernightIborBootstrap(false, false, 2);
        TestOvernightIborBootstrap(true, false, 2);
        TestOvernightIborBootstrap(false, true, 2);
        TestOvernightIborBootstrap(true, true, 2);
    }

    [Fact]
    public void TestOvernightIborStubIndexBootstrap()
    {
        // Testing overnight-IBOR bootstrap with an interpolated stub on the ibor leg...

        EvaluationContext context = Context();
        Calendar calendar = new NewZealand();

        YieldTermStructure shortForecastCurve = FlatRate(0.02, new Actual365Fixed(), context);
        YieldTermStructure iborForecastCurve = FlatRate(0.04, new Actual365Fixed(), context);

        var overnight = new OvernightIndex("Nzionia", 0, new NZDCurrency(), calendar, new Actual365Fixed());
        var bkbm1m = new Bkbm1M(shortForecastCurve);
        var bkbm3m = new Bkbm3M(iborForecastCurve);

        var stubIndexSelection = new StubIndexSelection(StubIndexSelectionConvention.Interpolated, [bkbm1m, bkbm3m]);

        OvernightIborBasisSwapRateHelper MakeHelper(double basis, Period tenor, StubIndexSelection selection) =>
            new(basis, tenor, 0, calendar, BusinessDayConvention.ModifiedFollowing, false, overnight, bkbm3m, context,
                null,
                true, // bootstrap the overnight curve; the ibor leg is exogenous
                0, null, true, DateGenerationRule.Backward, RateAveragingType.Compound, false, false, selection);

        OvernightIborBasisSwapRateHelper[] helpers =
        [
            MakeHelper(10e-4, new Period(6, TimeUnit.Months), stubIndexSelection),
            MakeHelper(12e-4, new Period(8, TimeUnit.Months), stubIndexSelection),
            MakeHelper(15e-4, new Period(1, TimeUnit.Years), stubIndexSelection),
        ];

        // the 8M helper's backward quarterly ibor schedule has a broken front period
        OvernightIborBasisSwapRateHelper stubHelper = helpers[1];
        var stubCoupon = Assert.IsType<StubIborCoupon>(stubHelper.Swap.Leg(1)[0]);

        Date fixingDate = stubCoupon.FixingDate();
        Date valueDate = stubCoupon.AccrualStartDate;
        Date target = stubCoupon.AccrualEndDate;
        Date shortMaturity = bkbm1m.MaturityDate(valueDate);
        Date longMaturity = bkbm3m.MaturityDate(valueDate);
        Assert.True(shortMaturity < target && target < longMaturity);
        double weight = (double)(target - shortMaturity) / (longMaturity - shortMaturity);
        double expected = bkbm1m.Fixing(fixingDate, context)
            + ((bkbm3m.Fixing(fixingDate, context) - bkbm1m.Fixing(fixingDate, context)) * weight);
        Assert.True(Math.Abs(stubCoupon.IndexFixing(context) - expected) <= 1e-14);

        var bound = new List<IBootstrapHelper>();
        _ = PiecewiseYieldCurve<Discount, LogLinear, CapturingBootstrap>.Create(
            0, calendar, helpers, new Actual365Fixed(), context, bootstrap: new CapturingBootstrap(bound));
        Assert.Equal(helpers.Length, bound.Count);
        foreach (IBootstrapHelper h in bound)
        {
            Assert.True(Math.Abs(h.ImpliedQuote(context) - h.Quote) <= 1e-10);
        }

        // the selection is rejected when the ibor forecast curve is the one being bootstrapped
        var overnightWithCurve = new OvernightIndex(
            "Nzionia", 0, new NZDCurrency(), calendar, new Actual365Fixed(), shortForecastCurve);
        Assert.Throws<ArgumentException>(() => new OvernightIborBasisSwapRateHelper(
            10e-4, new Period(8, TimeUnit.Months), 0, calendar, BusinessDayConvention.ModifiedFollowing, false,
            overnightWithCurve, bkbm3m, context, null, false, 0, null, true, DateGenerationRule.Backward,
            RateAveragingType.Compound, false, false, stubIndexSelection));
    }

    [Fact]
    public void TestIborIborStubIndexBootstrap()
    {
        // Testing ibor-ibor bootstrap with an interpolated stub on the exogenous leg...

        EvaluationContext context = Context();
        Calendar calendar = new NewZealand();

        YieldTermStructure shortForecastCurve = FlatRate(0.02, new Actual365Fixed(), context);
        YieldTermStructure otherForecastCurve = FlatRate(0.04, new Actual365Fixed(), context);
        YieldTermStructure discountCurve = FlatRate(0.03, new Actual365Fixed(), context);

        var bkbm1m = new Bkbm1M(shortForecastCurve);
        var bkbm3m = new Bkbm3M(); // curve to be bootstrapped
        var bkbm6m = new Bkbm6M(otherForecastCurve);

        var otherConfig = new StubIndexSelection(StubIndexSelectionConvention.Interpolated, [bkbm1m, bkbm6m]);

        IborIborBasisSwapRateHelper MakeHelper(double basis, Period tenor) =>
            new(basis, tenor, 0, calendar, BusinessDayConvention.ModifiedFollowing, false, bkbm3m, bkbm6m, discountCurve,
                true, // bootstrap the base (3M) curve; the 6M leg is exogenous
                context, true, DateGenerationRule.Backward, 0, new StubIndexSelection(), otherConfig);

        IborIborBasisSwapRateHelper[] helpers =
        [
            MakeHelper(10e-4, new Period(6, TimeUnit.Months)),
            MakeHelper(12e-4, new Period(8, TimeUnit.Months)),
            MakeHelper(15e-4, new Period(1, TimeUnit.Years)),
        ];

        // the 8M helper's backward semiannual schedule has a broken front period
        // on the exogenous 6M leg
        IborIborBasisSwapRateHelper stubHelper = helpers[1];
        var stubCoupon = Assert.IsType<StubIborCoupon>(stubHelper.Swap.Leg(1)[0]);

        Date fixingDate = stubCoupon.FixingDate();
        Date valueDate = stubCoupon.AccrualStartDate;
        Date target = stubCoupon.AccrualEndDate;
        Date shortMaturity = bkbm1m.MaturityDate(valueDate);
        Date longMaturity = bkbm6m.MaturityDate(valueDate);
        Assert.True(shortMaturity < target && target < longMaturity);
        double weight = (double)(target - shortMaturity) / (longMaturity - shortMaturity);
        double expected = bkbm1m.Fixing(fixingDate, context)
            + ((bkbm6m.Fixing(fixingDate, context) - bkbm1m.Fixing(fixingDate, context)) * weight);
        Assert.True(Math.Abs(stubCoupon.IndexFixing(context) - expected) <= 1e-14);

        var bound = new List<IBootstrapHelper>();
        _ = PiecewiseYieldCurve<ZeroYield, Linear, CapturingBootstrap>.Create(
            0, calendar, helpers, new Actual365Fixed(), context, bootstrap: new CapturingBootstrap(bound));
        Assert.Equal(helpers.Length, bound.Count);
        foreach (IBootstrapHelper h in bound)
        {
            Assert.True(Math.Abs(h.ImpliedQuote(context) - h.Quote) <= 1e-10);
        }

        // the selection is rejected on the leg whose curve is being bootstrapped
        Assert.Throws<ArgumentException>(() => new IborIborBasisSwapRateHelper(
            10e-4, new Period(8, TimeUnit.Months), 0, calendar, BusinessDayConvention.ModifiedFollowing, false, bkbm3m,
            bkbm6m, discountCurve, true, context, true, DateGenerationRule.Backward, 0, otherConfig,
            new StubIndexSelection()));
    }

    [Fact]
    public void TestOvernightIborAveragingAndTelescopicValueDates()
    {
        // Testing averaging and telescopic value dates for overnight-IBOR helpers...

        EvaluationContext context = Context();
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        YieldTermStructure curve = FlatRate(0.01, new Actual365Fixed(), context);
        var overnightIndex = new Sofr(curve);
        var iborIndex = new USDLibor(new Period(3, TimeUnit.Months), curve);
        double basis = 0.0010;

        OvernightIborBasisSwapRateHelper MakeHelper(RateAveragingType averagingMethod, bool telescopicValueDates) =>
            new(basis, new Period(2, TimeUnit.Years), 2, calendar, BusinessDayConvention.Following, false,
                overnightIndex, iborIndex, context, curve, false, 0, Frequency.Annual, null,
                DateGenerationRule.Backward, averagingMethod, telescopicValueDates);

        OvernightIborBasisSwapRateHelper fullSimpleHelper = MakeHelper(RateAveragingType.Simple, false);
        OvernightIborBasisSwapRateHelper telescopicSimpleHelper = MakeHelper(RateAveragingType.Simple, true);
        OvernightIborBasisSwapRateHelper fullCompoundedHelper = MakeHelper(RateAveragingType.Compound, false);
        OvernightIborBasisSwapRateHelper telescopicCompoundedHelper = MakeHelper(RateAveragingType.Compound, true);

        static OvernightIndexedCoupon OvernightCoupon(OvernightIborBasisSwapRateHelper helper) =>
            Assert.IsType<OvernightIndexedCoupon>(helper.Swap.Leg(0)[0]);
        OvernightIndexedCoupon fullSimpleCoupon = OvernightCoupon(fullSimpleHelper);
        OvernightIndexedCoupon telescopicSimpleCoupon = OvernightCoupon(telescopicSimpleHelper);
        OvernightIndexedCoupon fullCompoundedCoupon = OvernightCoupon(fullCompoundedHelper);
        OvernightIndexedCoupon telescopicCompoundedCoupon = OvernightCoupon(telescopicCompoundedHelper);

        Assert.Equal(RateAveragingType.Simple, fullSimpleCoupon.AveragingMethod);
        Assert.Equal(RateAveragingType.Compound, fullCompoundedCoupon.AveragingMethod);
        Assert.Equal(fullSimpleCoupon.ValueDates().Length, telescopicSimpleCoupon.ValueDates().Length);

        // Moved: the check of line 767, that the telescopic compounded coupon lists fewer value dates, which only
        // QuantLib's shortened dates give, moves to Divergences/TelescopicValueDatesTests.cs: nanoql lists every one.
        _ = telescopicCompoundedCoupon;
        _ = fullCompoundedCoupon;

        var lastBaseCoupon = Assert.IsType<OvernightIndexedCoupon>(telescopicCompoundedHelper.Swap.Leg(0)[^1]);
        var lastOtherCoupon = Assert.IsAssignableFrom<IborCoupon>(telescopicCompoundedHelper.Swap.Leg(1)[^1]);
        Date lastBaseFixingEndDate = overnightIndex.MaturityDate(overnightIndex.ValueDate(lastBaseCoupon.FixingDate()));
        Date lastPaymentDate = Max(
            telescopicCompoundedHelper.Swap.Leg(0)[^1].Date, telescopicCompoundedHelper.Swap.Leg(1)[^1].Date);
        Date expectedLatestRelevantDate = Max(
            Max(telescopicCompoundedHelper.MaturityDate!.Value, lastPaymentDate),
            Max(lastBaseFixingEndDate, lastOtherCoupon.FixingEndDate()));
        Assert.Equal(expectedLatestRelevantDate, telescopicCompoundedHelper.LatestRelevantDate);

        TestOvernightIborBootstrap(true, false, 0, false, RateAveragingType.Simple, true);
    }

    [Fact]
    public void TestOvernightOvernightOtherCurveBootstrap()
    {
        // Testing overnight-overnight basis-swap rate helpers (other curve bootstrap)...
        TestOvernightOvernightBootstrap(false, false);
        TestOvernightOvernightBootstrap(true, false);
    }

    [Fact]
    public void TestOvernightOvernightBaseCurveBootstrap()
    {
        // Testing overnight-overnight basis-swap rate helpers (base curve bootstrap)...
        TestOvernightOvernightBootstrap(false, true);
        TestOvernightOvernightBootstrap(true, true);
    }

    // Adapted: as TestOvernightIborBootstrapWithLateLinkedDiscountCurve.
    [Fact]
    public void TestOvernightOvernightBootstrapWithLateLinkedDiscountCurve()
    {
        // Testing overnight-overnight basis-swap rate helpers with a late-linked discount curve...
        TestOvernightOvernightBootstrap(true, false, true);
    }

    [Fact]
    public void TestOvernightOvernightDateGenerationRule()
    {
        // Testing the date-generation rule of overnight-overnight basis-swap helpers...

        var context = new EvaluationContext { EvaluationDate = new Date(27, Month.January, 2010) };
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        YieldTermStructure curve = FlatRate(0.01, new Actual365Fixed(), context);
        var baseIndex = new FedFunds(curve);
        var otherIndex = new Sofr(curve);
        double basis = 0.0010;

        var backwardHelper = new OvernightOvernightBasisSwapRateHelper(
            basis, new Period(18, TimeUnit.Months), 2, calendar, BusinessDayConvention.Following, false, baseIndex,
            otherIndex, context, curve, false, 0, Frequency.Annual, RateAveragingType.Compound,
            RateAveragingType.Compound, false, DateGenerationRule.Backward);
        var defaultHelper = new OvernightOvernightBasisSwapRateHelper(
            basis, new Period(18, TimeUnit.Months), 2, calendar, BusinessDayConvention.Following, false, baseIndex,
            otherIndex, context, curve);
        var forwardHelper = new OvernightOvernightBasisSwapRateHelper(
            basis, new Period(18, TimeUnit.Months), 2, calendar, BusinessDayConvention.Following, false, baseIndex,
            otherIndex, context, curve, false, 0, Frequency.Annual, RateAveragingType.Compound,
            RateAveragingType.Compound, false, DateGenerationRule.Forward);

        static Date FirstAccrualEnd(OvernightOvernightBasisSwapRateHelper helper, int leg) =>
            ((Coupon)helper.Swap.Leg(leg)[0]).AccrualEndDate;
        Date spot = calendar.Advance(context.EvaluationDate, 2, TimeUnit.Days);
        for (int i = 0; i < 2; ++i)
        {
            Assert.Equal(2, backwardHelper.Swap.Leg(i).Length);
            Assert.Equal(2, defaultHelper.Swap.Leg(i).Length);
            Assert.Equal(2, forwardHelper.Swap.Leg(i).Length);
            Assert.Equal(
                calendar.Adjust(spot + new Period(6, TimeUnit.Months), BusinessDayConvention.Following),
                FirstAccrualEnd(backwardHelper, i));
            Assert.Equal(FirstAccrualEnd(backwardHelper, i), FirstAccrualEnd(defaultHelper, i));
            Assert.Equal(
                calendar.Adjust(spot + new Period(1, TimeUnit.Years), BusinessDayConvention.Following),
                FirstAccrualEnd(forwardHelper, i));
        }

        Date expectedMaturity = calendar.Adjust(spot + new Period(18, TimeUnit.Months), BusinessDayConvention.Following);
        Assert.Equal(expectedMaturity, backwardHelper.MaturityDate);
        Assert.Equal(expectedMaturity, defaultHelper.MaturityDate);
        Assert.Equal(expectedMaturity, forwardHelper.MaturityDate);
    }

    [Fact]
    public void TestOvernightOvernightNoFrequency()
    {
        // Testing zero-coupon overnight-overnight basis-swap helpers...

        EvaluationContext context = Context();
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        YieldTermStructure curve = FlatRate(0.01, new Actual365Fixed(), context);
        var baseIndex = new FedFunds(curve);
        var otherIndex = new Sofr(curve);
        double basis = 0.0010;

        var helper = new OvernightOvernightBasisSwapRateHelper(
            basis, new Period(5, TimeUnit.Years), 2, calendar, BusinessDayConvention.Following, false, baseIndex,
            otherIndex, context, curve, false, 0, Frequency.NoFrequency);

        Assert.Single(helper.Swap.Leg(0));
        Assert.Single(helper.Swap.Leg(1));
        var baseCoupon = Assert.IsAssignableFrom<Coupon>(helper.Swap.Leg(0)[0]);
        var otherCoupon = Assert.IsAssignableFrom<Coupon>(helper.Swap.Leg(1)[0]);
        Assert.Equal(helper.EarliestDate, baseCoupon.AccrualStartDate);
        Assert.Equal(helper.EarliestDate, otherCoupon.AccrualStartDate);
        Assert.Equal(helper.MaturityDate, baseCoupon.AccrualEndDate);
        Assert.Equal(helper.MaturityDate, otherCoupon.AccrualEndDate);
    }

    [Fact]
    public void TestOvernightOvernightTelescopicValueDatesWithSimpleAveraging()
    {
        // Testing telescopic value dates with simple overnight averaging...

        EvaluationContext context = Context();
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        YieldTermStructure forecastCurve = FlatRate(0.01, new Actual365Fixed(), context);
        var baseIndex = new FedFunds(forecastCurve);
        var otherIndex = new Sofr();

        OvernightOvernightBasisSwapRateHelper MakeHelper(bool telescopicValueDates) =>
            new(0.0010, new Period(1, TimeUnit.Years), 2, calendar, BusinessDayConvention.Following, false, baseIndex,
                otherIndex, context, null, false, 0, Frequency.Annual, RateAveragingType.Simple,
                RateAveragingType.Compound, telescopicValueDates);

        OvernightOvernightBasisSwapRateHelper fullScheduleHelper = MakeHelper(false);
        OvernightOvernightBasisSwapRateHelper telescopicHelper = MakeHelper(true);

        var fullSimpleCoupon = Assert.IsType<OvernightIndexedCoupon>(fullScheduleHelper.Swap.Leg(0)[0]);
        var telescopicSimpleCoupon = Assert.IsType<OvernightIndexedCoupon>(telescopicHelper.Swap.Leg(0)[0]);
        var fullCompoundedCoupon = Assert.IsType<OvernightIndexedCoupon>(fullScheduleHelper.Swap.Leg(1)[0]);
        var telescopicCompoundedCoupon = Assert.IsType<OvernightIndexedCoupon>(telescopicHelper.Swap.Leg(1)[0]);

        Assert.Equal(fullSimpleCoupon.ValueDates().Length, telescopicSimpleCoupon.ValueDates().Length);

        // Moved: the check of line 920, as testOvernightIborAveragingAndTelescopicValueDates' of line 767.
        _ = telescopicCompoundedCoupon;
        _ = fullCompoundedCoupon;
        Assert.True(Math.Abs(telescopicSimpleCoupon.Rate(context) - fullSimpleCoupon.Rate(context)) <= 1.0e-14);
    }

    [Fact]
    public void TestOvernightIborMarginOnIborLeg()
    {
        // Testing overnight-ibor basis-swap helper with the margin quoted on the ibor leg...

        BasisSwapQuote[] quotes =
        [
            new(1, TimeUnit.Years, -0.0010),
            new(2, TimeUnit.Years, -0.0012),
            new(5, TimeUnit.Years, -0.0015),
            new(10, TimeUnit.Years, -0.0020),
        ];

        EvaluationContext context = Context();
        int settlementDays = 2;
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        var convention = BusinessDayConvention.Following;
        bool endOfMonth = false;

        YieldTermStructure knownForecastCurve = FlatRate(0.01, new Actual365Fixed(), context);
        YieldTermStructure discountCurve = FlatRate(0.005, new Actual365Fixed(), context);

        OvernightIndex baseIndex = new Sofr(knownForecastCurve);
        IborIndex otherIndex = new USDLibor(new Period(6, TimeUnit.Months));

        BootstrapHelper<YieldTermStructure>[] helpers =
        [
            .. quotes.Select(q => new OvernightIborBasisSwapRateHelper(
                q.Basis, new Period(q.N, q.Units), settlementDays, calendar, convention, endOfMonth, baseIndex,
                otherIndex, context, discountCurve, false, 0, null, null, DateGenerationRule.Backward,
                RateAveragingType.Compound, false, true)),
        ];

        var bootstrappedCurve = PiecewiseYieldCurve<ZeroYield, Linear, IterativeBootstrap>.Create(
            0, calendar, helpers, new Actual365Fixed(), context);

        Date today = context.EvaluationDate;
        Date spot = calendar.Advance(today, settlementDays, TimeUnit.Days);

        otherIndex = new USDLibor(new Period(6, TimeUnit.Months), bootstrappedCurve);

        foreach (BasisSwapQuote q in quotes)
        {
            // create swaps and check they're fair
            Date maturity = spot + new Period(q.N, q.Units);

            Schedule s = new MakeSchedule()
                .From(spot)
                .To(maturity)
                .WithTenor(otherIndex.Tenor)
                .WithCalendar(calendar)
                .WithConvention(convention)
                .WithRule(DateGenerationRule.Backward)
                .ToSchedule();

            ImmutableArray<CashFlow> leg1 = new OvernightLeg(s, baseIndex)
                .WithNotionals(100.0)
                .ToLeg();
            ImmutableArray<CashFlow> leg2 = new IborLeg(s, otherIndex)
                .WithSpreads(q.Basis)
                .WithNotionals(100.0)
                .ToLeg(context);

            var swap = new Swap(leg1, leg2);
            double npv = DiscountingSwapEngine.Calculate(swap, context, discountCurve).NPV!.Value;
            double tolerance = 1e-8;
            Assert.True(Math.Abs(npv) <= tolerance, $"Failed to price fair {q.N}-year(s) swap: calculated {npv}");
        }
    }

    private static void TestIborIborBootstrap(bool bootstrapBaseCurve, int paymentLag = 0)
    {
        BasisSwapQuote[] quotes =
        [
            new(1, TimeUnit.Years, 0.0010),
            new(2, TimeUnit.Years, 0.0012),
            new(3, TimeUnit.Years, 0.0015),
            new(5, TimeUnit.Years, 0.0015),
            new(8, TimeUnit.Years, 0.0018),
            new(10, TimeUnit.Years, 0.0020),
            new(15, TimeUnit.Years, 0.0021),
            new(20, TimeUnit.Years, 0.0021),
        ];

        EvaluationContext context = Context();
        int settlementDays = 2;
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        var convention = BusinessDayConvention.Following;
        bool endOfMonth = false;

        YieldTermStructure knownForecastCurve = FlatRate(0.01, new Actual365Fixed(), context);
        YieldTermStructure discountCurve = FlatRate(0.005, new Actual365Fixed(), context);

        IborIndex baseIndex, otherIndex;

        if (bootstrapBaseCurve)
        {
            baseIndex = new USDLibor(new Period(3, TimeUnit.Months));
            otherIndex = new USDLibor(new Period(6, TimeUnit.Months), knownForecastCurve);
        }
        else
        {
            baseIndex = new USDLibor(new Period(3, TimeUnit.Months), knownForecastCurve);
            otherIndex = new USDLibor(new Period(6, TimeUnit.Months));
        }

        BootstrapHelper<YieldTermStructure>[] helpers =
        [
            .. quotes.Select(q => new IborIborBasisSwapRateHelper(
                q.Basis, new Period(q.N, q.Units), settlementDays, calendar, convention, endOfMonth, baseIndex,
                otherIndex, discountCurve, bootstrapBaseCurve, context, null, DateGenerationRule.Backward, paymentLag)),
        ];

        var bootstrappedCurve = PiecewiseYieldCurve<ZeroYield, Linear, IterativeBootstrap>.Create(
            0, calendar, helpers, new Actual365Fixed(), context);

        Date today = context.EvaluationDate;
        Date spot = calendar.Advance(today, settlementDays, TimeUnit.Days);

        if (bootstrapBaseCurve)
        {
            baseIndex = new USDLibor(new Period(3, TimeUnit.Months), bootstrappedCurve);
            otherIndex = new USDLibor(new Period(6, TimeUnit.Months), knownForecastCurve);
        }
        else
        {
            baseIndex = new USDLibor(new Period(3, TimeUnit.Months), knownForecastCurve);
            otherIndex = new USDLibor(new Period(6, TimeUnit.Months), bootstrappedCurve);
        }

        foreach (BasisSwapQuote q in quotes)
        {
            // create swaps and check they're fair
            Date maturity = spot + new Period(q.N, q.Units);

            Schedule s1 = new MakeSchedule()
                .From(spot)
                .To(maturity)
                .WithTenor(baseIndex.Tenor)
                .WithCalendar(calendar)
                .WithConvention(convention)
                .WithRule(DateGenerationRule.Backward)
                .ToSchedule();
            ImmutableArray<CashFlow> leg1 = new IborLeg(s1, baseIndex)
                .WithSpreads(q.Basis)
                .WithNotionals(100.0)
                .WithPaymentLag(paymentLag)
                .ToLeg(context);

            Schedule s2 = new MakeSchedule()
                .From(spot)
                .To(maturity)
                .WithTenor(otherIndex.Tenor)
                .WithCalendar(calendar)
                .WithConvention(convention)
                .WithRule(DateGenerationRule.Backward)
                .ToSchedule();
            ImmutableArray<CashFlow> leg2 = new IborLeg(s2, otherIndex)
                .WithNotionals(100.0)
                .WithPaymentLag(paymentLag)
                .ToLeg(context);

            var swap = new Swap(leg1, leg2);
            double npv = DiscountingSwapEngine.Calculate(swap, context, discountCurve).NPV!.Value;
            double tolerance = 1e-8;
            Assert.True(Math.Abs(npv) <= tolerance, $"Failed to price fair {q.N}-year(s) swap: calculated {npv}");
        }
    }

    // linkDiscountCurveAfterConstruction gives the external curve at construction: see the late-linked cases.
    private static void TestOvernightIborBootstrap(
        bool externalDiscountCurve,
        bool bootstrapBaseCurve = false,
        int paymentLag = 0,
        bool linkDiscountCurveAfterConstruction = false,
        RateAveragingType averagingMethod = RateAveragingType.Compound,
        bool telescopicValueDates = false)
    {
        _ = linkDiscountCurveAfterConstruction;
        BasisSwapQuote[] quotes =
        [
            new(1, TimeUnit.Years, 0.0010),
            new(2, TimeUnit.Years, 0.0012),
            new(3, TimeUnit.Years, 0.0015),
            new(5, TimeUnit.Years, 0.0015),
            new(8, TimeUnit.Years, 0.0018),
            new(10, TimeUnit.Years, 0.0020),
            new(15, TimeUnit.Years, 0.0021),
            new(20, TimeUnit.Years, 0.0021),
        ];

        EvaluationContext context = Context();
        int settlementDays = 2;
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        var convention = BusinessDayConvention.Following;
        bool endOfMonth = false;

        YieldTermStructure knownForecastCurve = FlatRate(0.01, new Actual365Fixed(), context);

        YieldTermStructure? discountCurve = externalDiscountCurve
            ? FlatRate(0.005, new Actual365Fixed(), context)
            : null;

        OvernightIndex baseIndex;
        IborIndex otherIndex;

        if (bootstrapBaseCurve)
        {
            baseIndex = new Sofr();
            otherIndex = new USDLibor(new Period(6, TimeUnit.Months), knownForecastCurve);
        }
        else
        {
            baseIndex = new Sofr(knownForecastCurve);
            otherIndex = new USDLibor(new Period(6, TimeUnit.Months));
        }

        BootstrapHelper<YieldTermStructure>[] helpers =
        [
            .. quotes.Select(q => new OvernightIborBasisSwapRateHelper(
                q.Basis, new Period(q.N, q.Units), settlementDays, calendar, convention, endOfMonth, baseIndex,
                otherIndex, context, discountCurve, bootstrapBaseCurve, paymentLag, null, null,
                DateGenerationRule.Backward, averagingMethod, telescopicValueDates)),
        ];

        var bootstrappedCurve = PiecewiseYieldCurve<ZeroYield, Linear, IterativeBootstrap>.Create(
            0, calendar, helpers, new Actual365Fixed(), context);

        Date today = context.EvaluationDate;
        Date spot = calendar.Advance(today, settlementDays, TimeUnit.Days);

        if (bootstrapBaseCurve)
        {
            baseIndex = new Sofr(bootstrappedCurve);
            otherIndex = new USDLibor(new Period(6, TimeUnit.Months), knownForecastCurve);
        }
        else
        {
            baseIndex = new Sofr(knownForecastCurve);
            otherIndex = new USDLibor(new Period(6, TimeUnit.Months), bootstrappedCurve);
        }

        foreach (BasisSwapQuote q in quotes)
        {
            // create swaps and check they're fair
            Date maturity = spot + new Period(q.N, q.Units);

            Schedule s = new MakeSchedule()
                .From(spot)
                .To(maturity)
                .WithTenor(otherIndex.Tenor)
                .WithCalendar(calendar)
                .WithConvention(convention)
                .WithRule(DateGenerationRule.Backward)
                .ToSchedule();

            ImmutableArray<CashFlow> leg1 = new OvernightLeg(s, baseIndex)
                .WithSpreads(q.Basis)
                .WithNotionals(100.0)
                .WithPaymentLag(paymentLag)
                .WithTelescopicValueDates(telescopicValueDates && averagingMethod == RateAveragingType.Compound)
                .WithAveragingMethod(averagingMethod)
                .ToLeg();
            ImmutableArray<CashFlow> leg2 = new IborLeg(s, otherIndex)
                .WithNotionals(100.0)
                .WithPaymentLag(paymentLag)
                .ToLeg(context);

            var swap = new Swap(leg1, leg2);
            double npv = DiscountingSwapEngine.Calculate(swap, context, discountCurve ?? bootstrappedCurve).NPV!.Value;
            double tolerance = 1e-8;
            Assert.True(Math.Abs(npv) <= tolerance, $"Failed to price fair {q.N}-year(s) swap: calculated {npv}");
        }
    }

    // linkDiscountCurveAfterConstruction gives the external curve at construction: see the late-linked cases.
    private static void TestOvernightOvernightBootstrap(
        bool externalDiscountCurve, bool bootstrapBaseCurve, bool linkDiscountCurveAfterConstruction = false)
    {
        _ = linkDiscountCurveAfterConstruction;
        BasisSwapQuote[] quotes =
        [
            new(1, TimeUnit.Years, 0.0008),
            new(2, TimeUnit.Years, 0.0010),
            new(3, TimeUnit.Years, 0.0011),
            new(5, TimeUnit.Years, 0.0013),
            new(10, TimeUnit.Years, 0.0015),
        ];

        EvaluationContext context = Context();
        int settlementDays = 2;
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        var convention = BusinessDayConvention.Following;
        bool endOfMonth = false;
        int paymentLag = 2;
        var paymentFrequency = Frequency.Quarterly;
        var baseAveragingMethod = RateAveragingType.Simple;
        var otherAveragingMethod = RateAveragingType.Compound;

        YieldTermStructure knownForecastCurve = FlatRate(0.01, new Actual365Fixed(), context);

        YieldTermStructure? discountCurve = externalDiscountCurve
            ? FlatRate(0.005, new Actual365Fixed(), context)
            : null;

        OvernightIndex baseIndex, otherIndex;
        if (bootstrapBaseCurve)
        {
            baseIndex = new FedFunds();
            otherIndex = new Sofr(knownForecastCurve);
        }
        else
        {
            baseIndex = new FedFunds(knownForecastCurve);
            otherIndex = new Sofr();
        }

        OvernightOvernightBasisSwapRateHelper[] basisHelpers =
        [
            .. quotes.Select(q => new OvernightOvernightBasisSwapRateHelper(
                q.Basis, new Period(q.N, q.Units), settlementDays, calendar, convention, endOfMonth, baseIndex,
                otherIndex, context, discountCurve, bootstrapBaseCurve, paymentLag, paymentFrequency,
                baseAveragingMethod, otherAveragingMethod)),
        ];

        var firstBaseCoupon = Assert.IsType<OvernightIndexedCoupon>(basisHelpers[0].Swap.Leg(0)[0]);
        var firstOtherCoupon = Assert.IsType<OvernightIndexedCoupon>(basisHelpers[0].Swap.Leg(1)[0]);
        Assert.Equal(baseAveragingMethod, firstBaseCoupon.AveragingMethod);
        Assert.Equal(otherAveragingMethod, firstOtherCoupon.AveragingMethod);
        Assert.Equal(4, basisHelpers[0].Swap.Leg(0).Length);
        Assert.Equal(4, basisHelpers[0].Swap.Leg(1).Length);

        var bound = new List<IBootstrapHelper>();
        var bootstrappedCurve = PiecewiseYieldCurve<ZeroYield, Linear, CapturingBootstrap>.Create(
            0, calendar, basisHelpers, new Actual365Fixed(), context, bootstrap: new CapturingBootstrap(bound));

        if (bootstrapBaseCurve)
        {
            baseIndex = new FedFunds(bootstrappedCurve);
            otherIndex = new Sofr(knownForecastCurve);
        }
        else
        {
            baseIndex = new FedFunds(knownForecastCurve);
            otherIndex = new Sofr(bootstrappedCurve);
        }

        Date today = context.EvaluationDate;
        Date spot = calendar.Advance(today, settlementDays, TimeUnit.Days);

        for (int i = 0; i < quotes.Length; ++i)
        {
            BasisSwapQuote q = quotes[i];
            Date maturity = calendar.Advance(spot, q.N, q.Units, convention);

            Schedule schedule = new MakeSchedule()
                .From(spot)
                .To(maturity)
                .WithFrequency(paymentFrequency)
                .WithCalendar(calendar)
                .WithConvention(convention)
                .WithRule(DateGenerationRule.Forward)
                .ToSchedule();

            ImmutableArray<CashFlow> baseLeg = new OvernightLeg(schedule, baseIndex)
                .WithNotionals(100.0)
                .WithSpreads(q.Basis)
                .WithPaymentLag(paymentLag)
                .WithAveragingMethod(baseAveragingMethod)
                .ToLeg();
            ImmutableArray<CashFlow> otherLeg = new OvernightLeg(schedule, otherIndex)
                .WithNotionals(100.0)
                .WithPaymentLag(paymentLag)
                .WithAveragingMethod(otherAveragingMethod)
                .ToLeg();

            var swap = new Swap(baseLeg, otherLeg);
            double npv = DiscountingSwapEngine.Calculate(swap, context, discountCurve ?? bootstrappedCurve).NPV!.Value;

            double tolerance = 1e-8;
            Assert.True(Math.Abs(npv) <= tolerance);
            Assert.True(Math.Abs(bound[i].ImpliedQuote(context) - q.Basis) <= tolerance);
        }
    }

    private static EvaluationContext Context() => new() { EvaluationDate = RunnerDate };

    // The test suite's flatRate(forward, dc): a flat forward from the evaluation date.
    private static YieldTermStructure FlatRate(double forward, DayCounter dc, EvaluationContext context) =>
        FlatForward.Create(0, new NullCalendar(), forward, dc, context);

    private static Date Max(Date a, Date b) => a < b ? b : a;

    private readonly record struct BasisSwapQuote(int N, TimeUnit Units, double Basis);

    // QuantLib's IterativeBootstrap, keeping the helpers it binds to the curve, so that a case can read a helper's
    // implied quote after the bootstrap, as QuantLib's reads its helpers'.
    private readonly struct CapturingBootstrap : IBootstrap
    {
        private readonly List<IBootstrapHelper> bound;

        public CapturingBootstrap(List<IBootstrapHelper> bound)
        {
            this.bound = bound;
        }

        public void Calculate<TTermStructure, TTraits, TInterpolator>(
            BootstrapState<TTermStructure> state, TInterpolator interpolator, EvaluationContext context)
            where TTermStructure : TermStructure
            where TTraits : struct, IBootstrapTraits<TTermStructure>
            where TInterpolator : struct, IInterpolator
        {
            new IterativeBootstrap().Calculate<TTermStructure, TTraits, TInterpolator>(state, interpolator, context);
            bound.AddRange(state.Instruments);
        }
    }
}
