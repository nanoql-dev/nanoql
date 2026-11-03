// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The market models' boundary (the playbook's §2.5): a matrix read returns a copy, so changing it changes nothing; a
/// constructor that takes a matrix copies it; an evolver copies each step's pseudo-root, so one model serves many
/// evolvers on many threads, each path the same as on a model of its own; and the SVD-DD evolver owns a copy of its
/// volatility process, so the process given is left as it was and serves many evolvers; and the volatility
/// interpolation specifier's setters return a new specifier, leaving the one they're called on as it was.
/// </summary>
public class MarketModelImmutabilityTests
{
    private static readonly double[] RateTimes = [0.5, 1.0, 1.5, 2.0];

    [Fact]
    public void AMatrixReadIsACopy()
    {
        FlatVol m = Model();
        Matrix root = m.PseudoRoot(1), cov = m.Covariance(1), total = m.TotalCovariance(2);
        double r = root[2, 0], c = cov[2, 2], t = total[2, 2];
        root[2, 0] = 9.0;
        cov[2, 2] = 9.0;
        total[2, 2] = 9.0;
        Assert.Equal(r, m.PseudoRoot(1)[2, 0]);
        Assert.Equal(c, m.Covariance(1)[2, 2]);
        Assert.Equal(t, m.TotalCovariance(2)[2, 2]);

        var corr = new ExponentialForwardCorrelation(RateTimes, 0.5, 0.2, 0.5);
        Matrix x = corr.Correlation(0);
        double x01 = x[0, 1];
        x[0, 1] = 9.0;
        corr.Correlations()[0][0, 1] = 9.0;
        Assert.Equal(x01, corr.Correlation(0)[0, 1]);

        var cs = new LMMCurveState(RateTimes);
        cs.SetOnForwardRates([0.03, 0.035, 0.04]);
        var cot = new CotSwapFromFwdCorrelation(corr, cs, 0.0);
        for (int i = 0; i < cot.Times.Length; ++i)
        {
            Matrix period = cot.Correlation(i);
            Assert.Equal(cot.Correlations()[i].AsSpan().ToArray(), period.AsSpan().ToArray());
            double p01 = period[1, 2];
            period[1, 2] = 9.0;
            Assert.Equal(p01, cot.Correlation(i)[1, 2]);
        }
    }

    [Fact]
    public void AConstructorsMatrixIsCopied()
    {
        var fwd = new Matrix(3, 3, 0.5);
        var h = new TimeHomogeneousForwardCorrelation(fwd, RateTimes);
        fwd[1, 0] = 0.9;
        Assert.Equal(0.5, h.Correlation(0)[1, 0]);

        double[] fwds = [0.03, 0.04, 0.05], taus = [0.5, 0.5, 0.5], z = new double[3];
        var cot = new CoterminalSwapCurveState(RateTimes);
        cot.SetOnCoterminalSwapRates(fwds);
        var cms = new CMSwapCurveState(RateTimes, 1);
        cms.SetOnCMSwapRates(fwds);
        foreach (Func<Matrix, Action<double[]>> make in new Func<Matrix, Action<double[]>>[]
        {
            p =>
            {
                var c = new LMMDriftCalculator(p, z, taus, 3, 0);
                return d => c.Compute(fwds, d);
            },
            p =>
            {
                var c = new LMMNormalDriftCalculator(p, taus, 3, 0);
                return d => c.Compute(fwds, d);
            },
            p =>
            {
                var c = new SMMDriftCalculator(p, z, taus, 3, 0);
                return d => c.Compute(cot, d);
            },
            p =>
            {
                var c = new CMSMMDriftCalculator(p, z, taus, 3, 0, 1);
                return d => c.Compute(cms, d);
            },
        })
        {
            var pseudo = new Matrix(3, 2, 0.1);
            Action<double[]> compute = make(pseudo);
            double[] before = new double[3], after = new double[3];
            compute(before);
            pseudo[0, 0] = 5.0;
            compute(after);
            Assert.Equal(before, after);
        }
    }

    [Fact]
    public void OneModelServesManyEvolversOnManyThreads()
    {
        FlatVol shared = Model();
        double[][] Paths(MarketModel m)
        {
            var e = new LogNormalFwdRatePc(m, new MTBrownianGeneratorFactory(7), [3, 3, 3]);
            var result = new double[20][];
            for (int p = 0; p < result.Length; ++p)
            {
                e.StartNewPath();
                for (int s = 0; s < 3; ++s)
                {
                    e.AdvanceStep();
                }

                result[p] = e.CurrentState.ForwardRates().ToArray();
            }

            return result;
        }

        double[][] own = Paths(Model());
        double[][][] concurrent = new double[8][][];
        Parallel.For(0, concurrent.Length, i => concurrent[i] = Paths(shared));
        foreach (double[][] c in concurrent)
        {
            Assert.Equal(own, c);
        }
    }

