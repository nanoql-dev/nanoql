// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The market-model framework's refusals: each one's exception type, text and parameter, QuantLib's texts where
/// QuantLib refuses (the vectors record only that it throws), nanoql's where it refuses QuantLib's undefined paths
/// (<c>Divergences/MarketModelUndefinedReadsTests.cs</c>) and the inputs the user ruled refused (the S-mm plan's MR4).
/// </summary>
public class MarketModelRefusalTests
{
    private static readonly double[] Times = [0.5, 1.0, 1.5, 2.0];

    [Fact]
    public void TheCurveStatesRefusals()
    {
        var cs = new LMMCurveState(Times);
        Assert.Equal("curve state not initialized yet", Assert.Throws<NanoQLException>(() => cs.ForwardRate(0)).Message);
        Check<ArgumentException>(() => cs.SetOnForwardRates([0.03, 0.03]), "fwdRates", "rates mismatch: 3 required, 2 provided");
        Check<ArgumentOutOfRangeException>(() => cs.SetOnForwardRates([0.03, 0.03, 0.03], 3), "firstValidIndex", "first valid index must be less than 3: 3 not allowed");
        Check<ArgumentException>(() => cs.SetOnDiscountRatios([1.0, 0.99]), "discRatios", "too many discount ratios: 4 required, 2 provided");
        cs.SetOnForwardRates([0.03, 0.03, 0.03], 1);
        Check<ArgumentOutOfRangeException>(() => cs.ForwardRate(0), "i", "invalid index");
        Check<ArgumentOutOfRangeException>(() => cs.ForwardRate(3), "i", "invalid index");
        Check<ArgumentOutOfRangeException>(() => cs.DiscountRatio(1, 4), "j", "invalid index");
        Check<ArgumentOutOfRangeException>(() => cs.CoterminalSwapAnnuity(0, 1), "numeraire", "invalid numeraire");
        Check<ArgumentOutOfRangeException>(() => cs.CmSwapRates(-1), "spanningForwards", "");
        Check<ArgumentException>(() => cs.SwapRate(2, 2), "end", "empty range specified");
        Check<ArgumentException>(() => cs.SwapRate(1, 4), "end", "taus/end mismatch");

        var cot = new CoterminalSwapCurveState(Times);
        Check<ArgumentException>(() => cot.SetOnCoterminalSwapRates([0.03]), "swapRates", "rates mismatch: 3 required, 1 provided");
        var cms = new CMSwapCurveState(Times, 2);
        Check<ArgumentException>(() => cms.SetOnCMSwapRates([0.03]), "cmSwapRates", "rates mismatch: 3 required, 1 provided");
        Check<ArgumentOutOfRangeException>(() => new CMSwapCurveState(Times, -1), "spanningForwards", "");
        Check<ArgumentException>(() => new LMMCurveState([1.0]), "times", "at least two times are required, 1 provided");
    }

    [Fact]
    public void TheConversionsRefusals()
    {
        Check<ArgumentException>(() => CurveStateFunctions.ForwardsFromDiscountRatios(0, [1.0, 0.99], [0.5, 0.5], new double[1]), "taus", "taus.size()!=fwds.size()");
        Check<ArgumentException>(() => CurveStateFunctions.CoterminalFromDiscountRatios(0, [1.0], [], [], []), "cotSwapRates", "at least one coterminal swap rate is required");
        Check<ArgumentOutOfRangeException>(
            () => CurveStateFunctions.ConstantMaturityFromDiscountRatios(1, 2, [1.0, 0.99, 0.98], [0.5, 0.5], new double[2], new double[2]),
            "firstValidIndex",
            "first valid index (2) must be less than the number of swap rates (2)");
    }

