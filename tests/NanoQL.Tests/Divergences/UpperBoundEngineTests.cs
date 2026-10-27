// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The upper-bound engine's exercise values (the S-mmp plan's MMPR4 (vii), option (1), ruled by the user, with
/// Revision 4's (a) and (b)). QuantLib's engine reads each exercise value at every exercise date from an
/// <c>ExerciseAdapter</c> done at its first, relying on its composite to step a finished part again
/// (<c>upperboundengine.cpp</c>'s warning); under the composites' ruled end that adapter stops, so nanoql's two
/// exercise-value parts pay the exercise value at every exercise date, done at their last step. On a hedge that ends
/// before the last exercise date (a TARN's knock-out) the decorated hedge stops with it, so nanoql treats no date after
/// the step at which the hedge ends as an exercise date, and a call-specified product stays ended after its underlying.
/// On six semi-annual rates, FlatVol 20%, the money-market-plus measure (this run's <c>probes\c3_facts.cpp</c>; the
/// planner's MPP21, MPP23): QuantLib's tested callable receiver swap and a Bermudan whose nothing-underlying ends at the
/// last step, QuantLib's values bit for bit; a callable TARN with a cash rebate growing from 1% to 6%, against the same
/// engine on the TARN made inert after its end, where QuantLib's engine is right: QuantLib's engine on the plain TARN
/// +0.0226 ± 0.0007 (34 standard errors), nanoql's within 4. Checks: QuantLib's engine on its tested configurations
/// and on the inert TARN, bit for bit; the paired difference.
/// </summary>
public class UpperBoundEngineTests
{
    private const double K = 0.032;
    private static readonly MarketModelProductMarket Market = new(6);
    private static readonly double[] ExerciseTimes = Market.RateTimes[..^1];

    [Fact]
    public void OnQuantLibsTestedCallableSwapTheBoundIsQuantLibs()
    {
        GeneralStatistics s = Bound(Receiver(), new NothingExerciseValue(Market.RateTimes), 255, 64);
        Assert.Equal(0.0023852046631641253, s.Mean());
        Assert.Equal(0.00014712870142777538, s.ErrorEstimate());
        Assert.Equal(0.0, s.Min());
    }

    [Fact]
    public void ABermudanOnANothingUnderlyingIsQuantLibs()
    {
        var swaptions = new BermudanSwaptionExerciseValue(
            Market.RateTimes, [.. Enumerable.Repeat<Payoff>(new PlainVanillaPayoff(OptionType.Call, K), 6)]);
        var nothingLast = new MultiStepNothing(swaptions.Evolution, 1, ExerciseTimes.Length);
        GeneralStatistics s = Bound(nothingLast, swaptions, 255, 64);
        Assert.Equal(0.0022270917548886638, s.Mean());
        Assert.Equal(0.00017033567480140245, s.ErrorEstimate());
    }

    [Fact]
    public void OnAHedgeOfSixProductsTheBoundIsQuantLibs()
    {
        // QuantLib sums the hedge's products (upperboundengine.cpp:133, :257-259), its decorated hedge's rebate one
        // product, fewer than the hedge's (:124-125): six forwards struck at K hedging the callable receiver swap, 255 x 64
        // (this run's probes\c3_multihedge.cpp), QuantLib's bits
        var forwards = new MultiStepForwards(Market.RateTimes, Market.Accruals, Market.PaymentTimes, [.. Enumerable.Repeat(K, 6)]);
        var stats = new GeneralStatistics();
        Engine(Receiver(), new NothingExerciseValue(Market.RateTimes), forwards).MultiplePathValues(stats, 255, 64);
        Assert.Equal(0.016509178233197634, stats.Mean());
        Assert.Equal(0.0011785094761342407, stats.ErrorEstimate());
    }

    [Fact]
    public void ACallableTarnPairsWithItsInertTwin()
    {
        double[] strikes = [.. Enumerable.Repeat(0.08, 6)], mult = [.. Enumerable.Repeat(1.0, 6)];
        var tarn = new MultiStepTarn(
            Market.RateTimes, Market.Accruals, Market.Accruals, Market.PaymentTimes, Market.PaymentTimes, 0.06, strikes, mult, new double[6]);
        var inert = new ZeroTailProduct(tarn);
        double[] growing = [.. Enumerable.Range(1, ExerciseTimes.Length).Select(i => 0.01 * i)];
        var cash = new ScheduleCash(ExerciseTimes, growing, false, strikes, mult, 0.06);
        var inertCash = new ScheduleCash(ExerciseTimes, growing, true, strikes, mult, 0.06);
        UpperBoundEngine inertEngine = Engine(inert, inertCash), plainEngine = Engine(tarn, cash);
        var reference = new GeneralStatistics();
        var paired = new GeneralStatistics();
        for (int i = 0; i < 1023; ++i)
        {
            double q = inertEngine.SinglePathValue(64).Value;
            reference.Add(q);
            paired.Add(plainEngine.SinglePathValue(64).Value - q);
        }

        // the inert TARN, where QuantLib's engine is right: QuantLib's value, bit for bit
        Assert.Equal(0.019381570690630349, reference.Mean());

        // the plain TARN, paired by outer path: QuantLib's engine 0.0226 ± 0.0009 off
        Assert.True(
            Math.Abs(paired.Mean()) < 4 * paired.ErrorEstimate() || paired.ErrorEstimate() == 0.0,
            $"{paired.Mean()} ± {paired.ErrorEstimate()}");
        const double quantLibPaired = 0.022649095780137303, quantLibPairedError = 0.00065767141128298854;
        Assert.True(quantLibPaired > 4 * quantLibPairedError);
    }

