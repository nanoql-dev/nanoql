// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The square-root volatility process's first step (the S-mm plan's MR3 (xvii), fixed under R8). QuantLib's
/// <c>SquareRootAndersen</c> fills the first evolution step's sub-step lengths but not their e^{−κΔt}, left 0
/// (<c>squarerootandersen.cpp:51-52</c>), so the first step reverts in full: from v₀ 0.16, θ 0.04, κ 1, ε 0.5 the mean
/// of v at 0.5 over 20,000 Mersenne-twister paths is 0.0391 against CIR's 0.1128 (this run's
/// <c>probes\c2_facts.cpp</c>). QuantLib's <c>testStochVolForwardsAndOptionlets</c> prices the SVD-DD evolver's caplets
/// against
/// <c>AnalyticHestonEngine</c> within 4 standard errors at θ = v₀ = 1 (<c>marketmodel.cpp:4326</c>, <c>:4330</c>),
/// where the first step can't show; at v₀ 0.25, v₀ 2 or θ 0.5 its caplets are 6.3 to 38.6 standard errors off, and with
/// the first step filled within 1.71 (this run's copy of the S-mm plan's m6 probe, <c>probes\m6.out</c>). nanoql takes
/// e^{−κΔt} for the first step's sub-steps too. Checks: QuantLib's own process where its e^{−κΔt} underflows to 0
/// itself (the vectors), bit for bit; CIR's mean and variance at the first step; and QuantLib's test at v₀ ≠ θ.
/// </summary>
public class SquareRootAndersenFirstStepTests
{
    [Fact]
    public void TheFirstStepHasCirsMoments()
    {
        const double theta = 0.04, kappa = 1.0, eps = 0.5, v0 = 0.16, t = 0.5;
        const int paths = 20000, subSteps = 8;
        var process = new SquareRootAndersen(theta, kappa, eps, v0, [t, 1.0], subSteps, 0.5, 0.5);
        var generator = new MTBrownianGenerator(subSteps, 2, 42);
        double[] z = new double[subSteps];
        double sum = 0.0, sum2 = 0.0;
        for (int i = 0; i < paths; ++i)
        {
            generator.NextPath();
            process.NextPath();
            generator.NextStep(z);
            process.Nextstep(z);
            double v = process.StateVariables()[0];
            sum += v;
            sum2 += v * v;
        }

        double mean = sum / paths, variance = (sum2 / paths) - (mean * mean);
        double e = Math.Exp(-kappa * t);
        double cirMean = theta + ((v0 - theta) * e);
        double cirVariance = (v0 * eps * eps * e * (1 - e) / kappa) + (theta * eps * eps * (1 - e) * (1 - e) / (2 * kappa));
        double standardError = Math.Sqrt(variance / paths);
        Assert.True(Math.Abs(mean - cirMean) < 4 * standardError, $"mean {mean}, CIR {cirMean}, standard error {standardError}");
        Assert.Equal(cirVariance, variance, 0.1 * cirVariance);

        // QuantLib's mean, the first step reverting in full (this run's probes\c2_facts.cpp)
        Assert.True(Math.Abs(0.039072494604281374 - cirMean) > 100 * standardError);
    }

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(1.0, 0.25)]
    [InlineData(1.0, 2.0)]
    [InlineData(0.5, 1.0)]
    public void TheStochasticVolatilityCapletsAreHestonsAtAnyStartingVariance(double meanLevel, double v0)
    {
        // QuantLib's testStochVolForwardsAndOptionlets (marketmodel.cpp:4283-4460) on its semi-annual times to 5 years
        const int n = 9, paths = 32767;
        const double reversionSpeed = 1.0, volVar = 1.0;
        double[] rateTimes = [.. Enumerable.Range(1, n + 1).Select(i => 0.5 * i)];
        double[] accruals = [.. Enumerable.Range(0, n).Select(i => rateTimes[i + 1] - rateTimes[i])];
        double[] fwds = [.. Enumerable.Range(0, n).Select(i => 0.03 + (0.0010 * i))];
        double[] disc = new double[n + 1];
        disc[0] = 0.95;
        for (int i = 1; i <= n; ++i)
        {
            disc[i] = disc[i - 1] / (1.0 + (fwds[i - 1] * accruals[i - 1]));
        }

        double[] vols = [0.15541283, 0.18719678, 0.20890740, 0.22318179, 0.23212717, 0.23731450, 0.23988649, 0.24066384, 0.24023111];
        var evolution = new EvolutionDescription(rateTimes);
        Matrix c = ExpCorrelationsFunctions.ExponentialCorrelations(rateTimes, 0.5, 0.2);
        var model = new FlatVol(vols, new TimeHomogeneousForwardCorrelation(c, rateTimes), evolution, n, fwds, new double[n]);
        var volProcess = new SquareRootAndersen(meanLevel, reversionSpeed, volVar, v0, evolution.EvolutionTimes.AsSpan(), 8, 0.5, 0.5, 1.5);
        var evolver = new SVDDFwdRatePc(
            model, new MTBrownianGeneratorFactory(42), volProcess, 2, 2, EvolutionDescriptionFunctions.TerminalMeasure(evolution).AsSpan());

        // the accounting engine's optionlets: each caplet paid at the next rate time, in the terminal bond
        double[] sum = new double[n], sum2 = new double[n];
        for (int p = 0; p < paths; ++p)
        {
            double weight = evolver.StartNewPath();
            for (int s = 0; s < n; ++s)
            {
                weight *= evolver.AdvanceStep();
                CurveState cs = evolver.CurrentState;
                double value = weight * accruals[s] * Math.Max(cs.ForwardRate(s) - fwds[s], 0.0) * cs.DiscountRatio(s + 1, n);
                sum[s] += value;
                sum2[s] += value * value;
            }
        }

        var dc = new Actual365Fixed();
        var today = new Date(15, Month.October, 2026);
        var flat = new FlatForward(today, 0.0, dc);
        for (int i = 0; i < n; ++i)
        {
            double mean = sum[i] / paths;
            double error = disc[n] * Math.Sqrt(((sum2[i] / paths) - (mean * mean)) / (paths - 1));
            double volCoeff = vols[i];
            var heston = new HestonModel(new HestonProcess(
                flat, flat, fwds[i], v0 * volCoeff * volCoeff, reversionSpeed, volCoeff * volCoeff * meanLevel, volCoeff * volVar, 0.0));
            double trueValue = new AnalyticHestonEngine(heston, AnalyticHestonEngineComplexLogFormula.Gatheral, FourierIntegration.GaussLaguerre(128))
                .PriceVanillaPayoff(new PlainVanillaPayoff(OptionType.Call, fwds[i]), rateTimes[i]) * accruals[i] * disc[i + 1];
            double z = ((disc[n] * mean) - trueValue) / error;
            Assert.True(Math.Abs(z) < 4.0, $"caplet {i}: {z} standard errors");
        }
    }
}
