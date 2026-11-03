// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/dateinterval.hpp ql/experimental/commodities/dateinterval.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>An interval of dates, from a start date to an end date, both held.</summary>
/// <remarks>
/// <para>
/// Immutable, and the base of <see cref="PricingPeriod"/>. QuantLib's default <c>DateInterval()</c>, two null dates,
/// is a <c>DateInterval?</c> null where QuantLib returns it (<see cref="Intersection"/> of two disjoint intervals).
/// </para>
/// <para>
/// <see cref="IsDateBetween"/> includes or excludes each end as its flags say. QuantLib's excludes both ends whatever
/// they say: its <c>else if</c> follows the <c>includeFirst</c> test, so a start date that passes <c>date &gt;=
/// startDate</c> then fails <c>date &gt; startDate</c>, and the end likewise (<c>dateinterval.hpp:51-63</c>;
/// <c>Divergences/DateIntervalTests.cs</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public class DateInterval : IEquatable<DateInterval>
{
    /// <summary>Initializes a new instance of the <see cref="DateInterval"/> class.</summary>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date, on or after the start date.</param>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The end date is before the start date.</exception>
    public DateInterval(Date startDate, Date endDate)
    {
        Date.ThrowIfDefault(startDate, nameof(startDate));
        Date.ThrowIfDefault(endDate, nameof(endDate));
        if (!(endDate >= startDate))
        {
            throw new NanoQLException("end date must be >= start date");
        }

        StartDate = startDate;
        EndDate = endDate;
    }

    /// <summary>Gets the start date.</summary>
    public Date StartDate { get; }

    /// <summary>Gets the end date.</summary>
    public Date EndDate { get; }

    /// <summary>Whether two intervals have the same start and end dates.</summary>
    /// <param name="left">An interval, or <see langword="null"/>.</param>
    /// <param name="right">Another interval, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both are null or their dates are the same.</returns>
    public static bool operator ==(DateInterval? left, DateInterval? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Whether two intervals differ.</summary>
    /// <param name="left">An interval, or <see langword="null"/>.</param>
    /// <param name="right">Another interval, or <see langword="null"/>.</param>
    /// <returns>The negation of <c>==</c>.</returns>
    public static bool operator !=(DateInterval? left, DateInterval? right) => !(left == right);

    /// <summary>Whether a date lies in the interval.</summary>
    /// <param name="date">The date.</param>
    /// <param name="includeFirst">Whether the start date counts as in the interval.</param>
    /// <param name="includeLast">Whether the end date counts as in the interval.</param>
    /// <returns><see langword="true"/> when the date lies between the ends, each end included as its flag says.</returns>
    /// <exception cref="ArgumentException"><paramref name="date"/> is <c>default(Date)</c>.</exception>
    public bool IsDateBetween(Date date, bool includeFirst = true, bool includeLast = true)
    {
        Date.ThrowIfDefault(date, nameof(date));
        bool afterStart = includeFirst ? date >= StartDate : date > StartDate;
        bool beforeEnd = includeLast ? date <= EndDate : date < EndDate;
        return afterStart && beforeEnd;
    }

    /// <summary>The intersection of this interval with another.</summary>
    /// <param name="di">The other interval.</param>
    /// <returns>The common dates' interval; <see langword="null"/> when the two are disjoint, QuantLib's empty
    /// interval.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="di"/> is null.</exception>
    public DateInterval? Intersection(DateInterval di)
    {
        ArgumentNullException.ThrowIfNull(di);
        if ((StartDate < di.StartDate && EndDate < di.StartDate) || (StartDate > di.EndDate && EndDate > di.EndDate))
        {
            return null;
        }

        return new DateInterval(
            StartDate > di.StartDate ? StartDate : di.StartDate, EndDate < di.EndDate ? EndDate : di.EndDate);
    }

    /// <summary>Whether this interval has another's start and end dates: QuantLib's <c>operator==</c>, which a pricing
    /// period inherits.</summary>
    /// <param name="other">The other interval.</param>
    /// <returns><see langword="true"/> when the dates are the same.</returns>
    public bool Equals(DateInterval? other) => other is not null && StartDate == other.StartDate && EndDate == other.EndDate;

    /// <inheritdoc/>
    public sealed override bool Equals(object? obj) => obj is DateInterval other && Equals(other);

    /// <inheritdoc/>
    public sealed override int GetHashCode() => HashCode.Combine(StartDate, EndDate);

    /// <summary>The interval as QuantLib's <c>operator&lt;&lt;</c> writes it: "start to end".</summary>
    /// <returns>The text.</returns>
    public override string ToString() => $"{StartDate} to {EndDate}";
}
