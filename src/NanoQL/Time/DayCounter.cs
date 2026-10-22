// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/daycounter.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>
/// A day counter: the length of a period by a market convention, as a number of days and as a year fraction.
/// </summary>
/// <remarks>
/// <para>
/// Two day counters are equal when they are of the same type and have the same name; one whose counts read data its
/// name leaves out compares that data too: <see cref="Business252"/> its calendar, as calendars compare,
/// <see cref="ActualActual"/> with the ISMA or Bond convention the period dates of its schedule, and
/// <see cref="Thirty360"/> with the ISDA convention its termination date. QuantLib compares names alone
/// (<c>daycounter.hpp:135-138</c>), which is sound there only where a name fixes the counts. Comparing the runtime
/// types keeps equality symmetric when a derived day counter compares more than this does.
/// </para>
/// <para>QuantLib's empty day counter, <c>DayCounter()</c>, is <see langword="null"/>.</para>
/// </remarks>
public abstract class DayCounter : IEquatable<DayCounter>
{
    /// <summary>Gets the day counter's name, used for output and comparison; not meant for switch-on-type code.</summary>
    public abstract string Name { get; }

    /// <summary>Whether two day counters are equal: the same type, name and the data their counts read.</summary>
    /// <param name="d1">The first day counter.</param>
    /// <param name="d2">The second day counter.</param>
    /// <returns><see langword="true"/> when equal, or both are <see langword="null"/>.</returns>
    public static bool operator ==(DayCounter? d1, DayCounter? d2) => d1 is null ? d2 is null : d1.Equals(d2);

    /// <summary>Whether two day counters differ.</summary>
    /// <param name="d1">The first day counter.</param>
    /// <param name="d2">The second day counter.</param>
    /// <returns><see langword="true"/> when not equal.</returns>
    public static bool operator !=(DayCounter? d1, DayCounter? d2) => !(d1 == d2);

    /// <summary>The number of days between two dates.</summary>
    /// <param name="d1">The start date.</param>
    /// <param name="d2">The end date.</param>
    /// <returns>The day count; <c>d2 - d1</c> unless the convention says otherwise.</returns>
    public virtual int DayCount(Date d1, Date d2) => d2 - d1;

    /// <summary>The period between two dates as a fraction of a year.</summary>
    /// <param name="d1">The start date.</param>
    /// <param name="d2">The end date.</param>
    /// <param name="refPeriodStart">The reference period's start, for conventions that use one.</param>
    /// <param name="refPeriodEnd">The reference period's end, for conventions that use one.</param>
    /// <returns>The year fraction.</returns>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    public abstract double YearFraction(Date d1, Date d2, Date? refPeriodStart = null, Date? refPeriodEnd = null);

    /// <summary>
    /// Refuses <c>default(Date)</c> among <see cref="YearFraction"/>'s arguments, which a convention may not read; a
    /// derived day counter calls it first.
    /// </summary>
    /// <param name="d1">The start date.</param>
    /// <param name="d2">The end date.</param>
    /// <param name="refPeriodStart">The reference period's start, if any.</param>
    /// <param name="refPeriodEnd">The reference period's end, if any.</param>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    protected static void RequireDates(Date d1, Date d2, Date? refPeriodStart, Date? refPeriodEnd)
    {
        Date.ThrowIfDefault(d1, nameof(d1));
        Date.ThrowIfDefault(d2, nameof(d2));
        Date.ThrowIfDefault(refPeriodStart, nameof(refPeriodStart));
        Date.ThrowIfDefault(refPeriodEnd, nameof(refPeriodEnd));
    }

    /// <summary>Whether the day counter is of the same type and has the same name as another.</summary>
    /// <param name="other">The other day counter.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    /// <remarks>A day counter whose counts read data its name leaves out overrides this to compare that data
    /// too.</remarks>
    public virtual bool Equals(DayCounter? other) =>
        other is not null && (ReferenceEquals(this, other) || (GetType() == other.GetType() && Name == other.Name));

    /// <inheritdoc/>
    public sealed override bool Equals(object? obj) => Equals(obj as DayCounter);

    /// <inheritdoc/>
    public override int GetHashCode() => Name.GetHashCode(StringComparison.Ordinal);

    /// <summary>The day counter's name.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => Name;
}
