// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="AlphaFinder"/>'s three fixes (the S-mm plan's MR3 (ix), (x), (xii), under R8) and its b where a final
/// part fails (MR3 (viii) as the user ruled it on 2026-10-27, and the run's R4-4).
/// <list type="bullet">
/// <item>(viii) <c>alphafinder.cpp:335-346</c>, <c>:405-415</c>: where the turning point's value at α₀ is at most the
/// target, QuantLib returns α₀ without checking <c>finalPart</c>'s result, which fails where the variance left is
/// negative, and its search returns its α unchecked too. nanoql returns QuantLib's α, its b NaN, which the alpha
/// calibration counts as a failure (<c>Divergences/CTSMMCapletAlphaFormCalibrationTests.cs</c>).</item>
/// <item>(ix) <c>:372-373</c>: the downward search's loop tests <c>topValue</c>, which it never changes; on the
/// planner's MP25 inputs QuantLib's <c>solve</c> returns false where the acceptable α lie strictly below α₀, and
/// nanoql's loop, testing <c>bottomValue</c>, finds α −0.3111 at the target; on round 1's m10 inputs (the
/// reviewer's), QuantLib's returns false and nanoql's α 0.41286, whose final part fails.</item>
/// <item>(x) <c>:117</c>, <c>:145</c>: <c>Minimize</c> calls the feasibility test on the objective's value, not on the
/// trial α, so where that value isn't an acceptable α it stops at its first trial: on MP25's inputs QuantLib returns α
/// −0.48497 with homogeneity failure 3.3880, where the acceptable α run from −0.7519 to −0.3112 and the least failure
/// is 3.3628 at −0.7519 (MP25; the run's harness gives QuantLib's alpha-form calibration at maximum homogeneity, and
/// nanoql with QuantLib's test restored reproduces it bit for bit, the run's R4-2); on round 1's m15 inputs, QuantLib's
/// α 0.5168373510089983 fails by 0.12497903935064759 and nanoql's 0.4467697074628475 by 0.12229674463151122, and where
/// the small α QuantLib's test reads are acceptable both return 1.2307430953608208e-08.</item>
/// <item>(xii) <c>:30-111</c>: the bisections and <c>Minimize</c> loop on the bracket's width alone, so at tolerance 0
/// QuantLib's search never ends (MP8: killed after 20 s); nanoql's also stops when the bracket no longer moves.</item>
/// <item>R4-4: where a final part fails QuantLib leaves b at its caller's value; nanoql's is NaN.</item>
/// </list>
/// Checks: QuantLib's arithmetic restated at an α (the turning point's value, the final part, the homogeneity failure),
/// and the alpha-form calibration's QuantLib case at tolerance 0 against 1e-15.
/// </summary>
public class AlphaFinderTests
{
    // MP25's inputs: the hyperbolic form on 0.5, 1, 1.5; the step index 1.
    private static readonly double[] Times = [0.5, 1.0, 1.5];
    private static readonly double[] RateOne = [0.2, 0.2356];
    private static readonly double[] RateTwo = [1.0, 1.0, 1.0];
    private static readonly double[] Correlations = [0.9, 0.9];
    private const double Target = 0.0182;

    [Fact]
    public void AHalfSolvedInitialAlphaIsQuantLibsWithNoB()
    {
        // the variance left at alpha0 negative: alpha0's final part fails although its turning point is below the
        // target
        double[] rateOne = [0.2, 0.2356];
        double[] rateTwo = [0.1, 0.1, 0.01];
        double target = 0.02;
        var form = new AlphaFormLinearHyperbolic(Times);
        Assert.True(Restated.TurningPointValue(form, 0.0, rateOne, rateTwo, Correlations) <= target);
        Assert.False(Restated.FinalPart(form, 0.0, rateOne, rateTwo, Correlations, target, out _));

        double[] vols = [0.5, 0.5, 0.5];
        Assert.True(new AlphaFinder(form).Solve(
            0.0, 1, rateOne, rateTwo, Correlations, 1.0, 1.0, target, 1e-8, 1.0, -1.0, 100, out double alpha, out _, out double b, vols));

        // QuantLib's alpha0, its b NaN where QuantLib leaves the caller's, and the last volatility the caller's
        Assert.Equal(0.0, alpha);
        Assert.True(double.IsNaN(b));
        Assert.Equal(0.5, vols[2]);
    }

    [Fact]
    public void TheDownwardSearchOnRoundOnesInput()
    {
        // m10: the inverse-linear form on 0.5 and 1, alpha0 0.8 in [-0.9, 1], ten steps; QuantLib's solve returns false
        double[] vols = new double[3];
        Assert.True(new AlphaFinder(new AlphaFormInverseLinear([0.5, 1.0])).Solve(
            0.8, 1, [0.2, 0.2], [0.2, 0.2, 0.2], Correlations, 1.0, -0.5, 0.0156, 1e-10, 1.0, -0.9, 10, out double alpha, out _, out double b, vols));
        Assert.Equal(0.41286, alpha, 5);
        Assert.True(double.IsNaN(b));
        Assert.Equal(0.0, vols[2]);
    }

