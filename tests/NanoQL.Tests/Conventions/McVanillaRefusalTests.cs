// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The refusals of the Monte Carlo vanilla engines' components and the American payoffs, with QuantLib's texts: each
/// refusal's type, message and parameter asserted. Where QuantLib's behaviour is undefined, the refusal is nanoql's:
/// the least squares' arguments and values of different sizes, which QuantLib writes past the matrix's column; a
/// Longstaff-Schwartz grid of fewer than two times, whose coefficient count wraps; a calibration path of a type nanoql
/// can't copy; and an American payoff neither cash-or-nothing nor asset-or-nothing, whose paid amount QuantLib leaves
/// unset.
/// </summary>
public class McVanillaRefusalTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void TheLeastSquaresAndBasesRefuseAsQuantLibs()
    {
        Func<double, double>[] line = [_ => 1.0, x => x];
        AssertArgument("sample set need to be of the same size", "x", () => GeneralLinearLeastSquares.Create([1.0, 2.0], [1.0, 2.0, 3.0], line));
        AssertArgument("sample set is too small", "y", () => GeneralLinearLeastSquares.Create([1.0], [1.0], line));
        Assert.Equal("zero dimension", Assert.Throws<NanoQLException>(
            () => LsmBasisSystem.MultiPathBasisSystem(0, 2, LsmBasisSystemPolynomialType.Monomial)).Message);
        Assert.Equal("unknown regression type", Assert.Throws<NanoQLException>(
            () => LsmBasisSystem.PathBasisSystem(2, (LsmBasisSystemPolynomialType)7)).Message);
        Assert.Equal("order", Assert.Throws<ArgumentOutOfRangeException>(
            () => LsmBasisSystem.PathBasisSystem(-1, LsmBasisSystemPolynomialType.Monomial)).ParamName);
    }

    [Fact]
    public void ThePathPricersRefuseAsQuantLibs()
    {
        AssertArgument("strike less than zero not allowed", "strike", () => new EuropeanPathPricer(OptionType.Call, -1.0, 1.0));
        Assert.Equal("the path cannot be empty", Assert.Throws<NanoQLException>(
            () => new EuropeanPathPricer(OptionType.Call, 1.0, 1.0).Evaluate(new MonteCarloPath(new NanoQL.TimeGrid()))).Message);
        AssertArgument(
            "insufficient polynomial type",
            "polynomialType",
            () => new AmericanPathPricer(new PlainVanillaPayoff(OptionType.Put, 1.0), 2, LsmBasisSystemPolynomialType.Chebyshev));

        var pricer = new AmericanPathPricer(new PlainVanillaPayoff(OptionType.Put, 1.0), 2, LsmBasisSystemPolynomialType.Monomial);
        YieldTermStructure curve = new FlatForward(Today, 0.05, A365);
        AssertArgument(
            "at least two times required, 1 given",
            "times",
            () => new LongstaffSchwartzPathPricer<MonteCarloPath, double>(new NanoQL.TimeGrid([0.0]), pricer, curve, []));
        Assert.StartsWith(
            "using inadequate time grid: the nodes closest to the required time t = 0.3 are",
            Assert.Throws<NanoQLException>(
                () => new LongstaffSchwartzPathPricer<MonteCarloPath, double>(new NanoQL.TimeGrid(1.0, 4), pricer, curve, [0.3])).Message);

        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.0, A365), curve, new BlackConstantVol(Today, new NullCalendar(), 0.2, A365));
        var digital = new DigitalPathPricer(
            new CashOrNothingPayoff(OptionType.Call, 110.0, 10.0), new AmericanExercise(Today, Today + 365), curve, process,
            new MersenneTwisterUniformRsg(1, 76));
        Assert.Equal("the path cannot be empty", Assert.Throws<NanoQLException>(
            () => digital.Evaluate(new MonteCarloPath(new NanoQL.TimeGrid()))).Message);
    }

    [Fact]
    public void TheAmericanPayoffsRefuseAsQuantLibs()
    {
        var cash = new CashOrNothingPayoff(OptionType.Call, 110.0, 10.0);
        AssertArgument("positive spot value required", "spot", () => new AmericanPayoffAtHit(0.0, 0.9, 1.0, 0.04, cash));
        AssertArgument("positive discount required", "discount", () => new AmericanPayoffAtHit(100.0, 0.0, 1.0, 0.04, cash));
        AssertArgument("positive dividend discount required", "dividendDiscount", () => new AmericanPayoffAtHit(100.0, 0.9, 0.0, 0.04, cash));
        AssertArgument("negative variance not allowed", "variance", () => new AmericanPayoffAtHit(100.0, 0.9, 1.0, -0.04, cash));
        AssertArgument("positive spot value required", "spot", () => new AmericanPayoffAtExpiry(0.0, 0.9, 1.0, 0.04, cash));
        AssertArgument("positive discount required", "discount", () => new AmericanPayoffAtExpiry(100.0, 0.0, 1.0, 0.04, cash));
        AssertArgument(
            "positive dividend discount required", "dividendDiscount", () => new AmericanPayoffAtExpiry(100.0, 0.9, 0.0, 0.04, cash));
        AssertArgument("negative variance not allowed", "variance", () => new AmericanPayoffAtExpiry(100.0, 0.9, 1.0, -0.04, cash));
        AssertArgument("negative maturity not allowed", "maturity", () => new AmericanPayoffAtHit(100.0, 0.9, 1.0, 0.04, cash).Rho(-1.0));

        // QuantLib leaves the paid amount unset
        var vanilla = new PlainVanillaPayoff(OptionType.Call, 110.0);
        AssertArgument(
            "Vanilla payoff given: a cash-or-nothing or asset-or-nothing payoff required",
            "payoff",
            () => new AmericanPayoffAtHit(100.0, 0.9, 1.0, 0.04, vanilla));
        AssertArgument(
            "Vanilla payoff given: a cash-or-nothing or asset-or-nothing payoff required",
            "payoff",
            () => new AmericanPayoffAtExpiry(100.0, 0.9, 1.0, 0.04, vanilla));
    }

    [Fact]
    public void TheAnalyticDigitalAmericanEngineRefusesAsQuantLibs()
    {
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.0, A365), new FlatForward(Today, 0.05, A365),
            new BlackConstantVol(Today, new NullCalendar(), 0.2, A365));
        var cash = new CashOrNothingPayoff(OptionType.Call, 110.0, 10.0);
        AssertRefused(
            "non-American exercise given",
            () => AnalyticDigitalAmericanEngine.Calculate(new VanillaOption(cash, new EuropeanExercise(Today + 365)), Context, process));
        AssertRefused(
            "American option with window exercise not handled yet",
            () => AnalyticDigitalAmericanEngine.Calculate(
                new VanillaOption(cash, new AmericanExercise(Today + 10, Today + 365)), Context, process));
        var zeroSpot = new BlackScholesMertonProcess(
            0.0, new FlatForward(Today, 0.0, A365), new FlatForward(Today, 0.05, A365),
            new BlackConstantVol(Today, new NullCalendar(), 0.2, A365));
        AssertRefused(
            "negative or null underlying given",
            () => AnalyticDigitalAmericanEngine.Calculate(new VanillaOption(cash, new AmericanExercise(Today, Today + 365)), Context, zeroSpot));
    }

    private static void AssertRefused(string message, Action act) =>
        Assert.Equal(message, Assert.Throws<NanoQLException>(act).Message);

    private static void AssertArgument(string message, string paramName, Action act)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(act);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message);
    }
}
