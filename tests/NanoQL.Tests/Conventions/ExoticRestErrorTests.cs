// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// C4's refusals (the S-exo plan's ED14): each check's exception type, QuantLib's text and the parameter it names.
/// Where a check can't fire, the guard before it is asserted: the perpetual futures engine's unknown funding unit behind
/// the period's own unit check, the Monte Carlo variance-swap engine's unknown position behind the swap's, and the
/// variance pricer's interval count behind its empty-path check (a uniform grid's first step never exceeds its span).
/// </summary>
public class ExoticRestErrorTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void TheBachelierCalculatorsChecks()
    {
        ArgumentException stdDev = Assert.Throws<ArgumentException>(
            () => new BachelierCalculator(OptionType.Call, 100.0, 100.0, -1.0, 1.0));
        Assert.Equal("stdDev", stdDev.ParamName);
        Assert.StartsWith("stdDev (-1) must be non-negative", stdDev.Message, StringComparison.Ordinal);
        ArgumentException discount = Assert.Throws<ArgumentException>(
            () => new BachelierCalculator(OptionType.Call, 100.0, 100.0, 1.0, 0.0));
        Assert.Equal("discount", discount.ParamName);
        Assert.StartsWith("discount (0) must be positive", discount.Message, StringComparison.Ordinal);
        ArgumentException payoff = Assert.Throws<ArgumentException>(
            () => new BachelierCalculator(new PercentageStrikePayoff(OptionType.Call, 1.0), 100.0, 1.0, 1.0));
        Assert.Equal("payoff", payoff.ParamName);
        Assert.StartsWith("unsupported payoff type: PercentageStrike", payoff.Message, StringComparison.Ordinal);

        var calc = new BachelierCalculator(OptionType.Call, 100.0, 100.0, 10.0, 0.95);
        ArgumentException theta = Assert.Throws<ArgumentException>(() => calc.Theta(100.0, -1.0));
        Assert.Equal("maturity", theta.ParamName);
        Assert.StartsWith("maturity (-1) must be non-negative", theta.Message, StringComparison.Ordinal);
        foreach (Action negative in new Action[] { () => calc.Vega(-1.0), () => calc.Rho(-1.0), () => calc.DividendRho(-1.0) })
        {
            ArgumentException e = Assert.Throws<ArgumentException>(negative);
            Assert.Equal("maturity", e.ParamName);
            Assert.StartsWith("negative maturity not allowed", e.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ThePerpetualFuturesChecks()
    {
        ArgumentOutOfRangeException payoff = Assert.Throws<ArgumentOutOfRangeException>(
            () => new PerpetualFutures((PerpetualFuturesPayoffType)3));
        Assert.Equal("payoffType", payoff.ParamName);
        Assert.StartsWith("unknown payoff type", payoff.Message, StringComparison.Ordinal);
        ArgumentOutOfRangeException funding = Assert.Throws<ArgumentOutOfRangeException>(
            () => new PerpetualFutures(PerpetualFuturesPayoffType.Linear, (PerpetualFuturesFundingType)2));
        Assert.Equal("fundingType", funding.ParamName);
        Assert.StartsWith("unknown funding type", funding.Message, StringComparison.Ordinal);

        var futures = new PerpetualFutures(PerpetualFuturesPayoffType.Linear);
        Assert.Equal("fundingTimes", Refused(() => Price(futures, [], [0.01], [0.0])).ParamName);
        Assert.StartsWith("fundingTimes is empty", Refused(() => Price(futures, [], [0.01], [0.0])).Message, StringComparison.Ordinal);
        Assert.StartsWith("fundingRates is empty", Refused(() => Price(futures, [0.0], [], [0.0])).Message, StringComparison.Ordinal);
        Assert.StartsWith(
            "interestRateDiffs is empty", Refused(() => Price(futures, [0.0], [0.01], [])).Message, StringComparison.Ordinal);
        Assert.StartsWith(
            "fundingTimes and fundingRates must have the same size.",
            Refused(() => Price(futures, [0.0, 1.0], [0.01], [0.0, 0.0])).Message,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "fundingTimes and interestRateDiffs must have the same size.",
            Refused(() => Price(futures, [0.0, 1.0], [0.01, 0.01], [0.0])).Message,
            StringComparison.Ordinal);
        ArgumentOutOfRangeException interpolation = Assert.Throws<ArgumentOutOfRangeException>(
            () => DiscountingPerpetualFuturesEngine.Calculate(
                futures, Context, Flat(0.04), Flat(0.02), 100.0, [0.0], [0.01], [0.0],
                (DiscountingPerpetualFuturesEngineInterpolationType)3));
        Assert.Equal("fundingInterpType", interpolation.ParamName);
        Assert.StartsWith("Unknown interpolation type", interpolation.Message, StringComparison.Ordinal);

        Assert.Equal(
            "Only Linear and Inverse payoffs are supported in DiscountingPerpetualFuturesEngine",
            Assert.Throws<NanoQLException>(
                () => Price(new PerpetualFutures(PerpetualFuturesPayoffType.Quanto), [0.0], [0.01], [0.0])).Message);

        // "Unknown unit in fundingFrequency": every unit a period can hold is handled
        Assert.Throws<ArgumentOutOfRangeException>(() => new Period(8, (TimeUnit)99));
    }

    [Fact]
    public void TheStorageOptionsAndTotalReturnSwapsChecks()
    {
        var exercise = new BermudanExercise([Today + 30, Today + 60, Today + 90]);
        Assert.Equal(
            "positive capacity, load and change rate required",
            Assert.Throws<NanoQLException>(() => new VanillaStorageOption(exercise, 0.0, 0.0, 1.0).Validate()).Message);
        Assert.Equal(
            "illegal values load of changeRate",
            Assert.Throws<NanoQLException>(() => new VanillaStorageOption(exercise, 10.0, 11.0, 1.0).Validate()).Message);

        // QuantLib's fairMargin reads its legs' NPVs and BPS: results without them are "result not available"
        var swap = new EquityTotalReturnSwap(
            SwapType.Receiver, 1.0e7,
            new MakeSchedule().From(Today).To(Today + 182).WithTenor(new Period(3, TimeUnit.Months)).WithCalendar(new NullCalendar())
                .ToSchedule(),
            new NanoQL.Indexes.EquityIndex("eq", new NullCalendar(), new NanoQL.Currencies.USDCurrency(), Flat(0.04), Flat(0.01), 100.0),
            new NanoQL.Indexes.Sofr(Flat(0.04)),
            A365,
            0.0);
        Assert.Equal("result not available", Assert.Throws<NanoQLException>(() => swap.FairMargin(new SwapResults())).Message);
    }

    [Fact]
    public void TheVarianceSwapsMonteCarloChecks()
    {
        // "Unknown position": the swap refuses one outside its type
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new VarianceSwap((PositionType)7, 0.04, 1.0, Today, Today + 365));

        // the path pricer: an empty path, and a one-point path, whose first step QuantLib reads past its grid
        var pricer = new VariancePathPricer(new BlackScholesMertonProcess(
            100.0, Flat(0.01), Flat(0.05), new BlackConstantVol(Today, new NullCalendar(), 0.2, A365)));
        Assert.Equal(
            "the path cannot be empty",
            Assert.Throws<NanoQLException>(() => pricer.Evaluate(new MonteCarloPath(new NanoQL.TimeGrid()))).Message);
        ArgumentException noStep = Refused(
            () => pricer.Evaluate(new MonteCarloPath(new NanoQL.TimeGrid([0.0]), new Numerics.Vector([100.0]))));
        Assert.Equal("path", noStep.ParamName);
        Assert.Equal("the path has no step (Parameter 'path')", noStep.Message);

        // the interval count's guard: every grid starts at 0, so a grid of one step already holds one interval
        Assert.Equal(
            0.04, pricer.Evaluate(new MonteCarloPath(new NanoQL.TimeGrid(1.0, 1), new Numerics.Vector([100.0, 100.0]))), 1e-15);
    }

    private static FlatForward Flat(double rate) => new(Today, rate, A365);

    private static InstrumentResults Price(PerpetualFutures futures, double[] times, double[] rates, double[] diffs) =>
        DiscountingPerpetualFuturesEngine.Calculate(futures, Context, Flat(0.04), Flat(0.02), 100.0, times, rates, diffs);

    private static ArgumentException Refused(Action act) => Assert.Throws<ArgumentException>(act);
}
