// SPDX-FileCopyrightText: Copyright (C) 2010 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2010, 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/vanillaswingoption.hpp ql/instruments/vanillaswingoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A swing exercise: the option can be exercised only at a set of fixed date times, each a date and a number of seconds
/// into it.
/// </summary>
/// <remarks>
/// Immutable. The date times are sorted together, where QuantLib's Bermudan base sorts the dates and leaves the seconds
/// in the order given, so that date times given out of order are paired wrongly; and a fixed step of a day or more
/// moves as many days as it holds, where QuantLib's moves one, so that a two-day step exercises daily. In order, and at
/// steps under a day, they are QuantLib's. Two equal date times are refused, with QuantLib's message.
/// </remarks>
public sealed class SwingExercise : BermudanExercise
{
    private const int SecPerDay = 24 * 3600;

    /// <summary>Initializes a new instance of the <see cref="SwingExercise"/> class from date times.</summary>
    /// <param name="dates">The exercise dates.</param>
    /// <param name="seconds">The seconds into each date; empty, the default, for 0 at each.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="dates"/> is empty or holds <c>default(Date)</c>, the two differ in number, or two date times are
    /// equal, with QuantLib's messages.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">A number of seconds is negative or a day or more.</exception>
    public SwingExercise(IEnumerable<NanoQL.Time.Date> dates, IEnumerable<int>? seconds = null)
        : this(Sorted(dates, seconds))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwingExercise"/> class at a fixed step from one date to another.
    /// </summary>
    /// <param name="from">The first date, at its start.</param>
    /// <param name="to">The last date the steps may fall on.</param>
    /// <param name="stepSizeSecs">The step, in seconds.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stepSizeSecs"/> isn't positive.</exception>
    /// <exception cref="ArgumentException">
    /// A date is <c>default(Date)</c>, or <paramref name="to"/> is before <paramref name="from"/>.
    /// </exception>
    public SwingExercise(NanoQL.Time.Date from, NanoQL.Time.Date to, int stepSizeSecs)
        : this(CreateDateTimes(from, to, stepSizeSecs))
    {
    }

    private SwingExercise((ImmutableArray<NanoQL.Time.Date> Dates, ImmutableArray<int> Seconds) dateTimes)
        : base(dateTimes.Dates)
    {
        Seconds = dateTimes.Seconds;
    }

    /// <summary>Gets the seconds into each exercise date, in the dates' order.</summary>
    public ImmutableArray<int> Seconds { get; }

    /// <summary>The exercise times: each date's year fraction from a reference date, plus its seconds as a share of the
    /// next day's year fraction.</summary>
    /// <param name="dc">The day counter.</param>
    /// <param name="refDate">The reference date.</param>
    /// <returns>The times, in order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dc"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A time is negative, with QuantLib's message.</exception>
    public ImmutableArray<double> ExerciseTimes(DayCounter dc, NanoQL.Time.Date refDate)
    {
        ArgumentNullException.ThrowIfNull(dc);
        ImmutableArray<NanoQL.Time.Date> dates = Dates;
        var exerciseTimes = ImmutableArray.CreateBuilder<double>(dates.Length);
        for (int i = 0; i < dates.Length; ++i)
        {
            double t = dc.YearFraction(refDate, dates[i]);

            double dt = dc.YearFraction(refDate, dates[i] + new Period(1, TimeUnit.Days)) - t;

            t += dt * Seconds[i] / (24 * 3600.0);

            if (!(t >= 0))
            {
                throw new ArgumentException("exercise dates must not contain past date", nameof(refDate));
            }

            exerciseTimes.Add(t);
        }

        return exerciseTimes.MoveToImmutable();
    }

    private static (ImmutableArray<NanoQL.Time.Date> Dates, ImmutableArray<int> Seconds) Sorted(
        IEnumerable<NanoQL.Time.Date> dates, IEnumerable<int>? seconds)
    {
        ArgumentNullException.ThrowIfNull(dates);
        NanoQL.Time.Date[] d = [.. dates];
        int[] s = seconds is null ? [] : [.. seconds];
        if (s.Length == 0)
        {
            s = new int[d.Length];
        }

        if (d.Length != s.Length)
        {
            throw new ArgumentException("dates and seconds must have the same size", nameof(seconds));
        }

        foreach (int x in s)
        {
            if (x < 0 || x >= SecPerDay)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), x, "a date can not have more than 24*3600 seconds");
            }
        }

        // the date times sorted together, stably (BermudanExercise's own sort then keeps them)
        int[] order = [.. Enumerable.Range(0, d.Length).OrderBy(i => d[i]).ThenBy(i => s[i])];
        NanoQL.Time.Date[] sortedDates = [.. order.Select(i => d[i])];
        int[] sortedSeconds = [.. order.Select(i => s[i])];
        for (int i = 1; i < sortedDates.Length; ++i)
        {
            if (!(sortedDates[i - 1] < sortedDates[i]
                || (sortedDates[i - 1] == sortedDates[i] && sortedSeconds[i - 1] < sortedSeconds[i])))
            {
                throw new ArgumentException("date times must be sorted", nameof(dates));
            }
        }

        return ([.. sortedDates], [.. sortedSeconds]);
    }

    // createDateTimes: from the start of from, a step at a time while the date is at most to; a step of a day or more
    // passes as many dates as it holds days, where QuantLib's passes one (a run find)
    private static (ImmutableArray<NanoQL.Time.Date> Dates, ImmutableArray<int> Seconds) CreateDateTimes(
        NanoQL.Time.Date from, NanoQL.Time.Date to, int stepSizeSecs)
    {
        NanoQL.Time.Date.ThrowIfDefault(from, nameof(from));
        NanoQL.Time.Date.ThrowIfDefault(to, nameof(to));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stepSizeSecs);
        if (to < from)
        {
            throw new ArgumentException("no exercise date given", nameof(to));
        }

        var secs = ImmutableArray.CreateBuilder<int>();
        var dates = ImmutableArray.CreateBuilder<NanoQL.Time.Date>();

        NanoQL.Time.Date iterDate = from;
        long iterStepSize = 0;

        while (iterDate <= to)
        {
            dates.Add(iterDate);
            secs.Add((int)iterStepSize);

            // a whole day for each day the step passes, where QuantLib's adds one whatever the step
            iterStepSize += stepSizeSecs;
            if (iterStepSize >= SecPerDay)
            {
                iterDate += (int)(iterStepSize / SecPerDay);
                iterStepSize %= SecPerDay;
            }
        }

        return (dates.ToImmutable(), secs.ToImmutable());
    }
}
