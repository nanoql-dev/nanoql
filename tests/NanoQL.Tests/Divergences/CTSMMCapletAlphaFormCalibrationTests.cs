// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The alpha-form calibration where a step's final part fails, which QuantLib's master refuses after the pin and nanoql
/// refuses in the pass the calibration returns, and its fallback at maximum homogeneity, as the user ruled it on
/// 2026-10-27. <list type="bullet"> <item>A step whose final part fails (S-mm plan MR3 (viii), re-ruled by the upstream
/// sweep's SWR2 (a)): QuantLib 1.44's solve returns it as solved, the swap rate's last volatility left at its buffer's
/// 0, and its calibration returns <c>true</c> with no failure (<c>alphafinder.cpp:335-346</c>, <c>:405-415</c>;
/// <c>capletcoterminalalphacalibration.cpp:202-203</c>, its count commented out beside its throw). In the S-mm code
/// review's 7,800-input sweep, 517 calibrations return so, their swaptions missing by 6.3e-4 to 0.249. One at
/// QuantLib's own α₀ and bounds: displaced 2%, caplets × 0.95, three factors, QuantLib 1.44's swaptions missing by
/// 0.0347198 and its caplet root-mean-square error 0.0109794 (the S-mm run's harness). QuantLib's master's solve
/// returns false there (#2910, <c>dbe958954</c>), so its calibration throws "alpha form failure", and so does nanoql's
/// where the pass it returns has such a step (the sweep's ruling SWI4 (b)): on that sweep 4,091 calibrations at
/// QuantLib's settings, 517 on the linear-hyperbolic form and 3,574 on the inverse-linear one; and so does the
/// inverse-linear form at α₀ 0 in [−1, 1], whose calibration and pass function the vectors held with 1.44's numbers
/// (the generator now has them at ±0.5, where every step solves). Master throws at the first such step in any pass;
/// nanoql counts one in an earlier pass, whose successor re-targets the caplets and can fit, so a calibration whose
/// returned pass fits keeps QuantLib 1.44's numbers: displaced 2%, caplets × 1.2, five factors, α₀ 0.5, which QuantLib
/// converges to a caplet root-mean-square error of 4.6924312876899316e-05 (the run's harness) and master refuses; at
/// α-solving tolerances of 0.05 and 0.2 up to 172 more, at maximum homogeneity too (the sweep's code review's
/// B1-1).</item> <item>Maximum homogeneity (MR3 (x)): the fixed minimization's α at one caplet can leave a later caplet
/// no α where QuantLib's first trial leaves room, so the fixed calibration throws on 50 of the sweep's inputs QuantLib
/// calibrates; the pass then runs again on QuantLib's test of the objective's value, for a caplet with no α, not for a
/// failed final part, which is counted (the trigger the sweep's SWR2 (a) keeps): at α-solving tolerances of 0.01 and
/// above, 147 of 33,600 maximum-homogeneity calibrations have a failed final part at the first pass, and on 18 of them
/// a fallback for it too would complete where nanoql refuses, QuantLib 1.44's numbers on 15 (the sweep's code review's
/// F1-1). On caplets × 0.95 and eight factors QuantLib's caplet error is 3.4739757936758017e-07 and its swaptions exact
/// (the run's harness; the vectors hold the case bit for bit). On QuantLib's case the fixed calibration completes, more
/// homogeneous: its deformation, the sum over rates and steps of the squared change of each step's standard deviation,
/// 0.0010560 against QuantLib's 0.0018891 (the code review's run and the run's harness).</item> </list> Checks: the
/// master's refusal on the half-solved cases; QuantLib's numbers on the fallback; the fit and the deformation on
/// QuantLib's case.
/// </summary>
public class CTSMMCapletAlphaFormCalibrationTests
{
    [Fact]
    public void AHalfSolvedStepIsRefused()
    {
        var (calibration, _, _) = Calibration(0.02, 0.95, false);
        Assert.Equal("alpha form failure", Assert.Throws<NanoQLException>(() => calibration.Calibrate(3, 10, 1e-4, 100, 1e-8)).Message);
    }

