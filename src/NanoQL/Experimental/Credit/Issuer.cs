// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/issuer.hpp ql/experimental/credit/issuer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A credit name: the default-probability curves of its bond collections, each keyed by the contract conditions it
/// prices, and the history of its credit events.
/// </summary>
/// <remarks>
/// <para>
/// Immutable. QuantLib's curve handles are the curves themselves; QuantLib's empty handle, which its first read refuses,
/// is refused at construction.
/// </para>
/// <para>
/// The events are kept in date order, every one of them: two events on one date both count, in the order given.
/// QuantLib's event set orders by date alone (<c>issuer.hpp:36-38</c>), so it keeps one event per date and drops the
/// second, where its own documentation lets several events take place on one date. A documented divergence (the S-xcr
/// plan's XR8 (iv); <c>Divergences/IssuerEventsOnOneDateTests.cs</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class Issuer
{
    /// <summary>Initializes a new instance of the <see cref="Issuer"/> class.</summary>
    /// <param name="probabilities">The curves, each with its key: the probability of the issuer's bonds of the key's
    /// seniority and currency incurring the key's events; none when <see langword="null"/>.</param>
    /// <param name="events">The past events; none when <see langword="null"/>. No check is made that they make
    /// sense.</param>
    /// <exception cref="ArgumentNullException">A key, a curve or an event is <see langword="null"/>.</exception>
    public Issuer(
        IEnumerable<(DefaultProbKey Key, DefaultProbabilityTermStructure Curve)>? probabilities = null,
        IEnumerable<DefaultEvent>? events = null)
    {
        ImmutableArray<(DefaultProbKey Key, DefaultProbabilityTermStructure Curve)> p = probabilities is null ? [] : [.. probabilities];
        foreach ((DefaultProbKey key, DefaultProbabilityTermStructure curve) in p)
        {
            ArgumentNullException.ThrowIfNull(key, nameof(probabilities));
            ArgumentNullException.ThrowIfNull(curve, nameof(probabilities));
        }

        Probabilities = p;
        Events = Sorted(events);
    }

    /// <summary>Initializes a new instance of the <see cref="Issuer"/> class from the parts of its keys.</summary>
    /// <param name="eventTypes">Each key's event types.</param>
    /// <param name="currencies">Each key's currency; <see langword="null"/> elements are QuantLib's empty
    /// currency.</param>
    /// <param name="seniorities">Each key's seniority.</param>
    /// <param name="curves">Each key's curve.</param>
    /// <param name="events">The past events; none when <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">A list, an element of <paramref name="eventTypes"/> or
    /// <paramref name="curves"/>, or an event is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The lists' sizes differ ("Incompatible size of Issuer parameters."), or a
    /// key can't be built.</exception>
    public Issuer(
        IReadOnlyList<IReadOnlyList<DefaultType>> eventTypes,
        IReadOnlyList<Currency?> currencies,
        IReadOnlyList<Seniority> seniorities,
        IReadOnlyList<DefaultProbabilityTermStructure> curves,
        IEnumerable<DefaultEvent>? events = null)
    {
        ArgumentNullException.ThrowIfNull(eventTypes);
        ArgumentNullException.ThrowIfNull(currencies);
        ArgumentNullException.ThrowIfNull(seniorities);
        ArgumentNullException.ThrowIfNull(curves);
        if (eventTypes.Count != curves.Count || curves.Count != currencies.Count || currencies.Count != seniorities.Count)
        {
            throw new ArgumentException("Incompatible size of Issuer parameters.", nameof(eventTypes));
        }

        var p = ImmutableArray.CreateBuilder<(DefaultProbKey, DefaultProbabilityTermStructure)>(eventTypes.Count);
        for (int i = 0; i < eventTypes.Count; i++)
        {
            ArgumentNullException.ThrowIfNull(eventTypes[i], nameof(eventTypes));
            ArgumentNullException.ThrowIfNull(curves[i], nameof(curves));
            p.Add((new DefaultProbKey(eventTypes[i], currencies[i], seniorities[i]), curves[i]));
        }

        Probabilities = p.MoveToImmutable();
        Events = Sorted(events);
    }

    /// <summary>Gets the curves, each with its key.</summary>
    public ImmutableArray<(DefaultProbKey Key, DefaultProbabilityTermStructure Curve)> Probabilities { get; }

    /// <summary>Gets the past events, in date order, events on one date in the order given.</summary>
    public ImmutableArray<DefaultEvent> Events { get; }

    /// <summary>The curve of a key: the first whose key equals it.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The curve.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">No curve has the key ("Probability curve not available.").</exception>
    public DefaultProbabilityTermStructure DefaultProbability(DefaultProbKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        foreach ((DefaultProbKey k, DefaultProbabilityTermStructure curve) in Probabilities)
        {
            if (key == k)
            {
                return curve;
            }
        }

        throw new NanoQLException("Probability curve not available.");
    }

    /// <summary>The first event, in date order, that matches a key and occurs after a start date and by an end
    /// date.</summary>
    /// <param name="start">The start date.</param>
    /// <param name="end">The end date.</param>
    /// <param name="contractKey">The key.</param>
    /// <param name="context">The evaluation context, which a failure to pay's grace period reads.</param>
    /// <param name="includeRefDate">Whether an event on a date counts as not yet occurred on it (Event's
    /// rule).</param>
    /// <returns>The event, or <see langword="null"/> where none matches.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="contractKey"/> or <paramref name="context"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    public DefaultEvent? DefaultedBetween(
        Date start, Date end, DefaultProbKey contractKey, EvaluationContext context, bool includeRefDate = false)
    {
        ArgumentNullException.ThrowIfNull(contractKey);
        ArgumentNullException.ThrowIfNull(context);

        // to do: the set is ordered, see how to use it to speed this up
        foreach (DefaultEvent e in Events)
        {
            if (e.MatchesDefaultKey(contractKey, context) && Between(e, start, end, includeRefDate, context))
            {
                return e;
            }
        }

        return null;
    }

    /// <summary>Every event, in date order, that matches a key and occurs after a start date and by an end
    /// date.</summary>
    /// <param name="start">The start date.</param>
    /// <param name="end">The end date.</param>
    /// <param name="contractKey">The key.</param>
    /// <param name="includeRefDate">Whether an event on a date counts as not yet occurred on it.</param>
    /// <param name="context">The evaluation context, which a failure to pay's grace period reads.</param>
    /// <returns>The events.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="contractKey"/> or <paramref name="context"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    public ImmutableArray<DefaultEvent> DefaultsBetween(
        Date start, Date end, DefaultProbKey contractKey, bool includeRefDate, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(contractKey);
        ArgumentNullException.ThrowIfNull(context);
        var defaults = ImmutableArray.CreateBuilder<DefaultEvent>();

        // to do: the set is ordered, see how to use it to speed this up
        foreach (DefaultEvent e in Events)
        {
            if (e.MatchesDefaultKey(contractKey, context) && Between(e, start, end, includeRefDate, context))
            {
                defaults.Add(e);
            }
        }

        return defaults.ToImmutable();
    }

    private static bool Between(DefaultEvent e, Date start, Date end, bool includeRefDate, EvaluationContext context) =>
        !e.HasOccurred(context, start, includeRefDate) && e.HasOccurred(context, end, includeRefDate);

    private static ImmutableArray<DefaultEvent> Sorted(IEnumerable<DefaultEvent>? events)
    {
        if (events is null)
        {
            return [];
        }

        DefaultEvent[] e = [.. events];
        foreach (DefaultEvent x in e)
        {
            ArgumentNullException.ThrowIfNull(x, nameof(events));
        }

        // a stable sort by date: events on one date keep the order given
        return [.. e.OrderBy(x => x.Date)];
    }
}
