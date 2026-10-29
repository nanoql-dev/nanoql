// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Experimental;
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
/// C2's refusals and list conventions (the S-xeq plan's XER5 (xvii), (xxiii), XER1, the seasoned variance option; the
/// run's R2-1 and R2-3): variance gamma past its first moment; a Longstaff-Schwartz calibration with no paths; the
/// seasoned variance option; a list of options priced in its order with its expired options; Vecer's engine refusing an
/// option whose own start differs from the engine's; the path engines refusing unsorted or missing fixing dates.
/// </summary>
public class ExperimentalExoticConventionTests
{
    private static readonly Date Today = new(15, May, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly YieldTermStructure Risk = new FlatForward(Today, 0.05, A365);
    private static readonly YieldTermStructure Dividend = new FlatForward(Today, 0.02, A365);

    [Fact]
    public void VarianceGammaPastItsFirstMomentIsRefused()
    {
        // 1 − θν − σ²ν/2 = 1 − 1.2 − 0.06: E[e^(X_t)] is infinite, so no martingale correction and no price exist
        const string text = "1 - theta*nu - sigma^2*nu/2 must be positive: the variance gamma spot's mean is infinite";
        var process = new VarianceGammaProcess(100.0, Dividend, Risk, 0.2, 3.0, 0.4);
        VanillaOption option = Call(100.0, Today + 365);
        Assert.Equal(text, Assert.Throws<NanoQLException>(() => VarianceGammaEngine.Calculate(option, Context, process)).Message);
        Assert.Equal(text, Assert.Throws<NanoQLException>(() => FFTVarianceGammaEngine.Calculate(option, Context, process)).Message);
        Assert.Equal(text, Assert.Throws<NanoQLException>(() => FFTVarianceGammaEngine.Calculate([option], Context, process)).Message);
    }

    [Fact]
    public void ALongstaffSchwartzCalibrationNeedsPaths()
    {
        // QuantLib's pricer reads paths_[0] with none stored
        var option = new PathOption(new BermudanPut(100.0), [Today + 91, Today + 182]);
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
            () => MCAmericanPathEngine<PseudoRandom, GeneralStatistics>.Calculate(
                option, Context, OneAsset(), 2, null, false, false, false, 1000, null, null, 42, 0));
        Assert.Equal("nCalibrationSamples", e.ParamName);

        var pricer = new LongstaffSchwartzMultiPathPricer(
            new BermudanPut(100.0), [1, 2], [Risk, Risk], new Vector(2, 1.0), 2, LsmBasisSystemPolynomialType.Monomial);
        Assert.Equal(
            "no calibration paths: the number of calibration samples must be positive",
            Assert.Throws<NanoQLException>(pricer.Calibrate).Message);
    }

    [Fact]
    public void ASeasonedVarianceOptionIsRefused()
    {
        var process = new HestonProcess(Risk, Dividend, 1.0, 0.04, 1.5, 0.04, 0.3, -0.5);
        var option = new VarianceOption(new PlainVanillaPayoff(OptionType.Call, 0.05), 1.0, Today - 1, Today + 365);
        Assert.Equal(
            "this engine does not manage seasoned variance options",
            Assert.Throws<NanoQLException>(() => IntegralHestonVarianceOptionEngine.Calculate(option, Context, process)).Message);
    }

    [Fact]
    public void AListIsPricedInItsOrderWithItsExpiredOptions()
    {
        // an expired option among live ones of two expiries: its expired results in its place, each live option its own
        // price, within the FFT grid's error of the analytic European
        GeneralizedBlackScholesProcess process = Process();
        VanillaOption[] options =
        [
            Call(110.0, Today + 365),
            Call(100.0, Today - 1),
            new(new PlainVanillaPayoff(OptionType.Put, 90.0), new EuropeanExercise(Today + 182)),
            Call(100.0, Today + 365),
        ];
        ImmutableArray<OneAssetOptionResults> results = FFTVanillaEngine.Calculate(options, Context, process);
        Assert.Equal(options.Length, results.Length);
        Assert.Equal(0.0, results[1].NPV);
        foreach (int k in new[] { 0, 2, 3 })
        {
            double analytic = AnalyticEuropeanEngine.Calculate(options[k], Context, process).NPV!.Value;
            Assert.True(Math.Abs(results[k].NPV!.Value - analytic) <= 2e-3, $"option {k}: {results[k].NPV} against {analytic}");
        }
    }

