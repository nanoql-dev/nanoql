// SPDX-FileCopyrightText: Copyright (C) 2021 Marcin Rybacki
// SPDX-FileCopyrightText: Copyright (C) 2025 Uzair Beg
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/crosscurrencyratehelpers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

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
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

// QuantLib's helpers hold relinkable handles and their swaps' engines; here a helper is bound to a curve by a copy
// (SetTermStructure), and its swap is priced with the engine's arguments QuantLib's swap holds: the quote currency and
// its leg's discount curve, the base currency and its leg's, a spot of 1 and the settlement date's flows.
[Trait("Oracle", "QuantLib")]
public class CrossCurrencyRateHelpersTests
{
    [Fact]
    public void TestIndexedCouponOverrides()
    {
        // Testing indexed-coupon overrides for cross-currency rate helpers...

        CheckCrossCurrencyHelperCouponModes(true, true, true);
        CheckCrossCurrencyHelperCouponModes(false, false, false);
        CheckCrossCurrencyHelperCouponModes(true, null, false);
        CheckCrossCurrencyHelperCouponModes(false, null, true);
    }

    [Fact]
    public void TestConstNotionalBasisSwapsWithCollateralInQuoteAndBasisInBaseCcy()
    {
        // Testing constant notional basis swaps with collateral in quote ccy and basis in base ccy...
        TestConstantNotionalCrossCurrencySwapsNPV(false, true);
    }

    [Fact]
    public void TestConstNotionalBasisSwapsWithCollateralInBaseAndBasisInQuoteCcy()
    {
        // Testing constant notional basis swaps with collateral in base ccy and basis in quote ccy...
        TestConstantNotionalCrossCurrencySwapsNPV(true, false);
    }

    [Fact]
    public void TestConstNotionalBasisSwapsWithCollateralAndBasisInBaseCcy()
    {
        // Testing constant notional basis swaps with collateral and basis in base ccy...
        TestConstantNotionalCrossCurrencySwapsNPV(true, true);
    }

    [Fact]
    public void TestConstNotionalBasisSwapsWithCollateralAndBasisInQuoteCcy()
    {
        // Testing constant notional basis swaps with collateral and basis in quote ccy...
        TestConstantNotionalCrossCurrencySwapsNPV(false, false);
    }

    [Fact]
    public void TestResettingBasisSwapsWithCollateralInQuoteAndBasisInBaseCcy()
    {
        // Testing resetting basis swaps with collateral in quote ccy and basis in base ccy...
        TestResettingCrossCurrencySwaps(false, true, false);
    }

    [Fact]
    public void TestResettingBasisSwapsWithCollateralInBaseAndBasisInQuoteCcy()
    {
        // Testing resetting basis swaps with collateral in base ccy and basis in quote ccy...
        TestResettingCrossCurrencySwaps(true, false, true);
    }

    [Fact]
    public void TestResettingBasisSwapsWithCollateralAndBasisInBaseCcy()
    {
        // Testing resetting basis swaps with collateral and basis in base ccy...
        TestResettingCrossCurrencySwaps(true, true, true);
    }

    [Fact]
    public void TestResettingBasisSwapsWithCollateralAndBasisInQuoteCcy()
    {
        // Testing resetting basis swaps with collateral and basis in quote ccy...
        TestResettingCrossCurrencySwaps(false, false, false);
    }

    [Fact]
    public void TestResettingBasisSwapsWithArbitraryFreq()
    {
        // Testing resetting basis swaps with collateral in quote ccy and basis in base ccy...
        TestResettingCrossCurrencySwaps(false, true, false, Frequency.Weekly);
    }

    [Fact]
    public void TestResettingBasisSwapsWithAsymmetricFrequencies()
    {
        // Testing resetting basis swaps with different payment frequencies per leg...
        TestResettingCrossCurrencySwaps(false, true, false, Frequency.Semiannual, 0, false, Frequency.Quarterly);
    }

    [Fact]
    public void TestResettingBasisSwapsTreatNoFrequencyAsUnset()
    {
        // Testing that an explicit NoFrequency reproduces the default payment frequency...

        var vars = new CommonVars();

        YieldTermStructure collateralHandle = vars.QuoteCcyIdxHandle;

        // Default (unset) payment frequencies: the schedule is derived from the index tenor on both legs.
        MtMCrossCurrencyBasisSwapRateHelper[] defaultInstruments = vars.BuildResettingXccyRateHelpers(
            vars.BasisData, collateralHandle, false, true, false, null, 0, false, null);

        // An explicit NoFrequency on both legs must behave identically. Before NoFrequency was normalized to nullopt
        // it built a single-period schedule (Period(NoFrequency) has zero length, which Schedule maps to the Zero
        // date-generation rule), silently changing the result instead of using the index tenor.
        MtMCrossCurrencyBasisSwapRateHelper[] noFrequencyInstruments = vars.BuildResettingXccyRateHelpers(
            vars.BasisData,
            collateralHandle,
            false,
            true,
            false,
            Frequency.NoFrequency,
            0,
            false,
            Frequency.NoFrequency);

        YieldTermStructure defaultCurve = vars.Curve(defaultInstruments);
        YieldTermStructure noFrequencyCurve = vars.Curve(noFrequencyInstruments);

        double tolerance = 1.0e-12;
        for (int i = 0; i < vars.BasisData.Length; ++i)
        {
            Date maturity = defaultInstruments[i].MaturityDate!.Value;
            double defaultZero = defaultCurve.ZeroRate(maturity, vars.DayCount, Compounding.Continuous).Rate;
            double noFrequencyZero = noFrequencyCurve.ZeroRate(maturity, vars.DayCount, Compounding.Continuous).Rate;
            Assert.True(
                Math.Abs(defaultZero - noFrequencyZero) <= tolerance,
                "explicit NoFrequency does not match the default payment frequency\n" +
                $"    zero with default frequency:    {defaultZero}\n" +
                $"    zero with explicit NoFrequency:    {noFrequencyZero}\n    maturity:    {maturity}\n");
        }
    }

    [Fact]
    public void TestResettingBasisSwapsQuoteFrequencyDefaultsToBase()
    {
        // Testing that an unset quote-currency payment frequency defaults to the base frequency...

        var vars = new CommonVars();

        YieldTermStructure collateralHandle = vars.QuoteCcyIdxHandle;

        var baseFrequency = Frequency.Semiannual;

        // Quote-currency frequency left unset: it must fall back to the base frequency.
        MtMCrossCurrencyBasisSwapRateHelper[] fallbackInstruments = vars.BuildResettingXccyRateHelpers(
            vars.BasisData, collateralHandle, false, true, false, baseFrequency, 0, false, null);

        // Quote-currency frequency set explicitly to the base frequency: same result.
        MtMCrossCurrencyBasisSwapRateHelper[] explicitInstruments = vars.BuildResettingXccyRateHelpers(
            vars.BasisData, collateralHandle, false, true, false, baseFrequency, 0, false, baseFrequency);

        YieldTermStructure fallbackCurve = vars.Curve(fallbackInstruments);
        YieldTermStructure explicitCurve = vars.Curve(explicitInstruments);

        double tolerance = 1.0e-12;
        for (int i = 0; i < vars.BasisData.Length; ++i)
        {
            Date maturity = fallbackInstruments[i].MaturityDate!.Value;
            double fallbackZero = fallbackCurve.ZeroRate(maturity, vars.DayCount, Compounding.Continuous).Rate;
            double explicitZero = explicitCurve.ZeroRate(maturity, vars.DayCount, Compounding.Continuous).Rate;
            Assert.True(
                Math.Abs(fallbackZero - explicitZero) <= tolerance,
                "unset quote-currency frequency does not default to the base frequency\n" +
                $"    zero with quote frequency unset:    {fallbackZero}\n" +
                $"    zero with quote frequency = base:    {explicitZero}\n    maturity:    {maturity}\n");
        }
    }

    [Fact]
    public void TestResettingBasisSwapsWithPaymentLag()
    {
        // Testing resetting basis swaps with collateral in quote ccy and basis in base ccy...
        TestResettingCrossCurrencySwaps(false, true, false, null, 2);
    }

