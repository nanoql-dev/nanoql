// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Balland's and the iterative Balland's geometric mean (the S-mm plan's MR3 (xix), fixed under R8). Both evolvers take
/// <c>sqrt(f·f₀)</c> with f₀ the model's time-0 forward at every step, undisplaced
/// (<c>lognormalfwdrateballand.cpp:127</c>,
/// <c>lognormalfwdrateiballand.cpp:119</c>, <c>:135</c>), not the step's start, so the scheme doesn't converge to the
/// model as the steps refine: on a flat 30% model of ten semi-annual rates its caplets sit 6.25 standard errors from
/// Black at a million paths (the planner's MP21), and on this run's model at 4% its caplets are 0.15% to 0.97% above
/// the predictor-corrector's on the same paths at four steps a period, 0.03% to 0.87% at one (QuantLib's form
/// emulated in this run's measurement, 65,536 paths); and the undisplaced product is negative once a displaced forward
/// goes below 0, so the forward is NaN: 1,450 of 2,000 paths at displacement 2%, forwards 0.2%, volatility 30% (this
/// run's
/// <c>probes\c2_facts.cpp</c>). nanoql takes the geometric mean of the displaced forwards at the step's start and end,
/// sqrt((f_end + d)(f_start + d)) − d. Checks: on an evolution of one step from time 0, undisplaced, QuantLib's
/// arithmetic (the vectors), bit for bit; on multi-step evolutions the caplets within 4 standard errors of Black,
/// within 0.05% of the predictor-corrector's at four steps a period (the fix 0.010% at most), and converging to them as
/// the steps refine; no NaN path displaced; and a set initial state, whose forwards QuantLib's mean ignores, the
/// evolution of a model starting there, bit for bit.
/// </summary>
public class BallandGeometricMeanTests
{
    private const int N = 10, Paths = 32768;
    private const double Vol = 0.3, F = 0.04, Tau = 0.5;

    [Fact]
    public void TheCapletsConvergeToTheModelsAsTheStepsRefine()
    {
        (double[] Z, double[] Gap)[] coarse = Caplets(1), fine = Caplets(4);
        for (int k = 0; k < coarse.Length; ++k)
        {
            for (int i = 0; i < N; ++i)
            {
                Assert.True(Math.Abs(fine[k].Z[i]) < 4.0, $"scheme {k}, caplet {i}: {fine[k].Z[i]} standard errors from Black");
                Assert.True(Math.Abs(fine[k].Gap[i]) < 5e-4, $"scheme {k}, caplet {i}: {fine[k].Gap[i]} from the PC's");
            }

            // the last forward's drift is 0 in the terminal measure, so its caplet is the PC's on every path
            for (int i = 1; i < N - 1; ++i)
            {
                Assert.True(Math.Abs(fine[k].Gap[i]) < 0.5 * Math.Abs(coarse[k].Gap[i]), $"scheme {k}, caplet {i}: {coarse[k].Gap[i]} then {fine[k].Gap[i]}");
            }
        }
    }

    [Fact]
    public void ASetStateIsTheEvolutionOfAModelStartingThere()
    {
        double[] rateTimes = [0.5, 1.0, 1.5, 2.0, 2.5, 3.0];
        double[] set = [0.05, 0.051, 0.052, 0.053, 0.054];
        FlatVol Model(double[] rates) =>
            new([0.3, 0.3, 0.3, 0.3, 0.3], new ExponentialForwardCorrelation(rateTimes, 0.5, 0.2), new EvolutionDescription(rateTimes), 5, rates, [0.01, 0.01, 0.01, 0.01, 0.01]);
        var cs = new LMMCurveState(rateTimes);
        cs.SetOnForwardRates(set);
        Func<MarketModel, MarketModelEvolver>[] makes =
        [
            m => new LogNormalFwdRateBalland(m, new MTBrownianGeneratorFactory(42), [5, 5, 5, 5, 5]),
            m => new LogNormalFwdRateiBalland(m, new MTBrownianGeneratorFactory(42), [5, 5, 5, 5, 5]),
        ];
        foreach (Func<MarketModel, MarketModelEvolver> make in makes)
        {
            MarketModelEvolver fromSet = make(Model([0.03, 0.031, 0.032, 0.033, 0.034])), fromModel = make(Model(set));
            fromSet.SetInitialState(cs);
            for (int p = 0; p < 3; ++p)
            {
                Assert.Equal(fromModel.StartNewPath(), fromSet.StartNewPath());
                for (int s = 0; s < 5; ++s)
                {
                    Assert.Equal(fromModel.AdvanceStep(), fromSet.AdvanceStep());
                    Assert.Equal(fromModel.CurrentState.ForwardRates().ToArray(), fromSet.CurrentState.ForwardRates().ToArray());
                }
            }
        }
    }

