// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The extended Black-Scholes process's Milstein, Euler and predictor steps in the log price (fixed). QuantLib's
/// Milstein adds the asset's correction ½σ²(ΔW² − 1)Δt to the log price, its one-step mean 1.245e-3 high at Δt 0.25 and
/// σ 0.4; its Euler step and its predictor centre on the asset's mean x₀e^((r−q)Δt), which lacks the −σ²Δt/2, 2.02e-2
/// high a step there, and refuse a strike-dependent volatility ("not implemented"). On QuantLib 1.44 (x₀ 100, r 0.03, q
/// 0, a flat 0.4, Δt 0.25, ΔW from −2 to 2) Milstein steps to 70.29285698, 80.85603163, 96.80224498, 120.6230249 and
/// 156.4396302, Euler to 67.53663464, 82.48943182, 100.7528195, 123.0597717 and 150.3055446, and the
/// predictor-corrector, the exact step there, to 66.19931967, 80.85603163, 98.75778005, 120.6230249 and 147.3292954.
/// nanoql's three steps are that exact step, their one-step mean e^((r−q)Δt), and the predictor-corrector runs on a
/// smile. There QuantLib's corrector, its diffusion the mean of the two ends' on the same increment, would converge to
/// the Stratonovich equation, its error flat in the step count (9.7e-2 at 4, 16 and 64 steps on the first smile below,
/// E[S_T] 0.097 low, and 0.565 low on the second); nanoql's takes each end's drift less ½σ·∂σ/∂ln S. On two SABR smiles
/// each scheme converges, on the same Brownian increments, to a fine Milstein path of 16,384 steps, itself checked
/// against a fine Euler path: Euler at order ½, Milstein and the predictor-corrector at order 1.
/// </summary>
public class ExtendedBlackScholesProcessTests
{
    private static readonly Date Today = new(15, Month.June, 2026);

    [Theory]
    [InlineData(ExtendedBlackScholesMertonProcessDiscretization.Milstein)]
    [InlineData(ExtendedBlackScholesMertonProcessDiscretization.Euler)]
    [InlineData(ExtendedBlackScholesMertonProcessDiscretization.PredictorCorrector)]
    public void EachStepIsTheExactStepOnFlatParameters(ExtendedBlackScholesMertonProcessDiscretization scheme)
    {
        ExtendedBlackScholesMertonProcess process = Flat(scheme);
        double[] dw = [-2, -1, 0, 1, 2];
        double[] quantLibExact = [66.19931967, 80.85603163, 98.75778005, 120.6230249, 147.3292954];
        for (int i = 0; i < dw.Length; i++)
        {
            double step = process.Evolve(0.0, 100.0, 0.25, dw[i]);
            Assert.True(Math.Abs(step - quantLibExact[i]) <= 1e-9 * quantLibExact[i], $"{scheme} {dw[i]}: {step}");
        }

        // QuantLib's Milstein and Euler steps at a draw of 0
        Assert.True(Math.Abs(process.Evolve(0.0, 100.0, 0.25, 0.0) - 96.80224498) > 1.9);
        Assert.True(Math.Abs(process.Evolve(0.0, 100.0, 0.25, 0.0) - 100.7528195) > 1.9);
    }

    [Theory]
    [InlineData(ExtendedBlackScholesMertonProcessDiscretization.Milstein)]
    [InlineData(ExtendedBlackScholesMertonProcessDiscretization.Euler)]
    public void TheOneStepMeanIsTheForward(ExtendedBlackScholesMertonProcessDiscretization scheme)
    {
        ExtendedBlackScholesMertonProcess process = Flat(scheme);

        // E[S₁/S₀] over a standard normal draw, ∫ e^(−x²) f(√2 x) dx / √π, by the 64-point Gauss-Hermite rule, whose
        // weights integrate f itself, as QuantLib's
        var gh = new GaussHermiteIntegration(64);
        double mean = gh.Evaluate(x => Math.Exp(-x * x) * process.Evolve(0.0, 100.0, 0.25, Math.Sqrt(2.0) * x) / 100.0)
            / Math.Sqrt(Math.PI);
        double forward = Math.Exp(0.03 * 0.25);
        Assert.True(Math.Abs(mean / forward - 1.0) <= 1e-12, $"{mean} for {forward}");
    }

    [Fact]
    public void ThePredictorCorrectorRunsOnASmile()
    {
        // QuantLib's predictor refuses a strike-dependent volatility ("not implemented")
        BlackVolTermStructure smile = new SABRVolTermStructure(0.2, 0.5, 0.2, -0.9, 100.0, 0.03, Today, new Actual365Fixed());
        var process = new ExtendedBlackScholesMertonProcess(
            100.0,
            new FlatForward(Today, 0.0, new Actual365Fixed()),
            new FlatForward(Today, 0.03, new Actual365Fixed()),
            smile,
            evolDisc: ExtendedBlackScholesMertonProcessDiscretization.PredictorCorrector);
        double step = process.Evolve(0.0, 100.0, 0.25, 0.5);
        Assert.True(double.IsFinite(step) && step > 0.0);

        // the corrector: the two ends' drifts, each less ½σ·∂σ/∂ln S, and diffusions, averaged
        double DSigma(double t, double x) =>
            (smile.BlackVol(t, x * Math.Exp(1e-4), true) - smile.BlackVol(t, x * Math.Exp(-1e-4), true)) / 2e-4;
        double sigma0 = process.Diffusion(0.0, 100.0);
        double predictor = 100.0 * Math.Exp((process.Drift(0.0, 100.0) * 0.25) + (sigma0 * Math.Sqrt(0.25) * 0.5));
        double sigma1 = process.Diffusion(0.25, predictor);
        double rate0 = 0.03 - (0.5 * sigma0 * sigma0) - (0.5 * sigma0 * DSigma(0.0, 100.0));
        double rate1 = 0.03 - (0.5 * sigma1 * sigma1) - (0.5 * sigma1 * DSigma(0.25, predictor));
        double corrected = 100.0 * Math.Exp((0.5 * (rate0 + rate1) * 0.25) + (0.5 * (sigma0 + sigma1) * Math.Sqrt(0.25) * 0.5));
        Assert.True(Math.Abs(step - corrected) <= 1e-12 * corrected, $"{step} against {corrected}");

        // the Euler step on the smile is the log-Euler step on the process's drift and diffusion
        var euler = new ExtendedBlackScholesMertonProcess(
            100.0,
            new FlatForward(Today, 0.0, new Actual365Fixed()),
            new FlatForward(Today, 0.03, new Actual365Fixed()),
            smile,
            evolDisc: ExtendedBlackScholesMertonProcessDiscretization.Euler);
        double expected = 100.0 * Math.Exp((euler.Drift(0.0, 100.0) * 0.25) + (euler.Diffusion(0.0, 100.0) * Math.Sqrt(0.25) * 0.5));
        Assert.Equal(expected, euler.Evolve(0.0, 100.0, 0.25, 0.5), 12);
    }