    [Fact]
    public void TheSpecifiersSettersReturnACopy()
    {
        double[] small = [0.5, 1.0, 1.5, 2.0, 2.5];
        double[] longTimes = [0.5, 1.5, 2.5];
        PiecewiseConstantAbcdVariance[] longs =
        [
            new(-0.06, 0.17, 0.54, 0.17, 0, longTimes),
            new(-0.05, 0.18, 0.55, 0.16, 1, longTimes),
        ];
        var spec = new VolatilityInterpolationSpecifierabcd(2, 0, longs, small);
        double[][] before = [.. spec.InterpolatedVariances.Select(v => v.Variances.ToArray())];
        VolatilityInterpolationSpecifier scaled = spec.WithScalingFactors([1.2, 0.8]);
        VolatilityInterpolationSpecifier lastSet = spec.WithLastCapletVol(0.3);
        Assert.NotSame(spec, scaled);
        Assert.NotSame(spec, lastSet);
        Assert.Equal(before, [.. spec.InterpolatedVariances.Select(v => v.Variances.ToArray())]);
        Assert.NotEqual(before, [.. scaled.InterpolatedVariances.Select(v => v.Variances.ToArray())]);
        Assert.NotEqual(before, [.. lastSet.InterpolatedVariances.Select(v => v.Variances.ToArray())]);

        // the last short rate matches the caplet volatility given
        Assert.Equal(0.3, lastSet.InterpolatedVariances[^1].TotalVolatility(3), 1e-15);
    }

    [Fact]
    public void TheStochasticVolatilityEvolverOwnsACopyOfItsProcess()
    {
        FlatVol model = Model();
        var given = new SquareRootAndersen(1.0, 1.0, 1.0, 1.21, model.Evolution.EvolutionTimes.AsSpan(), 2, 0.5, 0.5);
        double[][] Paths(MarketModelVolProcess process)
        {
            var e = new SVDDFwdRatePc(model, new MTBrownianGeneratorFactory(7), process, 1, 1, [3, 3, 3]);
            var result = new double[10][];
            for (int p = 0; p < result.Length; ++p)
            {
                e.StartNewPath();
                for (int s = 0; s < 3; ++s)
                {
                    e.AdvanceStep();
                }

                result[p] = e.CurrentState.ForwardRates().ToArray();
            }

            return result;
        }

        double[][] own = Paths(given.Clone());
        double[][][] concurrent = new double[4][][];
        Parallel.For(0, concurrent.Length, i => concurrent[i] = Paths(given));
        foreach (double[][] c in concurrent)
        {
            Assert.Equal(own, c);
        }

        // the process given never started a path
        Assert.Throws<NanoQLException>(() => given.Nextstep([0.1, 0.2]));
    }