    [Fact]
    public void TheEvolutionsRefusals()
    {
        Check<ArgumentException>(() => new EvolutionDescription(Times, [], [(0, 3)]), "relevanceRates", "relevanceRates / evolutionTimes mismatch");
        Check<ArgumentException>(() => new EvolutionDescription(Times, [1.0, 0.5]), "times", "non increasing rate times: times[0]=1, times[1]=0.5");
        var e = new EvolutionDescription(Times);
        Check<ArgumentException>(() => EvolutionDescriptionFunctions.CheckCompatibility(e, [3, 3]), "numeraires", "Size mismatch between numeraires (2) and evolution times (3)");
        Check<ArgumentException>(
            () => EvolutionDescriptionFunctions.CheckCompatibility(e, [3, 0, 3]),
            "numeraires",
            "2nd step, evolution time 1: the numeraire (0), corresponding to rate time 0.5, is expired");
        Check<ArgumentOutOfRangeException>(
            () => EvolutionDescriptionFunctions.CheckCompatibility(e, [9, 3, 3]),
            "numeraires",
            "1st step: the numeraire (9) is not the index of a rate time (0 to 3)");
        Check<ArgumentOutOfRangeException>(() => EvolutionDescriptionFunctions.CheckCompatibility(e, [3, 3, -1]), "numeraires", "negative numeraire (-1)");
        Check<ArgumentException>(() => EvolutionDescriptionFunctions.IsInTerminalMeasure(e, []), "numeraires", "no numeraires given");
        Check<ArgumentException>(() => EvolutionDescriptionFunctions.IsInMoneyMarketMeasure(e, [1]), "numeraires", "fewer numeraires (1) than evolution times (3)");
        Check<ArgumentException>(() => EvolutionDescriptionFunctions.MoneyMarketPlusMeasure(e, 4), "offset", "offset (4) is greater than the max allowed value for numeraire (3)");
        Check<ArgumentOutOfRangeException>(() => EvolutionDescriptionFunctions.MoneyMarketPlusMeasure(e, -1), "offset", "");
        Check<ArgumentException>(() => UtilitiesFunctions.MergeTimes([ImmutableArray.Create(1.0, double.NaN)]), "times", "1st set of times holds a NaN");
        Check<ArgumentException>(() => UtilitiesFunctions.MergeTimes([ImmutableArray.Create(1.0), ImmutableArray.Create(2.0, 1.0)]), "times", "2nd set of times isn't sorted");
        Check<ArgumentException>(() => UtilitiesFunctions.IsInSubset([1.0], [1.0, 2.0]), "subset", "set is required to be larger or equal than subset");
        Check<ArgumentException>(() => UtilitiesFunctions.CheckIncreasingTimes([]), "times", "at least one time is required");
        Check<ArgumentException>(() => UtilitiesFunctions.CheckIncreasingTimes([0.0]), "times", "first time (0) must be greater than zero");
    }

    [Fact]
    public void TheDriftCalculatorsRefusals()
    {
        var pseudo = new Matrix(3, 2, 0.1);
        double[] z = new double[3], taus = [0.5, 0.5, 0.5];
        Check<ArgumentException>(() => new LMMDriftCalculator(pseudo, z, [], 3, 0), "taus", "Dim out of range");
        Check<ArgumentException>(() => new LMMDriftCalculator(pseudo, [0.0], taus, 3, 0), "displacements", "Displacements out of range");
        Check<ArgumentException>(() => new LMMDriftCalculator(new Matrix(2, 2, 0.1), z, taus, 3, 0), "pseudo", "pseudo.rows() not consistent with dim");
        Check<ArgumentException>(() => new LMMDriftCalculator(new Matrix(3, 4, 0.1), z, taus, 3, 0), "pseudo", "pseudo.rows() not consistent with pseudo.columns()");
        Check<ArgumentException>(() => new LMMDriftCalculator(pseudo, z, taus, 3, 3), "alive", "Alive out of bounds");
        Check<ArgumentException>(() => new LMMDriftCalculator(pseudo, z, taus, 4, 0), "numeraire", "Numeraire larger than dim");
        Check<ArgumentException>(() => new LMMDriftCalculator(pseudo, z, taus, 1, 2), "numeraire", "Numeraire smaller than alive");
        Check<ArgumentOutOfRangeException>(() => new LMMDriftCalculator(pseudo, z, taus, -1, 0), "numeraire", "");
        var lmm = new LMMDriftCalculator(pseudo, z, taus, 3, 0);
        Check<ArgumentException>(() => lmm.Compute([0.03, 0.03], new double[3]), "fwds", "numberOfRates <> dim");
        Check<ArgumentException>(() => lmm.ComputeReduced([0.03, 0.03, 0.03], new double[2]), "drifts", "drifts.size() <> dim");
        var normal = new LMMNormalDriftCalculator(pseudo, taus, 3, 0);
        Check<ArgumentException>(() => normal.ComputePlain([0.03, 0.03], new double[3]), "fwds", "numberOfRates <> dim");
        var smm = new SMMDriftCalculator(pseudo, z, taus, 3, 0);
        var cot = new CoterminalSwapCurveState(Times);
        cot.SetOnCoterminalSwapRates([0.03, 0.03, 0.03]);
        Check<ArgumentException>(() => smm.Compute(cot, new double[2]), "drifts", "drifts.size() <> numberOfRates");
        Check<ArgumentOutOfRangeException>(() => new CMSMMDriftCalculator(pseudo, z, taus, 3, 0, 0), "spanningFwds", "at least one spanning forward is required");
    }