    [Fact]
    public void VecersEngineRefusesAnOptionStartingOtherThanItsOwnStart()
    {
        // QuantLib's engine ignores the option's start date and averages from its own
        var option = new ContinuousAveragingAsianOption(
            AverageType.Arithmetic, Today + 10, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));
        Assert.Equal(
            "the option's start date differs from the engine's",
            Assert.Throws<NanoQLException>(
                () => ContinuousArithmeticAsianVecerEngine.Calculate(option, Context, Process(), 0.0, Today)).Message);
    }

    [Fact]
    public void ThePathEnginesRefuseUnsortedOrMissingFixingDates()
    {
        var unsorted = new PathOption(new BermudanPut(100.0), [Today + 182, Today + 91]);
        Assert.Equal(
            "Invalid dates/times",
            Assert.Throws<NanoQLException>(() => MCPathBasketEngine.Calculate<PseudoRandom, GeneralStatistics>(
                unsorted, Context, OneAsset(), 2, null, false, false, false, 1000, null, null, 42)).Message);
        Assert.Equal(
            "Invalid dates/times",
            Assert.Throws<NanoQLException>(() => MCAmericanPathEngine<PseudoRandom, GeneralStatistics>.Calculate(
                unsorted, Context, OneAsset(), 2, null, false, false, false, 1000, null, null, 42)).Message);

        var none = new PathOption(new BermudanPut(100.0), []);
        Assert.Equal(
            "no dates given",
            Assert.Throws<NanoQLException>(() => MCPathBasketEngine.Calculate<PseudoRandom, GeneralStatistics>(
                none, Context, OneAsset(), 2, null, false, false, false, 1000, null, null, 42)).Message);
        Assert.Equal("no fixing dates given", Assert.Throws<NanoQLException>(() => new HimalayaOption([], 100.0)).Message);
        Assert.Equal("no fixingDates given", Assert.Throws<NanoQLException>(() => new PagodaOption([], 0.2, 0.6)).Message);
    }

    private static VanillaOption Call(double strike, Date expiry) =>
        new(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(expiry));

    // S 100, q 0.02, σ 0.25 on r 0.05
    private static GeneralizedBlackScholesProcess Process() =>
        new BlackScholesMertonProcess(100.0, Dividend, Risk, new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    private static StochasticProcessArray OneAsset() => new([Process()], new Matrix(new[,] { { 1.0 } }));

    // a path option on a payoff and its fixing dates
    private sealed class PathOption(PathPayoff payoff, ImmutableArray<Date> fixingDates) : PathMultiAssetOption
    {
        public override PathPayoff PathPayoff => payoff;

        public override ImmutableArray<Date> FixingDates => fixingDates;
    }

    // a put exercisable at every fixing, its regression state the asset
    private sealed class BermudanPut(double strike) : PathPayoff
    {
        public override string Name => "BermudanPut";

        public override string Description => "max(K - S, 0) at any fixing";

        public override int BasisSystemDimension => 1;

        public override void Value(
            Matrix path, ImmutableArray<YieldTermStructure> forwardTermStructures, Vector payments, Vector exercises, Vector[] states)
        {
            for (int i = 0; i < path.Columns; ++i)
            {
                payments[i] = 0.0;
                exercises[i] = Math.Max(strike - path[0, i], 0.0);
                states[i] = new Vector(1, path[0, i]);
            }
        }
    }
}
