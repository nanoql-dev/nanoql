// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of the swaption's, its engines' and its builder's refusals, which the vectors record only as
/// throws (playbook §9.4): the undefined paths' refusals (SW1, SW2 and MS1, <see cref="ArgumentNullException"/>; EX2,
/// <see cref="NanoQLException"/> with QuantLib's <c>lastDate()</c> text); the enums stored or switched on, refused on
/// entry with <see cref="ArgumentOutOfRangeException"/> and QuantLib's texts; QuantLib's checks of the members' own
/// arguments (the engines' volatility type, <c>Settlement::checkTypeAndMethodConsistency</c>'s method, the implied
/// volatility's solver arguments, which name this member's parameters), <see cref="ArgumentException"/>; and its other
/// checks, <see cref="NanoQLException"/>, the option base's "no payoff given" among them.
/// </summary>
public class SwaptionErrorTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly FlatForward Curve = new(Today, 0.03, new Actual365Fixed());
    private static readonly Date ExerciseDate = new(15, Month.January, 2026);

    [Fact]
    public void TheSwaptionsRefusals()
    {
        InstrumentRefusals();
        EngineRefusals();
        ImpliedVolatilityRefusals();
        BuilderRefusals();
    }

    private static void InstrumentRefusals()
    {
        VanillaSwap swap = Swap();
        var exercise = new EuropeanExercise(ExerciseDate);

        // SW1, SW2: .NET's text for a null argument
        Assert.Equal("swap", Assert.Throws<ArgumentNullException>(() => new Swaption(null!, exercise)).ParamName);
        Assert.Equal("exercise", Assert.Throws<ArgumentNullException>(() => new Swaption(swap, null!)).ParamName);

        // the settlement enums, stored
        ArgumentOutOfRangeException r = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Swaption(swap, exercise, (SettlementType)5));
        Assert.Equal("delivery", r.ParamName);
        Assert.StartsWith("unknown Settlement::Type(5)", r.Message, StringComparison.Ordinal);
        r = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Swaption(swap, exercise, SettlementType.Cash, (SettlementMethod)9));
        Assert.Equal("settlementMethod", r.ParamName);
        Assert.StartsWith("unknown Settlement::Method(9)", r.Message, StringComparison.Ordinal);

        // the context and CashFlows.Bps' yield, null
        Assert.Equal(
            "context", Assert.Throws<ArgumentNullException>(() => new Swaption(swap, exercise).IsExpired(null!)).ParamName);
        Assert.Equal(
            "yield", Assert.Throws<ArgumentNullException>(() => CashFlows.Bps([], (InterestRate)null!, Context)).ParamName);

        // EX2: an exercise with no dates, refused where its date is read
        var undated = new Swaption(swap, new Exercise(ExerciseType.European));
        Assert.Equal("no exercise date given", Assert.Throws<NanoQLException>(() => undated.IsExpired(Context)).Message);

        // the context refused on entry, before the exercise's dates are read
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => undated.IsExpired(null!)).ParamName);

        // the settlement pair, checked when priced: the method, refused given the type
        foreach ((SettlementType type, SettlementMethod method, string text) in
            new[]
            {
                (SettlementType.Physical, SettlementMethod.ParYieldCurve, "invalid settlement method for physical settlement"),
                (SettlementType.Cash, SettlementMethod.PhysicalOTC, "invalid settlement method for cash settlement"),
            })
        {
            ArgumentException e = Assert.Throws<ArgumentException>(() => Settlement.CheckTypeAndMethodConsistency(type, method));
            Assert.Equal("settlementMethod", e.ParamName);
            Assert.StartsWith(text, e.Message, StringComparison.Ordinal);
            var inconsistent = new Swaption(swap, exercise, type, method);
            e = Assert.Throws<ArgumentException>(() => BlackSwaptionEngine.Calculate(inconsistent, Context, Curve, 0.2));
            Assert.Equal("settlementMethod", e.ParamName);
            Assert.StartsWith(text, e.Message, StringComparison.Ordinal);
        }

        // the option base's own check, for an option with no payoff that doesn't override it
        Assert.Equal(
            "no payoff given", Assert.Throws<NanoQLException>(() => new PayofflessOption(exercise).Validate()).Message);
        Assert.Equal("exercise", Assert.Throws<ArgumentNullException>(() => new PayofflessOption(null!)).ParamName);
    }

    private static void EngineRefusals()
    {
        var swaption = new Swaption(Swap(), new EuropeanExercise(ExerciseDate));
        var lognormal = new ConstantSwaptionVolatility(Today, new TARGET(), BusinessDayConvention.Following, 0.2, new Actual365Fixed());
        var normal = new ConstantSwaptionVolatility(
            Today, new TARGET(), BusinessDayConvention.Following, 0.008, new Actual365Fixed(), VolatilityType.Normal);

        ArgumentException e = Assert.Throws<ArgumentException>(
            () => BlackSwaptionEngine.Calculate(swaption, Context, Curve, normal));
        Assert.Equal("vol", e.ParamName);
        Assert.StartsWith("BlackSwaptionEngine requires (shifted) lognormal input volatility", e.Message, StringComparison.Ordinal);
        e = Assert.Throws<ArgumentException>(() => BachelierSwaptionEngine.Calculate(swaption, Context, Curve, lognormal));
        Assert.Equal("vol", e.ParamName);
        Assert.StartsWith("BachelierSwaptionEngine requires normal input volatility", e.Message, StringComparison.Ordinal);

        // .NET's text for a null argument
        Assert.Equal("swaption", Assert.Throws<ArgumentNullException>(() => BlackSwaptionEngine.Calculate(null!, Context, Curve, 0.2)).ParamName);
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => BlackSwaptionEngine.Calculate(swaption, null!, Curve, 0.2)).ParamName);
        Assert.Equal("discountCurve", Assert.Throws<ArgumentNullException>(() => BachelierSwaptionEngine.Calculate(swaption, Context, null!, 0.008)).ParamName);
        Assert.Equal("vol", Assert.Throws<ArgumentNullException>(() => BachelierSwaptionEngine.Calculate(swaption, Context, Curve, (SwaptionVolatilityStructure)null!)).ParamName);

        // the body's checks, of stored terms
        var bermudan = new Swaption(Swap(), new BermudanExercise([ExerciseDate, ExerciseDate + 180]));
        Assert.Equal(
            "not a European option",
            Assert.Throws<NanoQLException>(() => BlackSwaptionEngine.Calculate(bermudan, Context, Curve, 0.2)).Message);
        var late = new Swaption(Swap(), new EuropeanExercise(new Date(20, Month.January, 2026)));
        Assert.StartsWith(
            "swap start (",
            Assert.Throws<NanoQLException>(() => BachelierSwaptionEngine.Calculate(late, Context, Curve, 0.008)).Message,
            StringComparison.Ordinal);
    }

    private static void ImpliedVolatilityRefusals()
    {
        var swaption = new Swaption(Swap(), new EuropeanExercise(ExerciseDate));
        double price = BlackSwaptionEngine.Calculate(swaption, Context, Curve, 0.2).NPV!.Value;

        ArgumentOutOfRangeException r = Assert.Throws<ArgumentOutOfRangeException>(
            () => swaption.ImpliedVolatility(price, Curve, 0.1, Context, maxEvaluations: -1));
        Assert.Equal("maxEvaluations", r.ParamName);
        r = Assert.Throws<ArgumentOutOfRangeException>(
            () => swaption.ImpliedVolatility(price, Curve, 0.1, Context, type: (VolatilityType)7));
        Assert.Equal("type", r.ParamName);
        Assert.StartsWith("unknown VolatilityType (Unknown volatility type (7))", r.Message, StringComparison.Ordinal);

        // the solver's checks of this member's own arguments, under their names here
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => swaption.ImpliedVolatility(price, Curve, 0.1, Context, minVol: 0.5, maxVol: 0.5));
        Assert.Equal("minVol", e.ParamName);
        Assert.StartsWith("invalid range: xMin_ (0.5) >= xMax_ (0.5)", e.Message, StringComparison.Ordinal);
        Assert.Equal(
            "accuracy",
            Assert.Throws<ArgumentException>(() => swaption.ImpliedVolatility(price, Curve, 0.1, Context, accuracy: 0.0)).ParamName);
        Assert.Equal(
            "guess", Assert.Throws<ArgumentException>(() => swaption.ImpliedVolatility(price, Curve, 5.0, Context)).ParamName);
        Assert.StartsWith(
            "root not bracketed",
            Assert.Throws<NanoQLException>(() => swaption.ImpliedVolatility(1e9, Curve, 0.1, Context)).Message,
            StringComparison.Ordinal);

        // the swaption's own state
        var expired = new EvaluationContext { EvaluationDate = new Date(15, Month.February, 2026) };
        Assert.Equal(
            "instrument expired",
            Assert.Throws<NanoQLException>(() => swaption.ImpliedVolatility(price, Curve, 0.1, expired)).Message);
        var bermudan = new Swaption(Swap(), new BermudanExercise([ExerciseDate, ExerciseDate + 180]));
        Assert.Equal(
            "not a European option",
            Assert.Throws<NanoQLException>(() => bermudan.ImpliedVolatility(price, Curve, 0.1, Context)).Message);

        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => swaption.ImpliedVolatility(price, Curve, 0.1, null!)).ParamName);
        Assert.Equal("discountCurve", Assert.Throws<ArgumentNullException>(() => swaption.ImpliedVolatility(price, null!, 0.1, Context)).ParamName);
    }

    private static void BuilderRefusals()
    {
        var index = new EuriborSwapIsdaFixA(new Period(5, TimeUnit.Years), Curve);
        var builder = new MakeSwaption(index, new Period(1, TimeUnit.Years));

        // MS1: .NET's text for a null argument
        Assert.Equal(
            "swapIndex",
            Assert.Throws<ArgumentNullException>(() => new MakeSwaption(null!, new Period(1, TimeUnit.Years))).ParamName);

        // the setters' enums, stored
        foreach ((Func<object> f, string name) in new (Func<object>, string)[]
        {
            (() => builder.WithSettlementType((SettlementType)2), "delivery"),
            (() => builder.WithSettlementMethod((SettlementMethod)4), "settlementMethod"),
            (() => builder.WithOptionConvention((BusinessDayConvention)42), "bdc"),
            (() => builder.WithUnderlyingType((SwapType)0), "type"),
        })
        {
            Assert.Equal(name, Assert.Throws<ArgumentOutOfRangeException>(f).ParamName);
        }

        // default(Date), never a valid date
        Assert.Equal("fixingDate", Assert.Throws<ArgumentException>(() => new MakeSwaption(index, default(Date))).ParamName);
        Assert.Equal("date", Assert.Throws<ArgumentException>(() => builder.WithExerciseDate(default(Date))).ParamName);

        // the conversion's checks of stored settings
        Assert.StartsWith(
            "exercise date (February 15th, 2027) must be less than or equal to fixing date (",
            Assert.Throws<NanoQLException>(
                () => builder.WithExerciseDate(new Date(15, Month.February, 2027)).ToSwaption(Context)).Message,
            StringComparison.Ordinal);
        var bare = new EuriborSwapIsdaFixA(new Period(5, TimeUnit.Years));
        Assert.Equal(
            "null term structure set to this instance of EuriborSwapIsdaFixA5Y 30/360 (Bond Basis)",
            Assert.Throws<NanoQLException>(() => new MakeSwaption(bare, new Period(1, TimeUnit.Years)).ToSwaption(Context)).Message);
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => builder.ToSwaption(null!)).ParamName);
    }

    private static VanillaSwap Swap() =>
        new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(Curve))
            .WithForwardStart(new Period(1, TimeUnit.Years))
            .WithFixedRate(0.03)
            .ToVanillaSwap(Context);

    // An option that keeps the base's check of its terms.
    private sealed class PayofflessOption(Exercise exercise) : Option(null, exercise)
    {
        public override bool IsExpired(EvaluationContext context) => false;
    }
}
