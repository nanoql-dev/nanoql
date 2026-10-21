// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The cross-currency swaps' fair spread on an overnight leg compounding its spread daily (the S-rx plan's C3 review,
/// B1-2, fixed under R8). Each swap derives a leg's fair spread from its BPS, <c>spread − NPV/(BPS/1bp)</c>
/// (<c>constnotionalcrosscurrencybasisswap.cpp:180-187</c>,
/// <c>constnotionalcrosscurrencyfixedvsfloatingswap.cpp:161-163</c>, <c>mtmcrosscurrencybasisswap.cpp:235-249</c>),
/// which is exact only while the leg's amount moves one for one with its spread; compounded daily, the amount is
/// convex in it. On a five-year basis swap paying SOFR compounded with a 2-day lookback (QuantLib's path that
/// compounds the spread) against ESTR + 1%, QuantLib's fair pay spread leaves the swap worth −2,387.78 on 1,100,000.
/// nanoql solves a compounding leg's fair spread on the swap's own pricing, as C2's float-float swap does for capped
/// legs; a leg that doesn't compound keeps QuantLib's BPS route and its bits. QuantLib's check: its own NPV, whose
/// Brent root the solved spreads equal [executed on the wheel].
/// </summary>
public class CrossCurrencySwapCompoundedSpreadFairSpreadTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly YieldTermStructure UsdCurve = new FlatForward(Today, 0.04, new Actual365Fixed());
    private static readonly YieldTermStructure EurCurve = new FlatForward(Today, 0.025, new Actual365Fixed());

    [Fact]
    public void TheBasisSwapsCompoundingLegsFairSpreadIsSolved()
    {
        EvaluationContext context = Context();

        // QuantLib's BPS route: 0.010453752958708536, worth −2,387.78 on 1,100,000
        ConstNotionalCrossCurrencyBasisSwapResults compounded = Price(Basis(0.0, true, context), context);
        Assert.Equal(0.009995536930800793, compounded.FairPaySpread!.Value, 1e-11);
        Assert.True(Math.Abs(Price(Basis(compounded.FairPaySpread.Value, true, context), context).NPV!.Value) < 1e-6);
        Assert.Equal(-1.4686990135372863e-06, compounded.FairRecSpread!.Value, 1e-18);

        // not compounded: QuantLib's BPS route
        ConstNotionalCrossCurrencyBasisSwapResults added = Price(Basis(0.0, false, context), context);
        Assert.Equal(0.010453752958708536, added.FairPaySpread!.Value, 1e-18);
    }

    [Fact]
    public void TheFixedVsFloatingSwapsCompoundingLegsFairSpreadIsSolved()
    {
        EvaluationContext context = Context();
        Schedule schedule = Schedule(context);
        ConstNotionalCrossCurrencyFixedVsFloatingSwap Swap(double spread) => new(
            SwapType.Payer,
            1e6,
            new EURCurrency(),
            schedule,
            0.025,
            new Actual360(),
            BusinessDayConvention.ModifiedFollowing,
            0,
            Calendar(),
            1.1e6,
            new USDCurrency(),
            schedule,
            new Sofr(UsdCurve),
            spread,
            BusinessDayConvention.ModifiedFollowing,
            0,
            Calendar(),
            context,
            floatCompoundSpread: true,
            floatLookbackDays: 2);
        ConstNotionalCrossCurrencyFixedVsFloatingSwapResults Price(double spread) =>
            DiscountingConstNotionalCrossCurrencySwapEngine.Calculate(
                Swap(spread), context, new USDCurrency(), UsdCurve, new EURCurrency(), EurCurve, 1.1);

        // QuantLib's BPS route: 3.4619617563126026e-05
        ConstNotionalCrossCurrencyFixedVsFloatingSwapResults results = Price(0.0);
        Assert.Equal(3.326893049696357e-05, results.FairSpread!.Value, 1e-11);
        Assert.True(Math.Abs(Price(results.FairSpread.Value).NPV!.Value) < 1e-6);
        Assert.Equal(0.02496687820892678, results.FairFixedRate!.Value, 1e-18);
    }

    [Fact]
    public void TheMtMSwapsCompoundingLegsFairSpreadIsSolved()
    {
        EvaluationContext context = Context();
        Schedule schedule = Schedule(context);
        MtMCrossCurrencyBasisSwap Swap(double quoteSpread) => new(
            MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency,
            1e6,
            new EURCurrency(),
            schedule,
            new Estr(EurCurve),
            0.0,
            1.0,
            1.1e6,
            new USDCurrency(),
            schedule,
            new Sofr(UsdCurve),
            quoteSpread,
            1.0,
            false,
            context,
            fxQuoteCompoundSpread: true,
            fxQuoteLookbackDays: 2);
        MtMCrossCurrencyBasisSwapResults Price(double quoteSpread) =>
            DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate(
                Swap(quoteSpread), context, new USDCurrency(), UsdCurve, new EURCurrency(), EurCurve, 1.1);

        // QuantLib's BPS route: 1.5346935705734172e-06
        MtMCrossCurrencyBasisSwapResults results = Price(0.0);
        Assert.Equal(1.4748418404496015e-06, results.FairFxQuoteSpread!.Value, 1e-11);
        Assert.True(Math.Abs(Price(results.FairFxQuoteSpread.Value).NPV!.Value) < 1e-6);
        Assert.Equal(-1.511844533501542e-06, results.FairFxBaseSpread!.Value, 1e-18);
    }

    private static EvaluationContext Context() => new() { EvaluationDate = Today };

    private static Calendar Calendar() =>
        new JointCalendar(new UnitedStates(UnitedStatesMarket.GovernmentBond), new TARGET());

    // Five years from T+2, annual, Modified Following, backward: QuantLib's MakeSchedule.
    private static Schedule Schedule(EvaluationContext context)
    {
        Calendar calendar = Calendar();
        Date start = calendar.Advance(Today, 2, TimeUnit.Days);
        Date end = calendar.Advance(start, new Period(5, TimeUnit.Years));
        return new MakeSchedule()
            .From(start)
            .To(end)
            .WithTenor(new Period(1, TimeUnit.Years))
            .WithCalendar(calendar)
            .WithConvention(BusinessDayConvention.ModifiedFollowing)
            .ToSchedule();
    }

    // USD 1,100,000 paying SOFR with a 2-day lookback, its spread compounded daily or added, against EUR 1,000,000
    // receiving ESTR + 1%.
    private static ConstNotionalCrossCurrencyBasisSwap Basis(double paySpread, bool compound, EvaluationContext context)
    {
        Schedule schedule = Schedule(context);
        return new ConstNotionalCrossCurrencyBasisSwap(
            1.1e6,
            new USDCurrency(),
            schedule,
            new Sofr(UsdCurve),
            paySpread,
            1.0,
            1e6,
            new EURCurrency(),
            schedule,
            new Estr(EurCurve),
            0.01,
            1.0,
            context,
            payCompoundSpread: compound,
            payLookbackDays: 2);
    }

    private static ConstNotionalCrossCurrencyBasisSwapResults Price(
        ConstNotionalCrossCurrencyBasisSwap swap, EvaluationContext context) =>
        DiscountingConstNotionalCrossCurrencySwapEngine.Calculate(
            swap, context, new USDCurrency(), UsdCurve, new EURCurrency(), EurCurve, 1.1);
}
