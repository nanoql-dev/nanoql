// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The simple swing engine on exercises within a day (the S-van plan's VR4 (xvi)). QuantLib's engine times each
/// exercise by its date (<c>fdsimplebsswingengine.cpp:72-76</c>) where <c>SwingExercise::exerciseTimes</c> adds its
/// seconds (<c>vanillaswingoption.cpp:83-97</c>): 60 half-daily exercises price as the 30 daily ones, 6.0120197306323737
/// with 5 rights and 11.479815522463735 with 10, on a put forward at 30, spot 30, r 14%, q 2%, σ 40%, Actual/Actual
/// (ISDA) from 15 October 2026 [executed: <c>scratchpad\c4\probe\swing_seconds.cpp</c>]. nanoql times them by the
/// exercise's date times on the engine's clock, the maturity the last of them. The check, independent of QuantLib:
/// with every right forced (the minimum and the maximum the number of exercises) the value is the sum of the forward
/// payoffs' values at the exercise times, K·D(t) − S₀·D_q(t), a closed form.
/// </summary>
public class SwingExerciseSecondsTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter Dc = new ActualActual(ActualActualConvention.ISDA);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void WithEveryRightForcedTheValueIsTheForwardsAtTheExerciseTimes()
    {
        var exercise = new SwingExercise(Today + 1, Today + 30, 43200);
        Assert.Equal(60, exercise.Dates.Length);
        var option = new VanillaSwingOption(new VanillaForwardPayoff(OptionType.Put, 30.0), exercise, 60, 60);
        BlackScholesMertonProcess process = Process();

        double expected = 0.0;
        foreach (double t in exercise.ExerciseTimes(Dc, Today))
        {
            expected += (30.0 * process.RiskFreeRate.Discount(t)) - (30.0 * process.DividendYield.Discount(t));
        }

        double value = FdSimpleBSSwingEngine.Calculate(option, Context, process, 50, 200).NPV!.Value;
        Assert.Equal(expected, value, 2e-3);
    }

    [Theory]
    [InlineData(5, 6.0120197306323737)]
    [InlineData(10, 11.479815522463735)]
    public void HalfDailyExercisesAreWorthMoreThanDailyOnes(int rights, double quantLib)
    {
        // QuantLib's value for both, the daily one nanoql's too
        var forward = new VanillaForwardPayoff(OptionType.Put, 30.0);
        var daily = new SwingExercise(Enumerable.Range(1, 30).Select(i => Today + i));
        var halfDaily = new SwingExercise(Today + 1, Today + 30, 43200);
        double d = FdSimpleBSSwingEngine.Calculate(new VanillaSwingOption(forward, daily, 0, rights), Context, Process(), 50, 200).NPV!.Value;
        double h = FdSimpleBSSwingEngine.Calculate(new VanillaSwingOption(forward, halfDaily, 0, rights), Context, Process(), 50, 200).NPV!.Value;
        Assert.Equal(quantLib, d, 1e-12);
        Assert.True(h > d + 0.01, $"{h} against {d}");
    }

    private static BlackScholesMertonProcess Process() =>
        new(30.0, new FlatForward(Today, 0.02, Dc), new FlatForward(Today, 0.14, Dc), new BlackConstantVol(Today, new NullCalendar(), 0.4, Dc));
}