    [Fact]
    public void TheModelsAndCorrelationsRefusals()
    {
        var e = new EvolutionDescription(Times);
        var corr = new ExponentialForwardCorrelation(Times);
        double[] v = [0.2, 0.2, 0.2], r = [0.03, 0.03, 0.03], d = [0.0, 0.0, 0.0];
        Check<ArgumentException>(() => new FlatVol(v, corr, e, 3, [0.03], d), "initialRates", "mismatch between number of rates (1) and rate times");
        Check<ArgumentException>(() => new FlatVol(v, corr, e, 0, r, d), "numberOfFactors", "number of rates (3) greater than number of factors (0) times number of steps (3)");
        Check<ArgumentException>(() => new FlatVol(v, corr, e, 4, r, d), "numberOfFactors", "number of factors (4) cannot be greater than numberOfRates (3)");
        Check<ArgumentException>(
            () => new FlatVol(v, new TimeHomogeneousForwardCorrelation(new Matrix(3, 3, 0.5), [0.25, 0.5, 0.75, 1.0]), e, 3, r, d),
            "corr",
            "the correlation times end (0.75) before the last evolution time (1.5)");
        Check<ArgumentException>(
            () => new FlatVol(v, new ExponentialForwardCorrelation([0.5, 1.0, 1.5], 0.5, 0.2, 0.5, [0.5, 1.0, 1.5]), e, 3, r, d),
            "corr",
            "the 1st correlation matrix (2x2) is smaller than the rates (3)");
        var m = new FlatVol(v, corr, e, 3, r, d);
        Check<ArgumentOutOfRangeException>(() => m.PseudoRoot(3), "i", "the index 3 is invalid: it must be less than number of steps (3)");
        Check<ArgumentOutOfRangeException>(() => m.Covariance(3), "i", "i (3) must be less than covariance_.size() (3)");
        Check<ArgumentOutOfRangeException>(() => m.TotalCovariance(-1), "endIndex", "endIndex (-1) must be less than covariance_.size() (3)");
        Check<ArgumentOutOfRangeException>(() => m.TimeDependentVolatility(3), "i", "index (3) must less than number of rates (3)");
        Check<ArgumentException>(() => FlatVolFunctions.FlatVolCovariance(1.0, 0.5, 2.0, 2.0, 0.2, 0.2), "t2", "integrations bounds (1,0.5) are in reverse order");

        Check<ArgumentOutOfRangeException>(() => corr.Correlation(3), "i", "index (3) must be less than correlations vector size (3)");
        Check<ArgumentException>(() => new ExponentialForwardCorrelation([1.0]), "rateTimes", "Rate times must contain at least two values");
        Check<ArgumentException>(
            () => new ExponentialForwardCorrelation(Times, 0.5, 0.2, 1.0, [0.5, 1.0]),
            "times",
            "corr times ( 0.5 1 ) must be equal to (all) rate times (but the last) ( 0.5 1 1.5 )");
        Check<ArgumentException>(() => new ExponentialForwardCorrelation(Times, 0.5, 0.2, 0.5, [0.5, 1.5]), "times", "corr times ( 0.5 1.5 ) skip the 2nd rate time (1)");
        Check<ArgumentException>(() => ExpCorrelationsFunctions.ExponentialCorrelations(Times, 1.5), "longTermCorr", "Long term correlation (1.5) outside [0;1] interval");
        Check<ArgumentException>(() => ExpCorrelationsFunctions.ExponentialCorrelations(Times, 0.5, -0.1), "beta", "beta (-0.1) must be greater than zero");
        Check<ArgumentException>(() => ExpCorrelationsFunctions.ExponentialCorrelations(Times, 0.5, 0.2, 1.5), "gamma", "gamma (1.5) outside [0;1] interval");
        Check<ArgumentException>(
            () => new TimeHomogeneousForwardCorrelation(new Matrix(2, 3, 0.5), Times),
            "fwdCorrelation",
            "mismatch between number of rates (3) and fwdCorrelation rows (2)");
        Check<ArgumentException>(
            () => TimeHomogeneousForwardCorrelation.EvolvedMatrices(new Matrix(4, 2, 0.5)),
            "fwdCorrelation",
            "fwdCorrelation's columns (2) fewer than its rows (4) less one");
    }