    [Fact]
    public void TheCalibrationsCopyWhatTheyAreGivenAndWhatTheyReturn()
    {
        double[] times = [.. Enumerable.Range(1, 11).Select(i => 0.5 * i)];
        var cs = new LMMCurveState(times);
        cs.SetOnForwardRates([.. Enumerable.Range(0, 10).Select(i => 0.03 + (0.0025 * i))]);
        var corr = new CotSwapFromFwdCorrelation(new ExponentialForwardCorrelation(times, 0.5, 0.2), cs, 0.0);
        PiecewiseConstantVariance[] variances = [.. Enumerable.Range(0, 10).Select(i => new PiecewiseConstantAbcdVariance(0.0, 0.17, 1.0, 0.1, i, times))];
        double[] caplets = [0.1640, 0.1740, 0.1840, 0.1940, 0.1840, 0.1740, 0.1640, 0.1540, 0.1440, variances[9].TotalVolatility(9)];
        var evolution = new EvolutionDescription(times);

        // the form given is copied: QuantLib's calibration leaves it at its last alpha
        var form = new AlphaFormLinearHyperbolic(times, 0.25);
        double before = form.Evaluate(5);
        var calibration = new CTSMMCapletAlphaFormCalibration(
            evolution, corr, variances, caplets, cs, 0.0, new double[10], [.. Enumerable.Repeat(1.0, 10)], [.. Enumerable.Repeat(-1.0, 10)], false, form);
        CTSMMCapletCalibrationResults first = calibration.Calibrate(3, 10, 1e-4, 100, 1e-8);
        Assert.Equal(before, form.Evaluate(5));
        _ = CTSMMCapletAlphaFormCalibration.CapletAlphaFormCalibration(
            evolution, corr, variances, caplets, cs, 0.0, new double[10], [.. Enumerable.Repeat(1.0, 10)], [.. Enumerable.Repeat(-1.0, 10)], false, form, 3, 100, 1e-8);
        Assert.Equal(before, form.Evaluate(5));

        // the curve state given is copied, and a read is a copy; a second calibration repeats the first
        cs.SetOnForwardRates([.. Enumerable.Repeat(0.05, 10)]);
        CurveState read = calibration.CurveState();
        Assert.Equal(0.03, read.ForwardRate(0), 15);
        ((LMMCurveState)read).SetOnForwardRates([.. Enumerable.Repeat(0.07, 10)]);
        Assert.Equal(0.03, calibration.CurveState().ForwardRate(0), 15);
        CTSMMCapletCalibrationResults second = calibration.Calibrate(3, 10, 1e-4, 100, 1e-8);
        Assert.Equal(first.Alpha!.Value.ToArray(), second.Alpha!.Value.ToArray());
        Assert.True(first.SwapPseudoRoot(4) == second.SwapPseudoRoot(4));

        // a matrix read is a copy
        Matrix root = first.SwapPseudoRoot(4);
        root[0, 0] = 99.0;
        Assert.NotEqual(99.0, first.SwapPseudoRoot(4)[0, 0]);
        first.SwapPseudoRoots()[4][0, 0] = 99.0;
        Assert.NotEqual(99.0, first.SwapPseudoRoots()[4][0, 0]);

        // the facade copies its pseudo-roots and returns copies
        Matrix[] roots = [.. first.SwapPseudoRoots()];
        var facade = new PseudoRootFacade(roots, times, new double[10], new double[10]);
        double kept = facade.PseudoRoot(4)[4, 0];
        roots[4][4, 0] = 99.0;
        facade.PseudoRoot(4)[4, 0] = 99.0;
        Assert.Equal(kept, facade.PseudoRoot(4)[4, 0]);
        CTSMMCapletCalibrationStep step = CTSMMCapletOriginalCalibration.CalibrationFunction(
            evolution, corr, variances, caplets, cs, 0.0, [.. Enumerable.Repeat(-0.05, 10)], true, false, 3);
        step.SwapCovariancePseudoRoots()[0][0, 0] = 99.0;
        Assert.NotEqual(99.0, step.SwapCovariancePseudoRoots()[0][0, 0]);

        // the periodic calibration leaves the caller's specifier as it was
        double[] bigTimes = [.. Enumerable.Range(0, 6).Select(i => times[2 * i])];
        var specifier = new VolatilityInterpolationSpecifierabcd(
            2, 0, [.. Enumerable.Range(0, 5).Select(i => new PiecewiseConstantAbcdVariance(0.0, 0.17, 1.0, 0.1, i, bigTimes))], times);
        double[] variancesBefore = [.. specifier.InterpolatedVariances.SelectMany(v => v.Variances)];
        cs.SetOnForwardRates([.. Enumerable.Range(0, 10).Select(i => 0.03 + (0.0025 * i))]);
        CapletSwaptionPeriodicCalibrationResults periodic = CapletCoterminalPeriodicFunctions.CapletSwaptionPeriodicCalibration(
            evolution, corr, specifier, caplets, cs, 0.0, 1.0, 3, 2, 100, 1e-8, 10, 1e-5, 30, 1e-5);
        Assert.Equal(variancesBefore, specifier.InterpolatedVariances.SelectMany(v => v.Variances).ToArray());
        Assert.NotEqual(1.0, periodic.FinalScales[0]);
        periodic.ModelSwaptionVolsMatrix()[0, 0] = 99.0;
        Assert.NotEqual(99.0, periodic.ModelSwaptionVolsMatrix()[0, 0]);
        periodic.SwapCovariancePseudoRoots()[0][0, 0] = 99.0;
        Assert.NotEqual(99.0, periodic.SwapCovariancePseudoRoots()[0][0, 0]);
    }

    [Fact]
    public void TheOrthogonalProjectionsAreComputedOnce()
    {
        var vectors = new Matrix(3, 4);
        for (int i = 0; i < 3; ++i)
        {
            for (int j = 0; j < 4; ++j)
            {
                vectors[i, j] = 1.0 + i + (i * j * 0.5) + (j * j * 0.1);
            }
        }

        var projections = new OrthogonalProjections(vectors, 100.0, 1e-6);
        double kept = projections.GetVector(1)[2];
        vectors[1, 2] = 99.0;
        Assert.Equal(kept, projections.GetVector(1)[2]);
        Assert.Equal(3, projections.ValidVectors.Length);
    }

    private static FlatVol Model() =>
        new([0.2, 0.22, 0.24], new ExponentialForwardCorrelation(RateTimes), new EvolutionDescription(RateTimes), 2, [0.03, 0.04, 0.05], [0.0, 0.0, 0.0]);
}
