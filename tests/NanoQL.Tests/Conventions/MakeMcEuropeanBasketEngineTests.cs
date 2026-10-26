// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The Monte Carlo European basket engine's builder and checks (S2R4; SEQR2): the seed is required and 0, QuantLib's
/// seed from the clock, refused, naming the parameter; QuantLib's checks and texts on the steps, samples and tolerance;
/// a builder kept and reused is never changed; and the engine refuses a first process that isn't a Black-Scholes one.
/// </summary>
public class MakeMcEuropeanBasketEngineTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void TheSeedIsRequiredAndZeroRefused()
    {
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
            () => new MakeMCEuropeanBasketEngine<PseudoRandom, GeneralStatistics>(Array(), 0));
        Assert.Equal("seed", e.ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MakeMCEuropeanBasketEngine<PseudoRandom, GeneralStatistics>(Array(), 42).WithSeed(0));
    }

    [Fact]
    public void QuantLibsChecksAndTexts()
    {
        var builder = new MakeMCEuropeanBasketEngine<PseudoRandom, GeneralStatistics>(Array(), 42);
        Assert.Equal("number of steps not given", Assert.Throws<NanoQLException>(() => builder.ToEngine()).Message);
        Assert.Equal(
            "number of steps overspecified",
            Assert.Throws<NanoQLException>(() => builder.WithSteps(2).WithStepsPerYear(4).ToEngine()).Message);
        Assert.Equal(
            "tolerance already set",
            Assert.Throws<NanoQLException>(() => builder.WithAbsoluteTolerance(0.1).WithSamples(10)).Message);
        Assert.Equal(
            "number of samples already set",
            Assert.Throws<NanoQLException>(() => builder.WithSamples(10).WithAbsoluteTolerance(0.1)).Message);
        Assert.Equal(
            "chosen random generator policy does not allow an error estimate",
            Assert.Throws<NanoQLException>(
                () => new MakeMCEuropeanBasketEngine<LowDiscrepancy, GeneralStatistics>(Array(), 42).WithAbsoluteTolerance(0.1)).Message);

        var option = new BasketOption(new MaxBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 100.0)), new EuropeanExercise(Today + 365));
        Func<BasketOption, EvaluationContext, MultiAssetOptionResults> engine = builder.WithSteps(2).WithSamples(255).ToEngine();
        Assert.Equal(engine(option, Context).NPV, builder.WithSteps(2).WithSamples(255).ToEngine()(option, Context).NPV);
        Assert.Equal(
            "timeSteps must be positive, 0 not allowed (Parameter 'timeSteps')",
            Assert.Throws<ArgumentException>(() => builder.WithSteps(0).WithSamples(10).ToEngine()(option, Context)).Message);
        Assert.Equal(
            "neither tolerance nor number of samples set",
            Assert.Throws<NanoQLException>(() => builder.WithSteps(2).ToEngine()(option, Context)).Message);
    }

    [Fact]
    public void AFirstProcessThatIsntBlackScholesIsRefused()
    {
        var rho = new Matrix(2, 2, 0.0);
        rho[0, 0] = rho[1, 1] = 1.0;
        var array = new StochasticProcessArray(
            [new OrnsteinUhlenbeckProcess(0.1, 0.2, 100.0), Process()], rho);
        var option = new BasketOption(new MaxBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 100.0)), new EuropeanExercise(Today + 365));
        Assert.Equal(
            "Black-Scholes process required",
            Assert.Throws<NanoQLException>(() => MCEuropeanBasketEngine.Calculate<PseudoRandom, GeneralStatistics>(
                option, Context, array, 1, null, false, false, 10, null, null, 42)).Message);
    }

    private static GeneralizedBlackScholesProcess Process() =>
        new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.04, A365),
            new BlackConstantVol(Today, new NullCalendar(), 0.2, A365));

    private static StochasticProcessArray Array()
    {
        var rho = new Matrix(2, 2, 0.5);
        rho[0, 0] = rho[1, 1] = 1.0;
        return new StochasticProcessArray([Process(), Process()], rho);
    }
}
