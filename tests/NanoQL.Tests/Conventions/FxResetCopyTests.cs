// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// Setting an FX reset pricer returns copies (the S-rx plan's SRXD10, PR6.4b): QuantLib's <c>setFxResetPricer</c> sets
/// the pricer on the coupon or exchange it is given, and its leg function on each of the leg's flows; nanoql's return
/// a coupon, an exchange or a leg holding the pricer and leave the receiver as it was. The MtM engine prices copies of
/// the resettable leg with its own pricer, as QuantLib's 1.44 engine prices "local wrappers"
/// (<c>discountingmtmcrosscurrencybasisswapengine.cpp:160-175</c>), so the swap it prices keeps its pricer-less leg.
/// </summary>
public class FxResetCopyTests
{
    private static readonly Date Today = new(15, January, 2025);

    [Fact]
    public void SettingAPricerReturnsCopies()
    {
        EvaluationContext context = Context();
        Schedule schedule = Schedule(context);
        FloatingRateCoupon underlying =
            (FloatingRateCoupon)new IborLeg(schedule, new USDLibor(new Period(3, TimeUnit.Months), UsdCurve()))
                .WithNotionals(1.1e6)
                .ToLeg(context)[1];
        var reset = new FxReset(new Date(15, April, 2025), new Date(17, April, 2025));
        var coupon = new FxResetCoupon(underlying, 1e6, reset);
        var exchange = new FxResetNotionalExchange(new Date(17, April, 2025), 1e6, null, reset);
        DiscountingFxResetPricer pricer = Pricer();

        FxResetCoupon priced = coupon.SetFxResetPricer(pricer);
        Assert.NotSame(coupon, priced);
        Assert.Same(pricer, priced.FxResetPricer);
        Assert.Null(coupon.FxResetPricer);
        Assert.Throws<NanoQLException>(() => coupon.Nominal(context));
        Assert.Equal(1e6 * pricer.FxRate(coupon.FxReset, context), priced.Nominal(context));

        FxResetNotionalExchange pricedExchange = exchange.SetFxResetPricer(pricer);
        Assert.NotSame(exchange, pricedExchange);
        Assert.Null(exchange.FxResetPricer);
        Assert.Throws<NanoQLException>(() => exchange.Amount(context));
        Assert.Equal(-1e6 * pricer.FxRate(exchange.CurrentReset!.Value, context), pricedExchange.Amount(context));

        ImmutableArray<CashFlow> leg = [coupon, exchange, new SimpleCashFlow(1e6, new Date(17, April, 2025))];
        ImmutableArray<CashFlow> pricedLeg = FxResetCashFlowsFunctions.SetFxResetPricer(leg, pricer);
        Assert.Same(pricer, ((FxResetCoupon)pricedLeg[0]).FxResetPricer);
        Assert.Same(pricer, ((FxResetNotionalExchange)pricedLeg[1]).FxResetPricer);
        Assert.Same(leg[2], pricedLeg[2]);
        Assert.Null(((FxResetCoupon)leg[0]).FxResetPricer);
        Assert.Null(((FxResetNotionalExchange)leg[1]).FxResetPricer);
    }

