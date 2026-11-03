// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using static NanoQL.Tests.Divergences.TwoAssetMarket;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The Longstaff-Schwartz basket engine's builder, engine and path pricer checks: the seed is required and 0,
/// QuantLib's seed from the clock, refused, naming the parameter; QuantLib's checks and texts on the steps, samples and
/// tolerance; the engine's on its first process and its exercise; and the pricer's on its basis family, its payoff and
/// the paths' number. QuantLib's "Stochastic process array required" can't fire, an array of no process being refused
/// at its construction ("no processes given").
/// </summary>
public class MakeMcAmericanBasketEngineTests
{
    [Fact]
    public void TheSeedIsRequiredAndZeroRefused()
    {
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
            () => new MakeMCAmericanBasketEngine<PseudoRandom, GeneralStatistics>(Array(), 0));
        Assert.Equal("seed", e.ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MakeMCAmericanBasketEngine<PseudoRandom, GeneralStatistics>(Array(), 42).WithSeed(0));
    }

    [Fact]
    public void QuantLibsBuilderChecksAndTexts()
    {
        var builder = new MakeMCAmericanBasketEngine<PseudoRandom, GeneralStatistics>(Array(), 42);
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
                () => new MakeMCAmericanBasketEngine<LowDiscrepancy, GeneralStatistics>(Array(), 42).WithAbsoluteTolerance(0.1)).Message);

        // a builder kept and reused is never changed
        Func<BasketOption, EvaluationContext, MultiAssetOptionResults> engine =
            builder.WithSteps(4).WithSamples(255).WithCalibrationSamples(64).ToEngine();
        Assert.Equal(
            engine(American(), Context).NPV,
            builder.WithSteps(4).WithSamples(255).WithCalibrationSamples(64).ToEngine()(American(), Context).NPV);
    }

    [Fact]
    public void TheEnginesChecks()
    {
        Func<BasketOption, EvaluationContext, MultiAssetOptionResults> engine =
            new MakeMCAmericanBasketEngine<PseudoRandom, GeneralStatistics>(Array(), 42)
                .WithSteps(4).WithSamples(64).WithCalibrationSamples(32).ToEngine();
        Assert.Equal(
            "wrong exercise given",
            Assert.Throws<NanoQLException>(() => engine(Option(new EuropeanExercise(Today + 365)), Context)).Message);
        Assert.Equal(
            "payoff at expiry not handled",
            Assert.Throws<NanoQLException>(() => engine(Option(new AmericanExercise(Today, Today + 365, true)), Context)).Message);

        var ou = new StochasticProcessArray(
            [new OrnsteinUhlenbeckProcess(0.1, 0.2, 100.0), Process(95.0, 0.03, 0.05, 0.3)], Rho(0.0));
        Assert.Equal(
            "generalized Black-Scholes process required",
            Assert.Throws<NanoQLException>(
                () => new MakeMCAmericanBasketEngine<PseudoRandom, GeneralStatistics>(ou, 42)
                    .WithSteps(4).WithSamples(64).ToEngine()(American(), Context)).Message);
        ArgumentException none = Assert.Throws<ArgumentException>(() => new StochasticProcessArray([], new Matrix(1, 1, 1.0)));
        Assert.Equal("processes", none.ParamName);
    }

    [Fact]
    public void ThePathPricersChecks()
    {
        Payoff call = new PlainVanillaPayoff(OptionType.Call, 100.0);
        ArgumentException family = Assert.Throws<ArgumentException>(
            () => new AmericanBasketPathPricer(2, new MaxBasketPayoff(call), 2, LsmBasisSystemPolynomialType.Legendre));
        Assert.Equal("polynomialType", family.ParamName);
        Assert.StartsWith("insufficient polynomial type", family.Message, StringComparison.Ordinal);

        ArgumentException payoff = Assert.Throws<ArgumentException>(() => new AmericanBasketPathPricer(2, call));
        Assert.Equal("payoff", payoff.ParamName);
        Assert.StartsWith("payoff not a basket payoff", payoff.Message, StringComparison.Ordinal);

        var pricer = new AmericanBasketPathPricer(2, new MaxBasketPayoff(call));
        Assert.Equal(
            "invalid multipath",
            Assert.Throws<NanoQLException>(() => pricer.State(new MultiPath(3, new TimeGrid(1.0, 1)), 1)).Message);
    }

    private static StochasticProcessArray Array() =>
        new([Process(100.0, 0.02, 0.05, 0.25), Process(95.0, 0.03, 0.05, 0.3)], Rho(0.5));

    private static BasketOption Option(Exercise exercise) =>
        new(new MaxBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 100.0)), exercise);

    private static BasketOption American() => Option(new AmericanExercise(Today, Today + 365));
}
