// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The constant-notional cross-currency swaps' fair spread and fixed rate on a leg whose BPS is zero (fixed). Both
/// swaps test the leg's BPS against <c>Null</c> alone before dividing by it
/// (<c>constnotionalcrosscurrencybasisswap.cpp:180-187</c>,
/// <c>constnotionalcrosscurrencyfixedvsfloatingswap.cpp:157-163</c>), so a zero-notional leg's fair pay spread and fair
/// rate are an infinity. nanoql gives null, the results' "not available", as QuantLib's sibling swaps do with their
/// <c>legBPS != 0</c> checks (<c>fixedvsfloatingswap.cpp:201-207</c>, <c>floatfloatswap.cpp:560</c>, <c>:566</c>;
/// <c>mtmcrosscurrencybasisswap.cpp:238-246</c>, which refuses); the other leg's keeps QuantLib's value. QuantLib's
/// check: those siblings, and its <c>testZeroBpsFairSpread</c>, which asserts the float-float swap's "fair spread 1 not
/// available" (QuantLib-Python 1.44).
/// </summary>
public class CrossCurrencySwapZeroBpsTests
{
    private static readonly Date Today = new(15, June, 2026);

    [Fact]
    public void TheBasisSwapsFairPaySpreadOnAZeroNotionalLegIsNull()
    {
        EvaluationContext context = Context();
        var swap = new ConstNotionalCrossCurrencyBasisSwap(
            0.0,
            new EURCurrency(),
            Schedule(3, context),
            new Euribor3M(EurCurve()),
            0.001,
            1.0,
            110.0,
            new USDCurrency(),
            Schedule(12, context),
            new Sofr(UsdCurve()),
            0.0,
            1.0,
            context);
        ConstNotionalCrossCurrencyBasisSwapResults results = Calculate(swap, context);

        // QuantLib: inf; the receiving leg's QuantLib's formula, -9.026037678863755e-09 on the wheel, the NPV's
        // roundoff over the leg's BPS
        Assert.Equal(0.0, results.LegBPS[0]);
        Assert.Null(results.FairPaySpread);
        Assert.Equal(0.0 - (results.NPV!.Value / (results.LegBPS[1]!.Value / 1.0e-4)), results.FairRecSpread);
        Assert.True(Math.Abs(results.FairRecSpread!.Value) < 1e-7);
    }

    [Fact]
    public void TheFixedVsFloatingSwapsFairRateOnAZeroNotionalLegIsNull()
    {
        EvaluationContext context = Context();
        var calendar = new TARGET();
        var swap = new ConstNotionalCrossCurrencyFixedVsFloatingSwap(
            SwapType.Payer,
            0.0,
            new EURCurrency(),
            Schedule(12, context),
            0.02,
            new Thirty360(Thirty360Convention.BondBasis),
            BusinessDayConvention.ModifiedFollowing,
            0,
            calendar,
            110.0,
            new USDCurrency(),
            Schedule(12, context),
            new Sofr(UsdCurve()),
            0.0,
            BusinessDayConvention.ModifiedFollowing,
            0,
            calendar,
            context);
        ConstNotionalCrossCurrencyFixedVsFloatingSwapResults results = Calculate(swap, context);

        // QuantLib: inf; the floating leg's QuantLib's formula, -9.026037678863755e-09 on the wheel, the NPV's
        // roundoff over the leg's BPS
        Assert.Equal(0.0, results.LegBPS[0]);
        Assert.Null(results.FairFixedRate);
        Assert.Equal(0.0 - (results.NPV!.Value / (results.LegBPS[1]!.Value / 1.0e-4)), results.FairSpread);
        Assert.True(Math.Abs(results.FairSpread!.Value) < 1e-7);
    }

    private static EvaluationContext Context() => new() { EvaluationDate = Today };

    private static YieldTermStructure UsdCurve() => new FlatForward(Today, 0.04, new Actual365Fixed());

    private static YieldTermStructure EurCurve() => new FlatForward(Today, 0.02, new Actual365Fixed());

    // Two years from T+2 on TARGET, every given number of months.
    private static Schedule Schedule(int months, EvaluationContext context)
    {
        var calendar = new TARGET();
        Date start = calendar.Advance(Today, 2, TimeUnit.Days);
        return new MakeSchedule()
            .From(start)
            .To(start + new Period(2, TimeUnit.Years))
            .WithTenor(new Period(months, TimeUnit.Months))
            .WithCalendar(calendar)
            .ToSchedule();
    }

    private static ConstNotionalCrossCurrencyBasisSwapResults Calculate(
        ConstNotionalCrossCurrencyBasisSwap swap, EvaluationContext context) =>
        DiscountingConstNotionalCrossCurrencySwapEngine.Calculate(
            swap, context, new USDCurrency(), UsdCurve(), new EURCurrency(), EurCurve(), 1.1);

    private static ConstNotionalCrossCurrencyFixedVsFloatingSwapResults Calculate(
        ConstNotionalCrossCurrencyFixedVsFloatingSwap swap, EvaluationContext context) =>
        DiscountingConstNotionalCrossCurrencySwapEngine.Calculate(
            swap, context, new USDCurrency(), UsdCurve(), new EURCurrency(), EurCurve(), 1.1);
}
