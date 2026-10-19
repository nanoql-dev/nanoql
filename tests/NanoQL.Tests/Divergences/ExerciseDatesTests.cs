// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An exercise's dates read past their end. QuantLib's <c>Exercise::date(i)</c> is <c>dates_[i]</c>
/// (<c>exercise.hpp:45</c>), which reads past the vector for an index out of range, undefined, where its twin
/// <c>dateAt(i)</c> (<c>:46</c>) throws <c>std::out_of_range</c>. nanoql refuses the index in both, with
/// <see cref="ArgumentOutOfRangeException"/> naming <c>index</c>, as <see cref="Schedule.Date(int)"/> does.
/// </summary>
public class ExerciseDatesTests
{
    [Fact]
    public void AnIndexOutOfRangeIsRefused()
    {
        var today = new Date(15, Month.October, 2026);
        Exercise[] exercises =
        [
            new EuropeanExercise(today),
            new AmericanExercise(today, today + 365),
            new BermudanExercise([today, today + 180, today + 365]),
            new Exercise(ExerciseType.European),
        ];

        foreach (Exercise e in exercises)
        {
            foreach (int index in new[] { -1, e.Dates.Length, e.Dates.Length + 6, int.MaxValue })
            {
                Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(() => e.Date(index)).ParamName);
                Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(() => e.DateAt(index)).ParamName);
            }

            // In range, both read the date.
            for (int i = 0; i < e.Dates.Length; i++)
            {
                Assert.Equal(e.Dates[i], e.Date(i));
                Assert.Equal(e.Dates[i], e.DateAt(i));
            }
        }
    }
}