    [Fact]
    public void ACalibrationWhoseReturnedPassFitsKeepsQuantLibsNumbers()
    {
        // its first pass half-solved, its second fitting: QuantLib's converged fit, which master refuses (SWI4 (b))
        var (calibration, variances, times) = Calibration(0.02, 1.2, false, alpha0: 0.5);
        CTSMMCapletCalibrationResults r = calibration.Calibrate(5, 10, 1e-4, 100, 1e-8);
        Assert.True(r.Converged);
        Assert.Equal(0, r.Failures);
        Assert.Equal(4.6924312876899316e-05, r.CapletRmsError);
        Assert.True(SwaptionMisfit(r, variances, times) < 1e-14);

        // at maximum homogeneity and an α-solving tolerance of 0.2: 7decee9's fit, the first pass's failed final part
        // counted before the fallback runs on a later caplet with no α
        var (homogeneous, _, _) = Calibration(0.015, 0.95, true, alpha0: -0.5);
        CTSMMCapletCalibrationResults h = homogeneous.Calibrate(7, 10, 1e-4, 100, 0.2);
        Assert.True(h.Converged);
        Assert.Equal(1.189392951314546e-05, h.CapletRmsError);
    }

    [Fact]
    public void AFailedFinalPartIsNotTheFallbacksTrigger()
    {
        // the inverse-linear form at maximum homogeneity, α-solving tolerance 0.2 on five steps: the first pass's
        // failed final part is counted, the fallback runs for no caplet without an α, and the returned pass is refused;
        // a fallback on any failed step would complete at QuantLib 1.44's caplet error, 4.0121717792045765e-07
        double[] times = [.. Enumerable.Range(1, 11).Select(i => 0.5 * i)];
        var (calibration, _, _) = Calibration(0.0, 0.95, true, new AlphaFormInverseLinear(times), alpha0: -0.5);
        Assert.Equal("alpha form failure", Assert.Throws<NanoQLException>(() => calibration.Calibrate(4, 10, 1e-4, 5, 0.2)).Message);
    }

    [Fact]
    public void TheInverseLinearFormsHalfSolvedStepsAreRefused()
    {
        // the vectors' former inverse-linear cases, at alpha0 0 in [-1, 1], the caplets unscaled, three factors: the
        // calibration and the pass function
        double[] times = [.. Enumerable.Range(1, 11).Select(i => 0.5 * i)];
        var (calibration, _, _) = Calibration(0.0, 1.0, false, new AlphaFormInverseLinear(times));
        Assert.Equal("alpha form failure", Assert.Throws<NanoQLException>(() => calibration.Calibrate(3, 10, 1e-4, 100, 1e-8)).Message);

        var cs = new LMMCurveState(times);
        cs.SetOnForwardRates([.. Enumerable.Range(0, 10).Select(i => 0.03 + (0.0025 * i))]);
        var corr = new CotSwapFromFwdCorrelation(new ExponentialForwardCorrelation(times, 0.5, 0.2), cs, 0.0);
        PiecewiseConstantVariance[] variances = [.. Enumerable.Range(0, 10).Select(i => new PiecewiseConstantAbcdVariance(0.0, 0.17, 1.0, 0.1, i, times))];
        double[] caplets = [0.1640, 0.1740, 0.1840, 0.1940, 0.1840, 0.1740, 0.1640, 0.1540, 0.1440, variances[9].TotalVolatility(9)];
        Assert.Equal(
            "alpha form failure",
            Assert.Throws<NanoQLException>(() => CTSMMCapletAlphaFormCalibration.CapletAlphaFormCalibration(
                new EvolutionDescription(times), corr, variances, caplets, cs, 0.0, new double[10], [.. Enumerable.Repeat(1.0, 10)],
                [.. Enumerable.Repeat(-1.0, 10)], false, new AlphaFormInverseLinear(times), 3, 100, 1e-8)).Message);
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
        double displacement, double scale, bool maximizeHomogeneity, AlphaForm? form = null, double alpha0 = 0.0)
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
            [.. Enumerable.Repeat(alpha0, 10)],
            [.. Enumerable.Repeat(1.0, 10)],
            [.. Enumerable.Repeat(-1.0, 10)],
            maximizeHomogeneity,
            form);
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
