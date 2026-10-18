// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: Copyright (C) 2010 Liquidnet Holdings, Inc.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/timeseries.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections;
using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL;

/// <summary>
/// A container for historical data: a datum for each of a set of dates, read by date or in date order.
/// </summary>
/// <remarks>
/// <para>
/// Immutable. QuantLib's inserting <c>operator[]</c> and its inserting <c>find</c> aren't ported: a missing date is
/// read with <see cref="TryGetValue"/>, which leaves the series unchanged, where QuantLib's <c>operator[] const</c>
/// returns <c>Null&lt;T&gt;()</c>. QuantLib's container template parameter isn't ported: the series always keeps its
/// data in date order.
/// </para>
/// <para>
/// Two series are equal when they hold the same dates with equal values, compared by
/// <see cref="EqualityComparer{T}.Default"/>; the hash is computed when the series is built.
/// </para>
/// </remarks>
/// <typeparam name="T">The datum's type.</typeparam>
public sealed class TimeSeries<T> : IEquatable<TimeSeries<T>>, IEnumerable<KeyValuePair<Date, T>>
{
    private readonly ImmutableArray<Date> dates;
    private readonly ImmutableArray<T> values;
    private readonly int hash;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeSeries{T}"/> class from dates and their values, taken in
    /// pairs. A date given twice keeps its last value; values beyond the last date are ignored, as QuantLib's
    /// iterator constructor ignores them.
    /// </summary>
    /// <param name="dates">The dates, in any order.</param>
    /// <param name="values">The values, one per date.</param>
    /// <exception cref="ArgumentException">There are fewer values than dates, or a date is <c>default(Date)</c>.</exception>
    public TimeSeries(IEnumerable<Date> dates, IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(dates);
        ArgumentNullException.ThrowIfNull(values);
        var data = new SortedDictionary<Date, T>();
        using (IEnumerator<T> v = values.GetEnumerator())
        {
            foreach (Date d in dates)
            {
                Date.ThrowIfDefault(d, nameof(dates));
                if (!v.MoveNext())
                {
                    // QuantLib reads past the end of the values here, which is undefined.
                    throw new ArgumentException("fewer values than dates", nameof(values));
                }

                data[d] = v.Current;
            }
        }

        (this.dates, this.values) = Split(data);
        hash = ComputeHash(this.dates, this.values);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeSeries{T}"/> class from values on consecutive calendar dates,
    /// the first on <paramref name="firstDate"/>.
    /// </summary>
    /// <param name="firstDate">The date of the first value.</param>
    /// <param name="values">The values.</param>
    /// <exception cref="ArgumentException"><paramref name="firstDate"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A value falls on the last valid date: QuantLib moves the date past every value, the last included, and refuses
    /// to move it past the last valid date.
    /// </exception>
    public TimeSeries(Date firstDate, IEnumerable<T> values)
    {
        Date.ThrowIfDefault(firstDate, nameof(firstDate));
        ArgumentNullException.ThrowIfNull(values);
        var data = new SortedDictionary<Date, T>();
        Date d = firstDate;
        foreach (T value in values)
        {
            // QuantLib's values_[d++]: the increment after the last value refuses the last valid date, with the
            // date's range message.
            if (d == Date.MaxDate)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(values),
                    $"Date's serial number ({d.SerialNumber + 1}) outside allowed range "
                    + $"[{Date.MinDate.SerialNumber}-{Date.MaxDate.SerialNumber}], i.e. [{Date.MinDate}-{Date.MaxDate}]");
            }

            data[d] = value;
            d++;
        }

        (dates, this.values) = Split(data);
        hash = ComputeHash(dates, this.values);
    }

    private TimeSeries(ImmutableArray<Date> dates, ImmutableArray<T> values)
    {
        this.dates = dates;
        this.values = values;
        hash = ComputeHash(dates, values);
    }

    /// <summary>Gets the number of data.</summary>
    public int Size => dates.Length;

    /// <summary>Gets a value indicating whether the series holds no data.</summary>
    public bool Empty => dates.IsEmpty;

    /// <summary>The first date for which a datum exists.</summary>
    /// <returns>The date.</returns>
    /// <exception cref="NanoQLException">The series is empty.</exception>
    public Date FirstDate() => !dates.IsEmpty ? dates[0] : throw new NanoQLException("empty timeseries");

    /// <summary>The last date for which a datum exists.</summary>
    /// <returns>The date.</returns>
    /// <exception cref="NanoQLException">The series is empty.</exception>
    public Date LastDate() => !dates.IsEmpty ? dates[^1] : throw new NanoQLException("empty timeseries");

    /// <summary>Reads the datum for a date, without changing the series.</summary>
    /// <param name="date">The date.</param>
    /// <param name="value">The datum, or <c>default</c> when the date has none.</param>
    /// <returns><see langword="true"/> when the date has a datum.</returns>
    public bool TryGetValue(Date date, out T value)
    {
        int i = dates.BinarySearch(date);
        if (i >= 0)
        {
            value = values[i];
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>The dates for which data exist, in order.</summary>
    /// <returns>The dates.</returns>
    public ImmutableArray<Date> Dates() => dates;

    /// <summary>The data, in date order.</summary>
    /// <returns>The values.</returns>
    public ImmutableArray<T> Values() => values;

    /// <summary>Enumerates the data in date order.</summary>
    /// <returns>The enumerator.</returns>
    public IEnumerator<KeyValuePair<Date, T>> GetEnumerator()
    {
        for (int i = 0; i < dates.Length; ++i)
        {
            yield return new KeyValuePair<Date, T>(dates[i], values[i]);
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public bool Equals(TimeSeries<T>? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || hash != other.hash || dates.Length != other.dates.Length)
        {
            return false;
        }

        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < dates.Length; ++i)
        {
            if (dates[i] != other.dates[i] || !comparer.Equals(values[i], other.values[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TimeSeries<T> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => hash;

    /// <summary>Gets an empty series.</summary>
    internal static TimeSeries<T> None { get; } = new(ImmutableArray<Date>.Empty, ImmutableArray<T>.Empty);

    private static (ImmutableArray<Date> Dates, ImmutableArray<T> Values) Split(SortedDictionary<Date, T> data)
    {
        ImmutableArray<Date>.Builder d = ImmutableArray.CreateBuilder<Date>(data.Count);
        ImmutableArray<T>.Builder v = ImmutableArray.CreateBuilder<T>(data.Count);
        foreach (KeyValuePair<Date, T> kv in data)
        {
            d.Add(kv.Key);
            v.Add(kv.Value);
        }

        return (d.MoveToImmutable(), v.MoveToImmutable());
    }

    private static int ComputeHash(ImmutableArray<Date> dates, ImmutableArray<T> values)
    {
        var h = default(HashCode);
        h.Add(dates.Length);
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < dates.Length; ++i)
        {
            h.Add(dates[i]);
            h.Add(values[i], comparer);
        }

        return h.ToHashCode();
    }
}
