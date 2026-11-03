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
/// Two routes to one value for the experimental exotics: Everest, Himalaya and Pagoda on one asset and one fixing
/// against a forward, a call and a call spread; the path basket engine against Stulz's two-asset maximum call; the
/// American path engine against the Longstaff-Schwartz engine on a Bermudan put. Each Monte Carlo price within three of
/// its error estimates (the Longstaff-Schwartz pair within three of their combined estimates). Variance gamma's and the
/// FFT engine's routes sit with QuantLib's cases and the divergence facts.
/// </summary>
public class ExperimentalExoticTwoRouteTests
{
    private static readonly Date Today = new(15, May, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly Date Maturity = Today + 364;
    private const long Seed = 86421;
    private const int Samples = 20000;

    [Fact]
    public void AnEverestOnOneAssetIsAForward()
    {
        // (1 + S_T/S_0 − 1 + g) on the asset alone: g D + Q, D and Q the risk-free and dividend discounts
        const double guarantee = 0.1;
        var option = new EverestOption(1.0, guarantee, new EuropeanExercise(Maturity));
        EverestOptionResults results = new MakeMCEverestEngine<PseudoRandom, GeneralStatistics>(OneAsset(), Seed)
            .WithStepsPerYear(1)
            .WithSamples(Samples)
            .ToEngine()(option, Context);
        double forward = (guarantee * Risk.Discount(Maturity)) + Dividend.Discount(Maturity);
        Assert.True(
            Math.Abs(results.NPV!.Value - forward) <= 3 * results.ErrorEstimate!.Value,
            $"{results.NPV} against {forward} ± {results.ErrorEstimate}");
    }

    [Fact]
    public void AHimalayaOnOneAssetAndOneFixingIsACall()
    {
        // the best performer's price at the one fixing is the asset's: the call on it
        var option = new HimalayaOption([Maturity], 100.0);
        MultiAssetOptionResults results = new MakeMCHimalayaEngine<PseudoRandom, GeneralStatistics>(OneAsset(), Seed)
            .WithSamples(Samples)
            .ToEngine()(option, Context);
        double call = Call(100.0);
        Assert.True(
            Math.Abs(results.NPV!.Value - call) <= 3 * results.ErrorEstimate!.Value,
            $"{results.NPV} against {call} ± {results.ErrorEstimate}");
    }

    [Fact]
    public void APagodaOnOneAssetAndOneFixingIsACallSpread()
    {
        // fraction · min(roof, S_T − S_0)⁺, the performance in price units: the fraction of a call spread from S_0 to
        // S_0 + roof
        const double roof = 20.0, fraction = 0.62;
        var option = new PagodaOption([Maturity], roof, fraction);
        MultiAssetOptionResults results = new MakeMCPagodaEngine<PseudoRandom, GeneralStatistics>(OneAsset(), Seed)
            .WithSamples(Samples)
            .ToEngine()(option, Context);
        double spread = fraction * (Call(100.0) - Call(100.0 + roof));
        Assert.True(
            Math.Abs(results.NPV!.Value - spread) <= 3 * results.ErrorEstimate!.Value,
            $"{results.NPV} against {spread} ± {results.ErrorEstimate}");
    }

    [Fact]
    public void ThePathBasketIsStulzsMaximumCall()
    {
        // a payment of max(S₁, S₂) − K at the one fixing, the maturity, against Stulz's closed form
        const double strike = 100.0, correlation = 0.5;
        GeneralizedBlackScholesProcess first = Process(100.0, 0.02, 0.25), second = Process(95.0, 0.03, 0.30);
        var processes = new StochasticProcessArray(
            [first, second], new Matrix(new[,] { { 1.0, correlation }, { correlation, 1.0 } }));
        var option = new PathOption(new MaximumCall(strike), [Maturity]);
        InstrumentResults results = MCPathBasketEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option, Context, processes, 1, null, false, false, false, Samples, null, null, Seed);
        double stulz = StulzEngine.Calculate(
            new BasketOption(new MaxBasketPayoff(new PlainVanillaPayoff(OptionType.Call, strike)), new EuropeanExercise(Maturity)),
            Context,
            first,
            second,
            correlation).NPV!.Value;
        Assert.True(
            Math.Abs(results.NPV!.Value - stulz) <= 3 * results.ErrorEstimate!.Value,
            $"{results.NPV} against {stulz} ± {results.ErrorEstimate}");
    }