    [Fact]
    public void TheMtMEngineLeavesTheSwapsLegAsItWas()
    {
        EvaluationContext context = Context();
        Schedule schedule = Schedule(context);
        var swap = new MtMCrossCurrencyBasisSwap(
            MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency,
            1e6,
            new EURCurrency(),
            schedule,
            new Euribor3M(EurCurve()),
            0.0,
            1.0,
            1.1e6,
            new USDCurrency(),
            schedule,
            new USDLibor(new Period(3, TimeUnit.Months), UsdCurve()),
            0.0,
            1.0,
            false,
            context);
        ImmutableArray<CashFlow> before = swap.ResettingLeg;

        MtMCrossCurrencyBasisSwapResults Price() => DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate(
            swap, context, new USDCurrency(), UsdCurve(), new EURCurrency(), EurCurve(), 1.1);
        MtMCrossCurrencyBasisSwapResults first = Price();

        Assert.True(before.SequenceEqual(swap.ResettingLeg));
        foreach (CashFlow cf in swap.ResettingLeg)
        {
            switch (cf)
            {
                case FxResetCoupon c:
                    Assert.Null(c.FxResetPricer);
                    Assert.Throws<NanoQLException>(() => c.Amount(context));
                    break;
                case FxResetNotionalExchange x:
                    Assert.Null(x.FxResetPricer);
                    break;
            }
        }

        Assert.Equal(first, Price());
        Assert.NotEmpty(first.FxResetRates);

        // QuantLib's setFxResetPricer(swap->leg(j), pricer), through the swap overload: a copy of the swap, the leg's
        // flows holding the pricer, the other leg and the terms kept
        DiscountingFxResetPricer pricer = Pricer();
        MtMCrossCurrencyBasisSwap priced =
            FxResetCashFlowsFunctions.SetFxResetPricer(swap, swap.ResettingLegIndex, pricer);
        Assert.NotSame(swap, priced);
        Assert.All(priced.ResettingLeg.OfType<FxResetCoupon>(), c => Assert.Same(pricer, c.FxResetPricer));
        Assert.All(priced.ResettingLeg.OfType<FxResetNotionalExchange>(), x => Assert.Same(pricer, x.FxResetPricer));
        Assert.All(swap.ResettingLeg.OfType<FxResetCoupon>(), c => Assert.Null(c.FxResetPricer));
        Assert.True(swap.ConstantLeg.SequenceEqual(priced.ConstantLeg));
        Assert.Equal(swap.FxBaseNominal, priced.FxBaseNominal);
        Assert.Equal(swap.ResettingLegIndex, priced.ResettingLegIndex);
        Assert.Throws<ArgumentException>(() => FxResetCashFlowsFunctions.SetFxResetPricer(swap, 2, pricer));
    }

    [Fact]
    public void ACouponPricerSetOnTheLegReachesTheUnderlying()
    {
        // QuantLib's FxResetCoupon::setPricer sets the pricer on its underlying too (fxresetcashflows.hpp:140-143): an
        // in-arrears Ibor coupon's convexity adjustment, which reads the pricer's volatility, moves its amount
        EvaluationContext context = Context();
        FloatingRateCoupon underlying =
            (FloatingRateCoupon)new IborLeg(Schedule(context), new USDLibor(new Period(3, TimeUnit.Months), UsdCurve()))
                .WithNotionals(1.1e6)
                .InArrears()
                .ToLeg(context)[1];
        var reset = new FxReset(new Date(15, April, 2025), new Date(17, April, 2025));
        FxResetCoupon coupon = new FxResetCoupon(underlying, 1e6, reset).SetFxResetPricer(Pricer());
        ImmutableArray<CashFlow> leg = [coupon];

        double Amount(double volatility)
        {
            var pricer = new BlackIborCouponPricer(
                context,
                new ConstantOptionletVolatility(
                    Today, new TARGET(), BusinessDayConvention.Following, volatility, new Actual365Fixed()));
            var priced = (FxResetCoupon)CouponPricerFunctions.SetCouponPricer(leg, pricer)[0];
            Assert.Same(pricer, priced.Pricer);
            Assert.Same(pricer, priced.Underlying.Pricer);
            Assert.Equal(priced.Underlying.Rate(context), priced.Rate(context));
            return priced.Amount(context);
        }

        Assert.NotEqual(Amount(0.2), Amount(0.4));

        // the leg given keeps its coupon and the coupon its underlying's pricer
        Assert.Same(coupon, leg[0]);
        Assert.Same(underlying.Pricer, coupon.Underlying.Pricer);
    }

    private static EvaluationContext Context() => new() { EvaluationDate = Today };

    private static YieldTermStructure EurCurve() => new FlatForward(Today, 0.025, new Actual365Fixed());

    private static YieldTermStructure UsdCurve() => new FlatForward(Today, 0.04, new Actual365Fixed());

    // A year, quarterly, from T+2 on TARGET.
    private static Schedule Schedule(EvaluationContext context) =>
        new(
            new Date(17, January, 2025),
            new Date(17, January, 2026),
            new Period(3, TimeUnit.Months),
            new TARGET(),
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Backward,
            false,
            context);

    private static DiscountingFxResetPricer Pricer() => new(
        new EURCurrency(), new USDCurrency(), EurCurve(), UsdCurve(), 1.1, true);
}