    [Fact]
    public void TheMappingsAndEvolversRefusals()
    {
        var cs = new LMMCurveState(Times);
        cs.SetOnForwardRates([0.03, 0.03, 0.03]);
        Check<ArgumentException>(() => ForwardForwardMappings.RestrictCurveState(cs, 2, 2), "offset", "offset  must be less than period in  forward forward mappings");
        Check<ArgumentOutOfRangeException>(() => ForwardForwardMappings.ForwardForwardJacobian(cs, 5, 4), "offset", "offset (4) must be at most the number of rates (3)");
        Check<ArgumentException>(() => ForwardForwardMappings.YMatrix(cs, [0.0], [0.0], 1, 0), "shortDisplacements", "shortDisplacements must be of size equal to number of rates");
        Check<ArgumentOutOfRangeException>(() => SwapForwardMappings.Annuity(cs, 0, 4, 3), "endIndex", "end index (4) must be at most the number of rates (3)");
        Check<ArgumentOutOfRangeException>(() => SwapForwardMappings.CmSwapForwardJacobian(cs, -1), "spanningForwards", "");

        var m = new FlatVol([0.2, 0.2, 0.2], new ExponentialForwardCorrelation(Times), new EvolutionDescription(Times, [1.0, 1.5]), 3, [0.03, 0.03, 0.03], [0.0, 0.01, 0.0]);
        Check<ArgumentException>(() => SwapForwardMappings.SwaptionImpliedVolatility(m, 1, 1), "startIndex", "start index must be before end index in swaptionImpliedVolatility");
        Check<ArgumentOutOfRangeException>(() => SwapForwardMappings.SwaptionImpliedVolatility(m, 0, 4), "endIndex", "end index (4) must be at most the number of rates (3)");
        Check<ArgumentException>(() => SwapForwardMappings.SwaptionImpliedVolatility(m, 1, 2), "volStructure", "2nd displacement (0.01) not equal to the previous ones (0)");
        var equal = new FlatVol([0.2, 0.2, 0.2], new ExponentialForwardCorrelation(Times), new EvolutionDescription(Times, [1.0, 1.5]), 3, [0.03, 0.03, 0.03], [0.0, 0.0, 0.0]);
        Check<ArgumentException>(() => SwapForwardMappings.SwaptionImpliedVolatility(equal, 0, 2), "volStructure", "skipping 1st rate time");

        var factory = new MTBrownianGeneratorFactory(42);
        Check<ArgumentOutOfRangeException>(() => new LogNormalFwdRateEuler(equal, factory, [3, 3], 2), "initialStep", "initial step (2) must be less than the number of steps (2)");
        Check<ArgumentException>(() => new LogNormalFwdRatePc(equal, factory, [3]), "numeraires", "Size mismatch between numeraires (1) and evolution times (2)");
        var euler = new LogNormalFwdRateEuler(equal, factory, [3, 3]);
        var other = new LMMCurveState([0.5, 1.0]);
        other.SetOnForwardRates([0.03]);
        Check<ArgumentException>(() => euler.SetInitialState(other), "forwards", "mismatch between forwards and rateTimes");
    }

    [Fact]
    public void TheStochasticVolatilityRefusals()
    {
        Check<ArgumentException>(() => new SquareRootAndersen(0.04, 1.0, 0.5, 0.04, [], 2, 0.5, 0.5), "evolutionTimes", "at least one evolution time is required");
        Check<ArgumentException>(() => new SquareRootAndersen(0.04, 1.0, 0.5, 0.04, [0.0, 1.0], 2, 0.5, 0.5), "evolutionTimes", "Steps must be of positive size.");
        Check<ArgumentException>(() => new SquareRootAndersen(0.04, 1.0, 0.5, 0.04, [0.5, 0.5], 2, 0.5, 0.5), "evolutionTimes", "Steps must be of positive size.");
        Check<ArgumentOutOfRangeException>(() => new SquareRootAndersen(0.04, 1.0, 0.5, 0.04, [0.5], -1, 0.5, 0.5), "numberSubSteps", "");
        var process = new SquareRootAndersen(0.04, 1.0, 0.5, 0.04, [0.5, 1.0], 2, 0.5, 0.5);
        Assert.Equal("nextPath must be called before nextstep", Assert.Throws<NanoQLException>(() => process.Nextstep([0.1, 0.2])).Message);
        Assert.Equal("nextStep must be called before stepSd", Assert.Throws<NanoQLException>(() => process.StepSd()).Message);
        process.NextPath();
        Check<ArgumentException>(() => process.Nextstep([0.1]), "variates", "1 variates given, 2 required");

        var model = new FlatVol([0.2, 0.2, 0.2], new ExponentialForwardCorrelation(Times), new EvolutionDescription(Times), 3, [0.03, 0.03, 0.03], [0.0, 0.0, 0.0]);
        var factory = new MTBrownianGeneratorFactory(42);
        Check<ArgumentException>(() => new SVDDFwdRatePc(model, factory, process, 0, 1, [3, 3, 3], 1), "initialStep", "initial step zero only supported currently. ");
        Check<ArgumentException>(() => new SVDDFwdRatePc(model, factory, new NoVariates(), 0, 1, [3, 3, 3]), "volProcess", "the volatility process must draw at least one variate per step");
        Check<ArgumentException>(() => new SVDDFwdRatePc(model, factory, process, 0, 1, [3, 3]), "numeraires", "Size mismatch between numeraires (2) and evolution times (3)");
        Check<ArgumentOutOfRangeException>(() => new SVDDFwdRatePc(model, factory, process, -1, 1, [3, 3, 3]), "firstVolatilityFactor", "");
    }

