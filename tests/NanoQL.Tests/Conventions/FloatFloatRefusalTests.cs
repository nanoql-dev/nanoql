// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The refusals of the float-float swaption, its Gaussian1d engine and its calibration basket, and the Gaussian1d
/// cap/floor engine's of a coupon on another kind of index, by type, message and parameter (playbook §2): QuantLib's
/// texts, a check of an argument an <see cref="ArgumentException"/> naming it, a check of a value already stored a
/// <see cref="NanoQLException"/>. A basket asked of an engine of the other instrument is QuantLib's "argument types do
/// not match", its <c>setupArguments</c>' cast failing; in QuantLib the instrument holds the engine.
/// </summary>
public class FloatFloatRefusalTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());
    private static readonly TARGET Calendar = new();
    private static readonly Date Start = Calendar.Advance(Calendar.Advance(Today, 1, TimeUnit.Years), 2, TimeUnit.Days);
    private static readonly BermudanExercise Exercise = new([.. new[] { 1, 2, 3 }.Select(i => Calendar.Advance(Today, i, TimeUnit.Years))]);

    [Fact]
    public void TheFloatFloatSwaptionsRefusals()
    {
        FloatFloatSwap swap = Swap();
        Assert.Equal("swap", Assert.Throws<ArgumentNullException>(() => new FloatFloatSwaption(null!, Exercise)).ParamName);
        Assert.Equal("exercise", Assert.Throws<ArgumentNullException>(() => new FloatFloatSwaption(swap, null!)).ParamName);
        Assert.Equal("delivery", Assert.Throws<ArgumentOutOfRangeException>(() => new FloatFloatSwaption(swap, Exercise, (SettlementType)7)).ParamName);
        Assert.Equal(
            "settlementMethod",
            Assert.Throws<ArgumentOutOfRangeException>(() => new FloatFloatSwaption(swap, Exercise, SettlementType.Physical, (SettlementMethod)7)).ParamName);

        // the settlement's consistency, at pricing, as QuantLib's validate
        var inconsistent = new FloatFloatSwaption(swap, Exercise, SettlementType.Physical, SettlementMethod.CollateralizedCashPrice);
        Gsr model = Gaussian1dInputs.Constant(Curve, 0.01, 0.01);
        ArgumentException e = Assert.Throws<ArgumentException>(() => Gaussian1dFloatFloatSwaptionEngine.Calculate(inconsistent, Context, model));
        Assert.Equal("settlementMethod", e.ParamName);
        Assert.StartsWith("invalid settlement method for physical settlement", e.Message, StringComparison.Ordinal);

        var engine = new Gaussian1dFloatFloatSwaptionEngine(model);
        var swaption = new FloatFloatSwaption(swap, Exercise);
        var swapIndex = new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), Curve, Curve);
        SwaptionVolatilityStructure vol = Volatility();
        Assert.Equal("engine", Assert.Throws<ArgumentNullException>(() => swaption.CalibrationBasket(null!, swapIndex, vol, Context)).ParamName);
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => swaption.CalibrationBasket(engine, swapIndex, vol, null!)).ParamName);
    }

    [Fact]
    public void TheFloatFloatEnginesRefusals()
    {
        Assert.Equal("model", Assert.Throws<ArgumentNullException>(() => new Gaussian1dFloatFloatSwaptionEngine(null!)).ParamName);
        Gsr model = Gaussian1dInputs.Constant(Curve, 0.01, 0.01);
        var engine = new Gaussian1dFloatFloatSwaptionEngine(model);
        var swaption = new FloatFloatSwaption(Swap(), Exercise);
        Assert.Equal("swaption", Assert.Throws<ArgumentNullException>(() => engine.Calculate(null!, Context)).ParamName);
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => engine.Calculate(swaption, null!)).ParamName);
        Assert.Equal(
            "cash settled (ParYieldCurve) swaptions not priced with Gaussian1dFloatFloatSwaptionEngine",
            Assert.Throws<NanoQLException>(() => engine.Calculate(
                new FloatFloatSwaption(Swap(), Exercise, SettlementType.Cash, SettlementMethod.ParYieldCurve), Context)).Message);

        // a basket of the other instrument's engine
        SwaptionVolatilityStructure vol = Volatility();
        var swapIndex = new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), Curve, Curve);
        var nonstandard = new NonstandardSwaption(
            new Swaption(Gaussian1dInputs.SwapOf(new Euribor6M(Curve), Exercise.Dates[0], 5, 0.03, SwapType.Payer, Context), Exercise), Context);
        Assert.Equal(
            "argument types do not match",
            Assert.Throws<NanoQLException>(() => nonstandard.CalibrationBasket(engine, swapIndex, vol, Context)).Message);
        Assert.Equal(
            "argument types do not match",
            Assert.Throws<NanoQLException>(() => swaption.CalibrationBasket(new Gaussian1dNonstandardSwaptionEngine(model), swapIndex, vol, Context)).Message);
    }

    [Fact]
    public void TheCapFloorEngineRefusesACouponOnAnotherKindOfIndex()
    {
        // fixing on a Wednesday, the index's fixing day
        var index = new BMAIndex(Curve);
        Date start = new(21, Month.January, 2026), end = new(21, Month.January, 2027);
        var coupon = new FloatingRateCoupon(end, 1e6, start, end, 0, index, pricer: new IndexFixingPricer());
        Assert.Equal(
            $"Gaussian1dCapFloorEngine prices Ibor, overnight, CMS and CMS spread coupons only, not {index.Name}'s",
            Assert.Throws<NanoQLException>(
                () => Gaussian1dCapFloorEngine.Calculate(new Cap([coupon], [0.03]), Context, Gaussian1dInputs.Constant(Curve, 0.01, 0.01))).Message);
    }

    private static Schedule Schedule(int months) => new(
        Start,
        Calendar.Advance(Start, 5, TimeUnit.Years),
        new Period(months, TimeUnit.Months),
        Calendar,
        BusinessDayConvention.ModifiedFollowing,
        BusinessDayConvention.ModifiedFollowing,
        DateGenerationRule.Forward,
        false,
        Context);

    private static FloatFloatSwap Swap() => new(
        SwapType.Payer, 1e6, 1e6, Schedule(3), new Euribor3M(Curve), new Actual360(), Schedule(6), new Euribor6M(Curve), new Actual360(), Context);

    private static SwaptionVolatilityStructure Volatility() =>
        ConstantSwaptionVolatility.Create(0, Calendar, BusinessDayConvention.ModifiedFollowing, 0.2, new Actual365Fixed(), Context);

    // A pricer whose rate is the index's fixing, so that the cap's arguments read a forward.
    private sealed class IndexFixingPricer : FloatingRateCouponPricer
    {
        public override double SwapletPrice(FloatingRateCoupon coupon, EvaluationContext context) => 0.0;

        public override double SwapletRate(FloatingRateCoupon coupon, EvaluationContext context) => coupon.IndexFixing(context);

        public override double CapletPrice(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) => 0.0;

        public override double CapletRate(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) => 0.0;

        public override double FloorletPrice(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) => 0.0;

        public override double FloorletRate(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) => 0.0;
    }
}
