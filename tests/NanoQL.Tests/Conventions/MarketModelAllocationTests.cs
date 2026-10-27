// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The market-model evolution step's pin (the S-mm plan's MR1 (v), SD11): after a path's warm-up, a path of steps (the
/// generator's step, the drifts, the state's update and the curve state's set, the constraint's shift and the
/// volatility process's step where the evolver has them) allocates nothing, for each evolver on the Mersenne-twister,
/// Sobol and Burley-scrambled Sobol generators at reduced factors, and for the Euler and predictor-corrector ones at
/// full factors (the drifts' plain branch) too; the constrained evolver's constraints, which QuantLib's one caller
/// sets before each path, are set allocating nothing; and the curve states' reads allocate nothing.
/// </summary>
public class MarketModelAllocationTests
{
    private static readonly double[] RateTimes = [0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5];

    private static readonly string[] EvolverNames =
    [
        "LogNormalFwdRateEuler", "LogNormalFwdRatePc", "LogNormalFwdRateEulerConstrained", "LogNormalFwdRateIpc",
        "LogNormalFwdRateBalland", "LogNormalFwdRateiBalland", "NormalFwdRatePc", "LogNormalCotSwapRatePc",
        "LogNormalCmSwapRatePc", "SVDDFwdRatePc",
    ];

    public static TheoryData<string, string, int> Evolvers
    {
        get
        {
            var data = new TheoryData<string, string, int>();
            foreach (string evolver in EvolverNames)
            {
                foreach (string generator in new[] { "MT", "Sobol", "Burley" })
                {
                    data.Add(evolver, generator, 3);
                }
            }

            data.Add("LogNormalFwdRateEuler", "MT", 6);
            data.Add("LogNormalFwdRatePc", "Sobol", 6);
            return data;
        }
    }

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
    public void SettingAConstraintAllocatesNothing()
    {
        double[] constraints = [0.032, 0.033, 0.034, 0.035, 0.036, 0.037];
        bool[] active = [true, false, true, false, true, false];
        AllocationAssert.AllocatesNothing(
            () => (LogNormalFwdRateEulerConstrained)Make("LogNormalFwdRateEulerConstrained", "MT", 3),
            e => e.SetThisConstraint(constraints, active));
    }

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
        BrownianGeneratorFactory factory = generator switch
        {
            "MT" => new MTBrownianGeneratorFactory(42),
            "Sobol" => new SobolBrownianGeneratorFactory(SobolBrownianGeneratorBaseOrdering.Diagonal, 1234),
            _ => new Burley2020SobolBrownianGeneratorFactory(SobolBrownianGeneratorBaseOrdering.Diagonal, 1234, SobolRsgDirectionIntegers.Jaeckel, 5678),
        };
        int[] numeraires = [.. EvolutionDescriptionFunctions.TerminalMeasure(model.Evolution)];
        switch (evolver)
        {
            case "LogNormalFwdRateEuler":
                return new LogNormalFwdRateEuler(model, factory, numeraires);
            case "LogNormalFwdRatePc":
                return new LogNormalFwdRatePc(model, factory, numeraires);
            case "LogNormalFwdRateEulerConstrained":
                var constrained = new LogNormalFwdRateEulerConstrained(model, model, factory, numeraires);
                constrained.SetConstraintType([0, 1, 2, 3, 4, 5], [1, 2, 3, 4, 5, 6]);
                constrained.SetThisConstraint([0.032, 0.033, 0.034, 0.035, 0.036, 0.037], [true, false, true, false, true, false]);
                return constrained;
            case "LogNormalFwdRateIpc":
                return new LogNormalFwdRateIpc(model, factory, numeraires);
            case "LogNormalFwdRateBalland":
                return new LogNormalFwdRateBalland(model, factory, numeraires);
            case "LogNormalFwdRateiBalland":
                return new LogNormalFwdRateiBalland(model, factory, numeraires);
            case "NormalFwdRatePc":
                return new NormalFwdRatePc(model, factory, numeraires);
            case "LogNormalCotSwapRatePc":
                return new LogNormalCotSwapRatePc(model, factory, numeraires);
            case "LogNormalCmSwapRatePc":
                return new LogNormalCmSwapRatePc(2, model, factory, numeraires);
            default:
                var process = new SquareRootAndersen(1.0, 1.0, 1.0, 1.0, model.Evolution.EvolutionTimes.AsSpan(), 4, 0.5, 0.5);
                return new SVDDFwdRatePc(model, factory, process, 2, 2, numeraires);
        }
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