    [Fact]
    public void TheAmericanPathEngineIsTheLongstaffSchwartzEngine()
    {
        // a put exercisable at four quarterly fixings, 91 days apart, against the Longstaff-Schwartz engine on four
        // steps of 91 days
        const double strike = 100.0;
        Date[] fixings = [Today + 91, Today + 182, Today + 273, Maturity];
        var option = new PathOption(new BermudanPut(strike), [.. fixings]);
        InstrumentResults path = MCAmericanPathEngine<PseudoRandom, GeneralStatistics>.Calculate(
            option, Context, OneAsset(), 4, null, false, false, false, Samples, null, null, Seed, 4096);
        OneAssetOptionResults longstaffSchwartz = new MakeMCAmericanEngine<PseudoRandom, GeneralStatistics, PseudoRandom>(Process(100.0, 0.02, 0.25), Seed)
            .WithSteps(4)
            .WithSamples(Samples)
            .WithCalibrationSamples(4096)
            .ToEngine()(new VanillaOption(new PlainVanillaPayoff(OptionType.Put, strike), new AmericanExercise(Today, Maturity)), Context);
        double error = Math.Sqrt(
            (path.ErrorEstimate!.Value * path.ErrorEstimate.Value)
            + (longstaffSchwartz.ErrorEstimate!.Value * longstaffSchwartz.ErrorEstimate.Value));
        Assert.True(
            Math.Abs(path.NPV!.Value - longstaffSchwartz.NPV!.Value) <= 3 * error,
            $"{path.NPV} against {longstaffSchwartz.NPV} ± {error}");
    }

    private static readonly YieldTermStructure Risk = new FlatForward(Today, 0.05, A365);

    private static readonly YieldTermStructure Dividend = new FlatForward(Today, 0.02, A365);

    // the market's asset: S 100, q 0.02, σ 0.25 on r 0.05
    private static StochasticProcessArray OneAsset() =>
        new([Process(100.0, 0.02, 0.25)], new Matrix(new[,] { { 1.0 } }));

    private static GeneralizedBlackScholesProcess Process(double spot, double dividendYield, double volatility) =>
        new BlackScholesMertonProcess(
            spot,
            new FlatForward(Today, dividendYield, A365),
            Risk,
            new BlackConstantVol(Today, new NullCalendar(), volatility, A365));

    // the market asset's European call at the maturity
    private static double Call(double strike) => AnalyticEuropeanEngine.Calculate(
        new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(Maturity)),
        Context,
        Process(100.0, 0.02, 0.25)).NPV!.Value;

    // a path option on a payoff and its fixing dates
    private sealed class PathOption(PathPayoff payoff, ImmutableArray<Date> fixingDates) : PathMultiAssetOption
    {
        public override PathPayoff PathPayoff => payoff;

        public override ImmutableArray<Date> FixingDates => fixingDates;
    }

    // a payment of the larger asset less the strike at the last fixing
    private sealed class MaximumCall(double strike) : PathPayoff
    {
        public override string Name => "MaximumCall";

        public override string Description => "max(S1, S2) - K at the last fixing";

        public override int BasisSystemDimension => 1;

        public override void Value(
            Matrix path, ImmutableArray<YieldTermStructure> forwardTermStructures, Vector payments, Vector exercises, Vector[] states)
        {
            int last = path.Columns - 1;
            payments[last] = Math.Max(Math.Max(path[0, last], path[1, last]) - strike, 0.0);
        }
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
