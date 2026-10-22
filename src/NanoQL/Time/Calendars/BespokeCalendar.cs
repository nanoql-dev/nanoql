// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/bespokecalendar.hpp ql/time/calendars/bespokecalendar.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>Bespoke calendar.</summary>
/// <remarks>
/// <para>
/// This calendar has no predefined set of business days. Holidays and weekend days are defined by means of the
/// provided interface: <see cref="AddWeekend"/>, <see cref="Calendar.AddHoliday"/> and
/// <see cref="Calendar.RemoveHoliday"/>, each returning a modified copy. In QuantLib, copies of a bespoke calendar
/// stay linked to the original, and an edit changes them all.
/// </para>
/// <para>
/// Two bespoke calendars are equal when they have the same name, the same weekend, the same added holidays that fall
/// on a working weekday, and the same removed holidays; QuantLib compares their names alone, so that "different
/// bespoke calendars created with the same name (or different bespoke calendars created with no name) will compare as
/// equal" (<c>bespokecalendar.hpp:55-58</c>). A holiday added before its weekday became a weekend day stays in
/// <see cref="Calendar.AddedHolidays"/>, as QuantLib records it, but doesn't count: the calendar equals one rebuilt
/// with its weekend first and its holidays after, whatever the order of the edits.
/// </para>
/// </remarks>
public sealed class BespokeCalendar : Calendar
{
    private readonly string name;

    // Written only on a fresh copy (AddWeekend); bit w set for each weekend day w, as QuantLib's mask.
    private int weekendMask;

    /// <summary>Initializes a new instance of the <see cref="BespokeCalendar"/> class with no weekend and no
    /// holidays.</summary>
    /// <param name="name">The calendar's name.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public BespokeCalendar(string name = "")
    {
        ArgumentNullException.ThrowIfNull(name);
        this.name = name;
    }

    /// <inheritdoc/>
    public override string Name => name;

    /// <inheritdoc/>
    public override bool IsWeekend(Weekday w) =>
        w is >= Weekday.Sunday and <= Weekday.Saturday && (weekendMask & (1 << (int)w)) != 0;

    /// <summary>A copy of the calendar with the weekday marked as part of the weekend.</summary>
    /// <param name="w">The weekday.</param>
    /// <returns>The modified copy; the receiver is unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The weekday isn't a member of the enum.</exception>
    public BespokeCalendar AddWeekend(Weekday w)
    {
        EnumArguments.Check(w, nameof(w));
        var copy = (BespokeCalendar)MemberwiseClone();
        copy.weekendMask = weekendMask | (1 << (int)w);
        return copy;
    }

    /// <summary>
    /// Whether the calendar equals another: a bespoke calendar with the same name, weekend, added holidays on working
    /// weekdays and removed holidays.
    /// </summary>
    /// <param name="other">The other calendar.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    public override bool Equals(Calendar? other) =>
        other is BespokeCalendar b
        && (ReferenceEquals(this, b)
            || (name == b.name
                && weekendMask == b.weekendMask
                && WorkingDayHolidays().SequenceEqual(b.WorkingDayHolidays())
                && RemovedHolidays.SetEquals(b.RemovedHolidays)));

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(name);
        hash.Add(weekendMask);
        foreach (Date d in WorkingDayHolidays())
        {
            hash.Add(d);
        }

        hash.Add(-1);
        foreach (Date d in RemovedHolidays)
        {
            hash.Add(d);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    protected override bool IsBusinessDayCore(Date date) => !IsWeekend(date.Weekday);

    // The added holidays that fall on a working weekday, in order: the ones that change an answer.
    private IEnumerable<Date> WorkingDayHolidays() => AddedHolidays.Where(d => !IsWeekend(d.Weekday));
}