    [Theory]
    [InlineData(0.2, 0.0156, 0.0, -0.9, 1.2307430953608208e-08, 0.10169033548059958)]
    [InlineData(0.2 * 1.45 / 1.225, 0.01525, 0.45, -0.5, 0.4467697074628475, 0.12229674463151122)]
    public void MaximumHomogeneityOnRoundOnesInputs(double h1, double target, double alpha0, double alphaMin, double expected, double expectedFailure)
    {
        // m15: the inverse-linear form on 0.5 and 1; QuantLib's alphas 1.2307430953608208e-08 and 0.5168373510089983
        double[] h = [0.2, h1, 0.4];
        double[] vols = new double[3];
        Assert.True(new AlphaFinder(new AlphaFormInverseLinear([0.5, 1.0])).SolveWithMaxHomogeneity(
            alpha0, 1, [0.2, 0.2], h, Correlations, 1.0, -0.5, target, 1e-8, 1.0, alphaMin, 10, out double alpha, out _, out _, vols));
        Assert.Equal(expected, alpha);
        double failure = Enumerable.Range(0, 3).Sum(i => (vols[i] - h[i]) * (vols[i] - h[i]));
        Assert.Equal(expectedFailure, failure, 15);
    }

    [Fact]
    public void TheDownwardSearchFindsTheAcceptableAlphaBelowAlphaZero()
    {
        var form = new AlphaFormLinearHyperbolic(Times);

        // QuantLib's solve returns false here (MP25); the turning point's values at -1, -0.5, 0 and 1
        Assert.True(Restated.TurningPointValue(form, 0.0, RateOne, RateTwo, Correlations) > Target);
        Assert.True(Restated.TurningPointValue(form, 1.0, RateOne, RateTwo, Correlations) > Target);
        Assert.True(Restated.TurningPointValue(form, -1.0, RateOne, RateTwo, Correlations) > Target);
        Assert.True(Restated.TurningPointValue(form, -0.5, RateOne, RateTwo, Correlations) < Target);

        double[] vols = new double[3];
        Assert.True(new AlphaFinder(form).Solve(
            0.0, 1, RateOne, RateTwo, Correlations, 1.0, 1.0, Target, 1e-8, 1.0, -1.0, 100, out double alpha, out _, out double b, vols));
        Assert.Equal(-0.3111, alpha, 4);
        Assert.Equal(Target, Restated.TurningPointValue(form, alpha, RateOne, RateTwo, Correlations), 1e-9);
        Assert.True(double.IsFinite(b));
        Assert.Equal(Target, Restated.CapletVariance(RateOne, vols, Correlations), 1e-9);
    }

    [Fact]
    public void MaximumHomogeneityFindsTheAcceptableMinimum()
    {
        var form = new AlphaFormLinearHyperbolic(Times);
        double[] vols = new double[3];
        Assert.True(new AlphaFinder(form).SolveWithMaxHomogeneity(
            0.0, 1, RateOne, RateTwo, Correlations, 1.0, 1.0, Target, 1e-8, 1.0, -1.0, 100, out double alpha, out _, out _, vols));

        // an acceptable alpha, its failure the restated one
        Assert.True(Restated.Acceptable(form, alpha, RateOne, RateTwo, Correlations, Target));
        double failure = Enumerable.Range(0, 3).Sum(i => (vols[i] - RateTwo[i]) * (vols[i] - RateTwo[i]));
        Assert.Equal(Restated.HomogeneityFailure(form, alpha, RateOne, RateTwo, Correlations, Target), failure, 1e-12);

        // QuantLib's alpha, -0.48497, fails by 3.3880; the acceptable grid's least failure is nanoql's
        double quantLibs = Restated.HomogeneityFailure(form, -0.48497, RateOne, RateTwo, Correlations, Target);
        Assert.Equal(3.3880, quantLibs, 4);
        Assert.Equal(3.363, failure, 3);
        Assert.True(failure < quantLibs);
        double gridLeast = Enumerable.Range(0, 20001)
            .Select(k => -1.0 + (k * 1e-4))
            .Where(x => Restated.Acceptable(form, x, RateOne, RateTwo, Correlations, Target))
            .Min(x => Restated.HomogeneityFailure(form, x, RateOne, RateTwo, Correlations, Target));
        Assert.True(failure <= gridLeast + 1e-6, $"{failure} against the grid's {gridLeast}");
    }

