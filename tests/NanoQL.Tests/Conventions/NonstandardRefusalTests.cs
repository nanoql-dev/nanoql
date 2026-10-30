// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
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
/// The refusals of the rebated exercise, the nonstandard swap and swaption, the nonstandard engine, its calibration
/// basket and the Gaussian1d cap/floor engine, by type, message and parameter (playbook §2; the S-g1d plan's G1D17):
/// QuantLib's texts, a check of an argument an <see cref="ArgumentException"/> naming it, a check of a value already
/// stored a
/// <see cref="NanoQLException"/>.
/// </summary>
public class NonstandardRefusalTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());
    private static readonly TARGET Calendar = new();
    private static readonly Date Start = Calendar.Advance(Calendar.Advance(Today, 1, TimeUnit.Years), 2, TimeUnit.Days);
    private static readonly ImmutableArray<Date> Exercises = [.. new[] { 1, 2, 3 }.Select(i => Calendar.Advance(Today, i, TimeUnit.Years))];

    [Fact]
    public void TheRebatedExercisesRefusals()
    {
        var bermudan = new BermudanExercise(Exercises);
        Assert.Equal("exercise", Assert.Throws<ArgumentNullException>(() => new RebatedExercise(null!, 0.0)).ParamName);
        Assert.Equal("exercise", Assert.Throws<ArgumentNullException>(() => new RebatedExercise(null!, [1.0])).ParamName);
        Assert.Equal("rebates", Assert.Throws<ArgumentNullException>(() => new RebatedExercise(bermudan, (IEnumerable<double>)null!)).ParamName);
        Assert.Equal("rebateSettlementDays", Assert.Throws<ArgumentOutOfRangeException>(() => new RebatedExercise(bermudan, 0.0, -1)).ParamName);
        Assert.Equal(
            "rebatePaymentConvention",
            Assert.Throws<ArgumentOutOfRangeException>(() => new RebatedExercise(bermudan, [1.0, 2.0, 3.0], 0, null, (BusinessDayConvention)99)).ParamName);
        Check(() => new RebatedExercise(new EuropeanExercise(Exercises[0]), [1.0]), "exercise", "a rebate vector is allowed only for a bermudan style exercise");

        // QuantLib's text, its closing parenthesis missing
        Check(() => new RebatedExercise(bermudan, [1.0, 2.0]), "rebates", "the number of rebates (2) must be equal to the number of exercise dates (3 (");

        var rebated = new RebatedExercise(bermudan, [1.0, 2.0, 3.0]);
        Check(() => rebated.Rebate(3), "index", "rebate with index 3 does not exist (0...2)");
        Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(() => rebated.Rebate(-1)).ParamName);
        Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(() => rebated.RebatePaymentDate(3)).ParamName);
        Assert.Equal(
            "for american style exercises the rebate payment date has to be calculted in the client code",
            Assert.Throws<NanoQLException>(() => new RebatedExercise(new AmericanExercise(Today, Exercises[0]), 1.0).RebatePaymentDate(0)).Message);
    }

    [Fact]
    public void TheNonstandardSwapsRefusals()
    {
        Schedule fixedSchedule = Schedule(12), floatingSchedule = Schedule(6);
        var index = new Euribor6M(Curve);
        var dc = new Thirty360(Thirty360Convention.BondBasis);
        double[] five = [1.0, 1.0, 1.0, 1.0, 1.0], ten = [.. Enumerable.Repeat(1.0, 10)], rates = [0.03, 0.03, 0.03, 0.03, 0.03];
        NonstandardSwap Swap(
            SwapType type = SwapType.Payer,
            double[]? fixedNominal = null,
            double[]? floatingNominal = null,
            double[]? fixedRate = null,
            double[]? gearing = null,
            double[]? spread = null,
            BusinessDayConvention convention = BusinessDayConvention.ModifiedFollowing) => new(
            type,
            fixedNominal ?? five,
            floatingNominal ?? ten,
            fixedSchedule,
            fixedRate ?? rates,
            dc,
            floatingSchedule,
            index,
            gearing ?? ten,
            spread ?? [.. Enumerable.Repeat(0.0, 10)],
            new Actual360(),
            Context,
            paymentConvention: convention);

        Assert.Equal("fixedNominal", Assert.Throws<ArgumentNullException>(() => new NonstandardSwap(
            SwapType.Payer, null!, ten, fixedSchedule, rates, dc, floatingSchedule, index, 1.0, 0.0, null, Context)).ParamName);
        Assert.Equal("floatingNominal", Assert.Throws<ArgumentNullException>(() => new NonstandardSwap(
            SwapType.Payer, five, null!, fixedSchedule, rates, dc, floatingSchedule, index, 1.0, 0.0, null, Context)).ParamName);
        Assert.Equal("fixedSchedule", Assert.Throws<ArgumentNullException>(() => new NonstandardSwap(
            SwapType.Payer, five, ten, null!, rates, dc, floatingSchedule, index, 1.0, 0.0, null, Context)).ParamName);
        Assert.Equal("fixedRate", Assert.Throws<ArgumentNullException>(() => new NonstandardSwap(
            SwapType.Payer, five, ten, fixedSchedule, null!, dc, floatingSchedule, index, 1.0, 0.0, null, Context)).ParamName);
        Assert.Equal("fixedDayCount", Assert.Throws<ArgumentNullException>(() => new NonstandardSwap(
            SwapType.Payer, five, ten, fixedSchedule, rates, null!, floatingSchedule, index, 1.0, 0.0, null, Context)).ParamName);
        Assert.Equal("floatingSchedule", Assert.Throws<ArgumentNullException>(() => new NonstandardSwap(
            SwapType.Payer, five, ten, fixedSchedule, rates, dc, null!, index, 1.0, 0.0, null, Context)).ParamName);
        Assert.Equal("iborIndex", Assert.Throws<ArgumentNullException>(() => new NonstandardSwap(
            SwapType.Payer, five, ten, fixedSchedule, rates, dc, floatingSchedule, null!, 1.0, 0.0, null, Context)).ParamName);
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => new NonstandardSwap(
            SwapType.Payer, five, ten, fixedSchedule, rates, dc, floatingSchedule, index, 1.0, 0.0, null, null!)).ParamName);
        Assert.Equal("gearing", Assert.Throws<ArgumentNullException>(() => new NonstandardSwap(
            SwapType.Payer, five, ten, fixedSchedule, rates, dc, floatingSchedule, index, null!, ten, null, Context)).ParamName);
        Assert.Equal("spread", Assert.Throws<ArgumentNullException>(() => new NonstandardSwap(
            SwapType.Payer, five, ten, fixedSchedule, rates, dc, floatingSchedule, index, ten, null!, null, Context)).ParamName);
        Assert.Equal("fromVanilla", Assert.Throws<ArgumentNullException>(() => new NonstandardSwap(null!, Context)).ParamName);

        Assert.Equal("type", Assert.Throws<ArgumentOutOfRangeException>(() => Swap(type: (SwapType)7)).ParamName);
        Assert.Equal("paymentConvention", Assert.Throws<ArgumentOutOfRangeException>(() => Swap(convention: (BusinessDayConvention)99)).ParamName);
        Check(() => Swap(fixedRate: [0.03]), "fixedRate", "Fixed nominal size (5) does not match fixed rate size (1)");
        Check(() => Swap(fixedNominal: [1.0], fixedRate: [0.03]), "fixedNominal", "Fixed nominal size (1) does not match schedule size (6) - 1");
        Check(() => Swap(floatingNominal: [1.0]), "floatingNominal", "Floating nominal size (1) does not match schedule size (11) - 1");
        Check(() => Swap(spread: [0.0]), "spread", "Floating nominal size (10) does not match spread size (1)");
        Check(() => Swap(gearing: [1.0]), "gearing", "Floating nominal size (10) does not match gearing size (1)");

        NonstandardSwap vectors = Swap();
        Assert.Equal("spread is a vector, use spreads inspector instead", Assert.Throws<NanoQLException>(() => vectors.Spread()).Message);
        Assert.Equal("gearing is a vector, use gearings inspector instead", Assert.Throws<NanoQLException>(() => vectors.Gearing()).Message);

        // an amortizing overnight-indexed swap converts with its legs' nominals, QuantLib's master's (#2956,
        // Divergences/NonstandardSwapUpstreamFixesTests.cs), where 1.44 refuses it, "nominal is not constant"
        var sofr = new Sofr(Curve);
        var amortizing = new OvernightIndexedSwap(SwapType.Payer, [1e6, 9e5], Schedule(12, 2), 0.03, new Actual360(), sofr);
        var converted = new NonstandardSwap(amortizing, Context);
        Assert.Equal([1e6, 9e5], converted.FixedNominal);
        Assert.Equal([1e6, 9e5], converted.FloatingNominal);
    }

    [Fact]
    public void TheNonstandardSwaptionsRefusals()
    {
        NonstandardSwap swap = Swap(1.0, 1.0);
        var exercise = new BermudanExercise(Exercises);
        Assert.Equal("swap", Assert.Throws<ArgumentNullException>(() => new NonstandardSwaption(null!, exercise)).ParamName);
        Assert.Equal("exercise", Assert.Throws<ArgumentNullException>(() => new NonstandardSwaption(swap, null!)).ParamName);
        Assert.Equal("fromSwaption", Assert.Throws<ArgumentNullException>(() => new NonstandardSwaption(null!, Context)).ParamName);
        Assert.Equal("delivery", Assert.Throws<ArgumentOutOfRangeException>(() => new NonstandardSwaption(swap, exercise, (SettlementType)7)).ParamName);
        Assert.Equal(
            "settlementMethod",
            Assert.Throws<ArgumentOutOfRangeException>(() => new NonstandardSwaption(swap, exercise, SettlementType.Physical, (SettlementMethod)7)).ParamName);

        // the settlement's consistency, at pricing, as QuantLib's validate
        var inconsistent = new NonstandardSwaption(swap, exercise, SettlementType.Physical, SettlementMethod.CollateralizedCashPrice);
        Gsr model = Gaussian1dInputs.Constant(Curve, 0.01, 0.01);
        Check(() => Gaussian1dNonstandardSwaptionEngine.Calculate(inconsistent, Context, model), "settlementMethod", "invalid settlement method for physical settlement");

        var engine = new Gaussian1dNonstandardSwaptionEngine(model);
        var swaption = new NonstandardSwaption(swap, exercise);
        var swapIndex = new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), Curve, Curve);
        SwaptionVolatilityStructure vol = Volatility();
        Assert.Equal("engine", Assert.Throws<ArgumentNullException>(() => swaption.CalibrationBasket(null!, swapIndex, vol, Context)).ParamName);
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => swaption.CalibrationBasket(engine, swapIndex, vol, null!)).ParamName);
        Assert.Equal("standardSwapBase", Assert.Throws<ArgumentNullException>(() => swaption.CalibrationBasket(engine, null!, vol, Context)).ParamName);
        Assert.Equal("swaptionVolatility", Assert.Throws<ArgumentNullException>(() => swaption.CalibrationBasket(engine, swapIndex, null!, Context)).ParamName);
    }

    [Fact]
    public void TheNonstandardEnginesRefusals()
    {
        Assert.Equal("model", Assert.Throws<ArgumentNullException>(() => new Gaussian1dNonstandardSwaptionEngine(null!)).ParamName);
        Gsr model = Gaussian1dInputs.Constant(Curve, 0.01, 0.01);
        var engine = new Gaussian1dNonstandardSwaptionEngine(model);
        var exercise = new BermudanExercise(Exercises);
        var swaption = new NonstandardSwaption(Swap(1.0, 1.0), exercise);
        Assert.Equal("swaption", Assert.Throws<ArgumentNullException>(() => engine.Calculate(null!, Context)).ParamName);
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => engine.Calculate(swaption, null!)).ParamName);
        Assert.Equal(
            "cash settled (ParYieldCurve) swaptions not priced with Gaussian1dNonstandardSwaptionEngine",
            Assert.Throws<NanoQLException>(() => engine.Calculate(
                new NonstandardSwaption(Swap(1.0, 1.0), exercise, SettlementType.Cash, SettlementMethod.ParYieldCurve), Context)).Message);

        // the basket's
        SwaptionVolatilityStructure vol = Volatility();
        var swapIndex = new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), Curve, Curve);
        ArgumentOutOfRangeException type = Assert.Throws<ArgumentOutOfRangeException>(
            () => swaption.CalibrationBasket(engine, swapIndex, vol, Context, (BasketGeneratingEngineCalibrationBasketType)7));
        Assert.Equal("basketType", type.ParamName);
        Assert.StartsWith("Calibration basket type not known (7)", type.Message, StringComparison.Ordinal);
        Check(
            () => swaption.CalibrationBasket(engine, new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years)), vol, Context),
            "standardSwapBase",
            "standard swap base forwarding term structure must not be empty.");
        Check(
            () => swaption.CalibrationBasket(engine, new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), Curve, null), vol, Context),
            "standardSwapBase",
            "standard swap base discounting term structure must not be empty.");
        Assert.Equal(
            "(npv,delta,gamma) must have a positive norm",
            Assert.Throws<NanoQLException>(() => new NonstandardSwaption(Swap(0.0, 0.0), exercise).CalibrationBasket(engine, swapIndex, vol, Context)).Message);
        Assert.Equal(
            "sum of nominals on fixed leg must be positive (0)",
            Assert.Throws<NanoQLException>(() => new NonstandardSwaption(Swap(0.0, 1.0), exercise).CalibrationBasket(engine, swapIndex, vol, Context)).Message);
    }

    [Fact]
    public void TheCapFloorEnginesRefusals()
    {
        Gsr model = Gaussian1dInputs.Constant(Curve, 0.01, 0.01);
        var index = new Euribor6M(Curve);
        var cap = new Cap(new IborLeg(Schedule(6), index).WithNotionals(1e6).ToLeg(Context), [0.03]);
        Assert.Equal("capFloor", Assert.Throws<ArgumentNullException>(() => Gaussian1dCapFloorEngine.Calculate(null!, Context, model)).ParamName);
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => Gaussian1dCapFloorEngine.Calculate(cap, null!, model)).ParamName);
        Assert.Equal("model", Assert.Throws<ArgumentNullException>(() => Gaussian1dCapFloorEngine.Calculate(cap, Context, null!)).ParamName);

        var spread = new Cap(new IborLeg(Schedule(6), index).WithNotionals(1e6).WithSpreads(0.001).ToLeg(Context), [0.03]);
        Assert.Equal(
            "Non zero spreads (0.001) are not allowed.",
            Assert.Throws<NanoQLException>(() => Gaussian1dCapFloorEngine.Calculate(spread, Context, model)).Message);
    }

    private static Schedule Schedule(int months, int years = 5) => new(
        Start,
        Calendar.Advance(Start, years, TimeUnit.Years),
        new Period(months, TimeUnit.Months),
        Calendar,
        BusinessDayConvention.ModifiedFollowing,
        BusinessDayConvention.ModifiedFollowing,
        DateGenerationRule.Forward,
        false,
        Context);

    private static NonstandardSwap Swap(double fixedNominal, double floatingNominal) => new(
        SwapType.Payer,
        Enumerable.Repeat(fixedNominal, 5),
        Enumerable.Repeat(floatingNominal, 10),
        Schedule(12),
        Enumerable.Repeat(0.03, 5),
        new Thirty360(Thirty360Convention.BondBasis),
        Schedule(6),
        new Euribor6M(Curve),
        1.0,
        0.0,
        new Actual360(),
        Context,
        paymentConvention: BusinessDayConvention.ModifiedFollowing);

    private static SwaptionVolatilityStructure Volatility() =>
        ConstantSwaptionVolatility.Create(0, Calendar, BusinessDayConvention.ModifiedFollowing, 0.2, new Actual365Fixed(), Context);

    private static void Check(Func<object> call, string paramName, string message)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(call);
        Assert.Equal(typeof(ArgumentException), e.GetType());
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }

    private static void Check(Func<double> call, string paramName, string message) => Check(() => (object)call(), paramName, message);
}
