// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The market-model framework's inputs whose QuantLib path is undefined, each refused before the undefined step (the
/// S-mm plan's MR3 (xx), and those this run's C1 found, under the playbook's row for undefined behaviour). The
/// refusals' types, texts and parameters are asserted by <c>Conventions/MarketModelRefusalTests.cs</c>.
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
}
