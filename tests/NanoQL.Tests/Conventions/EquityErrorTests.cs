// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception type, message and parameter name of the equity contracts' refusals: those the vector files record as
/// <c>{"throws": true}</c>, accepting either type, those no vector reaches, and the undefined paths they refuse. An
/// argument a member checks is an <see cref="ArgumentException"/> naming it, with QuantLib's text where QuantLib has
/// one; a value the member computes or holds is a <see cref="NanoQLException"/>.
/// </summary>
public class EquityErrorTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter Dc = new Actual365Fixed();

    [Fact]
    public void BlackCalculatorRefusals()
    {
        var call = new PlainVanillaPayoff(OptionType.Call, 100.0);
        Refused<ArgumentException>(() => new BlackCalculator(OptionType.Call, -1.0, 100.0, 0.2), "strike", "strike (-1) must be non-negative");
        Refused<ArgumentException>(
            () => new BlackCalculator(new PlainVanillaPayoff(OptionType.Call, -1.0), 100.0, 0.2), "payoff", "strike (-1) must be non-negative");
        Refused<ArgumentException>(() => new BlackCalculator(call, 0.0, 0.2), "forward", "forward (0) must be positive");
        Refused<ArgumentException>(() => new BlackCalculator(call, 100.0, -0.1), "stdDev", "stdDev (-0.1) must be non-negative");
        Refused<ArgumentException>(() => new BlackCalculator(call, 100.0, 0.2, 0.0), "discount", "discount (0) must be positive");
        Refused<ArgumentException>(
            () => new BlackCalculator(new PercentageStrikePayoff(OptionType.Call, 1.0), 100.0, 0.2), "payoff", "unsupported payoff type: PercentageStrike");
        var c = new BlackCalculator(call, 104.0, 0.3, 0.95);
        Refused<ArgumentException>(() => c.Delta(0.0), "spot", "positive spot value required: 0 not allowed");
        Refused<ArgumentException>(() => c.Gamma(0.0), "spot", "positive spot value required: 0 not allowed");
        Refused<ArgumentException>(() => c.Vanna(0.0, 1.0), "spot", "positive spot value required: 0 not allowed");
        Refused<ArgumentException>(() => c.Theta(100.0, -0.1), "maturity", "maturity (-0.1) must be non-negative");
        Refused<ArgumentException>(() => c.Vega(-0.1), "maturity", "negative maturity not allowed");
        Refused<ArgumentException>(() => c.Rho(-0.1), "maturity", "negative maturity not allowed");
        Refused<ArgumentException>(() => c.DividendRho(-0.1), "maturity", "negative maturity not allowed");
        Refused<ArgumentException>(() => c.Vanna(100.0, -0.1), "maturity", "negative maturity not allowed");
        Refused<ArgumentException>(() => c.Volga(-0.1), "maturity", "negative maturity not allowed");
    }

    [Fact]
    public void VolatilityStructureRefusals()
    {
        Refused<ArgumentException>(
            () => new BlackVarianceCurve(Today, [Today + 182, Today + 365], [0.2], Dc), "blackVolCurve", "mismatch between date vector and black vol vector");
        Refused<ArgumentException>(
            () => new BlackVarianceCurve(Today, [Today, Today + 365], [0.2, 0.2], Dc), "dates", "cannot have dates[0] <= referenceDate");
        Refused<ArgumentException>(
            () => new BlackVarianceCurve(Today, [Today + 365, Today + 182], [0.2, 0.2], Dc), "dates", "dates must be sorted unique!");
        Refused<ArgumentException>(
            () => new BlackVarianceCurve(Today, [Today + 182, Today + 365], [0.4, 0.2], Dc), "blackVolCurve", "variance must be non-decreasing");
        Refused<ArgumentException>(() => new BlackVarianceCurve(Today, [], [], Dc), "dates", "no dates given");
        Refused<ArgumentException>(
            () => new BlackVarianceSurface(Today, null, [Today + 182, Today + 365], [90.0, 110.0], new Matrix(2, 1), Dc),
            "blackVolMatrix",
            "mismatch between date vector and vol matrix colums");
        Refused<ArgumentException>(
            () => new BlackVarianceSurface(Today, null, [Today + 182], [90.0, 100.0, 110.0], new Matrix(2, 1), Dc),
            "blackVolMatrix",
            "mismatch between money-strike vector and vol matrix rows");
        Refused<ArgumentException>(
            () => new BlackVarianceSurface(Today, null, [Today - 1], [90.0, 110.0], new Matrix(2, 1, 0.2), Dc), "dates", "cannot have dates[0] < referenceDate");
        Refused<ArgumentException>(
            () => new BlackVarianceSurface(Today, null, [Today + 365, Today + 182], [90.0, 110.0], new Matrix(2, 2, 0.2), Dc),
            "dates",
            "dates must be sorted unique!");
        Refused<ArgumentException>(
            () => new BlackVarianceSurface(Today, null, [], [90.0, 110.0], new Matrix(2, 0), Dc), "dates", "no dates given");
        Refused<ArgumentException>(
            () => new BlackVarianceSurface(Today, null, [Today + 365], [100.0], new Matrix(1, 1, 0.2), Dc),
            "y",
            "not enough y points to interpolate: at least 2 required, 1 provided");
        Refused<ArgumentOutOfRangeException>(
            () => new BlackVarianceSurface(
                Today, null, [Today + 182, Today + 365], [90.0, 110.0], new Matrix(2, 2, 0.2), Dc, (BlackVarianceSurfaceExtrapolation)5),
            "lowerExtrapolation",
            "unknown extrapolation type");
        Refused<ArgumentOutOfRangeException>(
            () => new BlackVarianceCurve(Today, [Today + 182], [0.2], Dc, true, (BlackVolTimeExtrapolationType)3),
            "timeExtrapolationType",
            "unknown extrapolation type");

        var curve = new BlackVarianceCurve(Today, [Today + 182, Today + 365], [0.40, 0.20], Dc, false);
        Refused<ArgumentException>(() => curve.BlackForwardVol(1.0, 0.5, 100.0), "time1", "1 later than 0.5");
        Refused<ArgumentException>(() => curve.BlackForwardVol(Today + 365, Today + 182, 100.0), "date1", null);
        Refused<NanoQLException>(() => curve.BlackForwardVariance(0.5, 1.0, 100.0), null, "variances must be non-decreasing");
        Refused<ArgumentException>(() => curve.BlackForwardVariance(1.0, 0.5, 100.0), "time1", "1 later than 0.5");
        Refused<ArgumentException>(
            () => curve.BlackForwardVariance(Today + 365, Today + 182, 100.0), "date1", $"{Today + 365} later than {Today + 182}");
        Refused<NanoQLException>(() => curve.BlackVol(-0.1, 100.0), null, "negative time (-0.1) given");
        Refused<NanoQLException>(() => curve.BlackVol(10.0, 100.0), null, "time (10) is past max curve time");
    }

    [Fact]
    public void TimeExtrapolationRefusals()
    {
        Refused<ArgumentException>(
            () => BlackVolTimeExtrapolation.ExtrapolatedVariance(BlackVolTimeExtrapolationType.LinearVariance, 3.0, [2.0, 1.0], t => t),
            "times",
            "times must be sorted");
        Refused<NanoQLException>(
            () => BlackVolTimeExtrapolation.ExtrapolatedVariance(BlackVolTimeExtrapolationType.LinearVariance, 3.0, [1.0, 2.0], t => -t),
            null,
            "variances must be non-decreasing");
        Refused<ArgumentException>(
            () => BlackVolTimeExtrapolation.ExtrapolatedVariance(BlackVolTimeExtrapolationType.LinearVariance, 3.0, [1.0], t => t),
            "times",
            "at least two times required for volatility extrapolation");
        Refused<ArgumentException>(
            () => BlackVolTimeExtrapolation.ExtrapolatedVariance(BlackVolTimeExtrapolationType.FlatVolatility, 3.0, [], t => t),
            "times",
            "at least one time required for volatility extrapolation");
        Refused<ArgumentException>(
            () => BlackVolTimeExtrapolation.ExtrapolatedVariance(BlackVolTimeExtrapolationType.LinearVariance, -1.0, [1.0, 2.0], t => t),
            "t",
            "t must be greater than 0.0");
        Refused<ArgumentException>(
            () => BlackVolTimeExtrapolation.ExtrapolatedVariance(BlackVolTimeExtrapolationType.LinearVariance, 1.5, [1.0, 2.0], t => t),
            "t",
            "t must be greater than times[1]");
        Refused<ArgumentOutOfRangeException>(
            () => BlackVolTimeExtrapolation.ExtrapolatedVariance((BlackVolTimeExtrapolationType)7, 3.0, [1.0], t => t),
            "type",
            "unknown extrapolation type");
    }

    [Fact]
    public void LocalVolatilityRefusals()
    {
        var falling = new BlackVarianceCurve(Today, [Today + 182, Today + 365], [0.40, 0.20], Dc, false);
        var surface = new LocalVolSurface(falling, new FlatForward(Today, 0.04, Dc), new FlatForward(Today, 0.02, Dc), 100.0);
        Refused<NanoQLException>(() => surface.LocalVol(0.75, 100.0, true), null, "decreasing variance at strike 100 between time");
        var fallingAtZero = new LocalVolSurface(
            new VarianceFallingAfterZero(Today, Dc), new FlatForward(Today, 0.04, Dc), new FlatForward(Today, 0.02, Dc), 100.0);
        Refused<NanoQLException>(
            () => fallingAtZero.LocalVol(0.0, 100.0, true), null, "decreasing variance at strike 100 between time 0 and time 0.0001");
        Refused<NanoQLException>(() => new LocalVolCurve(falling).LocalVol(0.75, 100.0, true), null, "negative local vol^2 at time 0.75");
        Refused<NanoQLException>(() => new LocalConstantVol(Today, 0.2, Dc).LocalVol(-0.5, 100.0), null, "negative time (-0.5) given");
    }

    [Fact]
    public void ProcessAndDividendRefusals()
    {
        var vols = new Matrix(2, 2, 0.2);
        var surface = new BlackVarianceSurface(Today, null, [Today + 182, Today + 365], [90.0, 110.0], vols, Dc);
        var process = new BlackScholesMertonProcess(100.0, new FlatForward(Today, 0.02, Dc), new FlatForward(Today, 0.04, Dc), surface);
        Refused<NanoQLException>(() => process.Expectation(0.0, 100.0, 0.5), null, "not implemented");
        Refused<ArgumentException>(
            () => DividendFunctions.DividendVector([Today + 30, Today + 60], [1.0]), "dividends", "size mismatch between dividend dates and amounts");
        Refused<NanoQLException>(
            () => new FractionalDividend(0.02, Today + 30).Amount(new EvaluationContext { EvaluationDate = Today }), null, "no nominal given");
    }

    [Fact]
    public void EngineAndImpliedVolatilityRefusals()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, Dc), new FlatForward(Today, 0.04, Dc), new BlackConstantVol(Today, null, 0.2, Dc));
        var payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);
        var european = new VanillaOption(payoff, new EuropeanExercise(Today + 182));
        var american = new VanillaOption(payoff, new AmericanExercise(Today, Today + 182));

        Refused<NanoQLException>(() => AnalyticEuropeanEngine.Calculate(american, context, process), null, "not an European option");
        Refused<NanoQLException>(
            () => AnalyticDividendEuropeanEngine.Calculate(american, context, process, []), null, "not an European option");
        var zeroSpot = new BlackScholesMertonProcess(
            0.0, new FlatForward(Today, 0.02, Dc), new FlatForward(Today, 0.04, Dc), new BlackConstantVol(Today, null, 0.2, Dc));
        Refused<NanoQLException>(() => AnalyticEuropeanEngine.Calculate(european, context, zeroSpot), null, "negative or null underlying given");
        Refused<NanoQLException>(
            () => AnalyticDividendEuropeanEngine.Calculate(european, context, process, DividendFunctions.DividendVector([Today + 30], [120.0])),
            null,
            "negative or null underlying after subtracting dividends");
        Refused<ArgumentException>(
            () => AnalyticDividendEuropeanEngine.Calculate(european, context, process, default), "dividends", "default(ImmutableArray) is not a dividend schedule");
        Refused<ArgumentException>(
            () => AnalyticDividendEuropeanEngine.Calculate(european, context, process, ImmutableArray.Create((Dividend)null!)), "dividends", "null dividend in schedule");

        var expired = new VanillaOption(payoff, new EuropeanExercise(Today - 1));
        Refused<NanoQLException>(() => expired.ImpliedVolatility(5.0, process, context), null, "option expired");
        Refused<ArgumentException>(
            () => european.ImpliedVolatility(5.0, process, context, 1e-4, 100, 2.0, 1.0), "minVol", "invalid range: xMin_ (2) >= xMax_ (1)");
        Refused<ArgumentOutOfRangeException>(() => european.ImpliedVolatility(5.0, process, context, 1e-4, -1), "maxEvaluations", null);
        Refused<NanoQLException>(() => european.ImpliedVolatility(150.0, process, context), null, "root not bracketed");
        Refused<NanoQLException>(() => american.ImpliedVolatility(150.0, process, context), null, "root not bracketed");
    }

    [Fact]
    public void BinomialEngineRefusals()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, Dc), new FlatForward(Today, 0.04, Dc), new BlackConstantVol(Today, null, 0.2, Dc));
        var european = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 182));

        Refused<ArgumentException>(
            () => BinomialCRRVanillaEngine.Calculate(european, context, process, 1), "timeSteps", "at least 2 time steps required, 1 provided");
        var digital = new VanillaOption(new CashOrNothingPayoff(OptionType.Call, 100.0, 1.0), new EuropeanExercise(Today + 182));
        Refused<NanoQLException>(() => BinomialCRRVanillaEngine.Calculate(digital, context, process, 51), null, "non-plain payoff given");
        var zeroSpot = new BlackScholesMertonProcess(
            0.0, new FlatForward(Today, 0.02, Dc), new FlatForward(Today, 0.04, Dc), new BlackConstantVol(Today, null, 0.2, Dc));
        Refused<NanoQLException>(
            () => BinomialCRRVanillaEngine.Calculate(european, context, zeroSpot, 51), null, "negative or null underlying given");
        var zeroStrike = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 0.0), new EuropeanExercise(Today + 182));
        Refused<ArgumentException>(() => BinomialLRVanillaEngine.Calculate(zeroStrike, context, process, 51), "strike", "strike must be positive");
        Refused<ArgumentException>(() => BinomialJ4VanillaEngine.Calculate(zeroStrike, context, process, 51), "strike", "strike must be positive");

        // a volatility too low for the trees' probabilities
        var lowVol = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.0, Dc), new FlatForward(Today, 0.3, Dc), new BlackConstantVol(Today, null, 0.01, Dc));
        Refused<NanoQLException>(() => BinomialCRRVanillaEngine.Calculate(european, context, lowVol, 3), null, "negative probability");
        // Trigeorgis's and Tian's probabilities stay in [0, 1] for real inputs; a NaN volatility reaches their checks
        var nanVol = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.0, Dc), new FlatForward(Today, 0.03, Dc), new BlackConstantVol(Today, null, double.NaN, Dc));
        Refused<NanoQLException>(() => BinomialTrigeorgisVanillaEngine.Calculate(european, context, nanVol, 3), null, "negative probability");
        Refused<NanoQLException>(() => BinomialTianVanillaEngine.Calculate(european, context, nanVol, 3), null, "negative probability");
        Refused<NanoQLException>(
            () => BinomialEQPVanillaEngine.Calculate(european, context, lowVol, 3),
            null,
            "drift per step too large for the additive EQP tree: more steps are needed");
    }

    [Fact]
    public void VarianceSwapRefusals()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.0, Dc), new FlatForward(Today, 0.05, Dc), new BlackConstantVol(Today, null, 0.2, Dc));
        var swap = new VarianceSwap(PositionType.Long, 0.04, 50000, Today, Today + 90);
        double[] calls = [100.0, 110.0, 120.0];
        double[] puts = [80.0, 90.0, 100.0];

        Refused<ArgumentOutOfRangeException>(() => new VarianceSwap((PositionType)2, 0.04, 1.0, Today, Today + 90), "position", "Unknown position");
        Refused<ArgumentException>(() => new VarianceSwap(PositionType.Long, 0.04, 1.0, default, Today + 90), "startDate", null);
        Refused<ArgumentException>(() => new VarianceSwap(PositionType.Long, 0.04, 1.0, Today, default), "maturityDate", null);

        Refused<ArgumentNullException>(() => ReplicatingVarianceSwapEngine.Calculate(swap, context, null!, 5.0, calls, puts), "process", "no process given");
        Refused<ArgumentException>(() => ReplicatingVarianceSwapEngine.Calculate(swap, context, process, 5.0, [], puts), "callStrikes", "no strike(s) given");
        Refused<ArgumentException>(() => ReplicatingVarianceSwapEngine.Calculate(swap, context, process, 5.0, calls, []), "putStrikes", "no strike(s) given");
        Refused<ArgumentException>(
            () => ReplicatingVarianceSwapEngine.Calculate(swap, context, process, 5.0, calls, [0.0, 100.0]), "putStrikes", "min put strike must be positive");
        Refused<ArgumentException>(
            () => ReplicatingVarianceSwapEngine.Calculate(swap, context, process, 5.0, calls, [80.0, 95.0]),
            "callStrikes",
            "min call and max put strikes differ");

        Refused<NanoQLException>(
            () => ReplicatingVarianceSwapEngine.Calculate(
                new VarianceSwap(PositionType.Long, 0.0, 1.0, Today, Today + 90), context, process, 5.0, calls, puts),
            null,
            "negative or null strike given");
        Refused<NanoQLException>(
            () => ReplicatingVarianceSwapEngine.Calculate(
                new VarianceSwap(PositionType.Short, 0.04, -1.0, Today, Today + 90), context, process, 5.0, calls, puts),
            null,
            "negative or null notional given");

        // an expired swap is worth nothing and has no variance
        VarianceSwapResults expired = ReplicatingVarianceSwapEngine.Calculate(
            new VarianceSwap(PositionType.Long, 0.04, 1.0, Today - 90, Today - 1), context, process, 5.0, calls, puts);
        Assert.Equal(0.0, expired.NPV);
        Assert.Null(expired.Variance);
        Refused<ArgumentException>(() => swap.FetchResults(new InstrumentResults()), "results", "wrong results type");
    }

    // A Black variance of 0.04 at time 0 and 0.01 after it, which no ported structure gives: LocalVolSurface's check at
    // t = 0 reads it.
    private sealed class VarianceFallingAfterZero(Date referenceDate, DayCounter dayCounter)
        : BlackVarianceTermStructure(referenceDate, null, BusinessDayConvention.Following, dayCounter)
    {
        public override Date MaxDate => Date.MaxDate;

        public override double MinStrike() => 0.0;

        public override double MaxStrike() => double.MaxValue;

        protected override double BlackVarianceImpl(double t, double strike) => t == 0.0 ? 0.04 : 0.01;
    }

    private static void Refused<TException>(Action call, string? paramName, string? message)
        where TException : Exception
    {
        TException e = Assert.Throws<TException>(call);
        if (e is ArgumentException a)
        {
            Assert.Equal(paramName, a.ParamName);
        }

        if (message is not null)
        {
            Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
        }
    }
}
