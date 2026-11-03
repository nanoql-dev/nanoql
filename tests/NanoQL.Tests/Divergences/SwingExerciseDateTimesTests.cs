// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="SwingExercise"/>'s date times. QuantLib's Bermudan base sorts the dates and leaves the seconds in the
/// order given (<c>vanillaswingoption.cpp:52-69</c>), so date times given out of order pair each second with another
/// date: days 3 and 1 at 0 and 43,200 seconds become day 1 at 0 and day 3 at 43,200; and its fixed step moves one day
/// whenever the seconds pass a day (<c>vanillaswingoption.cpp:37-48</c>), so a two-day step from day 1 to day 9
/// exercises on each of days 1 to 9 (QuantLib 1.44). nanoql sorts the date times together, and a step moves as many
/// days as it holds. Given in order, and at steps under a day, QuantLib's date times.
/// </summary>
public class SwingExerciseDateTimesTests
{
    private static readonly Date Today = new(15, Month.October, 2026);

    [Fact]
    public void DateTimesGivenOutOfOrderKeepTheirPairs()
    {
        var exercise = new SwingExercise([Today + 3, Today + 1], [0, 43200]);
        Assert.Equal([Today + 1, Today + 3], exercise.Dates);
        Assert.Equal([43200, 0], exercise.Seconds);

        // in order, QuantLib's
        var ordered = new SwingExercise([Today + 1, Today + 3], [43200, 0]);
        Assert.Equal(exercise.ExerciseTimes(new Actual365Fixed(), Today), ordered.ExerciseTimes(new Actual365Fixed(), Today));
    }

    [Fact]
    public void AStepOfDaysMovesThatManyDays()
    {
        var twoDays = new SwingExercise(Today + 1, Today + 9, 2 * 24 * 3600);
        Assert.Equal([Today + 1, Today + 3, Today + 5, Today + 7, Today + 9], twoDays.Dates);
        Assert.All(twoDays.Seconds, s => Assert.Equal(0, s));

        // a day and a half: 0, 1.5 and 3 days on, where QuantLib's reads 0, 1.5 and 2
        var dayAndHalf = new SwingExercise(Today + 1, Today + 4, 36 * 3600);
        Assert.Equal([Today + 1, Today + 2, Today + 4], dayAndHalf.Dates);
        Assert.Equal([0, 43200, 0], dayAndHalf.Seconds);

        // under a day, QuantLib's: the half-daily steps
        var halfDaily = new SwingExercise(Today + 1, Today + 2, 43200);
        Assert.Equal([Today + 1, Today + 1, Today + 2, Today + 2], halfDaily.Dates);
        Assert.Equal([0, 43200, 0, 43200], halfDaily.Seconds);
    }
}
