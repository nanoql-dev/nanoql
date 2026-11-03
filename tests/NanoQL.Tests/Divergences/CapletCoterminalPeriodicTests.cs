// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The periodic calibration's failures and passes (fixed). QuantLib returns the inner calibration's <c>calibrate</c>
/// value, a <c>bool</c>, as its failure count (<c>capletcoterminalperiodic.cpp:125-126</c>), and its
/// <c>++iterationsDone</c> runs only when the loop goes on (<c>:171</c>), one short of the passes when the errors end
/// it: on QuantLib's case it returns 1, its inner calibration's <c>failures()</c> 0, and at caplets × 0.7 it returns 0
/// with 3 failures, each counting 5 passes with 6 rows of its volatility matrix filled. nanoql returns the inner
/// failures and the passes run. Checks: the inner calibration's own failures on the last pass's variances, and the
/// matrix's filled rows.
/// </summary>
public class CapletCoterminalPeriodicTests
{
    [Theory]
    [InlineData(1.0, 0)]
    [InlineData(0.7, 3)]
    public void TheFailuresAreTheInnerCalibrationsAndThePassesTheRowsFilled(double scale, int failures)
    {
        double[] times = [.. Enumerable.Range(1, 11).Select(i => 0.5 * i)];
        var cs = new LMMCurveState(times);
        cs.SetOnForwardRates([.. Enumerable.Range(0, 10).Select(i => 0.03 + (0.0025 * i))]);
        var corr = new CotSwapFromFwdCorrelation(new ExponentialForwardCorrelation(times, 0.5, 0.2), cs, 0.0);
        var evolution = new EvolutionDescription(times);
        double[] bigTimes = [.. Enumerable.Range(0, 6).Select(i => times[2 * i])];
        PiecewiseConstantAbcdVariance[] longs = [.. Enumerable.Range(0, 5).Select(i => new PiecewiseConstantAbcdVariance(0.0, 0.17, 1.0, 0.1, i, bigTimes))];
        var specifier = new VolatilityInterpolationSpecifierabcd(2, 0, longs, times);
        double last = new PiecewiseConstantAbcdVariance(0.0, 0.17, 1.0, 0.1, 9, times).TotalVolatility(9);
        double[] caplets = [.. new[] { 0.1640, 0.1740, 0.1840, 0.1940, 0.1840, 0.1740, 0.1640, 0.1540, 0.1440 }.Select(x => x * scale), last];

        CapletSwaptionPeriodicCalibrationResults r = CapletCoterminalPeriodicFunctions.CapletSwaptionPeriodicCalibration(
            evolution, corr, specifier, caplets, cs, 0.0, 1.0, 3, 2, 100, 1e-8, 10, 1e-5, 30, 1e-5);

        Assert.Equal(failures, r.Failures);
        Assert.Null(r.DeformationSize);

        // the passes: the rows filled, the rest zero
        int filled = Enumerable.Range(0, 30).Count(k => r.ModelSwaptionVolsMatrix().Row(k).ToArray().Any(x => x != 0.0));
        Assert.Equal(filled, r.IterationsDone);
        Assert.True(r.IterationsDone < 30);

        // the inner calibration on the last pass's scales: its failures the periodic's
        VolatilityInterpolationSpecifier scaled = specifier.WithLastCapletVol(caplets[^1]).WithScalingFactors(Unscaled(r, specifier));
        var inner = new CTSMMCapletMaxHomogeneityCalibration(evolution, corr, scaled.InterpolatedVariances.AsSpan(), caplets, cs, 0.0, 1.0);
        Assert.Equal(failures, inner.Calibrate(3, 10, 1e-5, 100, 1e-8).Failures);
    }

    // The last pass's scales: the final scales less that pass's own rescaling, from its row of model volatilities.
    private static double[] Unscaled(CapletSwaptionPeriodicCalibrationResults r, VolatilityInterpolationSpecifier specifier)
    {
        double[] row = r.ModelSwaptionVolsMatrix().Row(r.IterationsDone - 1).ToArray();
        return [.. Enumerable.Range(0, r.FinalScales.Length)
            .Select(i => r.FinalScales[i] / (specifier.OriginalVariances[i].TotalVolatility(i) / row[i]))];
    }
}
