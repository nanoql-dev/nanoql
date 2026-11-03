// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Currencies;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The constant-notional cross-currency engine's spot with an explicit FX settlement date and an NPV date away from the
/// curves' reference date (fixed). QuantLib's engine takes its spot as the NPV date's rate, "w.r.t. a settlement equal
/// to the npv date", and uses it as is when no FX settlement date is given
/// (<c>discountingconstnotionalcrosscurrencyswapengine.hpp:54-67</c>, <c>.cpp:127</c>); given one, it moves the spot to
/// the reference date (<c>.cpp:128-137</c>) and multiplies values discounted to the NPV date by it, so a USD 1.1M
/// against EUR 1M two-year swap priced three months on is off its consistent NPV by −46.593470, with a settlement date
/// of T+2 and with one on the reference date, which QuantLib reads as unset (<c>:74-77</c>). nanoql moves an explicitly
/// settled spot to the NPV date, <c>S × D_dom(s)/D_for(s) × D_for(npv)/D_dom(npv)</c>, as QuantLib's own MtM engine
/// does (<c>discountingmtmcrosscurrencybasisswapengine.cpp:103-121</c>); without one, and with the NPV date on the
/// reference date, QuantLib's bits stand. QuantLib's check: its own engine with no settlement date and the spot moved
/// to the NPV date, 26,787.661557906977 (QuantLib-Python 1.44).
/// </summary>
public class CrossCurrencySwapNpvDateFxTests
{
    private const double ConsistentNpv = 26787.661557906977;
    private static readonly Date Today = new(15, March, 2024);
    private static readonly Date NpvDate = new(15, June, 2024);

    [Fact]
    public void AnExplicitFxSettlementDateMovesTheSpotToTheNpvDate()
    {
        Date settle = new TARGET().Advance(Today, 2, TimeUnit.Days);

        // QuantLib: 26,741.06808747431, its EUR leg 4,598.105647591939
        CrossCurrencySwapResults results = Price(SpotAt(settle), NpvDate, settle);
        Assert.Equal(ConsistentNpv, results.NPV!.Value, 1e-8);
        Assert.Equal(4644.699118024609, results.LegNPV[1]!.Value, 1e-8);
        Assert.Equal(22142.96243988237, results.LegNPV[0]!.Value, 1e-8);

        // QuantLib's bits where no settlement date is given: the spot read as the NPV date's
        Assert.Equal(ConsistentNpv, Price(SpotAt(NpvDate), NpvDate, null).NPV!.Value, 1e-8);
    }

    [Fact]
    public void AnExplicitFxSettlementDateOnTheReferenceDateMovesTheSpotToo()
    {
        // QuantLib reads the reference date as no settlement date: 26,741.06808747431
        Assert.Equal(ConsistentNpv, Price(1.1, NpvDate, Today).NPV!.Value, 1e-8);

        // QuantLib's bits with the NPV date on the reference date: 26,452.182052183267 with the spot settled either day
        Assert.Equal(26452.182052183267, Price(1.1, null, Today).NPV!.Value, 1e-8);
        Assert.Equal(26452.182052183267, Price(1.1, null, null).NPV!.Value, 1e-8);
    }

    private static YieldTermStructure UsdCurve() => new FlatForward(Today, 0.05, new Actual365Fixed());

    private static YieldTermStructure EurCurve() => new FlatForward(Today, 0.01, new Actual365Fixed());

    // USD per EUR for settlement on the date, by covered interest parity from 1.1 on the reference date.
    private static double SpotAt(Date d) => 1.1 * EurCurve().Discount(d) / UsdCurve().Discount(d);

    // USD 1.1M paying 4% against EUR 1M at 1.2%, annual over two years from T+2, the notionals exchanged; USD
    // domestic.
    private static CrossCurrencySwapResults Price(double spot, Date? npvDate, Date? fxSettle)
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        var calendar = new TARGET();
        Date start = calendar.Advance(Today, 2, TimeUnit.Days);
        Date end = start + new Period(2, TimeUnit.Years);
        var schedule = new Schedule(
            start,
            end,
            new Period(Frequency.Annual),
            calendar,
            BusinessDayConvention.Following,
            BusinessDayConvention.Following,
            DateGenerationRule.Forward,
            false,
            context);
        ImmutableArray<CashFlow> Leg(double notional, double rate) =>
        [
            new SimpleCashFlow(-notional, start),
            .. new FixedRateLeg(schedule).WithNotionals(notional).WithCouponRates(rate, new Actual360()).ToLeg(),
            new SimpleCashFlow(notional, end),
        ];
        var swap = new ConstNotionalCrossCurrencySwap(
            Leg(1.1e6, 0.04), new USDCurrency(), Leg(1e6, 0.012), new EURCurrency());
        return DiscountingConstNotionalCrossCurrencySwapEngine.Calculate(
            swap,
            context,
            new USDCurrency(),
            UsdCurve(),
            new EURCurrency(),
            EurCurve(),
            spot,
            npvDate: npvDate,
            spotFxSettleDate: fxSettle);
    }
}
