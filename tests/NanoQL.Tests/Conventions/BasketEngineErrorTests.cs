// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Time;
using static NanoQL.Tests.Divergences.Fd2dMismatchedTimesTests;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception type, message and parameter name of the basket engines' refusals: Choi's, the N-dimensional
/// finite-difference (ED28's limit among them), the single-factor and the Monte Carlo European engine's, each QuantLib's
/// text (the S-exo plan's ED14). Four of QuantLib's checks can't fire, here as in QuantLib, and stand as the guards
/// before them: the Monte Carlo engine's step checks come before its time grid's "time steps not specified"; a basket
/// option's payoff is a <see cref="BasketPayoff"/> by its type, so the engine's "non-basket payoff given" has no input;
/// and a multi-path with no asset or no step is refused by <see cref="MultiPath"/> itself before the path pricer's
/// "there must be some paths" and "the path cannot be empty".
/// </summary>
public class BasketEngineErrorTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    private static readonly GeneralizedBlackScholesProcess P1 = Process(100.0, Flat(0.02), Flat(0.05), Vol(0.25));
    private static readonly GeneralizedBlackScholesProcess P2 = Process(95.0, Flat(0.03), Flat(0.05), Vol(0.3));

    [Fact]
    public void ChoiRefusals()
    {
        var option = new BasketOption(AveragePut(), European());
        Refused<ArgumentException>(
            () => ChoiBasketEngine.Calculate(option, Context, [], Rho(2)), "processes", "No Black-Scholes process is given.");
        Refused<ArgumentException>(
            () => ChoiBasketEngine.Calculate(option, Context, [P1, P2], Rho(3)),
            "rho",
            "process and correlation matrix must have the same size.");
        Refused<ArgumentException>(
            () => ChoiBasketEngine.Calculate(option, Context, [P1, P2], Rho(2), 0.0), "lambda", "lambda must be positive");
        Refused<NanoQLException>(
            () => ChoiBasketEngine.Calculate(new BasketOption(AveragePut(), American()), Context, [P1, P2], Rho(2)),
            null,
            "not an European exercise");
        Refused<NanoQLException>(
            () => ChoiBasketEngine.Calculate(new BasketOption(MinCall(), European()), Context, [P1, P2], Rho(2)),
            null,
            "average or spread basket payoff expected");
        Refused<NanoQLException>(
            () => ChoiBasketEngine.Calculate(new BasketOption(DigitalAverage(), European()), Context, [P1, P2], Rho(2)),
            null,
            "non-plain vanilla payoff given");

        // no node count fits: the scale shrinks until QuantLib's limit
        Refused<NanoQLException>(
            () => ChoiBasketEngine.Calculate(option, Context, [P1, P2], Rho(2), 10.0, 0),
            null,
            "can not rescale lambda to fit max integration order");
    }

    [Fact]
    public void NdimRefusals()
    {
        var option = new BasketOption(AveragePut(), European());
        Refused<ArgumentException>(
            () => FdndimBlackScholesVanillaEngine.Calculate(option, Context, [], Rho(2), 20),
            "processes",
            "no Black-Scholes process is given.");
        Refused<ArgumentException>(
            () => FdndimBlackScholesVanillaEngine.Calculate(option, Context, [P1, P2], Rho(3), 20),
            "rho",
            "correlation matrix has the wrong size.");
        Refused<ArgumentException>(
            () => FdndimBlackScholesVanillaEngine.Calculate(option, Context, [P1, P2], Rho(2), [20, 20, 20]),
            "xGrids",
            "wrong number of xGrids is given.");
        Refused<ArgumentException>(
            () => FdndimBlackScholesVanillaEngine.Calculate(option, Context, [P1, P2], Rho(2), [3, 20]),
            "xGrids",
            "minimum grid size is four");

        // ED28: QuantLib's text without its remark on recompiling
        ImmutableArray<GeneralizedBlackScholesProcess> five = [P1, P2, P1, P2, P1];
        var fiveAssets = new BasketOption(
            new AverageBasketPayoff(new PlainVanillaPayoff(OptionType.Put, 100.0), [0.2, 0.2, 0.2, 0.2, 0.2]), European());
        Refused<ArgumentException>(
            () => FdndimBlackScholesVanillaEngine.Calculate(fiveAssets, Context, five, Rho(5), 20),
            "processes",
            "This engine does not support 5 underlyings. Max number of underlyings is 4.");
    }

    [Fact]
    public void SingleFactorRefusals()
    {
        Refused<NanoQLException>(
            () => SingleFactorBsmBasketEngine.Calculate(new BasketOption(SpreadCall(), European()), Context, [P1, P2]),
            null,
            "average basket payoff expected");
        Refused<NanoQLException>(
            () => SingleFactorBsmBasketEngine.Calculate(new BasketOption(DigitalAverage(), European()), Context, [P1, P2]),
            null,
            "non-plain vanilla payoff given");
        Refused<NanoQLException>(
            () => SingleFactorBsmBasketEngine.Calculate(new BasketOption(AveragePut(), European()), Context, [P1]),
            null,
            "wrong number of weights arguments in payoff");
        Refused<NanoQLException>(
            () => SingleFactorBsmBasketEngine.Calculate(new BasketOption(AveragePut(), American()), Context, [P1, P2]),
            null,
            "not an European exercise");
    }

    [Fact]
    public void MonteCarloRefusals()
    {
        var option = new BasketOption(AveragePut(), European());
        var array = new StochasticProcessArray([P1, P2], Rho(2));
        Refused<ArgumentException>(() => Mc(option, array, null, null), "timeSteps", "no time steps provided");
        Refused<ArgumentException>(
            () => Mc(option, array, 1, 1), "timeStepsPerYear", "both time steps and time steps per year were provided");
        Refused<ArgumentException>(() => Mc(option, array, 0, null), "timeSteps", "timeSteps must be positive, 0 not allowed");
        Refused<ArgumentException>(
            () => Mc(option, array, null, 0), "timeStepsPerYear", "timeStepsPerYear must be positive, 0 not allowed");

        // the guards before the checks that can't fire
        Assert.All(
            typeof(BasketOption).GetConstructors(),
            c => Assert.Equal(typeof(BasketPayoff), c.GetParameters()[0].ParameterType));
        var pricer = new EuropeanMultiPathPricer(AveragePut(), 0.95);
        Refused<NanoQLException>(() => pricer.Evaluate(new MultiPath()), null, "no path size for empty multi-path");
        Refused<ArgumentOutOfRangeException>(
            () => new MultiPath(0, new TimeGrid(1.0, 1)), "nAsset", "number of asset must be positive");
    }

    private static MultiAssetOptionResults Mc(
        BasketOption option, StochasticProcessArray array, int? timeSteps, int? timeStepsPerYear) =>
        MCEuropeanBasketEngine.Calculate<PseudoRandom, IncrementalStatistics>(
            option, Context, array, timeSteps, timeStepsPerYear, false, false, 1024, null, null, 42);

    private static BasketPayoff MinCall() => new MinBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 100.0));

    private static BasketPayoff DigitalAverage() =>
        new AverageBasketPayoff(new CashOrNothingPayoff(OptionType.Call, 100.0, 1.0), [0.5, 0.5]);

    private static Matrix Rho(int n)
    {
        var rho = new Matrix(n, n, 0.5);
        for (int i = 0; i < n; ++i)
        {
            rho[i, i] = 1.0;
        }

        return rho;
    }

    private static void Refused<TException>(Action call, string? paramName, string message)
        where TException : Exception
    {
        TException e = Assert.Throws<TException>(call);
        if (e is ArgumentException a)
        {
            Assert.Equal(paramName, a.ParamName);
        }

        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }
}
