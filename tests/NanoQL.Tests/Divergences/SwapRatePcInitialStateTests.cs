// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The swap-rate evolvers' initial state from another curve state (fixed). QuantLib's <c>LogNormalCotSwapRatePc</c> and
/// <c>LogNormalCmSwapRatePc::setInitialState</c> cast the state to their own type and dereference the result
/// (<c>lognormalcotswapratepc.cpp:92-93</c>, <c>lognormalcmswapratepc.cpp:93-94</c>), null for any other: given an
/// <c>LMMCurveState</c> the process stops (0xC0000005, 3 runs of 3). nanoql reads the base's coterminal and
/// constant-maturity swap rates, which every curve state answers. Checks: on the evolver's own state type QuantLib's
/// call, the same rates, so the same evolution bit for bit (the vectors set the state there); and from an LMM or
/// another state on the same curve, that evolution again.
/// </summary>
public class SwapRatePcInitialStateTests
{
    private static readonly double[] RateTimes = [0.5, 1.0, 1.5, 2.0, 2.5, 3.0];
    private static readonly double[] Forwards = [0.04, 0.041, 0.043, 0.042, 0.045];

    [Fact]
    public void TheCoterminalEvolverStartsFromAnyStatesCoterminalRates()
    {
        var lmm = new LMMCurveState(RateTimes);
        lmm.SetOnForwardRates(Forwards);
        var own = new CoterminalSwapCurveState(RateTimes);
        own.SetOnCoterminalSwapRates(lmm.CoterminalSwapRates());
        var other = new CMSwapCurveState(RateTimes, 2);
        other.SetOnCMSwapRates(lmm.CmSwapRates(2));

        double[][] reference = Evolve(() => new LogNormalCotSwapRatePc(Model(), new MTBrownianGeneratorFactory(42), [5, 5, 5, 5, 5]), own);
        Assert.Equal(reference, Evolve(() => new LogNormalCotSwapRatePc(Model(), new MTBrownianGeneratorFactory(42), [5, 5, 5, 5, 5]), lmm));
        double[][] fromOther = Evolve(() => new LogNormalCotSwapRatePc(Model(), new MTBrownianGeneratorFactory(42), [5, 5, 5, 5, 5]), other);
        AssertClose(reference, fromOther);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void TheConstantMaturityEvolverStartsFromAnyStatesSwapRates(int span)
    {
        var lmm = new LMMCurveState(RateTimes);
        lmm.SetOnForwardRates(Forwards);
        var own = new CMSwapCurveState(RateTimes, span);
        own.SetOnCMSwapRates(lmm.CmSwapRates(span));
        var other = new CoterminalSwapCurveState(RateTimes);
        other.SetOnCoterminalSwapRates(lmm.CoterminalSwapRates());

        double[][] reference = Evolve(() => new LogNormalCmSwapRatePc(span, Model(), new MTBrownianGeneratorFactory(42), [5, 5, 5, 5, 5]), own);
        Assert.Equal(reference, Evolve(() => new LogNormalCmSwapRatePc(span, Model(), new MTBrownianGeneratorFactory(42), [5, 5, 5, 5, 5]), lmm));
        double[][] fromOther = Evolve(() => new LogNormalCmSwapRatePc(span, Model(), new MTBrownianGeneratorFactory(42), [5, 5, 5, 5, 5]), other);
        AssertClose(reference, fromOther);
    }

    // Each step's forwards over two paths, from the state set.
    private static double[][] Evolve(Func<MarketModelEvolver> make, CurveState state)
    {
        MarketModelEvolver e = make();
        e.SetInitialState(state);
        var steps = new List<double[]>();
        for (int p = 0; p < 2; ++p)
        {
            e.StartNewPath();
            for (int s = 0; s < 5; ++s)
            {
                e.AdvanceStep();
                steps.Add(e.CurrentState.ForwardRates().ToArray());
            }
        }

        return [.. steps];
    }

    // A state of another type on the same curve gives the same rates to rounding: its own recurrences.
    private static void AssertClose(double[][] expected, double[][] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; ++i)
        {
            for (int k = 0; k < expected[i].Length; ++k)
            {
                Assert.Equal(expected[i][k], actual[i][k], 1e-13);
            }
        }
    }

    private static FlatVol Model() =>
        new(
            [0.2, 0.21, 0.22, 0.23, 0.24],
            new ExponentialForwardCorrelation(RateTimes, 0.5, 0.2),
            new EvolutionDescription(RateTimes),
            5,
            [0.03, 0.031, 0.032, 0.033, 0.034],
            [0.0, 0.0, 0.0, 0.0, 0.0]);
}
