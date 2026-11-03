// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The calibrations' deformation size. QuantLib's maximally homogeneous calibration sums, for each caplet i, the
/// squared change of the step index's own volatility once per moved volatility, <c>(theseNewVols[i] −
/// secondRateVols[i])²</c> for each <c>j &lt; i + 2</c> (<c>capletcoterminalmaxhomogeneity.cpp:370-371</c>), where its
/// loop runs over j: nanoql sums each moved volatility's own squared change (fixed; QuantLib's tests only print it).
/// And QuantLib's alpha-form and original calibrations never compute it, returning the initializer's 987654321
/// (<c>ctsmmcapletcalibration.cpp:131</c>, <c>:199</c>), nor its periodic calibration: nanoql's is null there. Checks:
/// the deformation rebuilt from the results' calibrated and given volatilities, each step's volatility times the root
/// of its length; QuantLib's sum restated on the same volatilities, 0.00177 on QuantLib's case.
/// </summary>
public class CTSMMCapletMaxHomogeneityDeformationTests
{
    [Fact]
    public void TheDeformationSumsEachMovedVolatilitysChange()
    {
        var (evolution, corr, variances, caplets, cs) = QuantLibsCase(1.0);
        var calibration = new CTSMMCapletMaxHomogeneityCalibration(evolution, corr, variances, caplets, cs, 0.0, 1.0);
        CTSMMCapletCalibrationResults r = calibration.Calibrate(3, 10, 1e-4, 100, 1e-8);

        double[] times = [.. evolution.RateTimes];
        double Step(int j) => j == 0 ? times[0] : times[j] - times[j - 1];
        double fixedSum = 0.0, quantLibs = 0.0;
        for (int i = 0; i < times.Length - 2; ++i)
        {
            for (int j = 0; j < i + 2; ++j)
            {
                double moved = r.TimeDependentCalibratedSwaptionVols(i + 1)[j] * Math.Sqrt(Step(j));
                double given = calibration.TimeDependentUnCalibratedSwaptionVols(i + 1)[j] * Math.Sqrt(Step(j));
                fixedSum += (moved - given) * (moved - given);
            }

            double ownMoved = r.TimeDependentCalibratedSwaptionVols(i + 1)[i] * Math.Sqrt(Step(i));
            double ownGiven = calibration.TimeDependentUnCalibratedSwaptionVols(i + 1)[i] * Math.Sqrt(Step(i));
            quantLibs += (i + 2) * (ownMoved - ownGiven) * (ownMoved - ownGiven);
        }

        Assert.Equal(fixedSum, r.DeformationSize!.Value, 1e-12);
        Assert.Equal(0.00177, quantLibs, 5);
        Assert.NotEqual(quantLibs, fixedSum, 1e-6);
    }

    [Fact]
    public void TheAlphaFormAndOriginalCalibrationsHaveNone()
    {
        var (evolution, corr, variances, caplets, cs) = QuantLibsCase(1.0);
        var alpha = new CTSMMCapletAlphaFormCalibration(
            evolution, corr, variances, caplets, cs, 0.0, new double[10], [.. Enumerable.Repeat(1.0, 10)], [.. Enumerable.Repeat(-1.0, 10)], false);
        Assert.Null(alpha.Calibrate(3, 10, 1e-4, 100, 1e-8).DeformationSize);
        var original = new CTSMMCapletOriginalCalibration(
            evolution, corr, variances, caplets, cs, 0.0, [.. Enumerable.Repeat(-0.05, 10)], true, false);
        Assert.Null(original.Calibrate(3, 2, 1e-5, 50, 1e-9).DeformationSize);
    }

    private static (EvolutionDescription Evolution, PiecewiseConstantCorrelation Corr, PiecewiseConstantVariance[] Variances,
        double[] Caplets, LMMCurveState Cs) QuantLibsCase(double scale)
    {
        double[] times = [.. Enumerable.Range(1, 11).Select(i => 0.5 * i)];
        var cs = new LMMCurveState(times);
        cs.SetOnForwardRates([.. Enumerable.Range(0, 10).Select(i => 0.03 + (0.0025 * i))]);
        var corr = new CotSwapFromFwdCorrelation(new ExponentialForwardCorrelation(times, 0.5, 0.2), cs, 0.0);
        PiecewiseConstantVariance[] variances = [.. Enumerable.Range(0, 10).Select(i => new PiecewiseConstantAbcdVariance(0.0, 0.17, 1.0, 0.1, i, times))];
        double[] caplets = [.. new[] { 0.1640, 0.1740, 0.1840, 0.1940, 0.1840, 0.1740, 0.1640, 0.1540, 0.1440 }.Select(x => x * scale), variances[9].TotalVolatility(9)];
        return (new EvolutionDescription(times), corr, variances, caplets, cs);
    }
}
