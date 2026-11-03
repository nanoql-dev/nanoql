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
/// A fixing date given twice (fixed). The average counts it twice, as QuantLib's analytic engines count it, where
/// QuantLib's finite-difference condition weighs every node by the first duplicate's count and its Monte Carlo grid
/// keeps the time once, so its pricers count it once: on spot 100, q 3%, r 6%, σ 20% (Act/360), a call at 100 fixing at
/// 90, 180, 180, 270 and 360 days prices 6.624103653021713 by QuantLib's FD engine (100 × 100 × 50) where the contract
/// is worth 6.0461 ± 0.0032, its arithmetic Monte Carlo 6.115069708532511 (the schedule counted once, 6.1358), and its
/// geometric Monte Carlo 5.934198148917606 ± 0.0144 against its analytic geometric engine's 5.884809360030421, 3.4σ.
/// nanoql weighs each node by its fixings' count. Checks: an exact Monte Carlo of the average with the date counted
/// twice, QuantLib's Mersenne twister and inverse normal, the FD engine within its error and the grid's own, measured
/// on the schedule without the duplicate; and the geometric engine.
/// </summary>
public class AsianDuplicateFixingsTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A360 = new Actual360();
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Today, 0.03, A360), new FlatForward(Today, 0.06, A360), new BlackConstantVol(Today, new NullCalendar(), 0.20, A360));

    private static readonly Date[] Twice = [Today + 90, Today + 180, Today + 180, Today + 270, Today + 360];
    private static readonly Date[] Once = [Today + 90, Today + 180, Today + 270, Today + 360];

    [Fact]
    public void TheFiniteDifferenceEngineCountsTheDateTwice()
    {
        (double twice, double twiceError) = ExactMc(Twice, 500000);
        (double once, double onceError) = ExactMc(Once, 500000);
        double fd = FdBlackScholesAsianEngine.Calculate(Arithmetic(Twice), Context, Process, 400, 400, 200).NPV!.Value;
        double grid = Math.Abs(FdBlackScholesAsianEngine.Calculate(Arithmetic(Once), Context, Process, 400, 400, 200).NPV!.Value - once)
            + (3.0 * onceError);
        Assert.True(Math.Abs(fd - twice) <= (3.0 * twiceError) + grid, $"{fd} against {twice} +- {twiceError}");
        Assert.True(Math.Abs(6.624103653021713 - twice) > 50.0 * twiceError);
    }

    [Fact]
    public void TheMonteCarloEnginesCountTheDateTwice()
    {
        (double exact, double exactError) = ExactMc(Twice, 500000);
        OneAssetOptionResults arithmetic = MCDiscreteArithmeticAPEngine.Calculate<PseudoRandom, GeneralStatistics>(
            Arithmetic(Twice), Context, Process, false, true, false, 100000, null, null, 42);
        Assert.True(Math.Abs(arithmetic.NPV!.Value - exact) <= 3.0 * Math.Sqrt((arithmetic.ErrorEstimate!.Value * arithmetic.ErrorEstimate.Value) + (exactError * exactError)));
        var geometricOption = new DiscreteAveragingAsianOption(
            AverageType.Geometric, 1.0, 0, Twice, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 360));
        OneAssetOptionResults geometric = MCDiscreteGeometricAPEngine.Calculate<PseudoRandom, GeneralStatistics>(
            geometricOption, Context, Process, false, true, 100000, null, null, 42);
        double analytic = AnalyticDiscreteGeometricAveragePriceAsianEngine.Calculate(geometricOption, Context, Process).NPV!.Value;
        Assert.Equal(5.884809360030421, analytic, 1e-10);
        Assert.True(Math.Abs(geometric.NPV!.Value - analytic) <= 3.0 * geometric.ErrorEstimate!.Value, $"{geometric.NPV} against {analytic}");
    }

    private static DiscreteAveragingAsianOption Arithmetic(Date[] dates) =>
        new(AverageType.Arithmetic, 0.0, 0, dates, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 360));

    // the call on the average of the spot at each fixing date, a date given twice counted twice, exact lognormal steps on
    // QuantLib's Mersenne twister and inverse normal, antithetic pairs
    private static (double Mean, double Error) ExactMc(Date[] dates, int pairs)
    {
        double r = 0.06, q = 0.03, v = 0.20, s0 = 100.0, k = 100.0;
        double[] times = [.. dates.Select(d => (d - Today) / 360.0)];
        double[] steps = [.. times.Select((t, i) => i == 0 ? t : t - times[i - 1])];
        var rng = new MersenneTwisterUniformRng(42);
        var inv = new InverseCumulativeNormal();
        double discount = Math.Exp(-r * 1.0);
        double sum = 0.0, sum2 = 0.0;
        var z = new double[steps.Length];
        for (int n = 0; n < pairs; n++)
        {
            for (int i = 0; i < z.Length; i++)
            {
                z[i] = inv.Evaluate(rng.Next().Value);
            }

            double pair = 0.0;
            foreach (double sign in (double[])[1.0, -1.0])
            {
                double ln = Math.Log(s0), average = 0.0;
                for (int i = 0; i < steps.Length; i++)
                {
                    ln += ((r - q - (0.5 * v * v)) * steps[i]) + (v * Math.Sqrt(steps[i]) * sign * z[i]);
                    average += Math.Exp(ln);
                }

                pair += 0.5 * Math.Max((average / times.Length) - k, 0.0) * discount;
            }

            sum += pair;
            sum2 += pair * pair;
        }

        double mean = sum / pairs;
        return (mean, Math.Sqrt(((sum2 / pairs) - (mean * mean)) / pairs));
    }
}
