// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The alpha-form calibration's two departures, as the user ruled them on 2026-10-27.
/// <list type="bullet">
/// <item>A step whose final part fails (S-mm plan MR3 (viii)): QuantLib's solve returns it as solved, the swap rate's
/// last volatility left at its buffer's 0, and its calibration returns <c>true</c> with no failure
/// (<c>alphafinder.cpp:335-346</c>, <c>:405-415</c>; <c>capletcoterminalalphacalibration.cpp:202-203</c>, its count
/// commented out beside its throw). In the code review's 7,800-input sweep, 517 calibrations return so, their swaptions
/// missing by 6.3e-4 to 0.249. One at QuantLib's own α₀ and bounds: displaced 2%, caplets × 0.95, three factors,
/// QuantLib's swaptions missing by 0.0347198 and its caplet root-mean-square error 0.0109794 (the run's harness).
/// nanoql returns QuantLib's numbers, counting each such step as a failure, so <c>Converged</c> is false.</item>
/// <item>Maximum homogeneity (MR3 (x)): the fixed minimization's α at one caplet can leave a later caplet no α where
/// QuantLib's first trial leaves room, so the fixed calibration throws on 50 of the sweep's inputs QuantLib calibrates;
/// the pass then runs again on QuantLib's test of the objective's value. On caplets × 0.95 and eight factors QuantLib's
/// caplet error is 3.4739757936758017e-07 and its swaptions exact (the run's harness; the vectors hold the case bit for
/// bit). On QuantLib's case the fixed calibration completes, more homogeneous: its deformation, the sum over rates and
/// steps of the squared change of each step's standard deviation, 0.0010560 against QuantLib's 0.0018891 (the code
/// review's run and the run's harness).</item>
/// </list>
/// Checks: QuantLib's numbers on the half-solved case and the fallback; the fit and the deformation on QuantLib's case.
/// </summary>
public class CTSMMCapletAlphaFormCalibrationTests
{
    [Fact]
    public void AHalfSolvedStepIsAFailure()
    {
        var (calibration, variances, times) = Calibration(0.02, 0.95, false);
        CTSMMCapletCalibrationResults r = calibration.Calibrate(3, 10, 1e-4, 100, 1e-8);
        Assert.False(r.Converged);
        Assert.True(r.Failures >= 1);
        Assert.Equal(0.0347198, SwaptionMisfit(r, variances, times), 7);
        Assert.Equal(0.0109794, r.CapletRmsError, 7);
    }

    [Fact]
    public void MaximumHomogeneityOnQuantLibsCaseIsMoreHomogeneous()
    {
        var (calibration, variances, times) = Calibration(0.0, 1.0, true);
        CTSMMCapletCalibrationResults r = calibration.Calibrate(3, 10, 1e-4, 100, 1e-8);
        Assert.True(r.Converged);
        Assert.True(SwaptionMisfit(r, variances, times) < 1e-14);
        Assert.True(r.CapletMaxError < 1e-4);

        double deformation = 0.0;
        for (int i = 0; i < 10; ++i)
        {
            for (int k = 0; k < 10; ++k)
            {
                double tau = k == 0 ? times[0] : times[k] - times[k - 1];
                double change = (r.TimeDependentCalibratedSwaptionVols(i)[k] - calibration.TimeDependentUnCalibratedSwaptionVols(i)[k]) * Math.Sqrt(tau);
                deformation += change * change;
            }
        }

        Assert.Equal(0.0010560191938004228, deformation, 15);
        Assert.True(deformation < 0.0018890664330702398);
    }

    [Fact]
    public void WhereTheFixLeavesACapletNoAlphaQuantLibsTestRuns()
    {
        var (calibration, variances, times) = Calibration(0.0, 0.95, true);
        CTSMMCapletCalibrationResults r = calibration.Calibrate(8, 10, 1e-4, 100, 1e-8);
        Assert.True(r.Converged);
        Assert.Equal(3.4739757936758017e-07, r.CapletRmsError);
        Assert.True(SwaptionMisfit(r, variances, times) < 1e-14);
    }

    private static (CTSMMCapletAlphaFormCalibration Calibration, PiecewiseConstantVariance[] Variances, double[] Times) Calibration(
        double displacement, double scale, bool maximizeHomogeneity)
    {
        double[] times = [.. Enumerable.Range(1, 11).Select(i => 0.5 * i)];
        var cs = new LMMCurveState(times);
        cs.SetOnForwardRates([.. Enumerable.Range(0, 10).Select(i => 0.03 + (0.0025 * i))]);
        var corr = new CotSwapFromFwdCorrelation(new ExponentialForwardCorrelation(times, 0.5, 0.2), cs, displacement);
        PiecewiseConstantVariance[] variances = [.. Enumerable.Range(0, 10).Select(i => new PiecewiseConstantAbcdVariance(0.0, 0.17, 1.0, 0.1, i, times))];
        double[] caplets = [.. new[] { 0.1640, 0.1740, 0.1840, 0.1940, 0.1840, 0.1740, 0.1640, 0.1540, 0.1440 }.Select(x => x * scale), variances[9].TotalVolatility(9)];
        var calibration = new CTSMMCapletAlphaFormCalibration(
            new EvolutionDescription(times),
            corr,
            variances,
            caplets,
            cs,
            displacement,
            new double[10],
            [.. Enumerable.Repeat(1.0, 10)],
            [.. Enumerable.Repeat(-1.0, 10)],
            maximizeHomogeneity);
        return (calibration, variances, times);
    }

    // The swaptions' largest miss: each rate's total volatility from the pseudo-roots against its variance's.
    private static double SwaptionMisfit(CTSMMCapletCalibrationResults r, PiecewiseConstantVariance[] variances, double[] times)
    {
        var total = new Matrix(10, 10, 0.0);
        double worst = 0.0;
        for (int i = 0; i < 10; ++i)
        {
            total += r.SwapPseudoRoot(i) * MatrixFunctions.Transpose(r.SwapPseudoRoot(i));
            worst = Math.Max(worst, Math.Abs(Math.Sqrt(total[i, i] / times[i]) - variances[i].TotalVolatility(i)));
        }

        return worst;
    }
}