    [Fact]
    public void TestMtMHelperMatchesStandaloneWithAsymmetricFxHolidays()
    {
        // Testing helper/instrument equivalence across asymmetric FX holidays...

        var today = new Date(3, July, 2024);
        var context = new EvaluationContext { EvaluationDate = today };

        Calendar legCalendar = new TARGET();
        Calendar fxCalendar = new JointCalendar(
            new TARGET(), new UnitedStates(UnitedStatesMarket.Settlement), JointCalendarRule.JoinHolidays);
        int settlementDays = 3;
        int fxResetFixingDays = 2;
        int paymentLag = 2;
        Date start = legCalendar.Advance(today, settlementDays, TimeUnit.Days);
        Date expectedFxFixingDate = fxCalendar.Advance(start, -fxResetFixingDays, TimeUnit.Days);

        Assert.Equal(new Date(8, July, 2024), start);
        Assert.Equal(today, expectedFxFixingDate);

        // July 4th is a TARGET business day but a US holiday. This proves that the separate FX calendar, rather than
        // the leg calendar, drives the reset.
        Assert.Equal(new Date(4, July, 2024), legCalendar.Advance(start, -fxResetFixingDays, TimeUnit.Days));

        YieldTermStructure eurForecast = new FlatForward(today, 0.015, new Actual365Fixed());
        YieldTermStructure usdCurve = new FlatForward(today, 0.030, new Actual365Fixed());
        var eurIndex = new Euribor3M(eurForecast);
        var usdIndex = new USDLibor(new Period(3, TimeUnit.Months), usdCurve);

        double observedFx = 1.25;
        context = context with
        {
            ExchangeRates = context.ExchangeRates.Add(
                new ExchangeRate(new EURCurrency(), new USDCurrency(), observedFx),
                expectedFxFixingDate,
                expectedFxFixingDate),
        };

        double basis = 10.0e-4;
        var helper = new MtMCrossCurrencyBasisSwapRateHelper(
            basis,
            new Period(1, TimeUnit.Years),
            settlementDays,
            legCalendar,
            BusinessDayConvention.Following,
            false,
            eurIndex,
            usdIndex,
            usdCurve,
            isFxBaseCurrencyCollateralCurrency: false,
            isBasisOnFxBaseCurrencyLeg: false,
            isFxBaseCurrencyLegResettable: false,
            context,
            Frequency.Quarterly,
            paymentLag,
            Frequency.Semiannual,
            fxResetFixingDays,
            fxCalendar);

        YieldTermStructure eurDiscount = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            today, [helper], new Actual365Fixed(), context).EnableExtrapolation();
        eurDiscount.Discount(helper.MaturityDate!.Value);

        MtMCrossCurrencyBasisSwap helperSwap = helper.Swap;
        var standalone = new MtMCrossCurrencyBasisSwap(
            MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency,
            1.0,
            new EURCurrency(),
            helperSwap.FxBaseSchedule,
            eurIndex,
            0.0,
            1.0,
            1.0,
            new USDCurrency(),
            helperSwap.FxQuoteSchedule,
            usdIndex,
            0.0,
            1.0,
            isFxBaseCurrencyLegResettable: false,
            context,
            fxResetFixingDays,
            fxCalendar,
            paymentLag,
            paymentLag,
            BusinessDayConvention.Following,
            BusinessDayConvention.Following);
        MtMCrossCurrencyBasisSwapResults Price(MtMCrossCurrencyBasisSwap swap) =>
            DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate(
                swap,
                context,
                new USDCurrency(),
                usdCurve,
                new EURCurrency(),
                eurDiscount,
                1.0,
                includeSettlementDateFlows: true);

        ImmutableArray<CashFlow> helperResettingLeg = helperSwap.ResettingLeg;
        ImmutableArray<CashFlow> standaloneResettingLeg = standalone.ResettingLeg;
        Assert.Equal(helperResettingLeg.Length, standaloneResettingLeg.Length);

        FxResetCoupon? firstHelperCoupon = null;
        for (int i = 0; i < helperResettingLeg.Length; ++i)
        {
            Assert.Equal(helperResettingLeg[i].Date, standaloneResettingLeg[i].Date);
            var helperCoupon = helperResettingLeg[i] as FxResetCoupon;
            var standaloneCoupon = standaloneResettingLeg[i] as FxResetCoupon;
            Assert.Equal(helperCoupon is not null, standaloneCoupon is not null);
            if (helperCoupon is not null)
            {
                firstHelperCoupon ??= helperCoupon;
                Assert.Equal(helperCoupon.FxResetDate, standaloneCoupon!.FxResetDate);
                Assert.Equal(helperCoupon.FxResetValueDate, standaloneCoupon.FxResetValueDate);
            }
        }

        Assert.NotNull(firstHelperCoupon);
        Assert.Equal(expectedFxFixingDate, firstHelperCoupon.FxResetDate);
        Assert.Equal(start, firstHelperCoupon.FxResetValueDate);
        MtMCrossCurrencyBasisSwapResults helperResults = Price(helperSwap);
        MtMCrossCurrencyBasisSwapResults standaloneResults = Price(standalone);
        CheckSmall(helperResults.NPV!.Value - standaloneResults.NPV!.Value, 1.0e-12);
        CheckSmall(helperResults.FairFxQuoteSpread!.Value - standaloneResults.FairFxQuoteSpread!.Value, 1.0e-12);
        CheckSmall(helperResults.FairFxQuoteSpread!.Value - basis, 1.0e-10);

        // Under the previous zero-day convention the first reset is still in the future and is projected instead of
        // using today's observed fixing.
        var zeroLagReset = new MtMCrossCurrencyBasisSwap(
            MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency,
            1.0,
            new EURCurrency(),
            helperSwap.FxBaseSchedule,
            eurIndex,
            0.0,
            1.0,
            1.0,
            new USDCurrency(),
            helperSwap.FxQuoteSchedule,
            usdIndex,
            0.0,
            1.0,
            isFxBaseCurrencyLegResettable: false,
            context,
            fxResetFixingDays: 0,
            fxResetFixingCalendar: null,
            paymentLag,
            paymentLag,
            BusinessDayConvention.Following,
            BusinessDayConvention.Following);