    [Fact]
    public void TheOtherEvolversRefusals()
    {
        var model = new FlatVol([0.2, 0.2, 0.2], new ExponentialForwardCorrelation(Times), new EvolutionDescription(Times), 3, [0.03, 0.03, 0.03], [0.0, 0.0, 0.0]);
        var factory = new MTBrownianGeneratorFactory(42);
        int[] moneyMarket = [.. EvolutionDescriptionFunctions.MoneyMarketMeasure(model.Evolution)];
        Check<ArgumentException>(() => new LogNormalFwdRateIpc(model, factory, moneyMarket), "numeraires", "terminal measure required for ipc ");
        Check<ArgumentException>(() => new LogNormalFwdRateiBalland(model, factory, moneyMarket), "numeraires", "terminal measure required for iBalland ");

        var e = new LogNormalFwdRateEulerConstrained(model, factory, [3, 3, 3]);
        e.StartNewPath();
        Assert.Equal("setThisConstraint must be called before advanceStep", Assert.Throws<NanoQLException>(() => e.AdvanceStep()).Message);
        Check<ArgumentException>(() => e.SetThisConstraint([0.03, 0.03], [true, true, true]), "rateConstraints", "wrong number of constraints specified");
        Check<ArgumentException>(() => e.SetThisConstraint([0.03, 0.03, 0.03], [true, true]), "isConstraintActive", "wrong number of isConstraintActive specified");
        e.SetThisConstraint([0.03, 0.03, 0.03], [true, true, true]);
        Assert.Equal("setConstraintType must be called before a constraint is active", Assert.Throws<NanoQLException>(() => e.AdvanceStep()).Message);
        Check<ArgumentException>(() => e.SetConstraintType([0, 1], [1, 2, 3]), "startIndexOfSwapRate", "Size mismatch in constraint specification.");
        Check<ArgumentException>(() => e.SetConstraintType([0, 1, 2], [1, 2]), "endIndexOfSwapRate", "Size mismatch in constraint specification.");
        Check<ArgumentException>(() => e.SetConstraintType([0, 1, 1], [1, 2, 3]), "endIndexOfSwapRate", "constrained euler currently only implemented for forward rates");
        Check<ArgumentOutOfRangeException>(() => e.SetConstraintType([0, 1, 3], [1, 2, 4]), "startIndexOfSwapRate", "the constrained rate must be one of the rates");
    }