    [Fact]
    public void OnCoterminalAndCmsEvolversTheBoundIsQuantLibs()
    {
        // S-mm's MR3 (v): the evolvers set on the outer state, each on its own state type as QuantLib's
        var cs = new LMMCurveState(Market.RateTimes);
        cs.SetOnForwardRates(Market.Forwards);
        var evolution = new EvolutionDescription(Market.RateTimes, ExerciseTimes);
        int[] numeraires = [.. EvolutionDescriptionFunctions.TerminalMeasure(evolution)];
        MarketModel cot = FlatVolOn(evolution, cs.CoterminalSwapRates().ToArray());
        MarketModel cms = FlatVolOn(evolution, cs.CmSwapRates(2).ToArray());
        var nullValue = new NothingExerciseValue(Market.RateTimes);
        var trigger = new SwapRateTrigger(Market.RateTimes, [.. Enumerable.Repeat(K, 6)], ExerciseTimes);
        double nv = Market.Discounts[numeraires[0]];

        var cotEngine = new UpperBoundEngine(
            new LogNormalCotSwapRatePc(cot, new MTBrownianGeneratorFactory(42 + 142), numeraires),
            [.. Enumerable.Range(0, 6).Select(s => (MarketModelEvolver)new LogNormalCotSwapRatePc(cot, new MTBrownianGeneratorFactory(42 + s), numeraires, s))],
            Receiver(), nullValue, Receiver(), nullValue, trigger, nv);
        var cotStats = new GeneralStatistics();
        cotEngine.MultiplePathValues(cotStats, 127, 32);
        Assert.Equal(0.0024347340007441014, cotStats.Mean());

        var cmsEngine = new UpperBoundEngine(
            new LogNormalCmSwapRatePc(2, cms, new MTBrownianGeneratorFactory(42 + 142), numeraires),
            [.. Enumerable.Range(0, 6).Select(s => (MarketModelEvolver)new LogNormalCmSwapRatePc(2, cms, new MTBrownianGeneratorFactory(42 + s), numeraires, s))],
            Receiver(), nullValue, Receiver(), nullValue, trigger, nv);
        var cmsStats = new GeneralStatistics();
        cmsEngine.MultiplePathValues(cmsStats, 127, 32);
        Assert.Equal(0.0024763145120939235, cmsStats.Mean());
    }

    [Fact]
    public void OnANormalEvolverEveryPathsGapIsAtLeastZero()
    {
        // S-mm's MR3 (iv): the inner paths start from the outer state, where QuantLib's start from the model's forwards
        var evolution = new EvolutionDescription(Market.RateTimes, ExerciseTimes);
        int[] numeraires = [.. EvolutionDescriptionFunctions.MoneyMarketPlusMeasure(evolution, 1)];
        MarketModel normal = Market.FlatVol(evolution, 3, 0.006);
        var nullValue = new NothingExerciseValue(Market.RateTimes);
        var engine = new UpperBoundEngine(
            new NormalFwdRatePc(normal, new MTBrownianGeneratorFactory(42 + 142), numeraires),
            [.. Enumerable.Range(0, 6).Select(s => (MarketModelEvolver)new NormalFwdRatePc(normal, new MTBrownianGeneratorFactory(42 + s), numeraires, s))],
            Receiver(), nullValue, Receiver(), nullValue,
            new SwapRateTrigger(Market.RateTimes, [.. Enumerable.Repeat(K, 6)], ExerciseTimes),
            Market.Discounts[numeraires[0]]);
        var stats = new GeneralStatistics();
        engine.MultiplePathValues(stats, 127, 32);
        Assert.Equal(0.0, stats.Min());
        Assert.True(stats.Mean() > 0.0 && double.IsFinite(stats.Mean()));
    }

    private static MultiStepSwap Receiver() =>
        new(Market.RateTimes, Market.Accruals, Market.Accruals, Market.PaymentTimes, K, false);

    private static MarketModel FlatVolOn(EvolutionDescription evolution, double[] rates)
    {
        Matrix corr = ExpCorrelationsFunctions.ExponentialCorrelations(evolution.RateTimes.AsSpan(), 0.5, 0.2);
        var c = new TimeHomogeneousForwardCorrelation(corr, evolution.RateTimes.AsSpan());
        return new FlatVol([.. Enumerable.Repeat(0.2, 6)], c, evolution, 3, rates, new double[6]);
    }

