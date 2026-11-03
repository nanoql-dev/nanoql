// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// Two routes to one value in the market-model framework: the drifts with and without factor reduction at full factors;
/// the constant-maturity state and drifts spanning one forward against the LIBOR ones; the three curve states on one
/// curve; the coterminal Jacobian against central bumps; and the Euler and iterative predictor-corrector evolvers'
/// caplets converging to the predictor-corrector's as the steps refine (Balland's in
/// <c>Divergences/BallandGeometricMeanTests.cs</c>); the coterminal adapters' round trip; and FlatVol as the AbcdVol of
/// a constant abcd function.
/// </summary>
public class MarketModelTwoRouteTests
{
    private static readonly double[] RateTimes = [0.5, 1.0, 1.5, 2.0, 2.5, 3.0];
    private static readonly double[] Forwards = [0.03, 0.033, 0.036, 0.034, 0.038];

    [Fact]
    public void PlainAndReducedDriftsAgreeAtFullFactors()
    {
        Matrix pseudo = Pseudo(5);
        foreach (int numeraire in new[] { 5, 2, 0 })
        {
            var c = new LMMDriftCalculator(pseudo, [0.0, 0.01, 0.0, 0.02, 0.0], [0.5, 0.5, 0.5, 0.5, 0.5], numeraire, 0);
            double[] plain = new double[5], reduced = new double[5];
            c.ComputePlain(Forwards, plain);
            c.ComputeReduced(Forwards, reduced);
            for (int i = 0; i < 5; ++i)
            {
                Assert.Equal(plain[i], reduced[i], 1e-16);
            }
        }
    }

    [Theory]
    [InlineData(5)]
    [InlineData(3)]
    public void EachCurveStateOverloadIsItsForwardsOverload(int factors)
    {
        // a state set on discount ratios, so its forwards are its own reads
        var cs = new LMMCurveState(RateTimes);
        var setOn = new LMMCurveState(RateTimes);
        setOn.SetOnForwardRates(Forwards);
        cs.SetOnDiscountRatios([.. Enumerable.Range(0, 6).Select(i => setOn.DiscountRatio(i, 0))]);
        double[] fwds = cs.ForwardRates().ToArray(), taus = [0.5, 0.5, 0.5, 0.5, 0.5];
        Matrix pseudo = Pseudo(factors);
        var lmm = new LMMDriftCalculator(pseudo, [0.0, 0.01, 0.0, 0.02, 0.0], taus, 2, 0);
        var normal = new LMMNormalDriftCalculator(pseudo, taus, 2, 0);
        var routes = new (Action<double[]> State, Action<double[]> Forwards)[]
        {
            (d => lmm.Compute(cs, d), d => lmm.Compute(fwds, d)),
            (d => lmm.ComputePlain(cs, d), d => lmm.ComputePlain(fwds, d)),
            (d => lmm.ComputeReduced(cs, d), d => lmm.ComputeReduced(fwds, d)),
            (d => normal.Compute(cs, d), d => normal.Compute(fwds, d)),
            (d => normal.ComputePlain(cs, d), d => normal.ComputePlain(fwds, d)),
            (d => normal.ComputeReduced(cs, d), d => normal.ComputeReduced(fwds, d)),
        };
        foreach ((Action<double[]> state, Action<double[]> forwards) in routes)
        {
            double[] a = new double[5], b = new double[5];
            state(a);
            forwards(b);
            Assert.Equal(b, a);
            Assert.Contains(a, x => x != 0.0);
        }
    }