    [Fact]
    public void ADisplacedForwardBelowZeroStaysANumber()
    {
        double[] rateTimes = [0.5, 1.0, 1.5, 2.0, 2.5, 3.0];
        var model = new FlatVol(
            [0.3, 0.3, 0.3, 0.3, 0.3],
            new ExponentialForwardCorrelation(rateTimes, 0.5, 0.2),
            new EvolutionDescription(rateTimes),
            5,
            [0.002, 0.002, 0.002, 0.002, 0.002],
            [0.02, 0.02, 0.02, 0.02, 0.02]);
        MarketModelEvolver[] evolvers =
        [
            new LogNormalFwdRateBalland(model, new MTBrownianGeneratorFactory(42), [5, 5, 5, 5, 5]),
            new LogNormalFwdRateiBalland(model, new MTBrownianGeneratorFactory(42), [5, 5, 5, 5, 5]),
        ];
        foreach (MarketModelEvolver e in evolvers)
        {
            int below = 0;
            for (int p = 0; p < 2000; ++p)
            {
                e.StartNewPath();
                for (int s = 0; s < 5; ++s)
                {
                    e.AdvanceStep();
                    for (int r = s; r < 5; ++r)
                    {
                        double f = e.CurrentState.ForwardRate(r);
                        Assert.True(double.IsFinite(f), $"path {p}, step {s}, rate {r}");
                        below += f < 0.0 ? 1 : 0;
                    }
                }
            }

            Assert.True(below > 0);
        }
    }

    // Balland's and the iterative Balland's caplets on a flat model, the given steps a period, against Black (in
    // standard errors) and against the predictor-corrector's on the same paths (relative to Black's value).
    private static (double[] Z, double[] Gap)[] Caplets(int stepsPerPeriod)
    {
        double[] rateTimes = [.. Enumerable.Range(1, N + 1).Select(i => Tau * i)];
        var evolutionTimes = new List<double>();
        for (int i = 0; i < N; ++i)
        {
            double start = i == 0 ? 0.0 : rateTimes[i - 1];
            for (int k = 1; k <= stepsPerPeriod; ++k)
            {
                evolutionTimes.Add(k == stepsPerPeriod ? rateTimes[i] : start + ((rateTimes[i] - start) * k / stepsPerPeriod));
            }
        }

        var evolution = new EvolutionDescription(rateTimes, [.. evolutionTimes]);
        var model = new FlatVol(
            Enumerable.Repeat(Vol, N).ToArray(),
            new ExponentialForwardCorrelation(rateTimes, 0.5, 0.2),
            evolution,
            N,
            Enumerable.Repeat(F, N).ToArray(),
            new double[N]);
        int[] numeraires = [.. EvolutionDescriptionFunctions.TerminalMeasure(evolution)];
        MarketModelEvolver[] schemes =
        [
            new LogNormalFwdRatePc(model, new MTBrownianGeneratorFactory(42), numeraires),
            new LogNormalFwdRateBalland(model, new MTBrownianGeneratorFactory(42), numeraires),
            new LogNormalFwdRateiBalland(model, new MTBrownianGeneratorFactory(42), numeraires),
        ];
        double[][] values = [.. schemes.Select(_ => new double[N])];
        double[,] sum = new double[schemes.Length, N], sum2 = new double[schemes.Length, N], gap = new double[schemes.Length, N];
        for (int p = 0; p < Paths; ++p)
        {
            for (int k = 0; k < schemes.Length; ++k)
            {
                MarketModelEvolver e = schemes[k];
                e.StartNewPath();
                for (int s = 0, caplet = 0; s < evolution.NumberOfSteps; ++s)
                {
                    e.AdvanceStep();
                    if (evolution.EvolutionTimes[s] == rateTimes[caplet])
                    {
                        CurveState cs = e.CurrentState;
                        values[k][caplet] = Tau * Math.Max(cs.ForwardRate(caplet) - F, 0.0) * cs.DiscountRatio(caplet + 1, N);
                        ++caplet;
                    }
                }
            }

            for (int k = 0; k < schemes.Length; ++k)
            {
                for (int i = 0; i < N; ++i)
                {
                    sum[k, i] += values[k][i];
                    sum2[k, i] += values[k][i] * values[k][i];
                    gap[k, i] += values[k][i] - values[0][i];
                }
            }
        }

        // the caplets in the terminal bond: Black's on the time-0 curve from the first rate time
        double terminal = Math.Pow(1 + (Tau * F), -N);
        var result = new (double[] Z, double[] Gap)[schemes.Length - 1];
        for (int k = 1; k < schemes.Length; ++k)
        {
            double[] z = new double[N], g = new double[N];
            for (int i = 0; i < N; ++i)
            {
                double black = Tau * Math.Pow(1 + (Tau * F), -(i + 1)) / terminal
                    * BlackFormulaFunctions.BlackFormula(OptionType.Call, F, F, Vol * Math.Sqrt(rateTimes[i]));
                double mean = sum[k, i] / Paths;
                double error = Math.Sqrt(((sum2[k, i] / Paths) - (mean * mean)) / (Paths - 1));
                z[i] = (mean - black) / error;
                g[i] = gap[k, i] / Paths / black;
            }

            result[k - 1] = (z, g);
        }

        return result;
    }
}