    [Fact]
    public async Task ToleranceZeroEnds()
    {
        // QuantLib's case: QuantLib's search at tolerance 0 never ends (MP8)
        double[] times = [.. Enumerable.Range(1, 11).Select(i => 0.5 * i)];
        var cs = new LMMCurveState(times);
        cs.SetOnForwardRates([.. Enumerable.Range(0, 10).Select(i => 0.03 + (0.0025 * i))]);
        var corr = new CotSwapFromFwdCorrelation(new ExponentialForwardCorrelation(times, 0.5, 0.2), cs, 0.0);
        PiecewiseConstantVariance[] variances = [.. Enumerable.Range(0, 10).Select(i => new PiecewiseConstantAbcdVariance(0.0, 0.17, 1.0, 0.1, i, times))];
        double[] caplets = [0.1640, 0.1740, 0.1840, 0.1940, 0.1840, 0.1740, 0.1640, 0.1540, 0.1440, variances[9].TotalVolatility(9)];
        var evolution = new EvolutionDescription(times);
        double[] zero = new double[10];
        double[] up = [.. Enumerable.Repeat(1.0, 10)];
        double[] down = [.. Enumerable.Repeat(-1.0, 10)];
        var form = new AlphaFormLinearHyperbolic(times);

        // run where a search that never ends fails the fact rather than the run
        var exactRun = Task.Run(() => CTSMMCapletAlphaFormCalibration.CapletAlphaFormCalibration(
            evolution, corr, variances, caplets, cs.Clone(), 0.0, zero, up, down, false, new AlphaFormLinearHyperbolic(times), 3, 100, 0.0));
        CTSMMCapletCalibrationStep exact = await exactRun.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
        CTSMMCapletCalibrationStep fine = CTSMMCapletAlphaFormCalibration.CapletAlphaFormCalibration(
            evolution, corr, variances, caplets, cs, 0.0, zero, up, down, false, form, 3, 100, 1e-15);
        for (int i = 0; i < 10; ++i)
        {
            Assert.Equal(fine.Alpha!.Value[i], exact.Alpha!.Value[i], 1e-12);
        }

        // MP25's maximum homogeneity at tolerance 0
        var homogeneous = Task.Run(() =>
        {
            double[] vols = new double[3];
            Assert.True(new AlphaFinder(new AlphaFormLinearHyperbolic(Times)).SolveWithMaxHomogeneity(
                0.0, 1, RateOne, RateTwo, Correlations, 1.0, 1.0, Target, 0.0, 1.0, -1.0, 100, out double alpha, out _, out _, vols));
            return alpha;
        });
        Assert.Equal(-0.7519, await homogeneous.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken), 4);
    }

    // QuantLib's AlphaFinder arithmetic at an alpha, restated (alphafinder.cpp:183-208, :210-229, :232-273), w0 = w1 =
    // 1.
    private static class Restated
    {
        public static double TurningPointValue(AlphaForm form, double alpha, double[] rateOne, double[] rateTwo, double[] rho)
        {
            Parts(form, alpha, rateOne, rateTwo, rho, out double q, out double l, out double c);
            return new Quadratic(q, l, c).ValueAtTurningPoint();
        }

        public static bool FinalPart(
            AlphaForm form, double alpha, double[] rateOne, double[] rateTwo, double[] rho, double target, out double[] vols)
        {
            Parts(form, alpha, rateOne, rateTwo, rho, out double q, out double l, out double c);
            new Quadratic(q, l, c - target).Roots(out double a, out _);
            form.SetAlpha(alpha);
            vols = new double[3];
            double soFar = 0.0;
            for (int i = 0; i < 2; ++i)
            {
                vols[i] = rateTwo[i] * form.Evaluate(i) * a;
                soFar += vols[i] * vols[i];
            }

            double left = rateTwo.Sum(h => h * h) - soFar;
            if (left < 0)
            {
                return false;
            }

            vols[2] = Math.Sqrt(left);
            return true;
        }

        public static bool Acceptable(AlphaForm form, double alpha, double[] rateOne, double[] rateTwo, double[] rho, double target) =>
            TurningPointValue(form, alpha, rateOne, rateTwo, rho) <= target && FinalPart(form, alpha, rateOne, rateTwo, rho, target, out _);

        public static double HomogeneityFailure(
            AlphaForm form, double alpha, double[] rateOne, double[] rateTwo, double[] rho, double target)
        {
            FinalPart(form, alpha, rateOne, rateTwo, rho, target, out double[] vols);
            return Enumerable.Range(0, 3).Sum(i => (vols[i] - rateTwo[i]) * (vols[i] - rateTwo[i]));
        }

        // the caplet's variance from the second rate's new volatilities: w0² Σ v1² + 2 w0 w1 Σ v1 v2 ρ + w1² Σ v2²
        public static double CapletVariance(double[] rateOne, double[] vols, double[] rho)
        {
            double v = 0.0;
            for (int i = 0; i < 2; ++i)
            {
                v += (rateOne[i] * rateOne[i]) + (2 * rateOne[i] * vols[i] * rho[i]) + (vols[i] * vols[i]);
            }

            return v;
        }

        private static void Parts(
            AlphaForm form, double alpha, double[] rateOne, double[] rateTwo, double[] rho, out double q, out double l, out double c)
        {
            form.SetAlpha(alpha);
            q = l = c = 0.0;
            for (int i = 0; i < 2; ++i)
            {
                double vol2 = rateTwo[i] * form.Evaluate(i);
                q += vol2 * vol2;
                l += 2 * vol2 * rateOne[i] * rho[i];
                c += rateOne[i] * rateOne[i];
            }
        }
    }
}