    [Fact]
    public void TheModelsVariancesAndAdaptersRefusals()
    {
        var corr = new ExponentialForwardCorrelation(Times);
        var evolution = new EvolutionDescription(Times);
        double[] rates = [0.03, 0.03, 0.03], d = [0.0, 0.0, 0.0];
        Check<ArgumentException>(() => new AbcdVol(-0.06, 0.17, 0.54, 0.17, [1.0, 1.0], corr, evolution, 3, rates, d), "ks", "mismatch between number of rates (3) and ks (2)");
        Check<ArgumentException>(() => new AbcdVol(-0.06, 0.17, 0.54, 0.17, [1.0, 1.0, 1.0], corr, evolution, 4, rates, d), "numberOfFactors", "number of factors (4) cannot be greater than numberOfRates (3)");
        Check<ArgumentException>(() => new AbcdVol(-0.06, 0.17, 0.54, 0.17, [1.0, 1.0, 1.0], corr, evolution, 0, rates, d), "numberOfFactors", "number of factors (0) must be greater than zero");

        Check<ArgumentException>(() => new PiecewiseConstantAbcdVariance(-0.06, 0.17, 0.54, 0.17, 0, [1.0]), "rateTimes", "Rate times must contain at least two values");
        Check<ArgumentException>(() => new PiecewiseConstantAbcdVariance(-0.06, 0.17, 0.54, 0.17, 0, []), "times", "at least one time is required");
        Check<ArgumentOutOfRangeException>(() => new PiecewiseConstantAbcdVariance(-0.06, 0.17, 0.54, 0.17, 2, [1.0, 2.0, 3.0]), "resetIndex", "resetIndex (2) must be less than rateTimes.size()-1 (2)");
        var variance = new PiecewiseConstantAbcdVariance(-0.06, 0.17, 0.54, 0.17, 1, [1.0, 2.0, 3.0]);
        Check<ArgumentOutOfRangeException>(() => variance.Variance(2), "i", "invalid step index");
        Check<ArgumentOutOfRangeException>(() => variance.TotalVolatility(-1), "i", "invalid step index");

        double[] small = [0.5, 1.0, 1.5, 2.0, 2.5];
        PiecewiseConstantAbcdVariance[] longs = [new(-0.06, 0.17, 0.54, 0.17, 0, [0.5, 1.5, 2.5]), new(-0.06, 0.17, 0.54, 0.17, 1, [0.5, 1.5, 2.5])];
        Check<ArgumentException>(() => new VolatilityInterpolationSpecifierabcd(2, 0, longs[..1], small), "originalVariances", "size mismatch in VolatilityInterpolationSpecifierabcd");
        PiecewiseConstantAbcdVariance[] off = [new(-0.06, 0.17, 0.54, 0.17, 0, [0.5, 1.4, 2.5]), new(-0.06, 0.17, 0.54, 0.17, 1, [0.5, 1.4, 2.5])];
        Check<ArgumentException>(() => new VolatilityInterpolationSpecifierabcd(2, 0, off, small), "originalVariances", "rate times in variances passed in don't match small times in VolatilityInterpolationSpecifierabcd");
        Check<ArgumentOutOfRangeException>(() => new VolatilityInterpolationSpecifierabcd(0, 0, longs, small), "period", "");
        Check<ArgumentOutOfRangeException>(() => new VolatilityInterpolationSpecifierabcd(2, -1, longs, small), "offset", "");
        Check<ArgumentException>(() => new VolatilityInterpolationSpecifierabcd(2, 0, [], small), "originalVariances", "at least one long rate's variance is required");
        Check<ArgumentException>(() => new VolatilityInterpolationSpecifierabcd(2, 0, longs, []), "timesForSmallRates", "at least one short rate time is required");
        var spec = new VolatilityInterpolationSpecifierabcd(2, 0, longs, small);
        Check<ArgumentException>(() => spec.WithScalingFactors([1.0]), "scales", "inappropriate number of scales passed in to VolatilityInterpolationSpecifierabcd::setScalingFactors ");

        var model = new FlatVol([0.2, 0.2, 0.2], corr, evolution, 3, rates, d);
        Check<ArgumentOutOfRangeException>(() => new FwdPeriodAdapter(model, 0, 0, []), "period", "period must  be greater than zero in fwdperiodadapter");
        Check<ArgumentException>(() => new FwdPeriodAdapter(model, 2, 2, []), "offset", "period must be greater than offset in fwdperiodadapter");
        Check<ArgumentException>(() => new FwdPeriodAdapter(model, 2, 0, [0.01, 0.01]), "newDisplacements", "newDisplacements should be empty,1, or number of new rates in fwdperiodadapter");
        Check<ArgumentException>(() => new FwdPeriodAdapter(model, 5, 4, []), "period", "no long forward of period 5 fits the 3 rates from offset 4 in fwdperiodadapter");
        var skipping = new FlatVol([0.2, 0.2, 0.2], corr, new EvolutionDescription(Times, [1.0, 1.5]), 3, rates, d);
        double[] four = [0.5, 1.0, 1.5, 2.0, 2.5];
        var fourSkipping = new FlatVol(
            [0.2, 0.2, 0.2, 0.2], new ExponentialForwardCorrelation(four), new EvolutionDescription(four, [1.0, 1.5, 2.0]), 4, [0.03, 0.03, 0.03, 0.03], [0.0, 0.0, 0.0, 0.0]);
        Check<ArgumentException>(() => new FwdPeriodAdapter(fourSkipping, 2, 0, []), "largeModel", "every new rate time except last must be an evolution time in fwdperiod adapter");
        Check<ArgumentException>(() => new FwdPeriodAdapter(skipping, 2, 0, []), "largeModel", "the short model's evolution has no time at or before the last long reset (0.5) in fwdperiodadapter");
        Check<ArgumentOutOfRangeException>(() => new FwdPeriodAdapter(model, 2, 0, []).PseudoRoot(1), "i", "the index 1 is invalid: it must be less than number of steps (1)");

        var displaced = new FlatVol([0.2, 0.2, 0.2], corr, evolution, 3, rates, [0.0, 0.01, 0.0]);
        Check<ArgumentException>(() => new FwdToCotSwapAdapter(displaced), "fwdModel", "2nd displacement (0.01) not equal to the previous ones (0)");
        Check<ArgumentException>(() => new CotSwapToFwdAdapter(displaced), "ctModel", "2nd displacement (0.01) not equal to the previous ones (0)");
        Check<ArgumentException>(() => new FwdToCotSwapAdapter(skipping), "fwdModel", "skipping 1st rate time");
        Check<ArgumentException>(() => new CotSwapToFwdAdapter(skipping), "ctModel", "skipping 1st rate time");
        Check<ArgumentOutOfRangeException>(() => new FwdToCotSwapAdapter(model).PseudoRoot(3), "i", "the index 3 is invalid: it must be less than number of steps (3)");
        Check<ArgumentOutOfRangeException>(() => new CotSwapToFwdAdapter(model).PseudoRoot(3), "i", "the index 3 is invalid: it must be less than number of steps (3)");

        var two = new LMMCurveState([0.5, 1.0, 1.5]);
        two.SetOnForwardRates([0.03, 0.03]);
        Check<ArgumentException>(() => new CotSwapFromFwdCorrelation(corr, two, 0.0), "curveState", "mismatch between number of rates in fwdCorr (3) and curveState (2)");
    }

    // A variance of one step, on the rates' times.
    private sealed class OneVariance(double[] times) : PiecewiseConstantVariance
    {
        public override ImmutableArray<double> Variances => [0.01];

        public override ImmutableArray<double> Volatilities => [0.2];

        public override ImmutableArray<double> RateTimes => [.. times];
    }

    // A volatility process drawing no variates, which the SVD-DD evolver refuses (QuantLib divides by them).
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

    [Fact]
    public void TheCalibrationsRefusals()
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
        double[] up = [.. Enumerable.Repeat(1.0, 10)];
        var form = new AlphaFormLinearHyperbolic(times);