        FxResetCoupon firstZeroLagCoupon = zeroLagReset.ResettingLeg.OfType<FxResetCoupon>().First();
        Assert.Equal(start, firstZeroLagCoupon.FxResetDate);
        double zeroLagFairSpreadDifference = Math.Abs(
            Price(zeroLagReset).FairFxQuoteSpread!.Value - standaloneResults.FairFxQuoteSpread!.Value);
        Assert.True(
            zeroLagFairSpreadDifference > 1.0e-8,
            $"zero-lag fair-spread difference was {zeroLagFairSpreadDifference}");
    }

    [Fact]
    public void TestResettingBasisSwapsWithOvernightIndex()
    {
        // Testing resetting basis swaps with collateral in quote ccy and basis in base ccy...
        TestResettingCrossCurrencySwaps(false, true, false, Frequency.Quarterly, 0, true);
    }

    [Fact]
    public void TestResettingBasisSwapsWithOvernightIndexException()
    {
        // Testing resetting basis swaps with collateral in quote ccy and basis in base ccy...
        Assert.Throws<ArgumentException>(() => TestResettingCrossCurrencySwaps(false, true, false, null, 0, true));
    }

    [Fact]
    public void TestExceptionWhenInstrumentTenorShorterThanIndexFrequency()
    {
        // Testing exception when instrument tenor is shorter than index frequency...

        var vars = new CommonVars();

        XccyTestDatum[] data = [new(1, TimeUnit.Months, 10.0)];

        Assert.Throws<ArgumentException>(() => vars.BuildConstantNotionalXccyRateHelpers(data, null, true, true));
    }

    // ConstNotionalCrossCurrencySwapRateHelper Tests

    // QuantLib relinks the collateral handle under a built helper; a helper's collateral curve is fixed here, so the
    // case compares two helpers on the two curves.
    [Fact]
    public void TestConstNotionalCrossCurrencySwapRateHelperRelinking()
    {
        // Testing ConstNotionalCrossCurrencySwapRateHelper reaction to relinked curves...

        var today = new Date(15, January, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        YieldTermStructure eurFwd = new FlatForward(today, 0.017, new Actual365Fixed());

        IborIndex euribor3m = new Euribor3M(eurFwd);
        double q = 0.018;

        ConstNotionalCrossCurrencySwapRateHelper Helper(YieldTermStructure usdCollat) => new(
            q,
            new Period(5, TimeUnit.Years),
            2,
            new TARGET(),
            BusinessDayConvention.Following,
            true,
            Frequency.Annual,
            new Thirty360(Thirty360Convention.BondBasis),
            euribor3m,
            usdCollat,
            true,
            context);

        YieldTermStructure bootstrapCurve = new FlatForward(today, 0.02, new Actual360());

        double oldQuote = Helper(new FlatForward(today, 0.02, new Actual365Fixed()))
            .SetTermStructure(bootstrapCurve).ImpliedQuote(context);

        // 3%
        double newQuote = Helper(new FlatForward(today, 0.03, new Actual365Fixed()))
            .SetTermStructure(bootstrapCurve).ImpliedQuote(context);

        Assert.NotEqual(oldQuote, newQuote);
    }

    [Fact]
    public void TestConstNotionalHelperFloatingLegConvention()
    {
        // Testing the floating-leg convention of fixed-vs-floating helpers...

        var today = new Date(29, July, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        Calendar calendar = new WeekendsOnly();
        YieldTermStructure collateralCurve = new FlatForward(today, 0.04, new Actual360());

        // An overnight index reports Following regardless of the market it models, so the helper convention has to
        // drive both legs.
        var overnightIndex = new OvernightIndex(
            "ON", 2, new USDCurrency(), calendar, new Actual360(), collateralCurve);
        Assert.Equal(BusinessDayConvention.Following, overnightIndex.BusinessDayConvention);

        // The helper convention alone, as passed by an ordinary caller, must reach the floating leg: 31st July 2027 is
        // a Saturday, so ModifiedFollowing rolls back to the 30th instead of forward into August.
        var helper = new ConstNotionalCrossCurrencySwapRateHelper(
            0.0,
            new Period(1, TimeUnit.Years),
            2,
            calendar,
            BusinessDayConvention.ModifiedFollowing,
            false,
            Frequency.Semiannual,
            new Actual365Fixed(),
            overnightIndex,
            collateralCurve,
            false,
            context,
            0,
            null,
            Frequency.Semiannual);

        var expectedMaturity = new Date(30, July, 2027);
        Assert.Equal(expectedMaturity, helper.MaturityDate);
        Assert.Equal(expectedMaturity, helper.Swap.FixedSchedule.EndDate());
        Assert.Equal(expectedMaturity, helper.Swap.FloatSchedule.EndDate());
        Assert.Equal(BusinessDayConvention.ModifiedFollowing, helper.Swap.FixedPaymentBdc);
        Assert.Equal(BusinessDayConvention.ModifiedFollowing, helper.Swap.FloatPaymentBdc);
    }

    [Fact]
    public void TestConstNotionalHelperIgnoresIndexFixingCalendar()
    {
        // Testing that fixed-vs-floating helpers roll both legs on the helper calendar...

        var today = new Date(1, July, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        Calendar calendar = new WeekendsOnly();
        YieldTermStructure collateralCurve = new FlatForward(today, 0.04, new Actual360());

        // Same index in every respect but the fixing calendar. A fixing calendar governs where the rate fixes, not
        // where the accrual schedule rolls, so neither the schedules nor the pillar may depend on it: 3rd July 2027 is
        // a Saturday and the 5th is the observed US Independence Day, so leaking the index calendar into the schedule
        // would push the floating leg to the 6th.
        var weekendIndex = new OvernightIndex(
            "ON weekend", 2, new USDCurrency(), calendar, new Actual360(), collateralCurve);
        var usIndex = new OvernightIndex(
            "ON US",
            2,
            new USDCurrency(),
            new UnitedStates(UnitedStatesMarket.GovernmentBond),
            new Actual360(),
            collateralCurve);
        Assert.True(usIndex.FixingCalendar.IsHoliday(new Date(5, July, 2027)));

        ConstNotionalCrossCurrencySwapRateHelper MakeHelper(IborIndex index) => new(
            0.0,
            new Period(1, TimeUnit.Years),
            2,
            calendar,
            BusinessDayConvention.ModifiedFollowing,
            false,
            Frequency.Semiannual,
            new Actual365Fixed(),
            index,
            collateralCurve,
            false,
            context,
            0,
            null,
            Frequency.Semiannual);
        ConstNotionalCrossCurrencySwapRateHelper weekendHelper = MakeHelper(weekendIndex);
        ConstNotionalCrossCurrencySwapRateHelper usHelper = MakeHelper(usIndex);

        var expectedMaturity = new Date(5, July, 2027);
        Assert.Equal(expectedMaturity, weekendHelper.Swap.FloatSchedule.EndDate());
        Assert.Equal(expectedMaturity, usHelper.Swap.FloatSchedule.EndDate());
        Assert.Equal(expectedMaturity, usHelper.Swap.FixedSchedule.EndDate());
        Assert.Equal(weekendHelper.MaturityDate, usHelper.MaturityDate);

        // The index keeps its own calendar for its fixings.
        Assert.NotEqual(weekendIndex.FixingCalendar, usIndex.FixingCalendar);
    }

    [Fact]
    public void TestConstNotionalHelperFloatingPaymentFrequency()
    {
        // Testing the floating-leg payment frequency of fixed-vs-floating helpers...

        var today = new Date(15, July, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        Calendar calendar = new TARGET();
        YieldTermStructure collateralCurve = new FlatForward(today, 0.04, new Actual360());
        var overnightIndex = new OvernightIndex(
            "ON", 0, new EURCurrency(), calendar, new Actual360(), collateralCurve);
        var euribor3m = new Euribor3M(collateralCurve);

        ConstNotionalCrossCurrencySwapRateHelper MakeHelper(IborIndex index, Frequency? floatFrequency) => new(
            0.01,
            new Period(1, TimeUnit.Years),
            2,
            calendar,
            BusinessDayConvention.Following,
            false,
            Frequency.Annual,
            new Actual365Fixed(),
            index,
            collateralCurve,
            false,
            context,
            0,
            null,
            floatFrequency);

        // An overnight index has a one-day tenor, which is not a payment frequency; asking it for one would silently
        // produce a daily-paying leg.
        Assert.Throws<ArgumentException>(() => MakeHelper(overnightIndex, null));

        // Given a frequency, the overnight leg pays on that schedule.
        ConstNotionalCrossCurrencySwapRateHelper overnightHelper = MakeHelper(overnightIndex, Frequency.Semiannual);
        Assert.Equal(3, overnightHelper.Swap.FloatSchedule.Size);
        Assert.Equal(2, overnightHelper.Swap.FixedSchedule.Size);

        // An ibor index still falls back to its own tenor.
        ConstNotionalCrossCurrencySwapRateHelper iborHelper = MakeHelper(euribor3m, null);
        Assert.Equal(5, iborHelper.Swap.FloatSchedule.Size);

        // An explicit frequency overrides the ibor tenor.
        ConstNotionalCrossCurrencySwapRateHelper iborSemiannualHelper = MakeHelper(euribor3m, Frequency.Semiannual);
        Assert.Equal(3, iborSemiannualHelper.Swap.FloatSchedule.Size);

        // NoFrequency is a synonym for an unset value, as in the basis helpers.
        ConstNotionalCrossCurrencySwapRateHelper iborNoFrequencyHelper = MakeHelper(euribor3m, Frequency.NoFrequency);
        Assert.Equal(5, iborNoFrequencyHelper.Swap.FloatSchedule.Size);
    }

    [Fact]
    public void TestPaymentLagDoesNotDelayNotionalExchanges()
    {
        // Testing that payment lag applies to coupons but not notional exchanges...

        var today = new Date(15, July, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        Calendar cal = new TARGET();
        YieldTermStructure collateralCurve = new FlatForward(today, 0.02, new Actual365Fixed());
        YieldTermStructure forwardingCurve = new FlatForward(today, 0.017, new Actual365Fixed());
        var euribor3m = new Euribor3M(forwardingCurve);

        var helper = new ConstNotionalCrossCurrencySwapRateHelper(
            0.02,
            new Period(3, TimeUnit.Months),
            2,
            cal,
            BusinessDayConvention.Following,
            false,
            Frequency.Quarterly,
            new Actual360(),
            euribor3m,
            collateralCurve,
            false,
            context,
            2);

        var effectiveDate = new Date(17, July, 2026);
        var maturityDate = new Date(19, October, 2026);
        var couponPaymentDate = new Date(21, October, 2026);

        ConstNotionalCrossCurrencyFixedVsFloatingSwap swap = helper.Swap;
        foreach (ImmutableArray<CashFlow> leg in swap.Legs)
        {
            Assert.Equal(3, leg.Length);
            Assert.Equal(effectiveDate, leg[0].Date);
            Assert.Equal(maturityDate, leg[1].Date);
            Assert.Equal(couponPaymentDate, leg[2].Date);
        }

        Assert.Equal(effectiveDate, helper.EarliestDate);
        Assert.Equal(maturityDate, helper.MaturityDate);
        Assert.Equal(couponPaymentDate, helper.LatestDate);
    }

    [Fact]
    public void TestBasisHelpersWithPaymentLagMatchUnderlyingSwaps()
    {
        // Testing payment-lagged basis helpers against their underlying swaps...

        var vars = new CommonVars();
        YieldTermStructure collateralCurve = vars.QuoteCcyIdxHandle;
        YieldTermStructure bootstrappedCurve = vars.BaseCcyIdxHandle;
        double quote = -20.0 * vars.BasisPoint;

        var constantHelper = new ConstNotionalCrossCurrencyBasisSwapRateHelper(
            quote,
            new Period(5, TimeUnit.Years),
            vars.InstrumentSettlementDays,
            vars.Calendar,
            vars.BusinessConvention,
            vars.EndOfMonth,
            vars.BaseCcyIdx,
            vars.QuoteCcyIdx,
            collateralCurve,
            false,
            true,
            vars.Context,
            null,
            2).SetTermStructure(bootstrappedCurve);

        double fairPaySpread = DiscountingConstNotionalCrossCurrencySwapEngine.Calculate(
            constantHelper.Swap,
            vars.Context,
            vars.QuoteCcyIdx.Currency,
            collateralCurve,
            vars.BaseCcyIdx.Currency,
            bootstrappedCurve,
            1.0,
            includeSettlementDateFlows: true).FairPaySpread!.Value;
        CheckSmall(constantHelper.ImpliedQuote(vars.Context) - fairPaySpread, 1.0e-12);

        var resettingHelper = new MtMCrossCurrencyBasisSwapRateHelper(
            quote,
            new Period(5, TimeUnit.Years),
            vars.InstrumentSettlementDays,
            vars.Calendar,
            vars.BusinessConvention,
            vars.EndOfMonth,
            vars.BaseCcyIdx,
            vars.QuoteCcyIdx,
            collateralCurve,
            false,
            true,
            false,
            vars.Context,
            null,
            2).SetTermStructure(bootstrappedCurve);

        double fairFxBaseSpread = DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate(
            resettingHelper.Swap,
            vars.Context,
            vars.QuoteCcyIdx.Currency,
            collateralCurve,
            vars.BaseCcyIdx.Currency,
            bootstrappedCurve,
            1.0,
            includeSettlementDateFlows: true).FairFxBaseSpread!.Value;
        CheckSmall(resettingHelper.ImpliedQuote(vars.Context) - fairFxBaseSpread, 1.0e-12);
    }

    [Fact]
    public void TestConstNotionalHelperCollateralOnFixedLeg()
    {
        // Testing const-notional CCS helper with collateral on fixed leg...
        TestConstNotionalHelperCollateral(true, 0);
    }

    [Fact]
    public void TestConstNotionalHelperCollateralOnFloatingLeg()
    {
        // Testing const-notional CCS helper with collateral on floating leg...
        TestConstNotionalHelperCollateral(false, 5);
    }

    [Fact]
    public void TestResettingBasisSwapsWithInterpolatedStubIndex()
    {
        // Testing MtM cross-currency bootstrap with an interpolated stub-index coupon...

        var today = new Date(27, May, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        Calendar calendar = new NewZealand();
        DayCounter dayCount = new Actual365Fixed();

        // Distinct projection curves so the interpolated stub fixing differs measurably from the leg index's own
        // fixing.
        YieldTermStructure shortProjection = new FlatForward(today, 0.02, dayCount);
        YieldTermStructure baseProjection = new FlatForward(today, 0.04, dayCount);
        YieldTermStructure usdProjection = new FlatForward(today, 0.015, new Actual360());

        var bkbm2m = new Bkbm2M(shortProjection);
        var bkbm3m = new Bkbm3M(baseProjection);
        var sofr = new Sofr(usdProjection);

        var stubIndexSelection = new StubIndexSelection(StubIndexSelectionConvention.Interpolated, [bkbm2m, bkbm3m]);

        YieldTermStructure collateralHandle = usdProjection;

        MtMCrossCurrencyBasisSwapRateHelper MakeHelper(
            double basis, Period tenor, StubIndexSelection? selection, bool isFxBaseCurrencyLegResettable) => new(
            basis,
            tenor,
            2,
            calendar,
            BusinessDayConvention.ModifiedFollowing,
            false,
            bkbm3m,
            sofr,
            collateralHandle,
            false, // collateral in quote currency
            true, // basis on the base-currency leg
            isFxBaseCurrencyLegResettable,
            context,
            Frequency.Quarterly,
            0,
            Frequency.Quarterly,
            0,
            null,
            true,
            selection);

        foreach (bool isFxBaseCurrencyLegResettable in new[] { false, true })
        {
            // The 9M swap's spot start (29 May 2026) plus 9M lands on 28 Feb 2027, so the backward quarterly schedule
            // leaves a broken first period.
            MtMCrossCurrencyBasisSwapRateHelper stubHelper = MakeHelper(
                -7e-4, new Period(9, TimeUnit.Months), stubIndexSelection, isFxBaseCurrencyLegResettable);

            CheckStubCoupon(stubHelper.Swap.Legs[0], bkbm2m, bkbm3m, context);
            var stubCoupon = (StubIborCoupon)FirstIborCoupon(stubHelper.Swap.Legs[0])!;
            Assert.True(Math.Abs(
                stubCoupon.IndexFixing(context) - bkbm3m.Fixing(stubCoupon.FixingDate(), context)) > 1e-4);

            // Regular periods are untouched: only the broken 9M front period gets a stub coupon, so the 6M and 1Y
            // helpers price identically either way.
            BootstrapHelper<YieldTermStructure>[] instruments =
            [
                MakeHelper(-5e-4, new Period(6, TimeUnit.Months), stubIndexSelection, isFxBaseCurrencyLegResettable),
                stubHelper,
                MakeHelper(-9e-4, new Period(1, TimeUnit.Years), stubIndexSelection, isFxBaseCurrencyLegResettable),
            ];

            YieldTermStructure stubCurve = BootstrapAndCheck(instruments, today, dayCount, context);

            // The same market priced with the default current-index convention must bootstrap to a measurably
            // different curve at the broken pillar.
            BootstrapHelper<YieldTermStructure>[] plainInstruments =
            [
                MakeHelper(-5e-4, new Period(6, TimeUnit.Months), null, isFxBaseCurrencyLegResettable),
                MakeHelper(-7e-4, new Period(9, TimeUnit.Months), null, isFxBaseCurrencyLegResettable),
                MakeHelper(-9e-4, new Period(1, TimeUnit.Years), null, isFxBaseCurrencyLegResettable),
            ];

            YieldTermStructure plainCurve = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
                today, plainInstruments, dayCount, context).EnableExtrapolation();

            Date brokenPillar = stubHelper.MaturityDate!.Value;
            Assert.True(Math.Abs(stubCurve.Discount(brokenPillar) - plainCurve.Discount(brokenPillar)) > 1e-6);
        }
    }

    [Fact]
    public void TestConstNotionalBasisSwapsWithInterpolatedStubIndex()
    {
        // Testing const-notional cross-currency bootstrap with an interpolated stub-index coupon...

        var today = new Date(27, May, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        Calendar calendar = new NewZealand();
        DayCounter dayCount = new Actual365Fixed();

        YieldTermStructure shortProjection = new FlatForward(today, 0.02, dayCount);
        YieldTermStructure baseProjection = new FlatForward(today, 0.04, dayCount);
        YieldTermStructure usdProjection = new FlatForward(today, 0.015, new Actual360());

        var bkbm2m = new Bkbm2M(shortProjection);
        var bkbm3m = new Bkbm3M(baseProjection);
        var sofr = new Sofr(usdProjection);

        var stubIndexSelection = new StubIndexSelection(StubIndexSelectionConvention.Interpolated, [bkbm2m, bkbm3m]);

        YieldTermStructure collateralHandle = usdProjection;

        ConstNotionalCrossCurrencyBasisSwapRateHelper MakeHelper(
            double basis, Period tenor, StubIndexSelection? selection) => new(
            basis,
            tenor,
            2,
            calendar,
            BusinessDayConvention.ModifiedFollowing,
            false,
            bkbm3m,
            sofr,
            collateralHandle,
            false, // collateral in quote currency
            true, // basis on the base-currency leg
            context,
            Frequency.Quarterly,
            0,
            Frequency.Quarterly,
            true,
            false,
            selection);

        // As in the MtM test, the 9M helper's backward quarterly schedule leaves a broken first period.
        ConstNotionalCrossCurrencyBasisSwapRateHelper stubHelper = MakeHelper(
            -7e-4, new Period(9, TimeUnit.Months), stubIndexSelection);

        CheckStubCoupon(stubHelper.Swap.Legs[0], bkbm2m, bkbm3m, context);

        BootstrapHelper<YieldTermStructure>[] instruments =
        [
            MakeHelper(-5e-4, new Period(6, TimeUnit.Months), stubIndexSelection),
            stubHelper,
            MakeHelper(-9e-4, new Period(1, TimeUnit.Years), stubIndexSelection),
        ];

        YieldTermStructure stubCurve = BootstrapAndCheck(instruments, today, dayCount, context);

        BootstrapHelper<YieldTermStructure>[] plainInstruments =
        [
            MakeHelper(-5e-4, new Period(6, TimeUnit.Months), null),
            MakeHelper(-7e-4, new Period(9, TimeUnit.Months), null),
            MakeHelper(-9e-4, new Period(1, TimeUnit.Years), null),
        ];

        YieldTermStructure plainCurve = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            today, plainInstruments, dayCount, context).EnableExtrapolation();

        Date brokenPillar = stubHelper.MaturityDate!.Value;
        Assert.True(Math.Abs(stubCurve.Discount(brokenPillar) - plainCurve.Discount(brokenPillar)) > 1e-6);
    }

    [Fact]
    public void TestBasisSwapsWithQuoteLegStubIndex()
    {
        // Testing cross-currency bootstrap with an interpolated stub on the quote leg...

        var today = new Date(27, May, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        Calendar calendar = new NewZealand();
        DayCounter dayCount = new Actual365Fixed();

        YieldTermStructure shortProjection = new FlatForward(today, 0.02, dayCount);
        YieldTermStructure quoteProjection = new FlatForward(today, 0.04, dayCount);
        YieldTermStructure usdProjection = new FlatForward(today, 0.015, new Actual360());

        var bkbm2m = new Bkbm2M(shortProjection);
        var bkbm3m = new Bkbm3M(quoteProjection);
        var sofr = new Sofr(usdProjection);

        var quoteConfig = new StubIndexSelection(StubIndexSelectionConvention.Interpolated, [bkbm2m, bkbm3m]);

        // collateral in the base currency (USD), so the bootstrapped curve discounts the quote-currency (NZD) leg
        YieldTermStructure collateralHandle = usdProjection;

        // MtM helper, both resettable-leg choices
        foreach (bool isFxBaseCurrencyLegResettable in new[] { false, true })
        {
            MtMCrossCurrencyBasisSwapRateHelper MakeHelper(double basis, Period tenor) => new(
                basis,
                tenor,
                2,
                calendar,
                BusinessDayConvention.ModifiedFollowing,
                false,
                sofr,
                bkbm3m,
                collateralHandle,
                true, // collateral in base currency
                true, // basis on the base-currency leg
                isFxBaseCurrencyLegResettable,
                context,
                Frequency.Quarterly,
                0,
                Frequency.Quarterly,
                0,
                null,
                true,
                new StubIndexSelection(),
                quoteConfig);

            // as in the base-leg tests, the 9M helper's backward quarterly schedule leaves a broken first period
            MtMCrossCurrencyBasisSwapRateHelper stubHelper = MakeHelper(-7e-4, new Period(9, TimeUnit.Months));
            CheckStubCoupon(stubHelper.Swap.Legs[1], bkbm2m, bkbm3m, context);

            BootstrapAndCheck(
                [MakeHelper(-5e-4, new Period(6, TimeUnit.Months)), stubHelper,
                    MakeHelper(-9e-4, new Period(1, TimeUnit.Years))],
                today,
                dayCount,
                context);
        }

        // Const-notional helper
        ConstNotionalCrossCurrencyBasisSwapRateHelper MakeCNHelper(double basis, Period tenor) => new(
            basis,
            tenor,
            2,
            calendar,
            BusinessDayConvention.ModifiedFollowing,
            false,
            sofr,
            bkbm3m,
            collateralHandle,
            true, // collateral in base currency
            true, // basis on the base-currency leg
            context,
            Frequency.Quarterly,
            0,
            Frequency.Quarterly,
            true,
            false,
            new StubIndexSelection(),
            quoteConfig);

        ConstNotionalCrossCurrencyBasisSwapRateHelper cnStubHelper =
            MakeCNHelper(-7e-4, new Period(9, TimeUnit.Months));
        CheckStubCoupon(cnStubHelper.Swap.Legs[1], bkbm2m, bkbm3m, context);

        BootstrapAndCheck(
            [MakeCNHelper(-5e-4, new Period(6, TimeUnit.Months)), cnStubHelper,
                MakeCNHelper(-9e-4, new Period(1, TimeUnit.Years))],
            today,
            dayCount,
            context);
    }

    [Fact]
    public void TestFixedVsFloatingSwapsWithFloatLegStubIndex()
    {
        // Testing fixed-vs-floating cross-currency bootstrap with an interpolated float-leg stub...

        var today = new Date(27, May, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        Calendar calendar = new NewZealand();
        DayCounter dayCount = new Actual365Fixed();

        YieldTermStructure shortProjection = new FlatForward(today, 0.02, dayCount);
        YieldTermStructure floatProjection = new FlatForward(today, 0.04, dayCount);
        YieldTermStructure usdCollateral = new FlatForward(today, 0.015, new Actual360());

        var bkbm2m = new Bkbm2M(shortProjection);
        var bkbm3m = new Bkbm3M(floatProjection);

        var floatConfig = new StubIndexSelection(StubIndexSelectionConvention.Interpolated, [bkbm2m, bkbm3m]);

        YieldTermStructure collateralHandle = usdCollateral;

        ConstNotionalCrossCurrencySwapRateHelper MakeHelper(
            double fixedRate, Period tenor, StubIndexSelection selection) =>
            new(
                fixedRate,
                tenor,
                2,
                calendar,
                BusinessDayConvention.ModifiedFollowing,
                false,
                Frequency.Quarterly,
                new Actual365Fixed(),
                bkbm3m,
                collateralHandle,
                true, // collateral on the fixed leg; the float-leg discount curve is bootstrapped
                context,
                0,
                true,
                null,
                selection);

        // as in the basis-swap tests, the 9M helper's backward quarterly schedule leaves a broken first period on the
        // floating leg
        ConstNotionalCrossCurrencySwapRateHelper stubHelper =
            MakeHelper(0.032, new Period(9, TimeUnit.Months), floatConfig);

        // the helper's swap pays fixed, so the floating leg is leg(1)
        CheckStubCoupon(stubHelper.Swap.Leg(1), bkbm2m, bkbm3m, context);

        BootstrapAndCheck(
            [MakeHelper(0.030, new Period(6, TimeUnit.Months), floatConfig), stubHelper,
                MakeHelper(0.034, new Period(1, TimeUnit.Years), floatConfig)],
            today,
            dayCount,
            context);
    }

    private static IborCoupon? FirstIborCoupon(ImmutableArray<CashFlow> leg)
    {
        foreach (CashFlow cashflow in leg)
        {
            if (cashflow is IborCoupon coupon)
            {
                return coupon;
            }

            if (cashflow is FxResetCoupon { Underlying: IborCoupon underlying })
            {
                return underlying;
            }
        }

        return null;
    }

    private static void CheckIndexedCouponMode(ImmutableArray<ImmutableArray<CashFlow>> legs, bool expectedIndexed)
    {
        int checkedLegs = 0;
        foreach (ImmutableArray<CashFlow> leg in legs)
        {
            IborCoupon? coupon = FirstIborCoupon(leg);
            if (coupon is not null)
            {
                ++checkedLegs;
                var pricer = coupon.Pricer as IborCouponPricer;
                Assert.NotNull(pricer);
                Assert.Equal(expectedIndexed, pricer.UseIndexedCoupon);
            }
        }

        Assert.True(checkedLegs > 0);
    }

    // QuantLib's IborCoupon::Settings, a global the case switches, is the context's at-par switch.
    private static void CheckCrossCurrencyHelperCouponModes(
        bool usingAtParCoupons, bool? useIndexedCoupons, bool expectedIndexed)
    {
        var vars = new CommonVars(usingAtParCoupons);
        YieldTermStructure collateralCurve = vars.QuoteCcyIdxHandle;
        double quote = -20.0 * vars.BasisPoint;

        var constantHelper = new ConstNotionalCrossCurrencyBasisSwapRateHelper(
            quote,
            new Period(2, TimeUnit.Years),
            vars.InstrumentSettlementDays,
            vars.Calendar,
            vars.BusinessConvention,
            vars.EndOfMonth,
            vars.BaseCcyIdx,
            vars.QuoteCcyIdx,
            collateralCurve,
            false,
            true,
            vars.Context,
            Frequency.Semiannual,
            0,
            Frequency.Semiannual,
            useIndexedCoupons);
        CheckIndexedCouponMode(constantHelper.Swap.Legs, expectedIndexed);

        var mtmHelper = new MtMCrossCurrencyBasisSwapRateHelper(
            quote,
            new Period(2, TimeUnit.Years),
            vars.InstrumentSettlementDays,
            vars.Calendar,
            vars.BusinessConvention,
            vars.EndOfMonth,
            vars.BaseCcyIdx,
            vars.QuoteCcyIdx,
            collateralCurve,
            false,
            true,
            false,
            vars.Context,
            Frequency.Semiannual,
            0,
            Frequency.Semiannual,
            0,
            null,
            useIndexedCoupons);
        CheckIndexedCouponMode(mtmHelper.Swap.Legs, expectedIndexed);

        var fixedVsFloatingHelper = new ConstNotionalCrossCurrencySwapRateHelper(
            0.01,
            new Period(2, TimeUnit.Years),
            vars.InstrumentSettlementDays,
            vars.Calendar,
            vars.BusinessConvention,
            vars.EndOfMonth,
            Frequency.Annual,
            new Thirty360(Thirty360Convention.BondBasis),
            vars.QuoteCcyIdx,
            collateralCurve,
            false,
            vars.Context,
            0,
            useIndexedCoupons);
        CheckIndexedCouponMode(fixedVsFloatingHelper.Swap.Legs, expectedIndexed);
    }

    private static void TestConstantNotionalCrossCurrencySwapsNPV(
        bool isFxBaseCurrencyCollateralCurrency, bool isBasisOnFxBaseCurrencyLeg)
    {
        var vars = new CommonVars();

        YieldTermStructure collateralHandle =
            isFxBaseCurrencyCollateralCurrency ? vars.BaseCcyIdxHandle : vars.QuoteCcyIdxHandle;

        ConstNotionalCrossCurrencyBasisSwapRateHelper[] instruments = vars.BuildConstantNotionalXccyRateHelpers(
            vars.BasisData, collateralHandle, isFxBaseCurrencyCollateralCurrency, isBasisOnFxBaseCurrencyLeg);
        YieldTermStructure foreignCcyHandle = vars.Curve(instruments);

        double tolerance = 1.0e-12;

        foreach (XccyTestDatum quote in vars.BasisData)
        {
            Swap[] xccySwapProxy = vars.BuildXccyBasisSwap(quote, vars.FxSpot, isBasisOnFxBaseCurrencyLeg);

            YieldTermStructure baseCurve = isFxBaseCurrencyCollateralCurrency ? collateralHandle : foreignCcyHandle;
            YieldTermStructure quoteCurve = isFxBaseCurrencyCollateralCurrency ? foreignCcyHandle : collateralHandle;

            var p = new Period(quote.N, quote.Units);

            double baseCcyLegNpv = vars.FxSpot
                * DiscountingSwapEngine.Calculate(xccySwapProxy[0], vars.Context, baseCurve).NPV!.Value;
            double quoteCcyLegNpv =
                DiscountingSwapEngine.Calculate(xccySwapProxy[1], vars.Context, quoteCurve).NPV!.Value;
            double npv = baseCcyLegNpv + quoteCcyLegNpv;

            Assert.True(
                Math.Abs(npv) <= tolerance,
                "unable to price the cross currency basis swap to par\n" +
                $"    calculated NPV:    {npv}\n    expected:    {0.0}\n    implied basis:    {quote.Basis}\n" +
                $"    tenor:    {p}\n");
        }
    }

    private static void TestResettingCrossCurrencySwaps(
        bool isFxBaseCurrencyCollateralCurrency,
        bool isBasisOnFxBaseCurrencyLeg,
        bool isFxBaseCurrencyLegResettable,
        Frequency? paymentFrequency = null,
        int paymentLag = 0,
        bool useOvernightIndex = false,
        Frequency? quoteCcyPaymentFrequency = null)
    {
        var vars = new CommonVars();

        YieldTermStructure collateralHandle =
            isFxBaseCurrencyCollateralCurrency ? vars.BaseCcyIdxHandle : vars.QuoteCcyIdxHandle;

        MtMCrossCurrencyBasisSwapRateHelper[] resettingInstruments = vars.BuildResettingXccyRateHelpers(
            vars.BasisData,
            collateralHandle,
            isFxBaseCurrencyCollateralCurrency,
            isBasisOnFxBaseCurrencyLeg,
            isFxBaseCurrencyLegResettable,
            paymentFrequency,
            paymentLag,
            useOvernightIndex,
            quoteCcyPaymentFrequency);

        ConstNotionalCrossCurrencyBasisSwapRateHelper[] constNotionalInstruments =
            vars.BuildConstantNotionalXccyRateHelpers(
                vars.BasisData, collateralHandle, isFxBaseCurrencyCollateralCurrency, isBasisOnFxBaseCurrencyLeg);

        YieldTermStructure resettingCurve = vars.Curve(resettingInstruments);
        YieldTermStructure constNotionalCurve = vars.Curve(constNotionalInstruments);

        double tolerance = 1.0e-4 * 5;

        for (int i = 0; i < vars.BasisData.Length; ++i)
        {
            Date maturity = resettingInstruments[i].MaturityDate!.Value;
            double resettingZero = resettingCurve.ZeroRate(maturity, vars.DayCount, Compounding.Continuous).Rate;
            double constNotionalZero =
                constNotionalCurve.ZeroRate(maturity, vars.DayCount, Compounding.Continuous).Rate;

            // The difference between resetting and constant notional curves is not expected to be substantial. With
            // the current setup it should amount to only a few basis points - hence the tolerance level was set at 5
            // bps.
            Assert.True(
                Math.Abs(resettingZero - constNotionalZero) <= tolerance,
                "too large difference between resetting and constant notional curve \n" +
                $"    zero from resetting curve:    {resettingZero}\n" +
                $"    zero from const notional curve:    {constNotionalZero}\n    maturity:    {maturity}\n");
        }
    }

    private static void TestConstNotionalHelperCollateral(bool collateralOnFixedLeg, int paymentLag)
    {
        var today = new Date(20, March, 2030);
        var context = new EvaluationContext { EvaluationDate = today };

        YieldTermStructure usdCollat = new FlatForward(today, 0.02, new Actual365Fixed());
        YieldTermStructure eurFwd = new FlatForward(today, 0.017, new Actual365Fixed());

        IborIndex euribor3m = new Euribor3M(eurFwd);

        int fixingDays = 5;
        Calendar cal = new TARGET();
        var bdc = BusinessDayConvention.Following;
        bool endOfMonth = true;
        var fixedFreq = Frequency.Annual;
        DayCounter fixedDC = new Thirty360(Thirty360Convention.BondBasis);

        (Period Tenor, double Quote)[] quotes =
        [
            (new Period(5, TimeUnit.Years), 0.018),
            (new Period(7, TimeUnit.Years), 0.019),
            (new Period(10, TimeUnit.Years), 0.022),
            (new Period(15, TimeUnit.Years), 0.024),
            (new Period(20, TimeUnit.Years), 0.028),
        ];

        ConstNotionalCrossCurrencySwapRateHelper[] helpers =
        [
            .. quotes.Select(q => new ConstNotionalCrossCurrencySwapRateHelper(
                q.Quote,
                q.Tenor,
                fixingDays,
                cal,
                bdc,
                endOfMonth,
                fixedFreq,
                fixedDC,
                euribor3m,
                usdCollat,
                collateralOnFixedLeg,
                context,
                paymentLag)),
        ];

        YieldTermStructure curveHandle = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            today, helpers, new Actual365Fixed(), context).EnableExtrapolation();

        YieldTermStructure fixedCurve = collateralOnFixedLeg ? usdCollat : curveHandle;
        YieldTermStructure floatCurve = collateralOnFixedLeg ? curveHandle : usdCollat;

        foreach ((Period tenor, double q) in quotes)
        {
            Date settlement = cal.Advance(today, fixingDays, TimeUnit.Days);
            Date maturity = cal.Advance(settlement, tenor, bdc, endOfMonth);

            var fixedSched = new Schedule(
                settlement,
                maturity,
                new Period(fixedFreq),
                cal,
                bdc,
                bdc,
                DateGenerationRule.Forward,
                endOfMonth,
                context);

            // The helper rolls both legs on its own calendar and convention; the index only supplies its fixing
            // calendar for fixings.
            var floatSched = new Schedule(
                settlement, maturity, euribor3m.Tenor, cal, bdc, bdc, DateGenerationRule.Forward, false, context);

            FixedRateLeg fixedBuilder = new FixedRateLeg(fixedSched)
                .WithNotionals(1.0)
                .WithCouponRates(q, fixedDC);
            IborLeg floatBuilder = new IborLeg(floatSched, euribor3m)
                .WithNotionals(1.0)
                .WithSpreads(0.0);
            if (paymentLag != 0)
            {
                fixedBuilder = fixedBuilder
                    .WithPaymentLag(paymentLag)
                    .WithPaymentAdjustment(bdc)
                    .WithPaymentCalendar(cal);
                floatBuilder = floatBuilder
                    .WithPaymentLag(paymentLag)
                    .WithPaymentAdjustment(bdc)
                    .WithPaymentCalendar(cal);
            }

            ImmutableArray<CashFlow> fixedLeg = fixedBuilder.ToLeg();
            ImmutableArray<CashFlow> floatLeg = floatBuilder.ToLeg(context);

            Date initialPaymentDate = CashFlows.StartDate(fixedLeg);
            fixedLeg = [new SimpleCashFlow(-1.0, initialPaymentDate), .. fixedLeg];
            floatLeg = [new SimpleCashFlow(-1.0, initialPaymentDate), .. floatLeg];

            Date finalPaymentDate = CashFlows.MaturityDate(fixedLeg);
            fixedLeg = [.. fixedLeg, new SimpleCashFlow(1.0, finalPaymentDate)];
            floatLeg = [.. floatLeg, new SimpleCashFlow(1.0, finalPaymentDate)];

            var fixedProxy = new Swap([fixedLeg], [true]);
            var floatProxy = new Swap([floatLeg], [false]);

            double npv = DiscountingSwapEngine.Calculate(fixedProxy, context, fixedCurve).NPV!.Value
                + DiscountingSwapEngine.Calculate(floatProxy, context, floatCurve).NPV!.Value;
            double tolerance = 1e-10;

            CheckSmall(npv, tolerance);
        }
    }

    // The leg's first Ibor coupon is a stub coupon whose fixing interpolates the two indexes' fixings, linearly in the
    // days to its accrual end.
    private static void CheckStubCoupon(
        ImmutableArray<CashFlow> leg, IborIndex shortIndex, IborIndex longIndex, EvaluationContext context)
    {
        var stubCoupon = FirstIborCoupon(leg) as StubIborCoupon;
        Assert.NotNull(stubCoupon);

        Date fixingDate = stubCoupon.FixingDate();
        Date valueDate = stubCoupon.AccrualStartDate;
        Date target = stubCoupon.AccrualEndDate;
        Date shortMaturity = shortIndex.MaturityDate(valueDate);
        Date longMaturity = longIndex.MaturityDate(valueDate);
        Assert.True(shortMaturity < target && target < longMaturity);

        double shortRate = shortIndex.Fixing(fixingDate, context);
        double longRate = longIndex.Fixing(fixingDate, context);
        double weight = (double)(target - shortMaturity) / (longMaturity - shortMaturity);
        double expected = shortRate + ((longRate - shortRate) * weight);
        CheckSmall(stubCoupon.IndexFixing(context) - expected, 1e-14);
    }

    // The curve on the helpers, each helper's implied quote on it its quote.
    private static YieldTermStructure BootstrapAndCheck(
        BootstrapHelper<YieldTermStructure>[] instruments, Date today, DayCounter dayCount, EvaluationContext context)
    {
        YieldTermStructure curve = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            today, instruments, dayCount, context).EnableExtrapolation();
        curve.Discount(1.0);
        foreach (BootstrapHelper<YieldTermStructure> instrument in instruments)
        {
            CheckSmall(instrument.SetTermStructure(curve).ImpliedQuote(context) - instrument.Quote, 1e-10);
        }

        return curve;
    }

    // QL_CHECK_SMALL, Boost's BOOST_CHECK_SMALL: |value| within the tolerance.
    private static void CheckSmall(double value, double tolerance) =>
        Assert.True(Math.Abs(value) <= tolerance, $"{value} is not small, tolerance {tolerance}");

    private sealed record XccyTestDatum(int N, TimeUnit Units, double Basis);

    private sealed class CommonVars
    {
        public CommonVars(bool? usingAtParCoupons = null)
        {
            Calendar = new TARGET();
            DayCount = new Actual365Fixed();

            Today = Calendar.Adjust(new Date(6, September, 2013));
            Context = usingAtParCoupons is bool atPar
                ? new EvaluationContext { EvaluationDate = Today, UseAtParCoupons = atPar }
                : new EvaluationContext { EvaluationDate = Today };

            InstrumentSettlementDt = Calendar.Advance(Today, InstrumentSettlementDays, TimeUnit.Days);
            CurveSettlementDt = Calendar.Advance(Today, CurveSettlementDays, TimeUnit.Days);

            BaseCcyIdxHandle = new FlatForward(CurveSettlementDt, 0.007, DayCount);
            QuoteCcyIdxHandle = new FlatForward(CurveSettlementDt, 0.015, DayCount);

            BaseCcyIdx = new Euribor3M(BaseCcyIdxHandle);
            QuoteCcyIdx = new USDLibor(new Period(3, TimeUnit.Months), QuoteCcyIdxHandle);
            BaseOvernightIndex = new Eonia(BaseCcyIdxHandle);
            QuoteOvernightIndex = new Sofr(QuoteCcyIdxHandle);
        }

        public double BasisPoint => 1.0e-4;

        public double FxSpot => 1.25;

        public int InstrumentSettlementDays => 2;

        public int CurveSettlementDays => 0;

        public Date Today { get; }

        public Date InstrumentSettlementDt { get; }

        public Date CurveSettlementDt { get; }

        public Calendar Calendar { get; }

        public BusinessDayConvention BusinessConvention => BusinessDayConvention.Following;

        public DayCounter DayCount { get; }

        public bool EndOfMonth => false;

        public EvaluationContext Context { get; }

        public IborIndex BaseCcyIdx { get; }

        public IborIndex QuoteCcyIdx { get; }

        public IborIndex QuoteOvernightIndex { get; }

        public IborIndex BaseOvernightIndex { get; }

        public YieldTermStructure BaseCcyIdxHandle { get; }

        public YieldTermStructure QuoteCcyIdxHandle { get; }

        // Data source: N. Moreni, A. Pallavicini (2015), FX Modelling in Collateralized Markets: foreign measures,
        // basis curves and pricing formulae, section 4.2.1, Table 2.
        public XccyTestDatum[] BasisData { get; } =
        [
            new(1, TimeUnit.Years, -14.5),
            new(18, TimeUnit.Months, -18.5),
            new(2, TimeUnit.Years, -20.5),
            new(3, TimeUnit.Years, -23.75),
            new(4, TimeUnit.Years, -25.5),
            new(5, TimeUnit.Years, -26.5),
            new(7, TimeUnit.Years, -26.75),
            new(10, TimeUnit.Years, -26.25),
            new(15, TimeUnit.Years, -24.75),
            new(20, TimeUnit.Years, -23.25),
            new(30, TimeUnit.Years, -20.50),
        ];

        // utilities
        public ConstNotionalCrossCurrencyBasisSwapRateHelper ConstantNotionalXccyRateHelper(
            XccyTestDatum q,
            YieldTermStructure? collateralHandle,
            bool isFxBaseCurrencyCollateralCurrency,
            bool isBasisOnFxBaseCurrencyLeg) => new(
            q.Basis * BasisPoint,
            new Period(q.N, q.Units),
            InstrumentSettlementDays,
            Calendar,
            BusinessConvention,
            EndOfMonth,
            BaseCcyIdx,
            QuoteCcyIdx,
            collateralHandle,
            isFxBaseCurrencyCollateralCurrency,
            isBasisOnFxBaseCurrencyLeg,
            Context);

        public ConstNotionalCrossCurrencyBasisSwapRateHelper[] BuildConstantNotionalXccyRateHelpers(
            XccyTestDatum[] xccyData,
            YieldTermStructure? collateralHandle,
            bool isFxBaseCurrencyCollateralCurrency,
            bool isBasisOnFxBaseCurrencyLeg) =>
            [
                .. xccyData.Select(i => ConstantNotionalXccyRateHelper(
                    i, collateralHandle, isFxBaseCurrencyCollateralCurrency, isBasisOnFxBaseCurrencyLeg)),
            ];

        public MtMCrossCurrencyBasisSwapRateHelper ResettingXccyRateHelper(
            XccyTestDatum q,
            YieldTermStructure collateralHandle,
            bool isFxBaseCurrencyCollateralCurrency,
            bool isBasisOnFxBaseCurrencyLeg,
            bool isFxBaseCurrencyLegResettable,
            Frequency? paymentFrequency = null,
            int paymentLag = 0,
            bool useOvernightIndex = false,
            Frequency? quoteCcyPaymentFrequency = null)
        {
            IborIndex baseIndex = useOvernightIndex ? BaseOvernightIndex : BaseCcyIdx;
            IborIndex quoteIndex = useOvernightIndex ? QuoteOvernightIndex : QuoteCcyIdx;
            return new MtMCrossCurrencyBasisSwapRateHelper(
                q.Basis * BasisPoint,
                new Period(q.N, q.Units),
                InstrumentSettlementDays,
                Calendar,
                BusinessConvention,
                EndOfMonth,
                baseIndex,
                quoteIndex,
                collateralHandle,
                isFxBaseCurrencyCollateralCurrency,
                isBasisOnFxBaseCurrencyLeg,
                isFxBaseCurrencyLegResettable,
                Context,
                paymentFrequency,
                paymentLag,
                quoteCcyPaymentFrequency);
        }

        public MtMCrossCurrencyBasisSwapRateHelper[] BuildResettingXccyRateHelpers(
            XccyTestDatum[] xccyData,
            YieldTermStructure collateralHandle,
            bool isFxBaseCurrencyCollateralCurrency,
            bool isBasisOnFxBaseCurrencyLeg,
            bool isFxBaseCurrencyLegResettable,
            Frequency? paymentFrequency = null,
            int paymentLag = 0,
            bool useOvernightQuoteIndex = false,
            Frequency? quoteCcyPaymentFrequency = null) =>
            [
                .. xccyData.Select(i => ResettingXccyRateHelper(
                    i,
                    collateralHandle,
                    isFxBaseCurrencyCollateralCurrency,
                    isBasisOnFxBaseCurrencyLeg,
                    isFxBaseCurrencyLegResettable,
                    paymentFrequency,
                    paymentLag,
                    useOvernightQuoteIndex,
                    quoteCcyPaymentFrequency)),
            ];

        // QuantLib's PiecewiseYieldCurve<Discount, LogLinear> on the curve settlement date, extrapolating.
        public YieldTermStructure Curve(IEnumerable<BootstrapHelper<YieldTermStructure>> instruments) =>
            new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
                CurveSettlementDt, instruments, DayCount, Context).EnableExtrapolation();

        public Schedule LegSchedule(Period tenor, IborIndex idx) =>
            new MakeSchedule()
                .From(InstrumentSettlementDt)
                .To(InstrumentSettlementDt + tenor)
                .WithTenor(idx.Tenor)
                .WithCalendar(Calendar)
                .WithConvention(BusinessConvention)
                .EndOfMonth(EndOfMonth)
                .Backwards()
                .ToSchedule();

        public ImmutableArray<CashFlow> ConstantNotionalLeg(
            Schedule schedule, IborIndex idx, double notional, double basis)
        {
            ImmutableArray<CashFlow> leg = new IborLeg(schedule, idx)
                .WithNotionals(notional)
                .WithSpreads(basis)
                .ToLeg(Context);

            Date initialPaymentDate = CashFlows.StartDate(leg);
            leg = [.. leg, new SimpleCashFlow(-notional, initialPaymentDate)];

            Date lastPaymentDate = CashFlows.MaturityDate(leg);
            return [.. leg, new SimpleCashFlow(notional, lastPaymentDate)];
        }

        public Swap[] BuildXccyBasisSwap(XccyTestDatum q, double fxSpot, bool isBasisOnFxBaseCurrencyLeg)
        {
            const double baseCcyLegNotional = 1.0;
            double quoteCcyLegNotional = baseCcyLegNotional * fxSpot;

            double baseCcyLegBasis = isBasisOnFxBaseCurrencyLeg ? q.Basis * BasisPoint : 0.0;
            double quoteCcyLegBasis = isBasisOnFxBaseCurrencyLeg ? 0.0 : q.Basis * BasisPoint;

            bool payer = true;

            ImmutableArray<CashFlow> baseCcyLeg = ConstantNotionalLeg(
                LegSchedule(new Period(q.N, q.Units), BaseCcyIdx), BaseCcyIdx, baseCcyLegNotional, baseCcyLegBasis);
            ImmutableArray<CashFlow> quoteCcyLeg = ConstantNotionalLeg(
                LegSchedule(new Period(q.N, q.Units), QuoteCcyIdx), QuoteCcyIdx, quoteCcyLegNotional, quoteCcyLegBasis);
            return [new Swap([baseCcyLeg], [!payer]), new Swap([quoteCcyLeg], [payer])];
        }
    }
}