    [Fact]
    public void ConstantMaturityOfOneForwardIsTheLiborModel()
    {
        var lmm = new LMMCurveState(RateTimes);
        lmm.SetOnForwardRates(Forwards);
        var cms = new CMSwapCurveState(RateTimes, 1);
        cms.SetOnCMSwapRates(Forwards);
        for (int i = 0; i <= 5; ++i)
        {
            Assert.Equal(lmm.DiscountRatio(i, 5), cms.DiscountRatio(i, 5), 1e-15);
        }

        Matrix pseudo = Pseudo(3);
        double[] taus = [0.5, 0.5, 0.5, 0.5, 0.5], d = new double[5], l = new double[5], m = new double[5];
        new LMMDriftCalculator(pseudo, d, taus, 5, 0).Compute(lmm, l);
        new CMSMMDriftCalculator(pseudo, d, taus, 5, 0, 1).Compute(cms, m);
        for (int i = 0; i < 5; ++i)
        {
            Assert.Equal(l[i], m[i], 1e-16);
        }
    }

    [Fact]
    public void TheThreeStatesOnOneCurveAgree()
    {
        var lmm = new LMMCurveState(RateTimes);
        lmm.SetOnForwardRates(Forwards);
        var cot = new CoterminalSwapCurveState(RateTimes);
        cot.SetOnCoterminalSwapRates(lmm.CoterminalSwapRates());
        var cms = new CMSwapCurveState(RateTimes, 3);
        cms.SetOnCMSwapRates(lmm.CmSwapRates(3));
        for (int i = 0; i <= 5; ++i)
        {
            double expected = lmm.DiscountRatio(i, 0);
            Assert.Equal(expected, cot.DiscountRatio(i, 0), 1e-15);
            Assert.Equal(expected, cms.DiscountRatio(i, 0), 1e-15);
        }
    }

    [Fact]
    public void TheCoterminalJacobianIsTheBumpedSwapRates()
    {
        var cs = new LMMCurveState(RateTimes);
        cs.SetOnForwardRates(Forwards);
        Matrix j = SwapForwardMappings.CoterminalSwapForwardJacobian(cs);
        const double h = 1e-6;
        for (int f = 0; f < 5; ++f)
        {
            double[] up = (double[])Forwards.Clone(), down = (double[])Forwards.Clone();
            up[f] += h;
            down[f] -= h;
            var u = new LMMCurveState(RateTimes);
            u.SetOnForwardRates(up);
            var w = new LMMCurveState(RateTimes);
            w.SetOnForwardRates(down);
            for (int i = 0; i < 5; ++i)
            {
                Assert.Equal((u.CoterminalSwapRate(i) - w.CoterminalSwapRate(i)) / (2 * h), j[i, f], 1e-8);
            }
        }
    }

    [Fact]
    public void TheEvolversCapletsConvergeAsTheStepsRefine()
    {
        double[] coarseEuler = CapletGaps(1, (m, f, n) => new LogNormalFwdRateEuler(m, f, n));
        double[] fineEuler = CapletGaps(4, (m, f, n) => new LogNormalFwdRateEuler(m, f, n));
        double[] fineIpc = CapletGaps(4, (m, f, n) => new LogNormalFwdRateIpc(m, f, n));
        for (int i = 1; i < 9; ++i)
        {
            Assert.True(Math.Abs(fineEuler[i]) < 0.5 * Math.Abs(coarseEuler[i]), $"caplet {i}: {coarseEuler[i]} then {fineEuler[i]}");
            Assert.True(Math.Abs(fineIpc[i]) < 1e-4, $"caplet {i}: {fineIpc[i]}");
        }
    }

