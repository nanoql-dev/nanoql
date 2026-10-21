// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/mtmcrosscurrencybasisswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Not ported: testFxResetCouponUpdates (observer notifications and deepUpdate, which immutable cash flows have no use for, R1)

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

// QuantLib's swaps hold their engines and its quotes notify them; here each pricing passes the engine's arguments,
// and a bumped quote is a second pricing at the new value. Fixings and exchange rates QuantLib adds to its global
// managers are added to the context's.
[Trait("Oracle", "QuantLib")]
public class MtMCrossCurrencyBasisSwapTests
{
    [Fact]
    public void TestFairFxQuoteSpreadRepricesToZero()
    {
        // Testing that a MtM cross-currency basis swap reprices to zero at its fair spread...

        var today = new Date(11, September, 2018);
        var context = new EvaluationContext { EvaluationDate = today };

        // USD is the domestic (NPV) currency, EUR the foreign one.
        YieldTermStructure usdCurve = FlatCurve(today, 0.02);
        YieldTermStructure eurCurve = FlatCurve(today, 0.01);

        var usdIndex = new USDLibor(new Period(3, TimeUnit.Months), usdCurve);
        var eurIndex = new Euribor(new Period(3, TimeUnit.Months), eurCurve);

        var cal = new TARGET();
        Date start = cal.Advance(today, 2, TimeUnit.Days);
        Date end = start + new Period(5, TimeUnit.Years);
        Schedule sch = Schedule(
            start, end, cal, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Backward, context);

        double usdNominal = 10000000.0;
        double spotFx = 1.10; // USD per EUR
        double eurNominal = usdNominal / spotFx;

        // Base USD (constant notional), quote EUR (resettable leg).
        MtMCrossCurrencyBasisSwap MakeSwap(double eurSpread) => new(
            MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency,
            usdNominal,
            new USDCurrency(),
            sch,
            usdIndex,
            0.0,
            1.0,
            eurNominal,
            new EURCurrency(),
            sch,
            eurIndex,
            eurSpread,
            1.0,
            isFxBaseCurrencyLegResettable: false,
            context);
        MtMCrossCurrencyBasisSwapResults Price(MtMCrossCurrencyBasisSwap swap) =>
            DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate(
                swap, context, new USDCurrency(), usdCurve, new EURCurrency(), eurCurve, spotFx);

        MtMCrossCurrencyBasisSwap swap = MakeSwap(0.0);
        Assert.True(swap.ResettingLegIndex == 1, "quote (EUR) leg should be resettable");

        double? fair = Price(swap).FairFxQuoteSpread;
        Assert.True(fair is not null, "fair FX-quote spread not available");

        // Re-pricing at the fair spread must give a (numerically) zero NPV.
        double npvAtFair = Price(MakeSwap(fair.Value)).NPV!.Value;
        double tol = 1.0e-2;
        Assert.True(
            Math.Abs(npvAtFair) <= tol,
            $"MtM swap NPV at fair FX-quote spread is not zero:\n    fair spread: {fair}\n" +
            $"    NPV:         {npvAtFair}");
    }