        // QuantLib's texts
        Check<ArgumentException>(
            () => CTSMMCapletCalibration.PerformChecks(evolution, corr, variances[..9], caplets, cs),
            "displacedSwapVariances",
            "mismatch between EvolutionDescription number of rates (10) and displacedSwapVariances size (9)");
        Check<ArgumentException>(
            () => CTSMMCapletCalibration.PerformChecks(evolution, corr, variances, caplets[..9], cs),
            "mktCapletVols",
            "mismatch between EvolutionDescription number of rates (10) and mktCapletVols size (9)");
        double[] offLast = [.. caplets[..9], 0.2];
        Check<ArgumentException>(
            () => CTSMMCapletCalibration.PerformChecks(evolution, corr, variances, offLast, cs),
            "mktCapletVols",
            "last caplet vol (0.2) must be equal to last swaption vol (");
        Check<ArgumentException>(
            () => CTSMMCapletCalibration.PerformChecks(new EvolutionDescription(times, [1.0, 2.0]), corr, variances, caplets, cs),
            "corr",
            "evolutionTimes ( 1 2 ) not equal to correlation times ( 0.5 1 1.5 2 2.5 3 3.5 4 4.5 5 )");
        Check<ArgumentException>(
            () => CTSMMCapletOriginalCalibration.CalibrationFunction(evolution, corr, variances, caplets, cs, 0.0, ten, true, false, 11),
            "numberOfFactors",
            "number of factors (11) cannot be greater than numberOfRates (10)");
        Check<ArgumentException>(
            () => CTSMMCapletMaxHomogeneityCalibration.CapletMaxHomogeneityCalibration(evolution, corr, variances, caplets, cs, 0.0, 1.0, 0, 100, 1e-8),
            "numberOfFactors",
            "number of factors (0) must be greater than zero");
        Check<ArgumentException>(
            () => new CTSMMCapletMaxHomogeneityCalibration(evolution, corr, variances, caplets, cs, 0.0, 1.5),
            "caplet0Swaption1Priority",
            "caplet0Swaption1Priority (1.5) must be in [0.0, 1.0]");
        Check<ArgumentException>(
            () => new CTSMMCapletOriginalCalibration(evolution, corr, variances, caplets, cs, 0.0, nine, true, false),
            "alpha",
            "mismatch between number of rates (10) and alpha (9)");
        Check<ArgumentException>(
            () => new CTSMMCapletAlphaFormCalibration(evolution, corr, variances, caplets, cs, 0.0, ten, up, nine, false),
            "alphaMin",
            "mismatch between number of rates (10) and alphaMin (9)");
        Check<ArgumentException>(
            () => new PseudoRootFacade([new Matrix(10, 3)], times, ten, ten),
            "covariancePseudoRoots",
            "number of rates (10) greater than number of factors (3) times number of steps (1)");
        Check<ArgumentException>(
            () => new PseudoRootFacade([new Matrix(1, 1)], [0.5], [0.03], [0.0]),
            "rateTimes",
            "Rate times must contain at least two values");
        Check<ArgumentException>(
            () => CapletCoterminalPeriodicFunctions.CapletSwaptionPeriodicCalibration(
                evolution, corr, new VolatilityInterpolationSpecifierabcd(2, 0, [new PiecewiseConstantAbcdVariance(0.0, 0.17, 1.0, 0.1, 0, [0.5, 1.5])], [0.5, 1.0, 1.5]),
                caplets, cs, 0.0, 1.0, 3, 2, 100, 1e-8, 10, 1e-5, 30, 1e-5),
            "displacedSwapVariances",
            "mismatch between number of swap variances given and number of rates and period");
        Assert.Equal(
            "alpha form failure",
            Assert.Throws<NanoQLException>(() => CTSMMCapletAlphaFormCalibration.CapletAlphaFormCalibration(
                evolution, corr, variances, [.. caplets[..9].Select(x => 0.7 * x), caplets[9]], cs, 0.0, ten, up, [.. up.Select(x => -x)], false, form, 3, 100, 1e-8)).Message);
        Assert.Equal(
            "intersection empty so no solution",
            Assert.Throws<NanoQLException>(() => SphereCylinderFunctions.SphereCylinderOptimizerClosest(1.0, 0.2, 3.0, 0.5, 0.5, 0.5, 100, 1e-8)).Message);
        Check<ArgumentException>(() => new SphereCylinderOptimizer(0.0, 0.2, 0.5, 0.5, 0.5, 0.5), "r", "sphere must have positive radius");
        Check<ArgumentException>(() => new SphereCylinderOptimizer(1.0, 0.2, -0.5, 0.5, 0.5, 0.5), "alpha", "cylinder centre must have positive coordinate");
        Check<ArgumentException>(
            () => new BasisIncompleteOrdered(3).AddVector([1.0, 0.0]), "newVector1", "missized vector passed to BasisIncompleteOrdered::addVector");

