// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of the exercises' refusals, which the vectors record only as throws: QuantLib's
/// <c>QL_REQUIRE</c>s on the constructors' arguments (<c>exercise.cpp:37-38, 55</c>) throw
/// <see cref="ArgumentException"/> naming the argument, with QuantLib's texts; <see cref="Exercise.LastDate"/> on an
/// exercise without dates, a stored value, <see cref="NanoQLException"/> with QuantLib's text, which a swaption's
/// expiry check reads; an index out of range, an exercise type outside <see cref="ExerciseType"/> and
/// <c>default(Date)</c>, which nanoql refuses on entry, <see cref="ArgumentOutOfRangeException"/> and
/// <see cref="ArgumentException"/>.
/// </summary>
public class ExerciseErrorTests
{
    [Fact]
    public void TheExercisesRefusals()
    {
        var today = new Date(15, Month.October, 2026);

        ArgumentException e = Assert.Throws<ArgumentException>(() => new AmericanExercise(today + 1, today));
        Assert.Equal("earliestDate", e.ParamName);
        Assert.StartsWith("earliest > latest exercise date", e.Message, StringComparison.Ordinal);

        e = Assert.Throws<ArgumentException>(() => new BermudanExercise([], true));
        Assert.Equal("dates", e.ParamName);
        Assert.StartsWith("no exercise date given", e.Message, StringComparison.Ordinal);
        Assert.Equal("dates", Assert.Throws<ArgumentNullException>(() => new BermudanExercise(null!)).ParamName);

        foreach (Exercise bare in new Exercise[] { new(ExerciseType.European), new EarlyExercise(ExerciseType.American, true) })
        {
            Assert.Equal("no exercise date given", Assert.Throws<NanoQLException>(() => bare.LastDate()).Message);
        }

        // EX1: an index out of range, read or checked alike; .NET's text. QuantLib's dateAt(i) is dates_.at(i)
        // (exercise.hpp:46), which throws std::out_of_range; its date(i) reads dates_[i] unchecked, undefined
        // (Divergences/ExerciseDatesTests).
        var european = new EuropeanExercise(today);
        foreach (Func<int, Date> read in new Func<int, Date>[] { european.Date, european.DateAt })
        {
            ArgumentOutOfRangeException r = Assert.Throws<ArgumentOutOfRangeException>(() => read(1));
            Assert.Equal("index", r.ParamName);
            Assert.StartsWith("index ('1') must be less than '1'.", r.Message, StringComparison.Ordinal);
            r = Assert.Throws<ArgumentOutOfRangeException>(() => read(-1));
            Assert.Equal("index", r.ParamName);
            Assert.StartsWith("index ('-1') must be a non-negative value.", r.Message, StringComparison.Ordinal);
        }

        // An exercise type outside ExerciseType, which QuantLib stores unchecked.
        foreach (Func<object> f in new Func<object>[]
        {
            () => new Exercise((ExerciseType)3),
            () => new EarlyExercise((ExerciseType)(-1)),
        })
        {
            ArgumentOutOfRangeException r = Assert.Throws<ArgumentOutOfRangeException>(f);
            Assert.Equal("type", r.ParamName);
            Assert.StartsWith("unknown exercise type", r.Message, StringComparison.Ordinal);
        }

        // default(Date), never a valid date (the null-date row), where QuantLib stores Date() as given.
        Date none = default;
        Assert.Equal("date", Assert.Throws<ArgumentException>(() => new EuropeanExercise(none)).ParamName);
        Assert.Equal("earliestDate", Assert.Throws<ArgumentException>(() => new AmericanExercise(none, today)).ParamName);
        Assert.Equal("latestDate", Assert.Throws<ArgumentException>(() => new AmericanExercise(today, none)).ParamName);
        Assert.Equal("latestDate", Assert.Throws<ArgumentException>(() => new AmericanExercise(none, true)).ParamName);
        Assert.Equal("dates", Assert.Throws<ArgumentException>(() => new BermudanExercise([today, none])).ParamName);
    }
}