    // Each caplet's mean gap to the predictor-corrector's on the same paths, relative to it: ten semi-annual rates on a
    // flat 30% model at 4%, the given steps a period, in the terminal measure.
    private static double[] CapletGaps(int stepsPerPeriod, Func<MarketModel, BrownianGeneratorFactory, int[], MarketModelEvolver> make)
    {
        const int n = 10, paths = 8192;
        double[] rateTimes = [.. Enumerable.Range(1, n + 1).Select(i => 0.5 * i)];
        var evolutionTimes = new List<double>();
        for (int i = 0; i < n; ++i)
        {
            double start = i == 0 ? 0.0 : rateTimes[i - 1];
            for (int k = 1; k <= stepsPerPeriod; ++k)
            {
                evolutionTimes.Add(k == stepsPerPeriod ? rateTimes[i] : start + ((rateTimes[i] - start) * k / stepsPerPeriod));
            }
        }

        var evolution = new EvolutionDescription(rateTimes, [.. evolutionTimes]);
        var model = new FlatVol(
            Enumerable.Repeat(0.3, n).ToArray(), new ExponentialForwardCorrelation(rateTimes, 0.5, 0.2), evolution, n, Enumerable.Repeat(0.04, n).ToArray(), new double[n]);
        int[] numeraires = [.. EvolutionDescriptionFunctions.TerminalMeasure(evolution)];
        MarketModelEvolver[] evolvers = [new LogNormalFwdRatePc(model, new MTBrownianGeneratorFactory(42), numeraires), make(model, new MTBrownianGeneratorFactory(42), numeraires)];
        double[,] sums = new double[2, n];
        for (int p = 0; p < paths; ++p)
        {
            for (int k = 0; k < 2; ++k)
            {
                MarketModelEvolver e = evolvers[k];
                e.StartNewPath();
                for (int s = 0, caplet = 0; s < evolution.NumberOfSteps; ++s)
                {
                    e.AdvanceStep();
                    if (evolution.EvolutionTimes[s] == rateTimes[caplet])
                    {
                        CurveState cs = e.CurrentState;
                        sums[k, caplet] += Math.Max(cs.ForwardRate(caplet) - 0.04, 0.0) * cs.DiscountRatio(caplet + 1, n);
                        ++caplet;
                    }
                }
            }
        }

        return [.. Enumerable.Range(0, n).Select(i => (sums[1, i] - sums[0, i]) / sums[0, i])];
    }

    [Fact]
    public void TheCoterminalAdaptersRoundTrip()
    {
        double[] times = [0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5];
        var model = new FlatVol(
            [0.2, 0.21, 0.22, 0.23, 0.24, 0.25],
            new ExponentialForwardCorrelation(times, 0.5, 0.2),
            new EvolutionDescription(times),
            6,
            [0.03, 0.032, 0.034, 0.033, 0.036, 0.035],
            [0.01, 0.01, 0.01, 0.01, 0.01, 0.01]);
        var back = new CotSwapToFwdAdapter(new FwdToCotSwapAdapter(model));
        for (int i = 0; i < 6; ++i)
        {
            Assert.Equal(model.InitialRates[i], back.InitialRates[i], 1e-15);
        }

        for (int k = 0; k < model.NumberOfSteps; ++k)
        {
            Matrix expected = model.Covariance(k), actual = back.Covariance(k);
            int alive = model.Evolution.FirstAliveRate[k];
            for (int i = alive; i < 6; ++i)
            {
                for (int j = alive; j < 6; ++j)
                {
                    Assert.Equal(expected[i, j], actual[i, j], 1e-14);
                }
            }
        }
    }

    [Fact]
    public void FlatVolIsTheAbcdVolOfAConstantFunction()
    {
        double[] times = [0.3, 0.8, 1.0, 1.75, 2.5, 4.0, 4.6];
        double[] vols = [0.18, 0.19, 0.2, 0.21, 0.22, 0.23];
        var corr = new ExponentialForwardCorrelation(times, 0.5, 0.2);
        var evolution = new EvolutionDescription(times);
        double[] rates = [0.03, 0.031, 0.032, 0.033, 0.034, 0.035], d = new double[6];
        var flat = new FlatVol(vols, corr, evolution, 6, rates, d);
        var abcd = new AbcdVol(0.0, 0.0, 1.0, 1.0, vols, corr, evolution, 6, rates, d);
        for (int k = 0; k < flat.NumberOfSteps; ++k)
        {
            Matrix expected = flat.Covariance(k), actual = abcd.Covariance(k);
            for (int i = 0; i < 6; ++i)
            {
                for (int j = 0; j < 6; ++j)
                {
                    Assert.Equal(expected[i, j], actual[i, j], 1e-15);
                }
            }
        }
    }

