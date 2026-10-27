// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The normal predictor-corrector evolver's set initial state (the S-mm plan's MR3 (iv), fixed under R8). QuantLib's
/// <c>NormalFwdRatePc::setForwards</c> has the drift computation for its loop body (<c>normalfwdratepc.cpp:80-81</c>),
/// so the starting forwards stay the model's while the drifts are the new state's: on four semi-annual rates at 4%,
/// normal volatility 0.8%, a state set at 7% starts its first path at the model's 4%, the first step's forwards
/// 0.040197, 0.038239, 0.036115 and 0.038379 (this run's <c>probes\c2_facts.cpp</c>; the planner's MP3). nanoql starts
/// from the forwards given, as the log-normal evolvers do. Check: QuantLib's own evolver on a model whose initial rates
/// are the given forwards (its arithmetic, the vectors'), bit for bit.
/// </summary>
public class NormalFwdRatePcInitialStateTests
{
    private static readonly double[] RateTimes = [0.5, 1.0, 1.5, 2.0, 2.5];

    [Fact]
    public void ASetStateIsTheEvolutionOfAModelStartingThere()
    {
        double[] set = [0.07, 0.07, 0.07, 0.07];
        var fromSet = new NormalFwdRatePc(Model([0.04, 0.04, 0.04, 0.04]), new MTBrownianGeneratorFactory(42), [4, 4, 4, 4]);
        var cs = new LMMCurveState(RateTimes);
        cs.SetOnForwardRates(set);
        fromSet.SetInitialState(cs);
        var fromModel = new NormalFwdRatePc(Model(set), new MTBrownianGeneratorFactory(42), [4, 4, 4, 4]);

        for (int p = 0; p < 3; ++p)
        {
            Assert.Equal(fromModel.StartNewPath(), fromSet.StartNewPath());
            for (int s = 0; s < 4; ++s)
            {
                Assert.Equal(fromModel.AdvanceStep(), fromSet.AdvanceStep());
                Assert.Equal(fromModel.CurrentState.ForwardRates().ToArray(), fromSet.CurrentState.ForwardRates().ToArray());
            }
        }

        // QuantLib's first step starts at the model's 4%; nanoql's at the 7% set
        var first = new NormalFwdRatePc(Model([0.04, 0.04, 0.04, 0.04]), new MTBrownianGeneratorFactory(42), [4, 4, 4, 4]);
        first.SetInitialState(cs);
        first.StartNewPath();
        first.AdvanceStep();
        Assert.InRange(first.CurrentState.ForwardRate(0), 0.07 - 0.01, 0.07 + 0.01);
        Assert.NotEqual(0.04019741810628543, first.CurrentState.ForwardRate(0), 0.02);
    }

    private static FlatVol Model(double[] rates) =>
        new(
            [0.008, 0.008, 0.008, 0.008],
            new ExponentialForwardCorrelation(RateTimes, 0.5, 0.2),
            new EvolutionDescription(RateTimes),
            4,
            rates,
            [0.0, 0.0, 0.0, 0.0]);
}
