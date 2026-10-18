// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// Every public member that takes the evaluation context throws <see cref="ArgumentNullException"/> for a null one,
/// before anything else. The context sits after QuantLib's parameters without defaults and before those with one.
/// </summary>
public class ContextChecksTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.October, 2026) };

    private static readonly Schedule Annual = new(
        new Date(15, Month.January, 2026),
        new Date(15, Month.January, 2030),
        new Period(1, TimeUnit.Years),
        new TARGET(),
        BusinessDayConvention.Following,
        BusinessDayConvention.Following,
        DateGenerationRule.Backward,
        false,
        Context);

    [Fact]
    public void ScheduleMembersRefuseANullContext()
    {
        // With an effective date given, the constructor never reads the context, and still refuses a null one.
        AssertContext(() => new Schedule(
            new Date(15, Month.January, 2026),
            new Date(15, Month.January, 2030),
            new Period(1, TimeUnit.Years),
            new TARGET(),
            BusinessDayConvention.Following,
            BusinessDayConvention.Following,
            DateGenerationRule.Backward,
            false,
            null!));
        AssertContext(() => Annual.LowerBound(null!, new Date(1, Month.March, 2027)));
        AssertContext(() => Annual.NextDate(new Date(1, Month.March, 2027), null!));
        AssertContext(() => Annual.PreviousDate(new Date(1, Month.March, 2027), null!));
    }

    [Fact]
    public void AMissingReferenceDateIsTheEvaluationDate()
    {
        // QuantLib's lower_bound(const Date& d = Date()) defaults its date, so the context comes first and the date
        // keeps its default; nextDate(const Date& refDate) and previousDate(const Date& refDate) have none, so the
        // date comes first. Each reads the evaluation date for a null date.
        Assert.Equal(Annual.LowerBound(Context, Context.EvaluationDate), Annual.LowerBound(Context));
        Assert.Equal(new Date(15, Month.January, 2027), Annual.NextDate(null, Context));
        Assert.Equal(new Date(15, Month.January, 2026), Annual.PreviousDate(null, Context));
    }

    [Fact]
    public void CashFlowCurveAndSwapMembersRefuseANullContext()
    {
        Date valid = Context.EvaluationDate;
        var cashFlow = new SimpleCashFlow(1.0, valid + 10);
        ImmutableArray<CashFlow> leg = [cashFlow];
        var curve = new FlatForward(valid, 0.03, new Actual365Fixed());
        var swap = new Swap(leg, leg);

        AssertContext(() => new AnEvent(valid).HasOccurred(null!));
        AssertContext(() => cashFlow.HasOccurred(null!, valid));
        AssertContext(() => cashFlow.TradingExCoupon(null!, valid));

        // An empty leg, which the functions value at 0 without reading the context, still needs one.
        AssertContext(() => CashFlows.Npv([], curve, null!));
        AssertContext(() => CashFlows.Bps([], curve, null!));
        AssertContext(() => CashFlows.NpvBps([], curve, null!));
        AssertContext(() => swap.IsExpired(null!));
        AssertContext(() => DiscountingSwapEngine.Calculate(swap, null!, curve));
        AssertContext(() => FlatForward.Create(2, new TARGET(), 0.03, new Actual365Fixed(), null!));

        // The cash-flow contract: every amount, rate and nominal takes the context, and a flow that knows its amount
        // checks it all the same.
        var coupon = new FixedRateCoupon(valid + 180, 100.0, 0.03, new Actual360(), valid, valid + 180);
        AssertContext(() => cashFlow.Amount(null!));
        AssertContext(() => coupon.Amount(null!));
        AssertContext(() => coupon.Rate(null!));
        AssertContext(() => coupon.Nominal(null!));
        AssertContext(() => coupon.AccruedAmount(valid + 10, null!));
        AssertContext(() => CashFlows.AccruedPeriod([], null!));
        AssertContext(() => CashFlows.AccruedDays([], null!));
        AssertContext(() => CashFlows.AccruedAmount([], null!));

        // The index reads.
        var euribor = new IborIndex(
            "Euribor", new Period(6, TimeUnit.Months), 2, null, new TARGET(), BusinessDayConvention.ModifiedFollowing,
            true, new Actual360(), curve);
        AssertContext(() => euribor.Fixing(valid, null!));
        AssertContext(() => euribor.PastFixing(valid, null!));
        AssertContext(() => euribor.HasHistoricalFixing(valid, null!));
        AssertContext(() => euribor.TimeSeries(null!));
        AssertContext(() => euribor.ForecastFixing(valid, null!));
    }

    [Fact]
    public void CashFlowMembersReadTheContextForAMissingDateOrFlag()
    {
        // QuantLib's tradingExCoupon(const Date& refDate = Date()) and hasOccurred(refDate = Date(), includeRefDate =
        // nullopt) read the evaluation date, and CashFlows' functions read includeReferenceDateEvents when
        // includeSettlementDateFlows is nullopt, through hasOccurred.
        Date today = Context.EvaluationDate;
        var coupon = new FixedRateCoupon(
            today + 10, 100.0, 0.03, new Actual360(), today - 170, today + 10, null, null, today - 1);
        Assert.True(coupon.TradingExCoupon(Context));
        Assert.False(coupon.TradingExCoupon(Context with { EvaluationDate = today - 2 }));
        Assert.Equal(coupon.TradingExCoupon(Context, today), coupon.TradingExCoupon(Context));

        // flows at T+0, T+1 and T+2, on a curve that doesn't discount
        ImmutableArray<CashFlow> leg = [new SimpleCashFlow(1.0, today), new SimpleCashFlow(1.0, today + 1),
            new SimpleCashFlow(1.0, today + 2)];
        var noDiscount = new FlatForward(today, 0.0, new Actual365Fixed());
        EvaluationContext excluding = Context with { IncludeReferenceDateEvents = false };
        EvaluationContext including = Context with { IncludeReferenceDateEvents = true };
        Assert.False(leg[0].HasOccurred(including));
        Assert.True(leg[0].HasOccurred(excluding));
        Assert.Equal(2.0, CashFlows.Npv(leg, noDiscount, excluding), 1e-12);
        Assert.Equal(3.0, CashFlows.Npv(leg, noDiscount, including), 1e-12);
        Assert.Equal(CashFlows.Npv(leg, noDiscount, including, true), CashFlows.Npv(leg, noDiscount, including));
        Assert.Equal(CashFlows.Bps(leg, noDiscount, excluding, false), CashFlows.Bps(leg, noDiscount, excluding));
        Assert.Equal(CashFlows.NpvBps(leg, noDiscount, including, true), CashFlows.NpvBps(leg, noDiscount, including));
        Assert.Equal(2.0, CashFlows.Npv(leg, noDiscount, including with { IncludeTodaysCashFlows = false }), 1e-12);
    }

    private static void AssertContext(Action action) =>
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(action).ParamName);

    private sealed class AnEvent(Date date) : Event
    {
        public override Date Date => date;
    }
}
