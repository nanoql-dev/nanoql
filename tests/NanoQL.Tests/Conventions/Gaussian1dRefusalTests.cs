// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The refusals of GSR, its process, the Gaussian1d model's members and the two Gaussian1d swaption engines, by type,
/// message and parameter (playbook §2): QuantLib's texts, a check of an argument an <see cref="ArgumentException"/>
/// naming it, a check of a value already stored a <see cref="NanoQLException"/>.
/// </summary>
public class Gaussian1dRefusalTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());
    private static readonly TARGET Calendar = new();

    [Fact]
    public void GsrsConstructorRefusals()
    {
        Assert.Equal("termStructure", Assert.Throws<ArgumentNullException>(() => new Gsr(null!, [], [0.01], 0.01)).ParamName);
        Date one = Calendar.Advance(Today, 1, TimeUnit.Years), two = Calendar.Advance(Today, 2, TimeUnit.Years);

        Check(() => new Gsr(Curve, [Today], [0.01, 0.01], 0.01), "volstepdates", "volsteptimes must be positive (0)");
        Check(() => new Gsr(Curve, [two, one], [0.01, 0.01, 0.01], 0.01), "volstepdates", "volsteptimes must be strictly increasing (2@0, 1@1)");
        Check(() => new Gsr(Curve, [one], [0.01], 0.01), "volatilities", "there must be n+1 volatilities (1) for n volatility step times (1)");
        Check(() => new Gsr(Curve, [one], [0.01, 0.01], [0.01, 0.02, 0.03]), "reversions", "there must be 1 or n+1 reversions (3) for n volatility step times (1)");

        var model = new Gsr(Curve, [one], [0.01, 0.01], 0.01);
        Check(() => model.MoveVolatility(2), "i", "volatility with index 2 does not exist (0...1)");
        Check(() => model.MoveReversion(1), "i", "reversion with index 1 does not exist (0...0)");
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => model.MoveVolatility(-1)).ParamName);
    }

    [Fact]
    public void TheProcesssRefusals()
    {
        var p = new GsrProcess([1.0, 2.0], [0.01, 0.012, 0.009], [0.03], 10.0);
        Check(() => p.Expectation(9.0, 0.0, 1.5), "dt", "t (10.5) must not be greater than forward measure time (10) and non-negative");
        Check(() => p.Variance(0.0, 0.0, -0.5), "dt", "t (-0.5) must not be greater than forward measure time (10) and non-negative");
        Check(() => p.Diffusion(10.5, 0.0), "t", "t (10.5) must not be greater than forward measure time (10) and non-negative");
        Check(() => p.Y(11.0), "t", "t (11) must not be greater than forward measure time (10) and non-negative");
        Check(() => p.G(3.0, 2.0, 0.0), "T", "G(t,w) should be called with w (2) not lesser than t (3)");
        Check(() => p.G(1.0, 12.0, 0.0), "T", "G(t,w) should be called with (t,w)=(1,12) in Range [0,10].");
        Check(() => new GsrProcess([1.0, 2.0], [0.01, 0.012], [0.03]), "vols", "number of volatilities (2) compared to number of times (2 must be bigger by one");
        Check(() => new GsrProcess([1.0, 2.0], [0.01, 0.012, 0.009], [0.03, 0.04]), "reversions", "number of reversions (3) compared to number of times (2 must be bigger by one, or exactly 1 reversion must be given");
        Check(() => new GsrProcess([2.0, 1.0], [0.01, 0.012, 0.009], [0.03]), "times", "times must be increasing (2@0 , 1@1)");
        NanoQLException time = Assert.Throws<NanoQLException>(() => p.Time(Today));
        Assert.Equal("time can not be computed without reference date and day counter", time.Message);
        Assert.Equal(1.0, new GsrProcess([1.0], [0.01, 0.01], [0.03], 10.0, Today, new Actual365Fixed()).Time(Today + 365));
    }

    [Fact]
    public void TheModelsMembersRefusals()
    {
        Gsr model = Gaussian1dInputs.Constant(Curve, 0.01, 0.01);
        Date fixing = Calendar.Advance(Today, 1, TimeUnit.Years);
        ArgumentNullException ibor = Assert.Throws<ArgumentNullException>(() => model.ForwardRate(fixing, Context));
        Assert.Equal("iborIdx", ibor.ParamName);
        Assert.StartsWith("no ibor index given", ibor.Message, StringComparison.Ordinal);
        Assert.Equal("iborIdx", Assert.Throws<ArgumentNullException>(() => model.ForwardRate(fixing, Context, null, 0.0, null)).ParamName);
        Assert.StartsWith("no swap index given", Assert.Throws<ArgumentNullException>(() => model.SwapRate(fixing, new Period(5, TimeUnit.Years), Context)).Message, StringComparison.Ordinal);
        Assert.Equal("swapIdx", Assert.Throws<ArgumentNullException>(() => model.SwapAnnuity(fixing, new Period(5, TimeUnit.Years), Context)).ParamName);
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => model.ForwardRate(fixing, null!, null, 0.0, new Euribor6M(Curve))).ParamName);
        Assert.Equal("gridPoints", Assert.Throws<ArgumentOutOfRangeException>(() => model.YGrid(7.0, -1)).ParamName);
        Assert.Equal("type", Assert.Throws<ArgumentOutOfRangeException>(() => model.ZerobondOption((OptionType)5, fixing, fixing, fixing + 365, 0.9)).ParamName);
        Assert.Equal("coupon", Assert.Throws<ArgumentNullException>(() => model.CompoundedRate(null!)).ParamName);
    }

    [Fact]
    public void TheEnginesRefusals()
    {
        Gsr model = Gaussian1dInputs.Constant(Curve, 0.01, 0.01);
        Date exercise = Calendar.Advance(Today, 1, TimeUnit.Years);
        VanillaSwap swap = Gaussian1dInputs.SwapOf(new Euribor6M(Curve), exercise, 5, 0.03, SwapType.Payer, Context);
        var parYield = new Swaption(swap, new EuropeanExercise(exercise), SettlementType.Cash, SettlementMethod.ParYieldCurve);
        Assert.Equal(
            "cash settled (ParYieldCurve) swaptions not priced with Gaussian1dSwaptionEngine",
            Assert.Throws<NanoQLException>(() => Gaussian1dSwaptionEngine.Calculate(parYield, Context, model)).Message);
        Assert.Equal(
            "cash settled (ParYieldCurve) swaptions not priced with Gaussian1dJamshidianSwaptionEngine",
            Assert.Throws<NanoQLException>(() => Gaussian1dJamshidianSwaptionEngine.Calculate(parYield, Context, model)).Message);

        var bermudan = new Swaption(swap, new BermudanExercise([exercise, Calendar.Advance(exercise, 1, TimeUnit.Years)]));
        Assert.Equal(
            "cannot use the Jamshidian decomposition on exotic swaptions",
            Assert.Throws<NanoQLException>(() => Gaussian1dJamshidianSwaptionEngine.Calculate(bermudan, Context, model)).Message);

        VanillaSwap spread = new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(Curve))
            .WithFixedRate(0.03).WithEffectiveDate(Calendar.Advance(exercise, 2, TimeUnit.Days)).WithFloatingLegSpread(0.001).ToVanillaSwap(Context);
        Assert.Equal(
            "non zero spread (0.001) not allowed",
            Assert.Throws<NanoQLException>(() => Gaussian1dJamshidianSwaptionEngine.Calculate(new Swaption(spread, new EuropeanExercise(exercise)), Context, model)).Message);

        Schedule fixedSchedule = new(Calendar.Advance(exercise, 2, TimeUnit.Days), Calendar.Advance(exercise, 2, TimeUnit.Days) + new Period(3, TimeUnit.Years), new Period(1, TimeUnit.Years), Calendar, BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Backward, false, Context);
        var amortizing = new OvernightIndexedSwap(SwapType.Payer, [1e6, 9e5, 8e5], fixedSchedule, 0.03, new Actual360(), new Estr(Curve));
        var nominals = new Swaption(amortizing, new EuropeanExercise(exercise));
        Assert.Equal("non-constant nominals are not supported yet", Assert.Throws<NanoQLException>(() => Gaussian1dSwaptionEngine.Calculate(nominals, Context, model)).Message);
        Assert.Equal("non-constant nominals are not supported yet", Assert.Throws<NanoQLException>(() => Gaussian1dJamshidianSwaptionEngine.Calculate(nominals, Context, model)).Message);

        Assert.Equal("model", Assert.Throws<ArgumentNullException>(() => Gaussian1dSwaptionEngine.Calculate(bermudan, Context, null!)).ParamName);
        Assert.Equal("swaption", Assert.Throws<ArgumentNullException>(() => Gaussian1dJamshidianSwaptionEngine.Calculate(null!, Context, model)).ParamName);
    }

    private static void Check(Func<object> call, string paramName, string message)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(call);
        Assert.Equal(typeof(ArgumentException), e.GetType());
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }

    private static void Check(Func<double> call, string paramName, string message) => Check(() => (object)call(), paramName, message);
}
