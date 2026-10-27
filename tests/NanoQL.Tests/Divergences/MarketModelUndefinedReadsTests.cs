// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The market-model framework's inputs whose QuantLib path is undefined, each refused before the undefined step (the
/// S-mm plan's MR3 (xx), and those this run's C1 to C4 found, under the playbook's row for undefined behaviour).
/// The refusals' types, texts and parameters are asserted by <c>Conventions/MarketModelRefusalTests.cs</c>.
/// </summary>
public class MarketModelUndefinedReadsTests
{
    private static readonly double[] Times = [0.5, 1.0, 1.5, 2.0];

    [Fact]
    public void ACurveStatesRateReadAtTheNumberOfRatesIsRefused()
    {
        // lmmcurvestate.cpp:107, :116, :140, :156, :168 and the coterminal and CMS states' checks admit i == n, then
        // read forwardRates_[n], cotAnnuities_[n], cmSwapRates_[n] past their vectors: forwardRate(6) of six rates
        // returns 3.3e-307 (the planner's MP5).
        foreach (CurveState cs in States())
        {
            int n = cs.NumberOfRates;
            Assert.Throws<ArgumentOutOfRangeException>(() => cs.ForwardRate(n));
            Assert.Throws<ArgumentOutOfRangeException>(() => cs.CoterminalSwapRate(n));
            Assert.Throws<ArgumentOutOfRangeException>(() => cs.CoterminalSwapAnnuity(n, n));
            Assert.Throws<ArgumentOutOfRangeException>(() => cs.CmSwapRate(n, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => cs.CmSwapAnnuity(n, n, 1));
            Assert.Equal(1.0, cs.DiscountRatio(n, n));
        }
    }

    [Fact]
    public void TheNumerairesFunctionsReadsPastTheirInputsAreRefused()
    {
        var evolution = new EvolutionDescription(Times);

        // evolutiondescription.cpp:122 reads rateTimes[numeraires[i]], past the rate times for a numeraire past them
        // (numeraire 9 of 7 rate times reads 1.9e-307, the planner's MP5).
        Assert.Throws<ArgumentOutOfRangeException>(() => EvolutionDescriptionFunctions.CheckCompatibility(evolution, [9, 3, 3]));

        // :132 dereferences std::min_element's end on no numeraires.
        Assert.Throws<ArgumentException>(() => EvolutionDescriptionFunctions.IsInTerminalMeasure(evolution, []));

        // :150 reads a numeraire per step, past fewer.
        Assert.Throws<ArgumentException>(() => EvolutionDescriptionFunctions.IsInMoneyMarketMeasure(evolution, [0, 1]));
        Assert.Throws<ArgumentException>(() => EvolutionDescriptionFunctions.IsInMoneyMarketPlusMeasure(evolution, [1, 2], 1));
    }

    [Fact]
    public void FlatVolsReadsPastItsCorrelationAreRefused()
    {
        var evolution = new EvolutionDescription(Times);
        double[] vols = [0.2, 0.2, 0.2], rates = [0.03, 0.03, 0.03], d = [0.0, 0.0, 0.0];

        // flatvol.cpp:96 reads corrTimes past their end where they end before the last evolution time: an abort,
        // 0xC0000409, 3 runs of 3 (the planner's MP5).
        var shortTimes = new TimeHomogeneousForwardCorrelation(new Matrix(3, 3, 0.5), [0.25, 0.5, 0.75, 1.0]);
        Assert.Throws<ArgumentException>(() => new FlatVol(vols, shortTimes, evolution, 3, rates, d));

        // :105 reads corrMatrix[i][j] for every rate of the model, past a correlation of fewer rates.
        var twoRates = new ExponentialForwardCorrelation([0.5, 1.0, 1.5], 0.5, 0.2, 0.5, [0.5, 1.0, 1.5]);
        Assert.Throws<ArgumentException>(() => new FlatVol(vols, twoRates, evolution, 3, rates, d));
    }

    [Fact]
    public void TheConstantMaturityDriftsOnNoSpanningForwardsAreRefused()
    {
        // cmsmmdriftcalculator.cpp:93-103: at no spanning forwards each swap's cross variation reads its own entry of
        // PjPnWk_, a Matrix the constructor leaves uninitialized (:38), before writing it.
        Assert.Throws<ArgumentOutOfRangeException>(() => new CMSMMDriftCalculator(new Matrix(3, 3, 0.1), new double[3], [0.5, 0.5, 0.5], 3, 0, 0));
    }

    [Fact]
    public void MergeTimesSortAndSearchOfInvalidSetsAreRefused()
    {
        // utilities.cpp:39 sorts the times, undefined for a NaN; :49 binary-searches each set, undefined for one out of
        // order.
        Assert.Throws<ArgumentException>(() => UtilitiesFunctions.MergeTimes([ImmutableArray.Create(1.0, double.NaN)]));
        Assert.Throws<ArgumentException>(() => UtilitiesFunctions.MergeTimes([ImmutableArray.Create(2.0, 1.0)]));
    }

    [Fact]
    public void TheEvolvedMatricesReadPastANarrowMatrixIsRefused()
    {
        // timehomogeneousforwardcorrelation.cpp:64 reads fwdCorrelation[i-k][j-k] for every j < i of its rows.
        Assert.Throws<ArgumentException>(() => TimeHomogeneousForwardCorrelation.EvolvedMatrices(new Matrix(4, 2, 0.5)));
        Assert.Equal(4, TimeHomogeneousForwardCorrelation.EvolvedMatrices(new Matrix(4, 3, 0.5)).Length);
    }

    [Fact]
    public void TheConversionsWritesPastTheirBuffersAreRefused()
    {
        // curvestate.cpp:73 writes the last coterminal annuity, before the vectors at no swap; :103 writes the first
        // valid constant-maturity annuity, past the vectors at an index at or past the swaps.
        Assert.Throws<ArgumentException>(() => CurveStateFunctions.CoterminalFromDiscountRatios(0, [1.0], [], [], []));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CurveStateFunctions.ConstantMaturityFromDiscountRatios(1, 2, [1.0, 0.99, 0.98], [0.5, 0.5], new double[2], new double[2]));
    }