    // The probes' engine: the predictor-corrector evolver on FlatVol 20%, 3 factors, the money-market-plus measure,
    // the outer paths on MT(184), an inner evolver on MT(42 + s) from each exercise step s; the swap-rate trigger at K.
    private static UpperBoundEngine Engine(
        MarketModelMultiProduct product, MarketModelExerciseValue rebate, MarketModelMultiProduct? hedge = null)
    {
        var evolution = new EvolutionDescription(Market.RateTimes, ExerciseTimes);
        MarketModel model = Market.FlatVol(evolution, 3);
        int[] numeraires = [.. EvolutionDescriptionFunctions.MoneyMarketPlusMeasure(evolution, 1)];
        return new UpperBoundEngine(
            new LogNormalFwdRatePc(model, new MTBrownianGeneratorFactory(42 + 142), numeraires),
            [.. Enumerable.Range(0, 6).Select(s => (MarketModelEvolver)new LogNormalFwdRatePc(model, new MTBrownianGeneratorFactory(42 + s), numeraires, s))],
            product,
            rebate,
            hedge ?? product,
            rebate,
            new SwapRateTrigger(Market.RateTimes, [.. Enumerable.Repeat(K, 6)], ExerciseTimes),
            Market.Discounts[numeraires[0]]);
    }

    private static GeneralStatistics Bound(MarketModelMultiProduct product, MarketModelExerciseValue rebate, int outer, int inner)
    {
        var stats = new GeneralStatistics();
        Engine(product, rebate).MultiplePathValues(stats, outer, inner);
        return stats;
    }

    // MPP23's TARN made inert after its end: no cash flow once ended, done at the last step.
    private sealed class ZeroTailProduct : MarketModelMultiProduct
    {
        private readonly MarketModelMultiProduct product;
        private readonly int steps;
        private int k;
        private bool ended;

        public ZeroTailProduct(MarketModelMultiProduct product)
        {
            this.product = product.Clone();
            steps = product.Evolution.NumberOfSteps;
        }

        private ZeroTailProduct(ZeroTailProduct other)
        {
            product = other.product.Clone();
            steps = other.steps;
            k = other.k;
            ended = other.ended;
        }

        public override EvolutionDescription Evolution => product.Evolution;

        public override int NumberOfProducts => product.NumberOfProducts;

        public override int MaxNumberOfCashFlowsPerProductPerStep => product.MaxNumberOfCashFlowsPerProductPerStep;

        public override ImmutableArray<int> SuggestedNumeraires() => product.SuggestedNumeraires();

        public override ImmutableArray<double> PossibleCashFlowTimes() => product.PossibleCashFlowTimes();

        public override void Reset()
        {
            product.Reset();
            ended = false;
            k = 0;
        }

        public override bool NextTimeStep(
            CurveState currentState, Span<int> numberCashFlowsThisStep, MarketModelMultiProductCashFlow[][] cashFlowsGenerated)
        {
            if (ended)
            {
                numberCashFlowsThisStep.Clear();
            }
            else
            {
                ended = product.NextTimeStep(currentState, numberCashFlowsThisStep, cashFlowsGenerated);
            }

            ++k;
            return k == steps;
        }

        public override MarketModelMultiProduct Clone() => new ZeroTailProduct(this);
    }

    // MPP23's cash rebate at each exercise, paid then; inert, nothing once the TARN's coupons reach its total.
    private sealed class ScheduleCash : MarketModelExerciseValue
    {
        private readonly double[] exerciseTimes;
        private readonly double[] cash;
        private readonly bool inert;
        private readonly double[] strikes;
        private readonly double[] mult;
        private readonly double total;
        private readonly EvolutionDescription evolution;
        private int currentIndex;
        private double paid;
        private bool ended;
        private MarketModelMultiProductCashFlow cf;

        public ScheduleCash(double[] exerciseTimes, double[] cash, bool inert, double[] strikes, double[] mult, double total)
        {
            this.exerciseTimes = exerciseTimes;
            this.cash = cash;
            this.inert = inert;
            this.strikes = strikes;
            this.mult = mult;
            this.total = total;
            evolution = new EvolutionDescription(Market.RateTimes, exerciseTimes);
        }

        public override int NumberOfExercises => exerciseTimes.Length;

        public override EvolutionDescription Evolution => evolution;

        public override ImmutableArray<double> PossibleCashFlowTimes() => [.. exerciseTimes];

        public override void NextStep(CurveState currentState)
        {
            cf.TimeIndex = currentIndex;
            cf.Amount = (inert && ended) ? 0.0 : cash[currentIndex];
            double l = currentState.ForwardRate(currentIndex);
            paid += Math.Max(strikes[currentIndex] - (mult[currentIndex] * l), 0.0) * Market.Accruals[currentIndex];
            if (paid >= total)
            {
                ended = true;
            }

            ++currentIndex;
        }

        public override void Reset()
        {
            currentIndex = 0;
            paid = 0.0;
            ended = false;
        }

        public override ImmutableArray<bool> IsExerciseTime() => [.. Enumerable.Repeat(true, exerciseTimes.Length)];

        public override MarketModelMultiProductCashFlow Value(CurveState currentState) => cf;

        public override MarketModelExerciseValue Clone() => (ScheduleCash)MemberwiseClone();
    }
}
