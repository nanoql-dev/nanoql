// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An exercise's dates read past their end. QuantLib's <c>Exercise::date(i)</c> is <c>dates_[i]</c>
/// (<c>exercise.hpp:45</c>), which reads past the vector for an index out of range, undefined, where its twin
/// <c>dateAt(i)</c> (<c>:46</c>) throws <c>std::out_of_range</c>, a convention (<c>Conventions/ExerciseErrorTests</c>).
/// nanoql refuses the index, with <see cref="ArgumentOutOfRangeException"/> naming <c>index</c>, as
/// <see cref="Schedule.Date(int)"/> does. And a swaption on an exercise with no dates, which QuantLib's public
/// <c>Exercise(type)</c> builds (<c>exercise.hpp:41</c>): QuantLib's swaption reads its last date in <c>isExpired</c>
/// (<c>dates().back()</c>, <c>swaption.cpp:159</c>), and its first in the engines and in <c>impliedVolatility</c>
/// (<c>date(0)</c>, <c>blackswaptionengine.hpp:227</c>, <c>swaption.cpp:198</c>), undefined for an empty vector.
/// nanoql's swaption builds on it and refuses where the date is read, with <see cref="NanoQLException"/> and QuantLib's
/// text for the same input, <c>lastDate()</c>'s "no exercise date given" (<c>exercise.cpp:29</c>).
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
            }

            // In range, both read the date.
            for (int i = 0; i < e.Dates.Length; i++)
            {
                Assert.Equal(e.Dates[i], e.Date(i));
                Assert.Equal(e.Dates[i], e.DateAt(i));
            }
        }
    }

    [Fact]
    public void ASwaptionOnAnExerciseWithNoDatesIsRefusedWhereItsDateIsRead()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var curve = new FlatForward(today, 0.03, new Actual365Fixed());
        VanillaSwap swap = new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(curve))
            .WithForwardStart(new Period(1, TimeUnit.Years))
            .WithFixedRate(0.03)
            .ToVanillaSwap(context);

        // It builds, as QuantLib's does.
        var swaption = new Swaption(swap, new Exercise(ExerciseType.European));

        Assert.Equal(
            "no exercise date given", Assert.Throws<NanoQLException>(() => swaption.IsExpired(context)).Message);
        Assert.Equal(
            "no exercise date given",
            Assert.Throws<NanoQLException>(() => BlackSwaptionEngine.Calculate(swaption, context, curve, 0.2)).Message);
        Assert.Equal(
            "no exercise date given",
            Assert.Throws<NanoQLException>(() => swaption.ImpliedVolatility(0.01, curve, 0.1, context)).Message);
    }
}
