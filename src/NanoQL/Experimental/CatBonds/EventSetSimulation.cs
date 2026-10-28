// SPDX-FileCopyrightText: Copyright (C) 2012, 2013 Grzegorz Andruszkiewicz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/catbonds/catrisk.hpp ql/experimental/catbonds/catrisk.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The simulation of an <see cref="EventSet"/>: its paths are the set's events in successive periods as long as the
/// simulated one, from the first such period starting in the set's first year, shifted to the simulated period, until
/// the set runs out.
/// </summary>
/// <remarks>
/// A period starting or ending on 29 February in a year without one takes 28 February, QuantLib's own clamp for a date
/// plus years, where QuantLib's constructor throws ("day outside month") (the S-xcr plan's XR8 (xvii);
/// <c>Divergences/EventSetTests.cs</c>).
/// </remarks>
[Experimental("NQL0001")]
public sealed class EventSetSimulation : CatSimulation
{
    private readonly ImmutableArray<KeyValuePair<Date, double>> events;
    private readonly Date eventsEnd;
    private readonly int years;
    private Date periodStart;
    private Date periodEnd;
    private int i;

    /// <summary>Initializes a new instance of the <see cref="EventSetSimulation"/> class.</summary>
    /// <param name="events">The events, their dates and losses, in date order.</param>
    /// <param name="eventsStart">The start of the period the events cover.</param>
    /// <param name="eventsEnd">The end of the period the events cover.</param>
    /// <param name="start">The simulated period's start.</param>
    /// <param name="end">The simulated period's end.</param>
    public EventSetSimulation(ImmutableArray<KeyValuePair<Date, double>> events, Date eventsStart, Date eventsEnd, Date start, Date end)
        : base(start, end)
    {
        this.events = events;
        this.eventsEnd = eventsEnd;
        years = End.Year - Start.Year;
        if (eventsStart.Month < Start.Month
            || (eventsStart.Month == Start.Month
                && eventsStart.DayOfMonth <= Start.DayOfMonth))
        {
            periodStart = DateIn(Start.DayOfMonth, Start.Month, eventsStart.Year);
        }
        else
        {
            periodStart = DateIn(Start.DayOfMonth, Start.Month, eventsStart.Year + 1);
        }

        periodEnd = DateIn(End.DayOfMonth, End.Month, periodStart.Year + years);

        // i points to the first element after the start of the relevant period.
        while (i < this.events.Length && this.events[i].Key < periodStart)
        {
            ++i;
        }
    }

    /// <inheritdoc/>
    public override bool NextPath(List<KeyValuePair<Date, double>> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        path.Clear();

        // Ran out of event data
        if (periodEnd > eventsEnd)
        {
            return false;
        }

        while (i < events.Length && events[i].Key < periodStart)
        {
            // skip the elements between the previous period and this period
            ++i;
        }

        while (i < events.Length && events[i].Key <= periodEnd)
        {
            var e = new KeyValuePair<Date, double>(
                events[i].Key + new Period(Start.Year - periodStart.Year, TimeUnit.Years), events[i].Value);
            path.Add(e);

            // i points to the first element after the start of the relevant period.
            ++i;
        }

        if (Start + new Period(years, TimeUnit.Years) < End)
        {
            periodStart += new Period(years + 1, TimeUnit.Years);
            periodEnd += new Period(years + 1, TimeUnit.Years);
        }
        else
        {
            periodStart += new Period(years, TimeUnit.Years);
            periodEnd += new Period(years, TimeUnit.Years);
        }

        return true;
    }

    // A date from its day, month and year, 28 February for 29 February in a year without one (XR8 (xvii)).
    private static Date DateIn(int day, Month month, int year)
    {
        Date last = Date.EndOfMonth(new Date(1, month, year));
        return day > last.DayOfMonth ? last : new Date(day, month, year);
    }
}
