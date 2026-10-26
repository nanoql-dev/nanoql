// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using static NanoQL.Tests.Divergences.TwoAssetMarket;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception type, message and parameter name of the refusals of C2's closed-form and copula engines, its
/// instruments and the bivariate normals, each QuantLib's text (the S-exo plan's ED14): the spread engines' (a
/// non-European exercise, a payoff that isn't a spread, a base payoff that isn't plain), the Gaussian copula engine's
/// (its correlation's range, and the shared risk-free curve checked by reference, QuantLib's handle identity, so two
/// equal curves are refused), Stulz's, both Margrabe engines' and the option's, the two-asset correlation engine's, the
/// cash-dividend engine's, Deng, Li and Zhou's, and the bivariate normals'. QuantLib's checks that can't fire here
/// stand as the guards before them: an option type outside Call and Put is refused by the payoff, a Margrabe option's
/// payoff is a null payoff by its type, and Stulz's, Margrabe's and the American Margrabe engine's second exercise
/// checks follow the first's on the exercise's type.
/// </summary>
public class TwoAssetEngineErrorTests
{
    private static readonly GeneralizedBlackScholesProcess P1 = Process(110.0, 0.0, 0.05, 0.3);
    private static readonly GeneralizedBlackScholesProcess P2 = Process(100.0, 0.0, 0.05, 0.2);

