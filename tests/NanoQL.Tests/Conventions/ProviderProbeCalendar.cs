// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A calendar of Saturday and Sunday weekends that records <see cref="ConventionProvider.Current"/> each time it is
/// asked about a date, so a fact can tell which provider was ambient while an object built on it computed its dates.
/// </summary>
public sealed class ProviderProbeCalendar : Calendar
{
    private readonly ConcurrentDictionary<ConventionProvider, byte> seen = new(ReferenceEqualityComparer.Instance);

    /// <inheritdoc/>
    public override string Name => "provider probe";

    /// <summary>Gets the providers ambient when the calendar was asked about a date.</summary>
    public IReadOnlyCollection<ConventionProvider> Seen => [.. seen.Keys];

    /// <summary>Whether the provider was ambient when the calendar was asked about a date.</summary>
    /// <param name="provider">The provider.</param>
    /// <returns><see langword="true"/> when it was.</returns>
    public bool Saw(ConventionProvider provider) => seen.ContainsKey(provider);

    /// <summary>Forgets the providers seen so far.</summary>
    public void Clear() => seen.Clear();

    /// <inheritdoc/>
    public override bool IsWeekend(Weekday w) => w is Weekday.Saturday or Weekday.Sunday;

    /// <inheritdoc/>
    protected override bool IsBusinessDayCore(Date date)
    {
        seen.TryAdd(ConventionProvider.Current, 0);
        return !IsWeekend(date.Weekday);
    }
}
