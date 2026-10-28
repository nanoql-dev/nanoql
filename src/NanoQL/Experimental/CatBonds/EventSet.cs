// SPDX-FileCopyrightText: Copyright (C) 2012, 2013 Grzegorz Andruszkiewicz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/catbonds/catrisk.hpp ql/experimental/catbonds/catrisk.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A catastrophe risk given by a historical set of events, each path the events of one period of the set's
/// years, shifted to the simulated period.</summary>
[Experimental("NQL0001")]
public sealed class EventSet : CatRisk
{
    private readonly ImmutableArray<KeyValuePair<Date, double>> events;
    private readonly Date eventsStart;
    private readonly Date eventsEnd;

    /// <summary>Initializes a new instance of the <see cref="EventSet"/> class.</summary>
    /// <param name="events">The events, their dates and losses, in date order.</param>
    /// <param name="eventsStart">The start of the period the events cover.</param>
    /// <param name="eventsEnd">The end of the period the events cover.</param>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public EventSet(IEnumerable<KeyValuePair<Date, double>> events, Date eventsStart, Date eventsEnd)
    {
        ArgumentNullException.ThrowIfNull(events);
        this.events = [.. events];
        this.eventsStart = eventsStart;
        this.eventsEnd = eventsEnd;
    }

    /// <inheritdoc/>
    public override CatSimulation NewSimulation(Date start, Date end) =>
        new EventSetSimulation(events, eventsStart, eventsEnd, start, end);
}