    [Fact]
    public void TheSpreadEnginesRefusals()
    {
        Func<BasketOption, MultiAssetOptionResults>[] engines =
        [
            o => KirkEngine.Calculate(o, Context, P1, P2, 0.5),
            o => BjerksundStenslandSpreadEngine.Calculate(o, Context, P1, P2, 0.5),
            o => OperatorSplittingSpreadEngine.Calculate(o, Context, P1, P2, 0.5),
            o => PearsonSpreadEngine.Calculate(o, Context, P1, P2, 0.5),
        ];
        foreach (Func<BasketOption, MultiAssetOptionResults> engine in engines)
        {
            Refused<NanoQLException>(
                () => engine(new BasketOption(SpreadCall(), new AmericanExercise(Today, Today + 365))), null, "not an European exercise");
            Refused<NanoQLException>(
                () => engine(new BasketOption(new MinBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 5.0)), European())),
                null,
                " spread payoff expected");
            Refused<NanoQLException>(
                () => engine(new BasketOption(new SpreadBasketPayoff(new CashOrNothingPayoff(OptionType.Call, 5.0, 1.0)), European())),
                null,
                "non-plain payoff given");
        }

        Refused<ArgumentOutOfRangeException>(
            () => new OperatorSplittingSpreadEngine(P1, P2, 0.5, (OperatorSplittingSpreadEngineOrder)2), "order", "unknown approximation type");
    }

    [Fact]
    public void TheGaussianCopulaEnginesRefusals()
    {
        var shared = Flat(0.05);
        var p1 = Process(110.0, 0.0, shared, 0.3);
        var p2 = Process(100.0, 0.0, shared, 0.2);
        Refused<ArgumentException>(
            () => GaussianCopulaSpreadEngine.Calculate(Spread(OptionType.Call, 5.0), Context, p1, p2, 1.5),
            "correlation",
            "correlation must be in [-1, 1], got 1.5");

        // equal curves, two objects: QuantLib compares the handles' links
        Refused<ArgumentException>(
            () => GaussianCopulaSpreadEngine.Calculate(Spread(OptionType.Call, 5.0), Context, P1, P2, 0.5),
            "process2",
            "process1 and process2 must share the risk-free term structure (used for discounting the spread payoff)");
        Refused<NanoQLException>(
            () => GaussianCopulaSpreadEngine.Calculate(
                new BasketOption(SpreadCall(), new AmericanExercise(Today, Today + 365)), Context, p1, p2, 0.5),
            null,
            "not a European exercise");
        Refused<NanoQLException>(
            () => GaussianCopulaSpreadEngine.Calculate(
                new BasketOption(new MaxBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 5.0)), European()), Context, p1, p2, 0.5),
            null,
            "spread payoff expected");
        Refused<NanoQLException>(
            () => GaussianCopulaSpreadEngine.Calculate(
                new BasketOption(new SpreadBasketPayoff(new CashOrNothingPayoff(OptionType.Call, 5.0, 1.0)), European()), Context, p1, p2, 0.5),
            null,
            "non-plain payoff given");
    }

    [Fact]
    public void StulzAndTheMargrabeEnginesRefusals()
    {
        var american = new AmericanExercise(Today, Today + 365);
        Refused<NanoQLException>(
            () => StulzEngine.Calculate(
                new BasketOption(new MinBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 100.0)), american), Context, P1, P2, 0.5),
            null,
            "not an European Option");
        Refused<NanoQLException>(
            () => StulzEngine.Calculate(
                new BasketOption(new AverageBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 100.0), [0.5, 0.5]), European()),
                Context,
                P1,
                P2,
                0.5),
            null,
            "unknown basket type");
        Refused<NanoQLException>(
            () => StulzEngine.Calculate(
                new BasketOption(new MinBasketPayoff(new CashOrNothingPayoff(OptionType.Call, 100.0, 1.0)), European()), Context, P1, P2, 0.5),
            null,
            "non-plain payoff given");

        Refused<NanoQLException>(
            () => AnalyticEuropeanMargrabeEngine.Calculate(new MargrabeOption(1, 1, american), Context, P1, P2, 0.5),
            null,
            "not an European Option");
        Refused<NanoQLException>(
            () => AnalyticAmericanMargrabeEngine.Calculate(new MargrabeOption(1, 1, European()), Context, P1, P2, 0.5),
            null,
            "not an American option");
        Refused<NanoQLException>(
            () => AnalyticEuropeanMargrabeEngine.Calculate(new MargrabeOption(0, 1, European()), Context, P1, P2, 0.5),
            null,
            "quantity of asset 1 must be positive");
        Refused<NanoQLException>(
            () => AnalyticEuropeanMargrabeEngine.Calculate(new MargrabeOption(1, -1, European()), Context, P1, P2, 0.5),
            null,
            "quantity of asset 2 must be positive");
        Refused<ArgumentException>(
            () => new MargrabeOption(1, 1, European()).FetchResults(new MultiAssetOptionResults { NPV = 1.0 }),
            "results",
            "wrong result type");

        // both engines' payoff checks ("non a Null Payoff type", "not a null payoff") can't fire: the option's payoff is
        // a null payoff by its type
        Assert.IsType<NullPayoff>(new MargrabeOption(1, 1, European()).Payoff);
    }

    [Fact]
    public void TheTwoAssetCorrelationAndCashDividendEnginesRefusals()
    {
        Refused<NanoQLException>(
            () => AnalyticTwoAssetCorrelationEngine.Calculate(
                new TwoAssetCorrelationOption(OptionType.Call, 0.0, 70.0, European()), Context, P1, P2, 0.5),
            null,
            "strike must be positive");
        Refused<NanoQLException>(
            () => AnalyticTwoAssetCorrelationEngine.Calculate(
                new TwoAssetCorrelationOption(OptionType.Call, 50.0, 70.0, European()), Context, Process(0.0, 0.0, 0.05, 0.3), P2, 0.5),
            null,
            "negative or null underlying given");

        System.Collections.Immutable.ImmutableArray<Dividend> dividends = DividendFunctions.DividendVector([Today + 30], [1.0]);
        Refused<NanoQLException>(
            () => CashDividendEuropeanEngine.Calculate(
                new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new AmericanExercise(Today, Today + 365)),
                Context,
                P1,
                dividends),
            null,
            "not an European option");
        Refused<NanoQLException>(
            () => CashDividendEuropeanEngine.Calculate(
                new VanillaOption(new CashOrNothingPayoff(OptionType.Call, 100.0, 1.0), European()), Context, P1, dividends),
            null,
            "non-striked payoff given");
        Refused<ArgumentOutOfRangeException>(
            () => CashDividendEuropeanEngine.Calculate(
                new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), European()),
                Context,
                P1,
                dividends,
                (CashDividendEuropeanEngineCashDividendModel)2),
            "cashDividendModel",
            "unknown cash dividend model");
    }

    [Fact]
    public void DengLiZhousRefusals()
    {
        System.Collections.Immutable.ImmutableArray<GeneralizedBlackScholesProcess> p = Both(P1, P2);
        Refused<ArgumentException>(
            () => DengLiZhouBasketEngine.Calculate(Spread(OptionType.Call, 5.0), Context, [], Rho(0.5)),
            "processes",
            "No Black-Scholes process is given.");
        Refused<ArgumentException>(
            () => DengLiZhouBasketEngine.Calculate(Spread(OptionType.Call, 5.0), Context, p, Rho(0.5, 3)),
            "rho",
            "process and correlation matrix must have the same size.");
        Refused<NanoQLException>(
            () => DengLiZhouBasketEngine.Calculate(
                new BasketOption(SpreadCall(), new AmericanExercise(Today, Today + 365)), Context, p, Rho(0.5)),
            null,
            "not an European exercise");
        Refused<NanoQLException>(
            () => DengLiZhouBasketEngine.Calculate(
                new BasketOption(new MinBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 5.0)), European()), Context, p, Rho(0.5)),
            null,
            "average or spread basket payoff expected");
        Refused<NanoQLException>(
            () => DengLiZhouBasketEngine.Calculate(Average(OptionType.Call, 5.0, 1.0, -1.0, 1.0), Context, p, Rho(0.5)),
            null,
            "wrong number of weights arguments in payoff");
        Refused<NanoQLException>(
            () => DengLiZhouBasketEngine.Calculate(
                new BasketOption(new SpreadBasketPayoff(new CashOrNothingPayoff(OptionType.Call, 5.0, 1.0)), European()), Context, p, Rho(0.5)),
            null,
            "non-plain vanilla payoff given");
        Refused<NanoQLException>(
            () => DengLiZhouBasketEngine.Calculate(Average(OptionType.Call, 5.0, -1.0, -1.0), Context, p, Rho(0.5)),
            null,
            "at least one positive asset weight must be given");
        Refused<NanoQLException>(
            () => DengLiZhouBasketEngine.Calculate(Average(OptionType.Call, 5.0, 1.0, 1.0), Context, p, Rho(0.5)),
            null,
            "at least one negative asset weight must be given");
        Refused<NanoQLException>(
            () => DengLiZhouBasketEngine.Calculate(Spread(OptionType.Call, 5.0), Context, p, Rho(1.0)),
            null,
            "approximation loses validity");
    }

    [Fact]
    public void TheBivariateNormalsRefusals()
    {
        Refused<ArgumentException>(() => new BivariateCumulativeNormalDistributionDr78(-1.5), "rho", "rho must be >= -1.0 (-1.5 not allowed)");
        Refused<ArgumentException>(() => new BivariateCumulativeNormalDistributionDr78(1.5), "rho", "rho must be <= 1.0 (1.5 not allowed)");
        Refused<ArgumentException>(() => new BivariateCumulativeNormalDistributionWe04DP(-1.5), "rho", "rho must be >= -1.0 (-1.5 not allowed)");
        Refused<ArgumentException>(() => new BivariateCumulativeNormalDistributionWe04DP(1.5), "rho", "rho must be <= 1.0 (1.5 not allowed)");
        Refused<NanoQLException>(
            () => new BivariateCumulativeNormalDistributionDr78(0.5).Evaluate(double.NaN, 0.0), null, "case not handled");
    }

    private static BasketPayoff SpreadCall() => new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 5.0));

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