    [Theory]
    [InlineData(0.2, 0.5, 0.2, -0.9)]
    [InlineData(0.3, 0.7, 0.6, -0.5)]
    public void EachSchemeConvergesToTheProcessOnASmile(double alpha, double beta, double nu, double rho)
    {
        BlackVolTermStructure smile = new SABRVolTermStructure(alpha, beta, nu, rho, 100.0, 0.03, Today, new Actual365Fixed());
        ExtendedBlackScholesMertonProcess Process(ExtendedBlackScholesMertonProcessDiscretization scheme) => new(
            100.0,
            new FlatForward(Today, 0.0, new Actual365Fixed()),
            new FlatForward(Today, 0.03, new Actual365Fixed()),
            smile,
            evolDisc: scheme);
        ExtendedBlackScholesMertonProcess[] schemes =
        [
            Process(ExtendedBlackScholesMertonProcessDiscretization.Euler),
            Process(ExtendedBlackScholesMertonProcessDiscretization.Milstein),
            Process(ExtendedBlackScholesMertonProcessDiscretization.PredictorCorrector),
        ];

        // 100 paths to a year, each scheme at 4 and 64 steps on the sums of a fine path's increments
        const int paths = 100, fine = 16384;
        int[] steps = [4, 64];
        var error = new double[3, 2];
        var mean = new double[3, 2];
        double referenceGap = 0.0;
        var rng = new MersenneTwisterUniformRng(42);
        var z = new double[fine];
        for (int p = 0; p < paths; p++)
        {
            for (int k = 0; k < fine; k++)
            {
                z[k] = InverseCumulativeNormal.StandardValue(rng.NextReal());
            }

            // the fine Milstein path, the reference, and the fine Euler path that checks it
            double dtFine = 1.0 / fine, euler = 100.0, reference = 100.0;
            for (int k = 0; k < fine; k++)
            {
                euler = schemes[0].Evolve(k * dtFine, euler, dtFine, z[k]);
                reference = schemes[1].Evolve(k * dtFine, reference, dtFine, z[k]);
            }

            referenceGap += (euler - reference) * (euler - reference);
            for (int si = 0; si < steps.Length; si++)
            {
                int n = steps[si], m = fine / n;
                double dt = 1.0 / n;
                double[] x = [100.0, 100.0, 100.0];
                for (int j = 0; j < n; j++)
                {
                    double sum = 0.0;
                    for (int k = j * m; k < (j + 1) * m; k++)
                    {
                        sum += z[k];
                    }

                    for (int q = 0; q < 3; q++)
                    {
                        x[q] = schemes[q].Evolve(j * dt, x[q], dt, sum / Math.Sqrt(m));
                    }
                }

                for (int q = 0; q < 3; q++)
                {
                    error[q, si] += (x[q] - reference) * (x[q] - reference);
                    mean[q, si] += (x[q] - reference) / paths;
                }
            }
        }

        double Rms(int q, int si) => Math.Sqrt(error[q, si] / paths);

        // the fine Euler path meets the reference within its own order-½ error at 16,384 steps
        Assert.True(Math.Sqrt(referenceGap / paths) < 0.125 * Rms(0, 1), $"the reference: {Math.Sqrt(referenceGap / paths)}");

        // 16 times the steps: Euler's error a quarter (order ½), Milstein's and the predictor-corrector's a sixteenth
        Assert.True(Rms(0, 0) / Rms(0, 1) > 3.0, $"Euler: {Rms(0, 0)} to {Rms(0, 1)}");
        Assert.True(Rms(1, 0) / Rms(1, 1) > 10.0, $"Milstein: {Rms(1, 0)} to {Rms(1, 1)}");
        Assert.True(Rms(2, 0) / Rms(2, 1) > 10.0, $"predictor-corrector: {Rms(2, 0)} to {Rms(2, 1)}");

        // and the predictor-corrector's mean meets the process's, where QuantLib's corrector's stays 0.097 and 0.565 low
        Assert.True(Math.Abs(mean[2, 1]) < 2.5e-3, $"predictor-corrector's mean: {mean[2, 1]}");
    }

    private static ExtendedBlackScholesMertonProcess Flat(ExtendedBlackScholesMertonProcessDiscretization scheme) =>
        new(
            100.0,
            new FlatForward(Today, 0.0, new Actual365Fixed()),
            new FlatForward(Today, 0.03, new Actual365Fixed()),
            new BlackConstantVol(Today, new NullCalendar(), 0.4, new Actual365Fixed()),
            evolDisc: scheme);
}
