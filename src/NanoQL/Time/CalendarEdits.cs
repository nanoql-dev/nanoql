// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;

namespace NanoQL.Time;

/// <summary>
/// A calendar preset's holiday edits, which <see cref="ConventionProvider.ResolveCalendarEdits"/> returns: the dates
/// added to its holidays and the dates removed from them, applied after its rules as
/// <see cref="Calendar.AddHoliday"/> and <see cref="Calendar.RemoveHoliday"/> apply them.
/// </summary>
/// <remarks>
/// Two edits are equal when they add the same dates and remove the same dates. A date is either added or removed,
/// never both.
/// </remarks>
public sealed record CalendarEdits
{
    /// <summary>Initializes a new instance of the <see cref="CalendarEdits"/> class.</summary>
    /// <param name="added">The dates added to the calendar's holidays.</param>
    /// <param name="removed">The dates removed from them.</param>
    /// <exception cref="ArgumentNullException">A sequence is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A date is <c>default(Date)</c>, or is both added and removed.
    /// </exception>
    public CalendarEdits(IEnumerable<Date> added, IEnumerable<Date> removed)
    {
        Added = Dates(added, nameof(added));
        Removed = Dates(removed, nameof(removed));
        if (Added.Overlaps(Removed))
        {
            throw new ArgumentException(
                $"{Added.Intersect(Removed).Min} is both added to the holidays and removed from them", nameof(removed));
        }
    }

    /// <summary>Gets the edits of QuantLib's presets: none.</summary>
    public static CalendarEdits None { get; } = new([], []);

    /// <summary>Gets the dates added to the calendar's holidays.</summary>
    public ImmutableSortedSet<Date> Added { get; }

    /// <summary>Gets the dates removed from the calendar's holidays.</summary>
    public ImmutableSortedSet<Date> Removed { get; }

    /// <summary>Whether the edits add and remove the same dates.</summary>
    /// <param name="other">The other edits.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    public bool Equals(CalendarEdits? other) =>
        other is not null && Added.SetEquals(other.Added) && Removed.SetEquals(other.Removed);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (Date d in Added)
        {
            hash.Add(d);
        }

        hash.Add(-1);
        foreach (Date d in Removed)
        {
            hash.Add(d);
        }

        return hash.ToHashCode();
    }

    private static ImmutableSortedSet<Date> Dates(IEnumerable<Date> dates, string paramName)
    {
        ArgumentNullException.ThrowIfNull(dates, paramName);
        ImmutableSortedSet<Date>.Builder builder = ImmutableSortedSet.CreateBuilder<Date>();
        foreach (Date d in dates)
        {
            Date.ThrowIfDefault(d, paramName);
            builder.Add(d);
        }

        return builder.ToImmutable();
    }
}
