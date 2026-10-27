// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The market-model evolution step's pin (the S-mm plan's MR1 (v), SD11): after a path's warm-up, a path of steps (the
/// generator's step, the drifts, the state's update and the curve state's set) allocates nothing, for each evolver on
/// the Mersenne-twister and Sobol generators, at reduced factors and at full ones (the drifts' plain branch); and the
/// curve states' reads allocate nothing.
/// </summary>
public class MarketModelAllocationTests
{
    private static readonly double[] RateTimes = [0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5];

    public static TheoryData<string, string, int> Evolvers => new()
    {
        { "LogNormalFwdRateEuler", "MT", 3 },
        { "LogNormalFwdRateEuler", "Sobol", 3 },
        { "LogNormalFwdRatePc", "MT", 3 },
        { "LogNormalFwdRatePc", "Sobol", 3 },
        { "LogNormalFwdRateEuler", "MT", 6 },
        { "LogNormalFwdRatePc", "Sobol", 6 },
    };

    [Theory]
    [MemberData(nameof(Evolvers))]
    public void APathsStepsAllocateNothing(string evolver, string generator, int factors) =>
        AllocationAssert.AllocatesNothing(
            () =>
            {
                MarketModelEvolver e = Make(evolver, generator, factors);
                Path(e);
                e.StartNewPath();
                return e;
            },
            e =>
            {
                for (int s = 0; s < 6; ++s)
                {
                    e.AdvanceStep();
                }
            });

    [Fact]
    public void TheCurveStatesReadsAllocateNothing()
    {
        double[] rates = [0.03, 0.031, 0.032, 0.033, 0.034, 0.035];
        CurveState[] states = [new LMMCurveState(RateTimes), new CoterminalSwapCurveState(RateTimes), new CMSwapCurveState(RateTimes, 2)];
        ((LMMCurveState)states[0]).SetOnForwardRates(rates);
        ((CoterminalSwapCurveState)states[1]).SetOnCoterminalSwapRates(rates);
        ((CMSwapCurveState)states[2]).SetOnCMSwapRates(rates);
        foreach (CurveState cs in states)
        {
            Reads(cs);
            AllocationAssert.AllocatesNothing(() => cs, Reads);
        }

        AllocationAssert.AllocatesNothing(() => (LMMCurveState)states[0], s => s.SetOnForwardRates(rates, 2));
    }

    private static void Reads(CurveState cs)
    {
        int n = cs.NumberOfRates;
        double sum = 0.0;
        for (int i = 0; i < n; ++i)
        {
            sum += cs.ForwardRate(i) + cs.DiscountRatio(i, n) + cs.CoterminalSwapRate(i) + cs.CoterminalSwapAnnuity(n, i)
                + cs.CmSwapRate(i, 2) + cs.CmSwapAnnuity(n, i, 3);
        }

        sum += cs.ForwardRates()[0] + cs.CoterminalSwapRates()[0] + cs.CmSwapRates(1)[0] + cs.SwapRate(0, n);
        Assert.True(sum > 0.0);
    }

    private static MarketModelEvolver Make(string evolver, string generator, int factors)
    {
        var model = new FlatVol(
            [0.2, 0.21, 0.22, 0.23, 0.24, 0.25],
            new ExponentialForwardCorrelation(RateTimes, 0.5, 0.2),
            new EvolutionDescription(RateTimes),
            factors,
            [0.03, 0.031, 0.032, 0.033, 0.034, 0.035],
            [0.0, 0.0, 0.0, 0.0, 0.0, 0.0]);
        BrownianGeneratorFactory factory = generator == "MT"
            ? new MTBrownianGeneratorFactory(42)
            : new SobolBrownianGeneratorFactory(SobolBrownianGeneratorBaseOrdering.Diagonal, 1234);
        int[] numeraires = [.. EvolutionDescriptionFunctions.TerminalMeasure(model.Evolution)];
        return evolver == "LogNormalFwdRateEuler"
            ? new LogNormalFwdRateEuler(model, factory, numeraires)
            : new LogNormalFwdRatePc(model, factory, numeraires);
    }

    private static void Path(MarketModelEvolver e)
    {
        e.StartNewPath();
        for (int s = 0; s < 6; ++s)
        {
            e.AdvanceStep();
        }
    }
}