    [Fact]
    public void AFacadeOnAModelsPseudoRootsIsTheModel()
    {
        double[] rates = [.. Enumerable.Range(0, 5).Select(i => 0.03 + (0.002 * i))];
        var model = new FlatVol(
            [0.2, 0.19, 0.21, 0.18, 0.2],
            new ExponentialForwardCorrelation(RateTimes, 0.5, 0.2, 1.0),
            new EvolutionDescription(RateTimes),
            3,
            rates,
            [.. Enumerable.Repeat(0.01, 5)]);
        var facade = new PseudoRootFacade(
            [.. Enumerable.Range(0, 5).Select(model.PseudoRoot)], RateTimes, rates, [.. Enumerable.Repeat(0.01, 5)]);
        Assert.Equal(model.Evolution.EvolutionTimes, facade.Evolution.EvolutionTimes);
        Assert.Equal(model.NumberOfFactors, facade.NumberOfFactors);
        for (int k = 0; k < 5; ++k)
        {
            Matrix expected = model.TotalCovariance(k), actual = facade.TotalCovariance(k);
            for (int i = 0; i < 5; ++i)
            {
                for (int j = 0; j < 5; ++j)
                {
                    Assert.Equal(expected[i, j], actual[i, j]);
                }
            }
        }
    }

    [Fact]
    public void AFacadeOnACalibrationIsTheFacadeOnItsPseudoRoots()
    {
        double[] times = [.. Enumerable.Range(1, 11).Select(i => 0.5 * i)];
        var cs = new LMMCurveState(times);
        cs.SetOnForwardRates([.. Enumerable.Range(0, 10).Select(i => 0.03 + (0.0025 * i))]);
        var corr = new CotSwapFromFwdCorrelation(new ExponentialForwardCorrelation(times, 0.5, 0.2), cs, 0.01);
        PiecewiseConstantVariance[] variances = [.. Enumerable.Range(0, 10).Select(i => new PiecewiseConstantAbcdVariance(0.0, 0.17, 1.0, 0.1, i, times))];
        double[] caplets = [0.1640, 0.1740, 0.1840, 0.1940, 0.1840, 0.1740, 0.1640, 0.1540, 0.1440, variances[9].TotalVolatility(9)];
        var calibration = new CTSMMCapletMaxHomogeneityCalibration(new EvolutionDescription(times), corr, variances, caplets, cs, 0.01);
        CTSMMCapletCalibrationResults results = calibration.Calibrate(3, 10, 1e-4, 100, 1e-8);

        var byCalibration = new PseudoRootFacade(calibration, results);
        var byRoots = new PseudoRootFacade(results.SwapPseudoRoots(), times, cs.CoterminalSwapRates(), [.. Enumerable.Repeat(0.01, 10)]);
        Assert.Equal(byRoots.InitialRates, byCalibration.InitialRates);
        Assert.Equal(byRoots.Displacements, byCalibration.Displacements);
        Assert.Equal(byRoots.Evolution.RateTimes, byCalibration.Evolution.RateTimes);
        for (int k = 0; k < 10; ++k)
        {
            Assert.True(byRoots.TotalCovariance(k) == byCalibration.TotalCovariance(k));
        }

        // the calibration's own fit: its model swaption volatilities from the facade's total covariance
        Matrix total = byCalibration.TotalCovariance(9);
        for (int i = 0; i < 10; ++i)
        {
            Assert.Equal(results.MdlSwaptionVols[i], Math.Sqrt(total[i, i] / times[i]), 15);
        }
    }

    private static Matrix Pseudo(int factors)
    {
        var m = new Matrix(5, factors);
        for (int i = 0; i < 5; ++i)
        {
            for (int k = 0; k < factors; ++k)
            {
                m[i, k] = 0.05 + (0.01 * i) - (0.02 * k) + (0.003 * i * k);
            }
        }

        return m;
    }
}
