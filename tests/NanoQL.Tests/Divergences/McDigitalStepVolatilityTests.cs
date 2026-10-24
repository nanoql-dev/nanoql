// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Monte Carlo digital engine's crossing volatility (the S-van plan's VR4 (iv), under R8, with an independent
/// check). QuantLib's <c>DigitalPathPricer</c> reads the volatility of step i at the step's end time,
/// <c>diffusion(timeGrid[i+1], S_i)</c>, under the comment "initial (timeGrid[i+1]) for the time being"
/// (<c>mcdigitalengine.cpp:53-54</c>, <c>:80-82</c>), so on a time-dependent volatility each step's Brownian bridge reads
/// the next step's. nanoql reads it at the step's start, (t_i, S_i). The check, QuantLib's having none: on a volatility
/// constant within each grid step the bridge's crossing probability is exact, so the fixed engine on a coarse grid
/// matches the same engine on a fine one within their errors, where QuantLib's is far off. The volatility: forward
/// volatilities of 10%, 40%, 10% and 40% over four quarters of 91 days (a <see cref="BlackVarianceCurve"/>), an up-and-in
/// call at 115 and a down-and-in put at 85 paying 10 at the hit; QuantLib's engine (seed 42, 100,000 samples) gives the
/// call 7.362620907677973 on 4 steps against 6.395034183222466 on 64, the put 6.14377515430365 against 5.417421714551026
/// [executed on the wheel: <c>oracle_c1c.py</c>].
/// </summary>
public class McDigitalStepVolatilityTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void OnAVolatilityConstantPerStepACoarseGridMatchesAFineOne()
    {
        double[] forward = [0.10, 0.40, 0.10, 0.40];
        var dates = new Date[4];
        double[] vols = new double[4];
        double variance = 0.0;
        for (int i = 0; i < 4; i++)
        {
            dates[i] = Today + (91 * (i + 1));
            variance += forward[i] * forward[i] * 91.0 / 365.0;
            vols[i] = Math.Sqrt(variance / A365.YearFraction(Today, dates[i]));
        }

        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.0, A365), new FlatForward(Today, 0.05, A365),
            new BlackVarianceCurve(Today, dates, vols, A365));
        foreach ((OptionType type, double strike) in new[] { (OptionType.Call, 115.0), (OptionType.Put, 85.0) })
        {
            var option = new VanillaOption(new CashOrNothingPayoff(type, strike, 10.0), new AmericanExercise(Today, dates[^1]));
            OneAssetOptionResults coarse = MCDigitalEngine.Calculate<PseudoRandom, GeneralStatistics>(
                option, Context, process, 4, null, false, false, 100000, null, null, 42);
            OneAssetOptionResults fine = MCDigitalEngine.Calculate<PseudoRandom, GeneralStatistics>(
                option, Context, process, 64, null, false, false, 100000, null, null, 42);
            double error = Math.Sqrt(
                (coarse.ErrorEstimate!.Value * coarse.ErrorEstimate.Value) + (fine.ErrorEstimate!.Value * fine.ErrorEstimate.Value));
            Assert.True(
                Math.Abs(coarse.NPV!.Value - fine.NPV!.Value) <= 3.0 * error,
                $"{type}: 4 steps {coarse.NPV}, 64 steps {fine.NPV}, error {error}");
        }
    }
}