    [Fact]
    public void TestRepricesToParOffHelperBootstrappedCurve()
    {
        // Testing that the MtM xccy basis swap reprices to par off a curve bootstrapped from MtM xccy rate helpers, for
        // all flag combinations...

        int fixingDays = 2;
        Calendar cal = new TARGET();
        var conv = BusinessDayConvention.Following;
        DayCounter dc = new Actual365Fixed();
        bool endOfMonth = false;
        int fxResetFixingDays = 2;

        Date today = cal.Adjust(new Date(6, September, 2013));
        var context = new EvaluationContext { EvaluationDate = today };
        Date settlementDt = cal.Advance(today, fixingDays, TimeUnit.Days);

        // EUR is the FX base currency, USD the quote currency; the index forecast curves double as the collateral
        // (discount) curve of their currency.
        YieldTermStructure eurForecast = new FlatForward(today, 0.007, dc);
        YieldTermStructure usdForecast = new FlatForward(today, 0.015, dc);
        var eurIndex = new Euribor3M(eurForecast);
        var usdIndex = new USDLibor(new Period(3, TimeUnit.Months), usdForecast);

        // (tenor, basis in bp): a representative subset of the Moreni-Pallavicini quotes.
        (Period Tenor, double Basis)[] basisData =
        [
            (new Period(1, TimeUnit.Years), -14.5),
            (new Period(2, TimeUnit.Years), -20.5),
            (new Period(5, TimeUnit.Years), -26.5),
            (new Period(10, TimeUnit.Years), -26.25),
            (new Period(30, TimeUnit.Years), -20.50),
        ];

        double bp = 1.0e-4;
        double tolerance = 1.0e-8;

        foreach (bool isFxBaseCurrencyCollateralCurrency in new[] { false, true })
        {
            foreach (bool isBasisOnFxBaseCurrencyLeg in new[] { false, true })
            {
                foreach (bool isFxBaseCurrencyLegResettable in new[] { false, true })
                {
                    YieldTermStructure collateral = isFxBaseCurrencyCollateralCurrency ? eurForecast : usdForecast;

                    MtMCrossCurrencyBasisSwapRateHelper[] helpers =
                    [
                        .. basisData.Select(q => new MtMCrossCurrencyBasisSwapRateHelper(
                            q.Basis * bp,
                            q.Tenor,
                            fixingDays,
                            cal,
                            conv,
                            endOfMonth,
                            eurIndex,
                            usdIndex,
                            collateral,
                            isFxBaseCurrencyCollateralCurrency,
                            isBasisOnFxBaseCurrencyLeg,
                            isFxBaseCurrencyLegResettable,
                            context,
                            null,
                            0,
                            null,
                            fxResetFixingDays,
                            cal)),
                    ];

                    YieldTermStructure resetting = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
                        today, helpers, dc, context).EnableExtrapolation();

                    // The collateral currency keeps its own curve; the other currency is discounted on the
                    // bootstrapped curve.
                    YieldTermStructure eurDiscount = isFxBaseCurrencyCollateralCurrency ? eurForecast : resetting;
                    YieldTermStructure usdDiscount = isFxBaseCurrencyCollateralCurrency ? resetting : usdForecast;

                    foreach ((Period tenor, double quote) in basisData)
                    {
                        double basis = quote * bp;
                        double baseLegBasis = isBasisOnFxBaseCurrencyLeg ? basis : 0.0;
                        double quoteLegBasis = isBasisOnFxBaseCurrencyLeg ? 0.0 : basis;

                        Schedule eurSch = new MakeSchedule()
                            .From(settlementDt)
                            .To(settlementDt + tenor)
                            .WithTenor(eurIndex.Tenor)
                            .WithCalendar(cal)
                            .WithConvention(conv)
                            .EndOfMonth(endOfMonth)
                            .Backwards()
                            .ToSchedule();
                        Schedule usdSch = new MakeSchedule()
                            .From(settlementDt)
                            .To(settlementDt + tenor)
                            .WithTenor(usdIndex.Tenor)
                            .WithCalendar(cal)
                            .WithConvention(conv)
                            .EndOfMonth(endOfMonth)
                            .Backwards()
                            .ToSchedule();

                        var swap = new MtMCrossCurrencyBasisSwap(
                            MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency,
                            1.0,
                            new EURCurrency(),
                            eurSch,
                            eurIndex,
                            baseLegBasis,
                            1.0,
                            1.0,
                            new USDCurrency(),
                            usdSch,
                            usdIndex,
                            quoteLegBasis,
                            1.0,
                            isFxBaseCurrencyLegResettable,
                            context,
                            fxResetFixingDays,
                            cal);

                        // Unit notionals and spot = 1, matching the helper's par convention.
                        double npv = DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate(
                            swap,
                            context,
                            new USDCurrency(),
                            usdDiscount,
                            new EURCurrency(),
                            eurDiscount,
                            1.0).NPV!.Value;
                        Assert.True(
                            Math.Abs(npv) <= tolerance,
                            "MtM xccy basis swap does not reprice to par off the bootstrapped curve\n" +
                            $"    NPV:     {npv}\n    tenor:   {tenor}\n    basis:   {quote}\n" +
                            $"    collateral in base ccy:  {isFxBaseCurrencyCollateralCurrency}\n" +
                            $"    basis on base ccy leg:   {isBasisOnFxBaseCurrencyLeg}\n" +
                            $"    base ccy leg resettable: {isFxBaseCurrencyLegResettable}\n");
                    }

                    // The helpers expose their underlying par swaps, priced on the helpers' handles: after
                    // bootstrapping, the fair spread of the basis leg must reproduce the helper quote. QuantLib's
                    // swap holds its engine on those handles, the quote currency's and the base currency's curves,
                    // a spot of 1 and the settlement date's flows; the same arguments price it here.
                    for (int i = 0; i < helpers.Length; ++i)
                    {
                        MtMCrossCurrencyBasisSwapRateHelper helper = helpers[i];
                        Assert.NotNull(helper.Swap);
                        Assert.Equal(fxResetFixingDays, helper.FxResetFixingDays);
                        Assert.Equal(cal, helper.FxResetFixingCalendar);
                        MtMCrossCurrencyBasisSwap underlying = helper.Swap;
                        bool foundFxResetCoupon = false;
                        foreach (CashFlow cf in underlying.ResettingLeg)
                        {
                            if (cf is FxResetCoupon coupon)
                            {
                                foundFxResetCoupon = true;
                                Date expectedFixing = cal.Advance(
                                    coupon.FxResetValueDate, -fxResetFixingDays, TimeUnit.Days);
                                Assert.Equal(expectedFixing, coupon.FxResetDate);
                                break;
                            }
                        }

                        Assert.True(foundFxResetCoupon);
                        MtMCrossCurrencyBasisSwapResults results = DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate(
                            underlying,
                            context,
                            new USDCurrency(),
                            usdDiscount,
                            new EURCurrency(),
                            eurDiscount,
                            1.0,
                            includeSettlementDateFlows: true);
                        double fair = isBasisOnFxBaseCurrencyLeg
                            ? results.FairFxBaseSpread!.Value
                            : results.FairFxQuoteSpread!.Value;
                        double expected = basisData[i].Basis * bp;
                        Assert.True(
                            Math.Abs(fair - expected) <= tolerance,
                            "the exposed helper swap does not reproduce the helper quote\n" +
                            $"    fair basis: {fair / bp} bp\n    quote:      {basisData[i].Basis} bp\n" +
                            $"    collateral in base ccy:  {isFxBaseCurrencyCollateralCurrency}\n" +
                            $"    basis on base ccy leg:   {isBasisOnFxBaseCurrencyLeg}\n" +
                            $"    base ccy leg resettable: {isFxBaseCurrencyLegResettable}\n");
                    }
                }
            }
        }
    }

    [Fact]
    public void TestFxResetObservationDatesAndProjection()
    {
        // Testing FX-reset fixing dates and value-date projection...

        var today = new Date(1, July, 2024);
        var context = new EvaluationContext { EvaluationDate = today };

        YieldTermStructure eurCurve = FlatCurve(today, 0.01);
        YieldTermStructure usdCurve = FlatCurve(today, 0.05);
        var eurIndex = new Euribor(new Period(3, TimeUnit.Months), eurCurve);
        var usdIndex = new USDLibor(new Period(3, TimeUnit.Months), usdCurve);

        var start = new Date(8, July, 2024);
        Schedule sch = Schedule(
            start,
            start + new Period(6, TimeUnit.Months),
            new TARGET(),
            BusinessDayConvention.Following,
            DateGenerationRule.Forward,
            context);
        Calendar fxCalendar = new JointCalendar(
            new TARGET(), new UnitedStates(UnitedStatesMarket.Settlement), JointCalendarRule.JoinHolidays);
        var fxResetConvention = new FxResetConvention(2, fxCalendar);

        double eurNominal = 10000000.0;
        double spotFx = 1.10;
        var swap = new MtMCrossCurrencyBasisSwap(
            MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency,
            eurNominal,
            new EURCurrency(),
            sch,
            eurIndex,
            0.0,
            1.0,
            eurNominal * spotFx,
            new USDCurrency(),
            sch,
            usdIndex,
            0.0,
            1.0,
            isFxBaseCurrencyLegResettable: false,
            context,
            fxResetConvention.FixingDays,
            fxResetConvention.FixingCalendar);
        Assert.NotNull(swap.FxResetFixingCalendar);
        MtMCrossCurrencyBasisSwapResults Price(double spot, Date? spotFxSettleDate = null) =>
            DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate(
                swap,
                context,
                new USDCurrency(),
                usdCurve,
                new EURCurrency(),
                eurCurve,
                spot,
                spotFxSettleDate: spotFxSettleDate);
        MtMCrossCurrencyBasisSwapResults results = Price(spotFx);
        double automaticallyDatedNpv = results.NPV!.Value;

        FxResetCoupon? firstCoupon = null;
        FxResetNotionalExchange? firstExchange = null;
        foreach (CashFlow cf in swap.ResettingLeg)
        {
            firstExchange ??= cf as FxResetNotionalExchange;
            if (cf is FxResetCoupon coupon)
            {
                firstCoupon = coupon;
                break;
            }
        }

        Assert.NotNull(firstCoupon);
        Assert.NotNull(firstExchange);
        Assert.Equal(new Date(3, July, 2024), firstCoupon.FxResetDate);
        Assert.Equal(start, firstCoupon.FxResetValueDate);
        Date spotFxSettleDate = fxResetConvention.ValueDate(today);
        Assert.Equal(new Date(3, July, 2024), spotFxSettleDate);
        Assert.Equal(start, fxResetConvention.ValueDate(firstCoupon.FxResetDate));
        Assert.Null(firstExchange.PreviousReset);
        Assert.NotNull(firstExchange.CurrentReset);
        Assert.Equal(firstCoupon.FxResetDate, firstExchange.CurrentReset.Value.FixingDate);
        Assert.Equal(firstCoupon.FxResetValueDate, firstExchange.CurrentReset.Value.ValueDate);
        Assert.Null(firstCoupon.FxResetPricer);
        Assert.Null(firstExchange.FxResetPricer);

        var fxResetPricer = new DiscountingFxResetPricer(
            new EURCurrency(), new USDCurrency(), eurCurve, usdCurve, spotFx, true, spotFxSettleDate);

        double expectedForward = spotFx * usdCurve.Discount(spotFxSettleDate) / eurCurve.Discount(spotFxSettleDate)
            * eurCurve.Discount(start) / usdCurve.Discount(start);
        ImmutableArray<double> fxResetRates = results.FxResetRates;
        ImmutableArray<double> fxResetNotionals = results.FxResetNotionals;
        Assert.Equal(fxResetRates.Length, fxResetNotionals.Length);

        int resetNo = 0;
        foreach (CashFlow cf in swap.ResettingLeg)
        {
            if (cf is FxResetCoupon coupon)
            {
                Assert.True(resetNo < fxResetRates.Length);
                double expectedRate = fxResetPricer.FxRate(coupon.FxReset, context);
                CheckClose(fxResetRates[resetNo], expectedRate, 1.0e-10);
                CheckClose(fxResetNotionals[resetNo], eurNominal * expectedRate, 1.0e-10);
                ++resetNo;
            }
        }

        Assert.Equal(fxResetRates.Length, resetNo);
        CheckClose(fxResetRates[0], expectedForward, 1.0e-10);

        MtMCrossCurrencyBasisSwapResults bumped = Price(1.20);
        CheckClose(bumped.FxResetRates[0], fxResetRates[0] * 1.20 / spotFx, 1.0e-10);
        CheckClose(bumped.FxResetNotionals[0], fxResetNotionals[0] * 1.20 / spotFx, 1.0e-10);

        ImmutableArray<CashFlow> priced = FxResetCashFlowsFunctions.SetFxResetPricer(swap.ResettingLeg, fxResetPricer);
        FxResetCoupon pricedFirstCoupon = priced.OfType<FxResetCoupon>().First();
        CheckClose(pricedFirstCoupon.Nominal(context), eurNominal * expectedForward, 1.0e-10);

        // Omitting the engine's explicit spot settlement date must derive the same date from the swap's reset
        // convention.
        CheckSmall(Price(spotFx, spotFxSettleDate).NPV!.Value - automaticallyDatedNpv, 1.0e-10 * eurNominal);

        // The equivalent reference-date FX quote must give the same result when an explicit reference-date settlement
        // overrides the convention.
        double referenceDateFx = spotFx * usdCurve.Discount(spotFxSettleDate) / eurCurve.Discount(spotFxSettleDate);
        CheckSmall(Price(referenceDateFx, today).NPV!.Value - automaticallyDatedNpv, 1.0e-10 * eurNominal);
    }

    [Fact]
    public void TestResetFixingStateUsesEvaluationDate()
    {
        // Testing that FX-reset fixing state is determined from the evaluation date...

        var today = new Date(1, July, 2024);
        var context = new EvaluationContext { EvaluationDate = today };

        var calendar = new TARGET();
        Date spotDate = calendar.Advance(today, 2, TimeUnit.Days);
        YieldTermStructure eurCurve = FlatCurve(spotDate, 0.01);
        YieldTermStructure usdCurve = FlatCurve(spotDate, 0.05);
        double spotFx = 1.10;
        var pricer = new DiscountingFxResetPricer(
            new EURCurrency(), new USDCurrency(), eurCurve, usdCurve, spotFx, true, spotDate);
        var reset = new FxReset(today, spotDate);

        // Although today's fixing precedes the curves' spot-date reference, it is forecast when no fixing has been
        // published yet.
        CheckClose(pricer.FxRate(reset, context), spotFx, 1.0e-12);

        double publishedFixing = 1.25;
        EvaluationContext published = context with
        {
            ExchangeRates = context.ExchangeRates.Add(
                new ExchangeRate(new EURCurrency(), new USDCurrency(), publishedFixing), today, today),
        };
        CheckClose(pricer.FxRate(reset, published), publishedFixing, 1.0e-12);

        EvaluationContext enforced = context with { EnforcesTodaysHistoricFixings = true };
        Assert.Throws<NanoQLException>(() => pricer.FxRate(reset, enforced));
    }

    [Fact]
    public void TestKnownFxResetBeforeAccrualStart()
    {
        // Testing that an FX reset fixed before accrual start uses its fixing...

        var today = new Date(5, July, 2024);
        var context = new EvaluationContext { EvaluationDate = today };

        YieldTermStructure eurCurve = FlatCurve(today, 0.01);
        YieldTermStructure usdCurve = FlatCurve(today, 0.05);
        var eurIndex = new Euribor(new Period(3, TimeUnit.Months), eurCurve);
        var usdIndex = new USDLibor(new Period(3, TimeUnit.Months), usdCurve);

        var start = new Date(8, July, 2024);
        Schedule sch = Schedule(
            start,
            start + new Period(6, TimeUnit.Months),
            new TARGET(),
            BusinessDayConvention.Following,
            DateGenerationRule.Forward,
            context);
        Calendar fxCalendar = new JointCalendar(
            new TARGET(), new UnitedStates(UnitedStatesMarket.Settlement), JointCalendarRule.JoinHolidays);
        var fxResetConvention = new FxResetConvention(2, fxCalendar);
        var fxFixingDate = new Date(3, July, 2024);
        double fixedFx = 1.25;
        context = context with
        {
            ExchangeRates = context.ExchangeRates.Add(
                new ExchangeRate(new EURCurrency(), new USDCurrency(), fixedFx), fxFixingDate, fxFixingDate),
        };

        Date eurFixingDate = eurIndex.FixingDate(start);
        if (eurFixingDate < today)
        {
            context = context with { Fixings = context.Fixings.AddFixing(eurIndex, eurFixingDate, 0.01) };
        }

        Date usdFixingDate = usdIndex.FixingDate(start);
        if (usdFixingDate < today)
        {
            context = context with { Fixings = context.Fixings.AddFixing(usdIndex, usdFixingDate, 0.05) };
        }

        double eurNominal = 10000000.0;
        var swap = new MtMCrossCurrencyBasisSwap(
            MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency,
            eurNominal,
            new EURCurrency(),
            sch,
            eurIndex,
            0.0,
            1.0,
            eurNominal * 1.10,
            new USDCurrency(),
            sch,
            usdIndex,
            0.0,
            1.0,
            isFxBaseCurrencyLegResettable: false,
            context,
            fxResetConvention.FixingDays,
            fxResetConvention.FixingCalendar);
        DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate(
            swap, context, new USDCurrency(), usdCurve, new EURCurrency(), eurCurve, 1.10);

        FxResetCoupon firstCoupon = swap.ResettingLeg.OfType<FxResetCoupon>().First();
        Assert.Equal(fxFixingDate, firstCoupon.FxResetDate);
        Assert.Equal(start, firstCoupon.FxResetValueDate);
        ImmutableArray<CashFlow> priced = FxResetCashFlowsFunctions.SetFxResetPricer(
            swap.ResettingLeg,
            new DiscountingFxResetPricer(new EURCurrency(), new USDCurrency(), eurCurve, usdCurve, 1.10, true));
        CheckClose(priced.OfType<FxResetCoupon>().First().Nominal(context), eurNominal * fixedFx, 1.0e-10);
    }

    [Fact]
    public void TestResettableLegCashFlowsMatchLegResults()
    {
        // Testing that enumerating the resettable leg's cash flows reproduces the leg NPV and the FX-reset
        // notionals...

        var today = new Date(11, September, 2018);
        var context = new EvaluationContext { EvaluationDate = today };

        YieldTermStructure usdCurve = FlatCurve(today, 0.02);
        YieldTermStructure eurCurve = FlatCurve(today, 0.01);
        var usdIndex = new USDLibor(new Period(3, TimeUnit.Months), usdCurve);
        var eurIndex = new Euribor(new Period(3, TimeUnit.Months), eurCurve);

        var cal = new TARGET();
        Date start = cal.Advance(today, 2, TimeUnit.Days);
        Date end = start + new Period(5, TimeUnit.Years);
        Schedule sch = Schedule(
            start, end, cal, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Backward, context);

        double usdNominal = 10000000.0;
        double spotFx = 1.10; // USD per EUR

        // Base USD (constant notional), quote EUR (resettable leg).
        var swap = new MtMCrossCurrencyBasisSwap(
            MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency,
            usdNominal,
            new USDCurrency(),
            sch,
            usdIndex,
            0.0,
            1.0,
            usdNominal / spotFx,
            new EURCurrency(),
            sch,
            eurIndex,
            10.0e-4,
            1.0,
            isFxBaseCurrencyLegResettable: false,
            context);
        Assert.Null(swap.FxResetFixingCalendar);
        MtMCrossCurrencyBasisSwapResults results = DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate(
            swap, context, new USDCurrency(), usdCurve, new EURCurrency(), eurCurve, spotFx);

        int resettingLegNo = swap.ResettingLegIndex;
        double legNpv = results.InCcyLegNPV[resettingLegNo]!.Value;
        ImmutableArray<CashFlow> leg = FxResetCashFlowsFunctions.SetFxResetPricer(
            swap.Leg(resettingLegNo),
            new DiscountingFxResetPricer(new USDCurrency(), new EURCurrency(), usdCurve, eurCurve, spotFx, false));

        // With the same pricer assigned explicitly, enumerating and discounting the live leg's cash flows must
        // reproduce the engine's in-currency leg NPV.
        double npv = 0.0;
        int couponCount = 0, exchangeCount = 0;
        foreach (CashFlow cf in leg)
        {
            npv += cf.Amount(context) * eurCurve.Discount(cf.Date);
            if (cf is FxResetCoupon coupon)
            {
                ++couponCount;

                // The nominal is the constant-leg notional converted at the forward FX rate (EUR per USD) of the
                // period's reset date.
                Date reset = coupon.FxResetDate;
                double expected = usdNominal * (1.0 / spotFx) * usdCurve.Discount(reset) / eurCurve.Discount(reset);
                CheckClose(coupon.Nominal(context), expected, 1.0e-8);
            }
            else if (cf is FxResetNotionalExchange)
            {
                ++exchangeCount;
            }
            else
            {
                Assert.Fail("unexpected cash-flow type on the resettable leg");
            }
        }

        Assert.Equal(couponCount + 1, exchangeCount);
        CheckSmall(npv - legNpv, 1.0e-8 * usdNominal);
    }

    [Fact]
    public void TestSameDayResetUsesSpot()
    {
        // Testing that a reset on the curve reference date uses spot FX...

        var today = new Date(11, September, 2018);
        var context = new EvaluationContext { EvaluationDate = today };

        YieldTermStructure usdCurve = FlatCurve(today, 0.02);
        YieldTermStructure eurCurve = FlatCurve(today, 0.01);
        var usdIndex = new USDLibor(new Period(3, TimeUnit.Months), usdCurve);
        var eurIndex = new Euribor(new Period(3, TimeUnit.Months), eurCurve);

        var cal = new TARGET();
        Schedule sch = Schedule(
            today,
            today + new Period(3, TimeUnit.Months),
            cal,
            BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Forward,
            context);
        double usdNominal = 10000000.0;
        double spotFx = 1.10;
        var swap = new MtMCrossCurrencyBasisSwap(
            MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency,
            usdNominal,
            new USDCurrency(),
            sch,
            usdIndex,
            0.0,
            1.0,
            usdNominal / spotFx,
            new EURCurrency(),
            sch,
            eurIndex,
            0.0,
            1.0,
            isFxBaseCurrencyLegResettable: false,
            context);

        for (int legNo = 0; legNo < 2; ++legNo)
        {
            foreach (CashFlow cf in swap.Leg(legNo))
            {
                if (cf is FloatingRateCoupon frc)
                {
                    Date fixingDate = frc.FixingDate();
                    if (fixingDate < today)
                    {
                        context = context with
                        {
                            Fixings = context.Fixings.AddFixing(frc.Index, fixingDate, legNo == 0 ? 0.02 : 0.01, true),
                        };
                    }
                }
            }
        }

        DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate(
            swap, context, new USDCurrency(), usdCurve, new EURCurrency(), eurCurve, spotFx);
    }

    [Fact]
    public void TestFxSettlementAndNpvDateConsistency()
    {
        // Testing FX-settlement carry, NPV-date conversion, and scalar discount...

        var today = new Date(11, September, 2018);
        var context = new EvaluationContext { EvaluationDate = today };

        YieldTermStructure usdCurve = FlatCurve(today, 0.03);
        YieldTermStructure eurCurve = FlatCurve(today, 0.01);
        var usdIndex = new USDLibor(new Period(3, TimeUnit.Months), usdCurve);
        var eurIndex = new Euribor(new Period(3, TimeUnit.Months), eurCurve);

        var cal = new TARGET();
        Date start = cal.Advance(today, 2, TimeUnit.Days);
        Date end = start + new Period(2, TimeUnit.Years);
        Schedule sch = Schedule(
            start, end, cal, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, context);
        double usdNominal = 10000000.0;
        double referenceSpot = 1.10;

        var swap = new MtMCrossCurrencyBasisSwap(
            MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency,
            usdNominal,
            new USDCurrency(),
            sch,
            usdIndex,
            0.0,
            1.0,
            usdNominal / referenceSpot,
            new EURCurrency(),
            sch,
            eurIndex,
            15.0e-4,
            1.0,
            isFxBaseCurrencyLegResettable: false,
            context);
        MtMCrossCurrencyBasisSwapResults Price(double spot, Date? npvDate = null, Date? spotFxSettleDate = null) =>
            DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate(
                swap,
                context,
                new USDCurrency(),
                usdCurve,
                new EURCurrency(),
                eurCurve,
                spot,
                npvDate: npvDate,
                spotFxSettleDate: spotFxSettleDate);

        double referenceNpv = Price(referenceSpot).NPV!.Value;

        Date fxSettlementDate = cal.Advance(today, 6, TimeUnit.Months);
        double settlementFx = referenceSpot * eurCurve.Discount(fxSettlementDate) / usdCurve.Discount(fxSettlementDate);

        double tolerance = 1.0e-10 * usdNominal;
        CheckSmall(Price(settlementFx, spotFxSettleDate: fxSettlementDate).NPV!.Value - referenceNpv, tolerance);

        Date npvDate = cal.Advance(today, 9, TimeUnit.Months);
        MtMCrossCurrencyBasisSwapResults forwardNpv = Price(referenceSpot, npvDate);
        double domesticNpvDateDiscount = usdCurve.Discount(npvDate);
        CheckSmall((forwardNpv.NPV!.Value * domesticNpvDateDiscount) - referenceNpv, tolerance);
        CheckClose(forwardNpv.NpvDateDiscount!.Value, domesticNpvDateDiscount, 1.0e-10);
    }

    [Fact]
    public void TestSeasonedResetPeriodNeedsExchangeRate()
    {
        // Testing that an already-started MtM reset period requires a stored exchange rate...

        var today = new Date(11, September, 2018);
        var context = new EvaluationContext { EvaluationDate = today };

        YieldTermStructure usdCurve = FlatCurve(today, 0.02);
        YieldTermStructure eurCurve = FlatCurve(today, 0.01);

        var usdIndex = new USDLibor(new Period(3, TimeUnit.Months), usdCurve);
        var eurIndex = new Euribor(new Period(3, TimeUnit.Months), eurCurve);

        var cal = new TARGET();
        Schedule sch = Schedule(
            new Date(11, August, 2018),
            new Date(11, February, 2019),
            cal,
            BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Forward,
            context);

        double usdNominal = 10000000.0;
        double spotFx = 1.10;
        double eurNominal = usdNominal / spotFx;

        var swap = new MtMCrossCurrencyBasisSwap(
            MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency,
            usdNominal,
            new USDCurrency(),
            sch,
            usdIndex,
            0.0,
            1.0,
            eurNominal,
            new EURCurrency(),
            sch,
            eurIndex,
            0.0,
            1.0,
            isFxBaseCurrencyLegResettable: true,
            context);

        Assert.Throws<NanoQLException>(() => DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate(
            swap, context, new USDCurrency(), usdCurve, new EURCurrency(), eurCurve, spotFx));
    }

    [Fact]
    public void TestSeasonedTriangulatedResetMatchesConstantNotional()
    {
        // Testing that an in-progress MtM reset obtained by triangulation prices identically to the equivalent
        // constant-notional swap built from the realised reset notional...

        var today = new Date(11, September, 2018);
        var context = new EvaluationContext { EvaluationDate = today };

        YieldTermStructure usdCurve = FlatCurve(today, 0.02);
        YieldTermStructure eurCurve = FlatCurve(today, 0.01);

        var usdIndex = new USDLibor(new Period(3, TimeUnit.Months), usdCurve);
        var eurIndex = new Euribor(new Period(3, TimeUnit.Months), eurCurve);

        var cal = new TARGET();

        // A single 3M period that started a month ago: the only period is in progress, so its reset notional is fully
        // determined by the realised FX fixing and the swap is economically a constant-notional swap with that EUR
        // notional.
        Date start = cal.Advance(today, -1, TimeUnit.Months);
        Date end = start + new Period(3, TimeUnit.Months);
        Schedule sch = Schedule(
            start, end, cal, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, context);

        double usdNominal = 10000000.0;
        double spotFx = 1.10; // USD per EUR
        double eurBasis = 25 * 1.0e-4; // basis on the resettable EUR leg
        double realisedFx = 0.92; // realised EUR per USD at the past reset (deliberately != 1/spotFx)

        var mtm = new MtMCrossCurrencyBasisSwap(
            MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency,
            usdNominal,
            new USDCurrency(),
            sch,
            usdIndex,
            0.0,
            1.0,
            usdNominal / spotFx,
            new EURCurrency(),
            sch,
            eurIndex,
            eurBasis,
            1.0,
            isFxBaseCurrencyLegResettable: false,
            context);

        // Seed the realised rate fixings (both legs) and the realised FX reset.
        context = SeedFixings(mtm, context, today, 0.021, 0.012);
        foreach (CashFlow cf in mtm.ResettingLeg)
        {
            if (cf is Coupon cpn)
            {
                Date reset = cpn.AccrualStartDate;
                if (reset <= today)
                {
                    double usdGbp = 0.80;
                    context = context with
                    {
                        ExchangeRates = context.ExchangeRates
                            .Add(new ExchangeRate(new USDCurrency(), new GBPCurrency(), usdGbp), reset, reset)
                            .Add(
                                new ExchangeRate(new GBPCurrency(), new EURCurrency(), realisedFx / usdGbp),
                                reset,
                                reset),
                    };
                }
            }
        }

        double spot = spotFx;
        double mtmNpv = DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate(
            mtm, context, new USDCurrency(), usdCurve, new EURCurrency(), eurCurve, spot).NPV!.Value;

        // Reference: constant-notional swap whose EUR notional is the realised reset notional (usdNominal * realisedFx)
        double realisedEurNotional = usdNominal * realisedFx;
        var reference = new ConstNotionalCrossCurrencyBasisSwap(
            usdNominal,
            new USDCurrency(),
            sch,
            usdIndex,
            0.0,
            1.0,
            realisedEurNotional,
            new EURCurrency(),
            sch,
            eurIndex,
            eurBasis,
            1.0,
            context);
        double refNpv = DiscountingConstNotionalCrossCurrencySwapEngine.Calculate(
            reference, context, new USDCurrency(), usdCurve, new EURCurrency(), eurCurve, spot).NPV!.Value;

        double tol = 1.0e-6 * usdNominal;
        double diff = Math.Abs(mtmNpv - refNpv);
        Assert.True(
            diff <= tol,
            "seasoned MtM reset does not match the equivalent constant-notional swap\n" +
            $"    MtM NPV:        {mtmNpv}\n    const-not. NPV: {refNpv}\n    difference:     {diff}\n");
    }

    [Fact]
    public void TestSeasonedEurUsdMarketExchangeRate()
    {
        // Testing a seasoned EURUSD MtM swap (exchange rate stored as USD per EUR, the market convention) reprices to
        // the equivalent constant-notional swap...

        var today = new Date(11, September, 2018);
        var context = new EvaluationContext { EvaluationDate = today };

        YieldTermStructure eurCurve = FlatCurve(today, 0.01);
        YieldTermStructure usdCurve = FlatCurve(today, 0.02);
        var eurIndex = new Euribor(new Period(3, TimeUnit.Months), eurCurve);
        var usdIndex = new USDLibor(new Period(3, TimeUnit.Months), usdCurve);

        var cal = new TARGET();

        // a single in-progress 3M period (started a month ago)
        Date start = cal.Advance(today, -1, TimeUnit.Months);
        Date end = start + new Period(3, TimeUnit.Months);
        Schedule sch = Schedule(
            start, end, cal, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, context);

        double eurNotional = 10000000.0;
        double basis = 20 * 1.0e-4; // on the EUR (base, constant) leg
        double marketFx = 1.10; // EURUSD market convention: USD per EUR

        // base = EUR (constant), quote = USD (resettable); reset notional = EUR notional * USD per EUR.
        double realizedUsdNotional = eurNotional * marketFx;
        var mtm = new MtMCrossCurrencyBasisSwap(
            MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency,
            eurNotional,
            new EURCurrency(),
            sch,
            eurIndex,
            basis,
            1.0,
            realizedUsdNotional,
            new USDCurrency(),
            sch,
            usdIndex,
            0.0,
            1.0,
            isFxBaseCurrencyLegResettable: false,
            context);

        context = SeedFixings(mtm, context, today, 0.011, 0.021);
        foreach (CashFlow cf in mtm.ResettingLeg)
        {
            if (cf is Coupon cpn && cpn.AccrualStartDate <= today)
            {
                Date reset = cpn.AccrualStartDate;
                context = context with
                {
                    ExchangeRates = context.ExchangeRates.Add(
                        new ExchangeRate(new EURCurrency(), new USDCurrency(), marketFx), reset, reset),
                };
            }
        }

        double spot = marketFx; // USD per EUR = domestic(USD) per foreign(EUR)
        double mtmNpv = DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate(
            mtm, context, new USDCurrency(), usdCurve, new EURCurrency(), eurCurve, spot).NPV!.Value;

        var reference = new ConstNotionalCrossCurrencyBasisSwap(
            eurNotional,
            new EURCurrency(),
            sch,
            eurIndex,
            basis,
            1.0,
            realizedUsdNotional,
            new USDCurrency(),
            sch,
            usdIndex,
            0.0,
            1.0,
            context);
        double refNpv = DiscountingConstNotionalCrossCurrencySwapEngine.Calculate(
            reference, context, new USDCurrency(), usdCurve, new EURCurrency(), eurCurve, spot).NPV!.Value;

        double tol = 1.0e-6 * realizedUsdNotional;
        double diff = Math.Abs(mtmNpv - refNpv);
        Assert.True(
            diff <= tol,
            "seasoned EURUSD MtM does not match the constant-notional reference\n" +
            $"    MtM NPV: {mtmNpv}\n    ref NPV: {refNpv}\n    diff:    {diff}\n");
    }

    [Fact]
    public void TestSeasonedUsdJpyMarketExchangeRate()
    {
        // Testing a seasoned USDJPY MtM swap (exchange rate stored as JPY per USD, the market convention) reprices to
        // the equivalent constant-notional swap...

        var today = new Date(11, September, 2018);
        var context = new EvaluationContext { EvaluationDate = today };

        YieldTermStructure usdCurve = FlatCurve(today, 0.02);
        YieldTermStructure jpyCurve = FlatCurve(today, 0.001);
        var usdIndex = new USDLibor(new Period(3, TimeUnit.Months), usdCurve);
        var jpyIndex = new JPYLibor(new Period(3, TimeUnit.Months), jpyCurve);

        var cal = new TARGET();
        Date start = cal.Advance(today, -1, TimeUnit.Months);
        Date end = start + new Period(3, TimeUnit.Months);
        Schedule sch = Schedule(
            start, end, cal, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, context);

        double jpyNotional = 1500000000.0;
        double basis = 15 * 1.0e-4; // on the JPY (quote, constant) leg
        double marketFx = 150.0; // USDJPY market convention: JPY per USD

        // base = USD (resettable), quote = JPY (constant); reset notional = JPY notional * USD per JPY.
        double realizedUsdNotional = jpyNotional / marketFx;
        var mtm = new MtMCrossCurrencyBasisSwap(
            MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency,
            realizedUsdNotional,
            new USDCurrency(),
            sch,
            usdIndex,
            0.0,
            1.0,
            jpyNotional,
            new JPYCurrency(),
            sch,
            jpyIndex,
            basis,
            1.0,
            isFxBaseCurrencyLegResettable: true,
            context);

        context = SeedFixings(mtm, context, today, 0.021, 0.0005);
        foreach (CashFlow cf in mtm.ResettingLeg)
        {
            if (cf is Coupon cpn && cpn.AccrualStartDate <= today)
            {
                Date reset = cpn.AccrualStartDate;
                context = context with
                {
                    ExchangeRates = context.ExchangeRates.Add(
                        new ExchangeRate(new USDCurrency(), new JPYCurrency(), marketFx), reset, reset),
                };
            }
        }

        double spot = marketFx; // JPY per USD = domestic(JPY) per foreign(USD)
        double mtmNpv = DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate(
            mtm, context, new JPYCurrency(), jpyCurve, new USDCurrency(), usdCurve, spot).NPV!.Value;

        var reference = new ConstNotionalCrossCurrencyBasisSwap(
            realizedUsdNotional,
            new USDCurrency(),
            sch,
            usdIndex,
            0.0,
            1.0,
            jpyNotional,
            new JPYCurrency(),
            sch,
            jpyIndex,
            basis,
            1.0,
            context);
        double refNpv = DiscountingConstNotionalCrossCurrencySwapEngine.Calculate(
            reference, context, new JPYCurrency(), jpyCurve, new USDCurrency(), usdCurve, spot).NPV!.Value;

        double tol = 1.0e-6 * jpyNotional;
        double diff = Math.Abs(mtmNpv - refNpv);
        Assert.True(
            diff <= tol,
            "seasoned USDJPY MtM does not match the constant-notional reference\n" +
            $"    MtM NPV: {mtmNpv}\n    ref NPV: {refNpv}\n    diff:    {diff}\n");
    }

    [Fact]
    public void TestSeasonedOvernightLegsMatchConstantNotional()
    {
        // Testing that a seasoned MtM swap on overnight indices reprices to the equivalent constant-notional swap and
        // accrues like its underlying overnight coupon...

        var today = new Date(11, September, 2018);
        var context = new EvaluationContext { EvaluationDate = today };

        YieldTermStructure usdCurve = FlatCurve(today, 0.02);
        YieldTermStructure eurCurve = FlatCurve(today, 0.01);
        var usdIndex = new Sofr(usdCurve);
        var eurIndex = new Estr(eurCurve);

        var cal = new TARGET();

        // a single in-progress 3M period (started a month ago)
        Date start = cal.Advance(today, -1, TimeUnit.Months);
        Date end = start + new Period(3, TimeUnit.Months);
        Schedule sch = Schedule(
            start, end, cal, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, context);

        double usdNominal = 10000000.0;
        double spotFx = 1.10; // USD per EUR
        double eurBasis = 25 * 1.0e-4; // basis on the resettable EUR leg
        double realisedFx = 0.92; // realised EUR per USD at the past reset

        // Seed the realised overnight fixings up to today.
        foreach (OvernightIndex index in new OvernightIndex[] { usdIndex, eurIndex })
        {
            double fixing = ReferenceEquals(index, usdIndex) ? 0.021 : 0.012;
            for (Date d = start; d < today; d += 1)
            {
                if (index.FixingCalendar.IsBusinessDay(d))
                {
                    context = context with { Fixings = context.Fixings.AddFixing(index, d, fixing, true) };
                }
            }
        }

        context = context with
        {
            ExchangeRates = context.ExchangeRates.Add(
                new ExchangeRate(new USDCurrency(), new EURCurrency(), realisedFx), start, start),
        };

        double spot = spotFx;
        var mtm = new MtMCrossCurrencyBasisSwap(
            MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency,
            usdNominal,
            new USDCurrency(),
            sch,
            usdIndex,
            0.0,
            1.0,
            usdNominal / spotFx,
            new EURCurrency(),
            sch,
            eurIndex,
            eurBasis,
            1.0,
            isFxBaseCurrencyLegResettable: false,
            context);
        double mtmNpv = DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate(
            mtm, context, new USDCurrency(), usdCurve, new EURCurrency(), eurCurve, spot).NPV!.Value;

        double realisedEurNotional = usdNominal * realisedFx;
        var reference = new ConstNotionalCrossCurrencyBasisSwap(
            usdNominal,
            new USDCurrency(),
            sch,
            usdIndex,
            0.0,
            1.0,
            realisedEurNotional,
            new EURCurrency(),
            sch,
            eurIndex,
            eurBasis,
            1.0,
            context);
        double refNpv = DiscountingConstNotionalCrossCurrencySwapEngine.Calculate(
            reference, context, new USDCurrency(), usdCurve, new EURCurrency(), eurCurve, spot).NPV!.Value;

        double tol = 1.0e-6 * usdNominal;
        CheckSmall(mtmNpv - refNpv, tol);

        // The FX-resetting coupon must accrue like the reference constant-notional coupon built on the realised reset
        // notional: only the overnight fixings realised so far compound into the accrued amount.
        ImmutableArray<CashFlow> priced = FxResetCashFlowsFunctions.SetFxResetPricer(
            mtm.ResettingLeg,
            new DiscountingFxResetPricer(new USDCurrency(), new EURCurrency(), usdCurve, eurCurve, spot, false));
        FxResetCoupon mtmCoupon = priced.OfType<FxResetCoupon>().Last();
        Coupon refCoupon = reference.Leg(1).OfType<Coupon>().Last();
        CheckClose(mtmCoupon.Nominal(context), refCoupon.Nominal(context), 1.0e-8);
        CheckClose(mtmCoupon.AccruedAmount(today, context), refCoupon.AccruedAmount(today, context), 1.0e-8);
        CheckClose(mtmCoupon.Amount(context), refCoupon.Amount(context), 1.0e-8);
    }

    [Fact]
    public void TestResetExchangePaymentDates()
    {
        // Testing initial, interim and final reset-exchange payment dates...

        var today = new Date(11, September, 2018);
        var context = new EvaluationContext { EvaluationDate = today };

        YieldTermStructure usdCurve = FlatCurve(today, 0.02);
        YieldTermStructure eurCurve = FlatCurve(today, 0.01);
        var usdIndex = new USDLibor(new Period(3, TimeUnit.Months), usdCurve);
        var eurIndex = new Euribor(new Period(3, TimeUnit.Months), eurCurve);

        var cal = new TARGET();

        // Unadjusted schedule with period-end dates falling on weekends: the coupons are lagged, while principal
        // exchanges remain on the effective and maturity dates adjusted with the explicit payment convention.
        var start = new Date(15, September, 2018); // a Saturday
        Schedule sch = Schedule(
            start,
            start + new Period(1, TimeUnit.Years),
            cal,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Forward,
            context);

        double usdNominal = 10000000.0;
        double spotFx = 1.10;
        var swap = new MtMCrossCurrencyBasisSwap(
            MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency,
            usdNominal,
            new USDCurrency(),
            sch,
            usdIndex,
            0.0,
            1.0,
            usdNominal / spotFx,
            new EURCurrency(),
            sch,
            eurIndex,
            0.0,
            1.0,
            isFxBaseCurrencyLegResettable: false,
            context,
            fxResetFixingDays: 0,
            fxResetFixingCalendar: null,
            fxBasePaymentLag: 2,
            fxQuotePaymentLag: 2,
            fxBasePaymentConvention: BusinessDayConvention.Preceding,
            fxQuotePaymentConvention: BusinessDayConvention.Preceding);
        DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate(
            swap, context, new USDCurrency(), usdCurve, new EURCurrency(), eurCurve, spotFx);

        var exchanges = new List<FxResetNotionalExchange>();
        var coupons = new List<Coupon>();
        foreach (CashFlow cf in swap.Leg(swap.ResettingLegIndex))
        {
            if (cf is FxResetNotionalExchange exchange)
            {
                exchanges.Add(exchange);
            }
            else if (cf is Coupon coupon)
            {
                coupons.Add(coupon);
            }
        }

        Assert.Equal(coupons.Count + 1, exchanges.Count);
        Assert.Equal(cal.Adjust(start, BusinessDayConvention.Preceding), exchanges[0].Date);
        Assert.Equal(
            cal.Adjust(start + new Period(1, TimeUnit.Years), BusinessDayConvention.Preceding), exchanges[^1].Date);

        for (int i = 1; i + 1 < exchanges.Count; ++i)
        {
            Assert.NotNull(exchanges[i].CurrentReset);
            Assert.Equal(exchanges[i].CurrentReset!.Value.ValueDate, exchanges[i].Date);
        }

        ImmutableArray<CashFlow> constantLeg = swap.Leg(swap.ConstantLegIndex);
        SimpleCashFlow[] constantNotionalExchanges = [.. constantLeg.OfType<SimpleCashFlow>()];
        Assert.Equal(2, constantNotionalExchanges.Length);

        // Both legs exchange notionals on the same business dates, even when the unadjusted schedule dates fall on a
        // weekend.
        Assert.Equal(exchanges[0].Date, constantNotionalExchanges[0].Date);
        Assert.Equal(exchanges[^1].Date, constantNotionalExchanges[^1].Date);
        Assert.NotEqual(exchanges[^1].Date, coupons[^1].Date);
    }

    private static YieldTermStructure FlatCurve(Date reference, double r) =>
        new FlatForward(reference, r, new Actual360());

    private static Schedule Schedule(
        Date start,
        Date end,
        Calendar cal,
        BusinessDayConvention convention,
        DateGenerationRule rule,
        EvaluationContext context) =>
        new(start, end, new Period(3, TimeUnit.Months), cal, convention, convention, rule, false, context);

    // The fixings of both legs' coupons fixed on or before today: the first leg's at one rate, the second's at the
    // other.
    private static EvaluationContext SeedFixings(
        MtMCrossCurrencyBasisSwap swap, EvaluationContext context, Date today, double first, double second)
    {
        for (int legNo = 0; legNo < 2; ++legNo)
        {
            foreach (CashFlow cf in swap.Leg(legNo))
            {
                if (cf is FloatingRateCoupon frc)
                {
                    Date fd = frc.FixingDate();
                    if (fd <= today)
                    {
                        context = context with
                        {
                            Fixings = context.Fixings.AddFixing(frc.Index, fd, legNo == 0 ? first : second, true),
                        };
                    }
                }
            }
        }

        return context;
    }

    // QL_CHECK_CLOSE, Boost's BOOST_CHECK_CLOSE: |a - b| within the tolerance, in percent, of both values.
    private static void CheckClose(double a, double b, double tolerancePercent)
    {
        double diff = Math.Abs(a - b);
        Assert.True(
            diff <= tolerancePercent / 100.0 * Math.Abs(a) && diff <= tolerancePercent / 100.0 * Math.Abs(b),
            $"difference between {a} and {b} exceeds {tolerancePercent}%");
    }

    // QL_CHECK_SMALL, Boost's BOOST_CHECK_SMALL: |value| within the tolerance.
    private static void CheckSmall(double value, double tolerance) =>
        Assert.True(Math.Abs(value) <= tolerance, $"{value} is not small, tolerance {tolerance}");
}
