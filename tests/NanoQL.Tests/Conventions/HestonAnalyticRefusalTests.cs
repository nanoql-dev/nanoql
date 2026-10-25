// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The refusals of the Heston extensions' analytic engines, models, density calculators and fractional numerics (the
/// playbook's refusal rows): an argument a member checks on entry throws <see cref="ArgumentException"/> (or its
/// null and range kinds) naming the parameter, with QuantLib's message where QuantLib checks it; an enum value outside
/// its members throws <see cref="ArgumentOutOfRangeException"/> with QuantLib's message, on entry; a check on the
/// option, the market or a stored value throws <see cref="NanoQLException"/> with QuantLib's message.
/// </summary>
public class HestonAnalyticRefusalTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly YieldTermStructure RTS = new FlatForward(Today, 0.04, A365);
    private static readonly YieldTermStructure QTS = new FlatForward(Today, 0.02, A365);
    private static readonly HestonProcess Process = new(RTS, QTS, 100.0, 0.04, 1.5, 0.04, 0.4, -0.6);
    private static readonly HestonModel Model = new(Process);

    private static readonly VanillaOption Call =
        new(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));

    private static readonly VanillaOption American =
        new(new PlainVanillaPayoff(OptionType.Call, 100.0), new AmericanExercise(Today, Today + 365));

    private static readonly VanillaOption Digital =
        new(new CashOrNothingPayoff(OptionType.Call, 100.0, 1.0), new EuropeanExercise(Today + 365));

    [Fact]
    public void TheEnumArgumentsAreCheckedOnEntry()
    {
        AssertRefused<ArgumentOutOfRangeException>(
            "unknown complex log formula", "cpxLog",
            () => new AnalyticPTDHestonEngine(PtdModel(), (AnalyticPTDHestonEngineComplexLogFormula)2, FourierIntegration.GaussLaguerre(64)));
        AssertRefused<ArgumentOutOfRangeException>(
            "unknown approximation", "approximation",
            () => new AnalyticRoughHestonEngine(new RoughHestonModel(Process, 0.1), 128, 256, (AnalyticRoughHestonEngineApproximation)3));
        AssertRefused<ArgumentOutOfRangeException>(
            "unknown expansion formula", "formula",
            () => HestonExpansionEngine.Calculate(Call, Context, Model, (HestonExpansionEngineHestonExpansionFormula)3));
        AssertRefused<ArgumentOutOfRangeException>(
            "unknown complex log formula", "cv",
            () => new ExponentialFittingHestonEngine(Model, (AnalyticHestonEngineComplexLogFormula)99));
        AssertRefused<ArgumentOutOfRangeException>(
            "unknown complex log formula", "cpxLogFormula",
            () => new HestonBlackVolSurface(Model, (AnalyticHestonEngineComplexLogFormula)99));
    }

    [Fact]
    public void TheEnginesRefuseQuantLibsCases()
    {
        AssertRefused<NanoQLException>("not an European option", null, () => COSHestonEngine.Calculate(American, Context, Model));
        AssertRefused<NanoQLException>("not an European option", null, () => ExponentialFittingHestonEngine.Calculate(American, Context, Model));
        AssertRefused<NanoQLException>("not an European option", null, () => AnalyticPDFHestonEngine.Calculate(American, Context, Model));
        AssertRefused<NanoQLException>(
            "not an European option", null, () => HestonExpansionEngine.Calculate(American, Context, Model, HestonExpansionEngineHestonExpansionFormula.LPP2));
        AssertRefused<NanoQLException>("not an European option", null, () => AnalyticPTDHestonEngine.Calculate(American, Context, PtdModel()));
        AssertRefused<NanoQLException>("non plain vanilla payoff given", null, () => ExponentialFittingHestonEngine.Calculate(Digital, Context, Model));
        AssertRefused<NanoQLException>("non-striked payoff given", null, () => AnalyticPTDHestonEngine.Calculate(Digital, Context, PtdModel()));
        AssertRefused<NanoQLException>(
            "Gatheral and Branch-Correction are not supported as control-variate", null,
            () => ExponentialFittingHestonEngine.Calculate(Call, Context, Model, AnalyticHestonEngineComplexLogFormula.Gatheral));

        // the time-dependent engine: a maturity past the grid, and a grid of one point
        var past = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + (365 * 20)));
        AssertRefused<NanoQLException>("is too large, time grid is bounded by", null, () => AnalyticPTDHestonEngine.Calculate(past, Context, PtdModel()));
        AssertRefused<NanoQLException>("is too large, time grid is bounded by", null, () => new AnalyticPTDHestonEngine(PtdModel()).LnChF(Complex.One, 20.0));
        var onePoint = new PiecewiseTimeDependentHestonModel(
            RTS, QTS, 100.0, 0.04, Constant(0.04), Constant(1.5), Constant(0.4), Constant(-0.6), new TimeGrid([0.0]));
        var today = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today));
        AssertRefused<NanoQLException>("at least two model points needed", null, () => AnalyticPTDHestonEngine.Calculate(
            today, new EvaluationContext { EvaluationDate = Today, IncludeReferenceDateEvents = true }, onePoint));

        // the rough engine at the model's time 0, and its members past their domain
        var engine = new AnalyticRoughHestonEngine(new RoughHestonModel(Process, 0.1), 64, 64);
        AssertRefused<NanoQLException>(
            "maturity must be positive", null,
            () => engine.Calculate(today, new EvaluationContext { EvaluationDate = Today, IncludeReferenceDateEvents = true }));
        AssertRefused<ArgumentException>("maturity must be positive", "t", () => engine.RiccatiSolution(Complex.One, 0.0));
        AssertRefused<ArgumentException>("maturity must be positive", "maturity", () => engine.PriceVanillaPayoff(new PlainVanillaPayoff(OptionType.Call, 100.0), Today));
        AssertRefused<ArgumentException>("at least one lifted factor required", "nFactors", () => new AnalyticRoughHestonEngine(new RoughHestonModel(Process, 0.1), 64, 64, AnalyticRoughHestonEngineApproximation.Lifted, 0));
    }

    [Fact]
    public void TheModelsRefuseTheirArguments()
    {
        AssertRefused<ArgumentException>("invalid value", "v0", () => new PiecewiseTimeDependentHestonModel(
            RTS, QTS, 100.0, -0.04, Constant(0.04), Constant(1.5), Constant(0.4), Constant(-0.6), new TimeGrid(1.0, 2)));
        AssertRefused<ArgumentNullException>(null, "timeGrid", () => new PiecewiseTimeDependentHestonModel(
            RTS, QTS, 100.0, 0.04, Constant(0.04), Constant(1.5), Constant(0.4), Constant(-0.6), null!));
        AssertRefused<ArgumentException>("invalid value", "hurst", () => new RoughHestonModel(Process, 0.6));
        AssertRefused<ArgumentNullException>(null, "process", () => new RoughHestonModel(null!, 0.1));
        AssertRefused<ArgumentNullException>(null, "model", () => new COSHestonEngine(null!));
        AssertRefused<ArgumentNullException>(null, "model", () => new AnalyticPDFHestonEngine(null!));
        AssertRefused<ArgumentNullException>(null, "hestonModel", () => new HestonBlackVolSurface(null!));
        AssertRefused<ArgumentNullException>(null, "hestonProcess", () => new HestonRNDCalculator(null!));
    }

    [Fact]
    public void TheFractionalNumericsRefuseTheirArguments()
    {
        AssertRefused<ArgumentException>("fractional order alpha (0) must be in (0, 1]", "alpha", () => new FractionalAdams<double>(0.0));
        AssertRefused<ArgumentException>("fractional order alpha (1.5) must be in (0, 1]", "alpha", () => new FractionalAdams<double>(1.5));
        AssertRefused<ArgumentException>("tMax (0) must be positive", "tMax", () => new FractionalAdams<double>(0.6).Solve((_, y) => y, 1.0, 0.0, 10));
        AssertRefused<ArgumentException>("at least one step required", "steps", () => new FractionalAdams<double>(0.6).Solve((_, y) => y, 1.0, 1.0, 0));
        AssertRefused<ArgumentNullException>(null, "ode", () => new FractionalAdams<double>(0.6).Solve(null!, 1.0, 1.0, 10));

        AssertRefused<ArgumentException>("integration order alpha (-1E-08) must be non-negative", "alpha", () => new RiemannLiouvilleWeights(-1e-8, 4));
        AssertRefused<ArgumentException>("at least one grid step required", "steps", () => new RiemannLiouvilleWeights(0.6, 0));
        AssertRefused<ArgumentException>(
            "grid size (3) does not match the weights (5 values expected)", "y", () => new RiemannLiouvilleWeights(0.6, 4).Integrate<double>([1.0, 2.0, 3.0], 0.1));
        AssertRefused<ArgumentException>("grid spacing dt (0) must be positive", "dt", () => new RiemannLiouvilleWeights(0.6, 2).Integrate<double>([1.0, 2.0, 3.0], 0.0));

        AssertRefused<ArgumentException>(
            "integration order alpha (-1) must be non-negative", "alpha", () => FractionalAdamsFunctions.RiemannLiouvilleIntegral<double>([1.0, 2.0], -1.0, 0.1));
        AssertRefused<ArgumentException>("at least two grid values required", "y", () => FractionalAdamsFunctions.RiemannLiouvilleIntegral<double>([1.0], 0.5, 0.1));
        AssertRefused<ArgumentException>(
            "grid spacing dt (-0.1) must be positive", "dt", () => FractionalAdamsFunctions.RiemannLiouvilleIntegral<double>([1.0, 2.0], 0.5, -0.1));

        AssertRefused<ArgumentException>("tMax (0) must be positive", "tMax", () => new FractionalKernelApproximation(0.6, 10, 0.0));
        AssertRefused<ArgumentException>("at least one factor required", "n", () => new FractionalKernelApproximation(0.6, 0, 1.0));
        AssertRefused<ArgumentException>("fractional order alpha (1.2) must be in (0, 1]", "alpha", () => new FractionalKernelApproximation(1.2, 10, 1.0));
        AssertRefused<ArgumentException>("must satisfy 0 < etaLo <= etaHi", "tMax", () => new FractionalKernelApproximation(0.6, 10, double.PositiveInfinity));
    }

    private static ConstantParameter Constant(double value) => new(value, new NoConstraint());

    private static PiecewiseTimeDependentHestonModel PtdModel() =>
        new(RTS, QTS, 100.0, 0.04, Constant(0.04), Constant(1.5), Constant(0.4), Constant(-0.6), new TimeGrid(10.0, 4));

    // the exception's type, its message starting with QuantLib's text where given, and its parameter's name
    private static void AssertRefused<TException>(string? message, string? paramName, Action action)
        where TException : Exception
    {
        TException e = Assert.Throws<TException>(action);
        if (message is not null)
        {
            Assert.Contains(message, e.Message, StringComparison.Ordinal);
        }

        if (e is ArgumentException argument)
        {
            Assert.Equal(paramName, argument.ParamName);
        }
    }
}