    [Fact]
    public void TheIndexesQuantLibsUnsignedArithmeticWrapsAreRefused()
    {
        var cs = new LMMCurveState(Times);
        cs.SetOnForwardRates([0.03, 0.03, 0.03]);

        // forwardforwardmappings.cpp:36: (n − offset)/multiplier wraps past an offset past the rates, and the matrix it
        // sizes throws std::bad_alloc.
        Assert.Throws<ArgumentOutOfRangeException>(() => ForwardForwardMappings.ForwardForwardJacobian(cs, 5, 4));

        // swapforwardmappings.cpp:40 reads rateTaus()[i] past the vector at an end past the rates.
        Assert.Throws<ArgumentOutOfRangeException>(() => SwapForwardMappings.Annuity(cs, 0, 4, 3));

        // lognormalfwdrateeuler.cpp:51, :85: the generator for steps − initialStep steps, and the initial step's drift
        // calculator, read past the calculators at an initial step at or past the steps.
        MarketModel m = new FlatVol(
            [0.2, 0.2, 0.2],
            new ExponentialForwardCorrelation(Times),
            new EvolutionDescription(Times),
            3,
            [0.03, 0.03, 0.03],
            [0.0, 0.0, 0.0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => new LogNormalFwdRateEuler(m, new MTBrownianGeneratorFactory(42), [3, 3, 3], 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LogNormalFwdRatePc(m, new MTBrownianGeneratorFactory(42), [3, 3, 3], 3));
    }

    [Fact]
    public void TheEvolversAndTheVolatilityProcessesReadsPastTheirStateAreRefused()
    {
        // squarerootandersen.cpp:51-52 reads evolutionTimes[0] of no evolution times; a first time of 0 gives a
        // sub-step of no length, its variance 0 and its ψ 0, NaN.
        Assert.Throws<ArgumentException>(() => new SquareRootAndersen(0.04, 1.0, 0.5, 0.04, [], 2, 0.5, 0.5));
        Assert.Throws<ArgumentException>(() => new SquareRootAndersen(0.04, 1.0, 0.5, 0.04, [0.0, 1.0], 2, 0.5, 0.5));

        // :126-133: nextstep reads v_, set by nextPath only, writes vPath_ past its sub-steps after the path's last
        // step, and reads variates[j] past fewer variates than sub-steps.
        var process = new SquareRootAndersen(0.04, 1.0, 0.5, 0.04, [0.5, 1.0], 2, 0.5, 0.5);
        Assert.Throws<NanoQLException>(() => process.Nextstep([0.1, 0.2]));
        process.NextPath();
        Assert.Throws<ArgumentException>(() => process.Nextstep([0.1]));
        process.Nextstep([0.1, 0.2]);
        process.Nextstep([0.1, 0.2]);
        Assert.Throws<NanoQLException>(() => process.Nextstep([0.1, 0.2]));

        // svddfwdratepc.cpp:89 divides the variates' spread by the process's variates per step, 0 for a process drawing
        // none.
        MarketModel m = new FlatVol(
            [0.2, 0.2, 0.2],
            new ExponentialForwardCorrelation(Times),
            new EvolutionDescription(Times),
            3,
            [0.03, 0.03, 0.03],
            [0.0, 0.0, 0.0]);
        Assert.Throws<ArgumentException>(() => new SVDDFwdRatePc(m, new MTBrownianGeneratorFactory(42), new NoVariates(), 0, 1, [3, 3, 3]));

        // lognormalfwdrateeulerconstrained.cpp:183, :195: advanceStep indexes isConstraintActive_, empty before
        // setThisConstraint, and an active step's covariances_, empty before setConstraintType; :122 reads the
        // pseudo-root row of a constrained rate past the rates.
        var e = new LogNormalFwdRateEulerConstrained(m, m, new MTBrownianGeneratorFactory(42), [3, 3, 3]);
        e.StartNewPath();
        Assert.Throws<NanoQLException>(() => e.AdvanceStep());
        e.SetThisConstraint([0.03, 0.03, 0.03], [true, true, true]);
        Assert.Throws<NanoQLException>(() => e.AdvanceStep());
        Assert.Throws<ArgumentOutOfRangeException>(() => e.SetConstraintType([0, 1, 3], [1, 2, 4]));
    }

    [Fact]
    public void TheModelsVariancesAndAdaptersReadsPastTheirInputsAreRefused()
    {
        // abcdvol.cpp:82, as FlatVol: the steps' loop reads corrTimes[kk] past correlation times ending before the
        // last evolution time.
        var short2 = new TimeHomogeneousForwardCorrelation(new Matrix(3, 3, 0.5), [0.25, 0.5, 0.75, 1.0]);
        var evolution = new EvolutionDescription(Times);
        Assert.Throws<ArgumentException>(() => new AbcdVol(-0.06, 0.17, 0.54, 0.17, [1.0, 1.0, 1.0], short2, evolution, 3, [0.03, 0.03, 0.03], [0.0, 0.0, 0.0]));

        // piecewiseconstantabcdvariance.cpp:31-32 sizes its vectors rateTimes.size() − 1, wrapping at no rate times
        // (std::length_error) before its checks; piecewiseconstantvariance.cpp:48 reads rateTimes()[i] unchecked, past
        // an extension's rate times.
        Assert.Throws<ArgumentException>(() => new PiecewiseConstantAbcdVariance(-0.06, 0.17, 0.54, 0.17, 0, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShortRateTimes().TotalVolatility(1));

        // volatilityinterpolationspecifierabcd.cpp:56 divides by the period; :63 reads the last long rate's variance,
        // at index −1 with none; :46, :54 size the short rates from their times less one, wrapping at none; :60 reads
        // the short times past their end at a long rate with more times.
        double[] small = [0.5, 1.0, 1.5, 2.0, 2.5];
        PiecewiseConstantAbcdVariance[] longs = [new(-0.06, 0.17, 0.54, 0.17, 0, [0.5, 1.5, 2.5]), new(-0.06, 0.17, 0.54, 0.17, 1, [0.5, 1.5, 2.5])];
        Assert.Throws<ArgumentOutOfRangeException>(() => new VolatilityInterpolationSpecifierabcd(0, 0, longs, small));
        Assert.Throws<ArgumentException>(() => new VolatilityInterpolationSpecifierabcd(2, 0, [], small));
        Assert.Throws<ArgumentException>(() => new VolatilityInterpolationSpecifierabcd(2, 0, longs, []));
        PiecewiseConstantAbcdVariance[] tooLong = [new(-0.06, 0.17, 0.54, 0.17, 0, [0.5, 1.5, 2.5, 3.5]), new(-0.06, 0.17, 0.54, 0.17, 1, [0.5, 1.5, 2.5, 3.5])];
        Assert.Throws<ArgumentException>(() => new VolatilityInterpolationSpecifierabcd(2, 0, tooLong, small));

        // fwdperiodadapter.cpp:37 sizes the long rates (n − offset)/period, wrapping past an offset past the rates;
        // :80-86 leaves the steps uncomputed where no evolution time precedes the last long reset, its evolution then
        // the rate times'.
        MarketModel m = new FlatVol(
            [0.2, 0.2, 0.2],
            new ExponentialForwardCorrelation(Times),
            new EvolutionDescription(Times),
            3,
            [0.03, 0.03, 0.03],
            [0.0, 0.0, 0.0]);
        Assert.Throws<ArgumentException>(() => new FwdPeriodAdapter(m, 5, 4, []));
        double[] four = [0.5, 1.0, 1.5, 2.0, 2.5];
        MarketModel late = new FlatVol(
            [0.2, 0.2, 0.2, 0.2],
            new ExponentialForwardCorrelation(four, 0.5, 0.2, 1.0),
            new EvolutionDescription(four, [1.2, 2.0]),
            4,
            [0.03, 0.03, 0.03, 0.03],
            [0.0, 0.0, 0.0, 0.0]);
        Assert.Throws<ArgumentException>(() => new FwdPeriodAdapter(late, 3, 0, []));

        // fwdtocotswapadapter.hpp:98, cotswaptofwdadapter.hpp:97, fwdperiodadapter.hpp:88 read pseudoRoots_[i]
        // unchecked.
        Assert.Throws<ArgumentOutOfRangeException>(() => new FwdToCotSwapAdapter(m).PseudoRoot(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CotSwapToFwdAdapter(m).PseudoRoot(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FwdPeriodAdapter(m, 2, 0, []).PseudoRoot(2));

        // cotswapfromfwdcorrelation.cpp:55 reads corrTimes[k] for each matrix, past a correlation's fewer times.
        var cs = new LMMCurveState(Times);
        cs.SetOnForwardRates([0.03, 0.03, 0.03]);
        Assert.Throws<ArgumentException>(() => new CotSwapFromFwdCorrelation(new FewerTimes(), cs, 0.0));
    }

    [Fact]
    public void TheCalibrationsReadsPastTheirInputsAreRefused()
    {
        double[] times = [.. Enumerable.Range(1, 11).Select(i => 0.5 * i)];
        var cs = new LMMCurveState(times);
        cs.SetOnForwardRates([.. Enumerable.Range(0, 10).Select(i => 0.03 + (0.0025 * i))]);
        var corr = new CotSwapFromFwdCorrelation(new ExponentialForwardCorrelation(times, 0.5, 0.2), cs, 0.0);
        PiecewiseConstantVariance[] variances = [.. Enumerable.Range(0, 10).Select(i => new PiecewiseConstantAbcdVariance(0.0, 0.17, 1.0, 0.1, i, times))];
        double[] caplets = [0.1640, 0.1740, 0.1840, 0.1940, 0.1840, 0.1740, 0.1640, 0.1540, 0.1440, variances[9].TotalVolatility(9)];
        var evolution = new EvolutionDescription(times);
        double[] ten = new double[10];
        double[] nine = new double[9];
        PiecewiseConstantVariance[] shortSecond = [variances[0], new OneVariance(times), .. variances[2..]];

        // pseudorootfacade.cpp:29-30, :40-41 read the first of no pseudo-roots.
        Assert.Throws<ArgumentException>(() => new PseudoRootFacade(Array.Empty<Matrix>(), times, ten, ten));

        // capletcoterminalalphacalibration.cpp:142, capletcoterminalmaxhomogeneity.cpp:334 and
        // capletcoterminalswaptioncalibration.cpp:112, :155 read each rate's variances to its own step, past a variance
        // of fewer, after performChecks reads only the last's.
        Assert.Throws<ArgumentException>(() => CTSMMCapletCalibration.PerformChecks(evolution, corr, shortSecond, caplets, cs));

        // capletcoterminalswaptioncalibration.cpp:115 and capletcoterminalalphacalibration.cpp:167, :184 read the α
        // vectors to the last rate, which only the constructors check.
        Assert.Throws<ArgumentException>(() => CTSMMCapletOriginalCalibration.CalibrationFunction(
            evolution, corr, variances, caplets, cs, 0.0, nine, true, false, 3));
        Assert.Throws<ArgumentException>(() => CTSMMCapletAlphaFormCalibration.CapletAlphaFormCalibration(
            evolution, corr, variances, caplets, cs, 0.0, ten, nine, ten, false, new AlphaFormLinearHyperbolic(times), 3, 100, 1e-8));

        // capletcoterminalperiodic.cpp:74 divides by the period; :89 reads the last of the caplet volatilities before
        // the inner calibration checks them, the back of none undefined; :165 writes the first pass's row of a matrix
        // of maxPeriodIterations rows.
        double[] bigTimes = [.. Enumerable.Range(0, 6).Select(i => times[2 * i])];
        var specifier = new VolatilityInterpolationSpecifierabcd(
            2, 0, [.. Enumerable.Range(0, 5).Select(i => new PiecewiseConstantAbcdVariance(0.0, 0.17, 1.0, 0.1, i, bigTimes))], times);
        Assert.Throws<ArgumentOutOfRangeException>(() => CapletCoterminalPeriodicFunctions.CapletSwaptionPeriodicCalibration(
            evolution, corr, specifier, caplets, cs, 0.0, 1.0, 3, 0, 100, 1e-8, 10, 1e-5, 30, 1e-5));
        Assert.Throws<ArgumentException>(() => CapletCoterminalPeriodicFunctions.CapletSwaptionPeriodicCalibration(
            evolution, corr, specifier, [], cs, 0.0, 1.0, 3, 2, 100, 1e-8, 10, 1e-5, 30, 1e-5));
        Assert.Throws<ArgumentOutOfRangeException>(() => CapletCoterminalPeriodicFunctions.CapletSwaptionPeriodicCalibration(
            evolution, corr, specifier, caplets, cs, 0.0, 1.0, 3, 2, 100, 1e-8, 10, 1e-5, 0, 1e-5));

        // basisincompleteordered.cpp:216 and alphaformconcrete.cpp:30, :42 read their vectors unchecked;
        // alphafinder.cpp:188-189, :202 read the vectors given to the step index plus one or two.
        var projections = new OrthogonalProjections(new Matrix(2, 3, 1.0), 100.0, 1e-6);
        Assert.Throws<ArgumentOutOfRangeException>(() => projections.GetVector(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AlphaFormInverseLinear([0.5, 1.0]).Evaluate(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AlphaFormLinearHyperbolic([0.5, 1.0]).Evaluate(-1));
        Assert.Throws<ArgumentException>(() => new AlphaFinder(new AlphaFormLinearHyperbolic([0.5, 1.0, 1.5])).Solve(
            0.0, 1, [0.2], [1.0, 1.0, 1.0], [0.9, 0.9], 1.0, 1.0, 0.02, 1e-8, 1.0, -1.0, 100, out _, out _, out _, new double[3]));
        Assert.Throws<ArgumentException>(() => new AlphaFinder(new AlphaFormLinearHyperbolic([0.5, 1.0, 1.5])).Solve(
            0.0, 1, [0.2, 0.2], [1.0, 1.0, 1.0], [0.9, 0.9], 1.0, 1.0, 0.02, 1e-8, 1.0, -1.0, 100, out _, out _, out _, new double[2]));
    }

    private static CurveState[] States()
    {
        var lmm = new LMMCurveState(Times);
        lmm.SetOnForwardRates([0.03, 0.035, 0.04]);
        var cot = new CoterminalSwapCurveState(Times);
        cot.SetOnCoterminalSwapRates([0.035, 0.037, 0.04]);
        var cms = new CMSwapCurveState(Times, 2);
        cms.SetOnCMSwapRates([0.032, 0.037, 0.04]);
        return [lmm, cot, cms];
    }

    // A volatility process drawing no variates.
    private sealed class NoVariates : MarketModelVolProcess
    {
        public override int VariatesPerStep => 0;

        public override int NumberSteps => 3;

        public override int NumberStateVariables => 0;

        public override void NextPath()
        {
        }

        public override double Nextstep(ReadOnlySpan<double> variates) => 1.0;

        public override double StepSd() => 1.0;

        public override ReadOnlySpan<double> StateVariables() => [];

        public override MarketModelVolProcess Clone() => new NoVariates();
    }

    // A variance with fewer rate times than steps.
    private sealed class ShortRateTimes : PiecewiseConstantVariance
    {
        public override ImmutableArray<double> Variances => [0.01, 0.01];

        public override ImmutableArray<double> Volatilities => [0.1, 0.1];

        public override ImmutableArray<double> RateTimes => [1.0];
    }

    // A variance of one step, on the rates' times.
    private sealed class OneVariance(double[] times) : PiecewiseConstantVariance
    {
        public override ImmutableArray<double> Variances => [0.01];

        public override ImmutableArray<double> Volatilities => [0.2];

        public override ImmutableArray<double> RateTimes => [.. times];
    }

    // A correlation with fewer times than matrices.
    private sealed class FewerTimes : PiecewiseConstantCorrelation
    {
        public override ImmutableArray<double> Times => [0.5];

        public override ImmutableArray<double> RateTimes => [.. MarketModelUndefinedReadsTests.Times];

        public override int NumberOfRates => 3;

        public override ImmutableArray<Matrix> Correlations() => [new Matrix(3, 3, 0.5), new Matrix(3, 3, 0.5)];
    }
}