        // nanoql's, where QuantLib's path is undefined (Divergences/MarketModelUndefinedReadsTests.cs)
        Check<ArgumentException>(() => new PseudoRootFacade(Array.Empty<Matrix>(), times, ten, ten), "covariancePseudoRoots", "no pseudo-roots given");
        Check<ArgumentException>(
            () => CTSMMCapletCalibration.PerformChecks(evolution, corr, shortSecond, caplets, cs),
            "displacedSwapVariances",
            "the 2nd displaced swap variance has 1 variances, fewer than its 2 steps");
        Check<ArgumentException>(
            () => CTSMMCapletOriginalCalibration.CalibrationFunction(evolution, corr, variances, caplets, cs, 0.0, nine, true, false, 3),
            "alpha",
            "mismatch between number of rates (10) and alpha (9)");
        Check<ArgumentException>(
            () => CTSMMCapletAlphaFormCalibration.CapletAlphaFormCalibration(evolution, corr, variances, caplets, cs, 0.0, nine, up, ten, false, form, 3, 100, 1e-8),
            "alphaInitial",
            "mismatch between number of rates (10) and alphaInitial (9)");
        double[] bigTimes = [.. Enumerable.Range(0, 6).Select(i => times[2 * i])];
        var specifier = new VolatilityInterpolationSpecifierabcd(
            2, 0, [.. Enumerable.Range(0, 5).Select(i => new PiecewiseConstantAbcdVariance(0.0, 0.17, 1.0, 0.1, i, bigTimes))], times);
        Check<ArgumentOutOfRangeException>(
            () => CapletCoterminalPeriodicFunctions.CapletSwaptionPeriodicCalibration(evolution, corr, specifier, caplets, cs, 0.0, 1.0, 3, 0, 100, 1e-8, 10, 1e-5, 30, 1e-5),
            "period",
            "period must be at least 1");
        Check<ArgumentOutOfRangeException>(
            () => CapletCoterminalPeriodicFunctions.CapletSwaptionPeriodicCalibration(evolution, corr, specifier, caplets, cs, 0.0, 1.0, 3, 2, 100, 1e-8, 10, 1e-5, 0, 1e-5),
            "maxPeriodIterations",
            "maxPeriodIterations must be at least 1");
        Check<ArgumentException>(
            () => CapletCoterminalPeriodicFunctions.CapletSwaptionPeriodicCalibration(evolution, corr, specifier, [], cs, 0.0, 1.0, 3, 2, 100, 1e-8, 10, 1e-5, 30, 1e-5),
            "capletVols",
            "mismatch between EvolutionDescription number of rates (10) and mktCapletVols size (0)");
        Check<ArgumentOutOfRangeException>(
            () => new OrthogonalProjections(new Matrix(2, 3, 1.0), 100.0, 1e-6).GetVector(2), "index", "index (2) must be less than the number of vectors (2)");
        Check<ArgumentOutOfRangeException>(() => new AlphaFormInverseLinear([0.5, 1.0]).Evaluate(2), "i", "index (2) must be less than the number of times (2)");
        Check<ArgumentException>(
            () => new AlphaFinder(form).Solve(0.0, 1, [0.2], [1.0, 1.0, 1.0], [0.9, 0.9], 1.0, 1.0, 0.02, 1e-8, 1.0, -1.0, 100, out _, out _, out _, new double[3]),
            "rateOneVols",
            "1 values given, the step index reads 2");

        // a count below zero, which QuantLib's unsigned counts wrap
        var calibration = new CTSMMCapletMaxHomogeneityCalibration(evolution, corr, variances, caplets, cs, 0.0, 1.0);
        Check<ArgumentOutOfRangeException>(() => calibration.Calibrate(3, -1, 1e-4), "maxIterations", "");
        Check<ArgumentOutOfRangeException>(() => calibration.Calibrate(3, 10, 1e-4, -1), "innerMaxIterations", "");
        Check<ArgumentOutOfRangeException>(
            () => CTSMMCapletMaxHomogeneityCalibration.CapletMaxHomogeneityCalibration(evolution, corr, variances, caplets, cs, 0.0, 1.0, 3, -1, 1e-8),
            "maxIterations",
            "");
        Check<ArgumentOutOfRangeException>(
            () => new AlphaFinder(form).SolveWithMaxHomogeneity(0.0, 1, [0.2, 0.2], [1.0, 1.0, 1.0], [0.9, 0.9], 1.0, 1.0, 0.02, 1e-8, 1.0, -1.0, -1, out _, out _, out _, new double[3]),
            "steps",
            "");
        Check<ArgumentOutOfRangeException>(() => new SphereCylinderOptimizer(1.0, 0.5, 1.5, 0.5, 0.5, 0.5).FindClosest(-1, 1e-8, out _, out _, out _), "maxIterations", "");
        Check<ArgumentOutOfRangeException>(
            () => SphereCylinderFunctions.SphereCylinderOptimizerClosest(1.0, 0.5, 1.5, 0.5, 0.5, 0.5, -1, 1e-8), "maxIterations", "");
        Check<ArgumentOutOfRangeException>(() => new BasisIncompleteOrdered(-1), "euclideanDimension", "");
    }

    // The refusal's type exactly, its parameter, and its text (an ArgumentException's own suffix after it).
    private static void Check<T>(Action a, string paramName, string text)
        where T : ArgumentException
    {
        T e = Assert.Throws<T>(a);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(text, e.Message, StringComparison.Ordinal);
    }
}
