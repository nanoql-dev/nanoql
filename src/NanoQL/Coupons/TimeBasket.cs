// SPDX-FileCopyrightText: Copyright (C) 2003 Decillion Pty(Ltd)
// SPDX-FileCopyrightText: Copyright (C) 2003 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/timebasket.hpp ql/cashflows/timebasket.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections;
using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>A distribution of values over dates: a map from each date to its value, in date order.</summary>
/// <remarks>
/// Immutable (the S-cms plan's CMD10): QuantLib's map is changed in place by its <c>operator[]</c>, <c>+=</c> and
/// <c>-=</c>; here the indexer reads, a date the basket doesn't hold reading 0 as QuantLib's inserted entry does, and
/// <see cref="op_Addition"/>, <see cref="op_Subtraction"/> and <see cref="Rebin"/> return new baskets.
/// </remarks>
public sealed class TimeBasket : IReadOnlyCollection<KeyValuePair<Date, double>>
{
    private readonly ImmutableSortedDictionary<Date, double> entries;

    /// <summary>Initializes a new instance of the <see cref="TimeBasket"/> class, empty.</summary>
    public TimeBasket()
        : this(ImmutableSortedDictionary<Date, double>.Empty)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TimeBasket"/> class: each date with its value, a repeated
    /// date taking its last value.</summary>
    /// <param name="dates">The dates.</param>
    /// <param name="values">The values, one per date.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The dates and values differ in number, QuantLib's "number of dates differs
    /// from number of values".</exception>
    public TimeBasket(IEnumerable<Date> dates, IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(dates);
        ArgumentNullException.ThrowIfNull(values);
        Date[] d = [.. dates];
        double[] v = [.. values];
        if (!(d.Length == v.Length))
        {
            throw new ArgumentException("number of dates differs from number of values", nameof(values));
        }

        ImmutableSortedDictionary<Date, double>.Builder builder = ImmutableSortedDictionary.CreateBuilder<Date, double>();
        for (int i = 0; i < d.Length; i++)
        {
            Date.ThrowIfDefault(d[i], nameof(dates));
            builder[d[i]] = v[i];
        }

        entries = builder.ToImmutable();
    }

    private TimeBasket(ImmutableSortedDictionary<Date, double> entries)
    {
        this.entries = entries;
    }

    /// <summary>Gets the number of entries.</summary>
    public int Count => entries.Count;

    /// <summary>Gets the value on a date, 0 for a date the basket doesn't hold.</summary>
    /// <param name="date">The date.</param>
    /// <returns>The value.</returns>
    public double this[Date date] => entries.TryGetValue(date, out double value) ? value : 0.0;

    /// <summary>The sum of two baskets, date by date: QuantLib's <c>+=</c>, on a new basket.</summary>
    /// <param name="left">The first basket.</param>
    /// <param name="right">The second basket.</param>
    /// <returns>A basket holding both baskets' dates.</returns>
    /// <exception cref="ArgumentNullException">A basket is <see langword="null"/>.</exception>
    public static TimeBasket operator +(TimeBasket left, TimeBasket right) => Combine(left, right, 1.0);

    /// <summary>The difference of two baskets, date by date: QuantLib's <c>-=</c>, on a new basket.</summary>
    /// <param name="left">The basket.</param>
    /// <param name="right">The basket subtracted.</param>
    /// <returns>A basket holding both baskets' dates.</returns>
    /// <exception cref="ArgumentNullException">A basket is <see langword="null"/>.</exception>
    public static TimeBasket operator -(TimeBasket left, TimeBasket right) => Combine(left, right, -1.0);

    /// <summary>The sum of two baskets, date by date.</summary>
    /// <param name="left">The first basket.</param>
    /// <param name="right">The second basket.</param>
    /// <returns>A basket holding both baskets' dates.</returns>
    public static TimeBasket Add(TimeBasket left, TimeBasket right) => left + right;

    /// <summary>The difference of two baskets, date by date.</summary>
    /// <param name="left">The basket.</param>
    /// <param name="right">The basket subtracted.</param>
    /// <returns>A basket holding both baskets' dates.</returns>
    public static TimeBasket Subtract(TimeBasket left, TimeBasket right) => left - right;

    /// <summary>Whether the basket holds a date.</summary>
    /// <param name="date">The date.</param>
    /// <returns>Whether it does.</returns>
    public bool HasDate(Date date) => entries.ContainsKey(date);

    /// <summary>The entries redistributed over the given dates: each entry to its date's bucket, or split between the
    /// two buckets around it in proportion to its days to each; an entry before the first bucket or after the last goes
    /// to it.</summary>
    /// <param name="buckets">The buckets' dates, in any order.</param>
    /// <returns>A basket holding the buckets.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="buckets"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There are no buckets, QuantLib's "empty bucket structure".</exception>
    public TimeBasket Rebin(IEnumerable<Date> buckets)
    {
        ArgumentNullException.ThrowIfNull(buckets);
        Date[] sbuckets = [.. buckets];
        if (sbuckets.Length == 0)
        {
            throw new ArgumentException("empty bucket structure", nameof(buckets));
        }

        Array.Sort(sbuckets);
        var result = new SortedDictionary<Date, double>();
        foreach (Date bucket in sbuckets)
        {
            result[bucket] = 0.0;
        }

        foreach ((Date date, double value) in entries)
        {
            // std::lower_bound: the first bucket not before the date
            int bi = LowerBound(sbuckets, date);
            Date pDate = bi == sbuckets.Length ? sbuckets[^1] : sbuckets[bi];
            Date? nDate = bi != 0 && bi != sbuckets.Length ? sbuckets[bi - 1] : null;
            if (pDate == date || nDate is null)
            {
                result[pDate] += value;
            }
            else
            {
                double pDays = pDate - date;
                double nDays = date - nDate.Value;
                double tDays = pDate - nDate.Value;
                result[pDate] += value * (nDays / tDays);
                result[nDate.Value] += value * (pDays / tDays);
            }
        }

        return new TimeBasket(result.ToImmutableSortedDictionary());
    }

    /// <summary>The entries in date order, QuantLib's map iterators.</summary>
    /// <returns>The enumerator.</returns>
    public IEnumerator<KeyValuePair<Date, double>> GetEnumerator() => entries.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static TimeBasket Combine(TimeBasket left, TimeBasket right, double sign)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        ImmutableSortedDictionary<Date, double>.Builder builder = left.entries.ToBuilder();
        foreach ((Date date, double value) in right.entries)
        {
            double current = builder.TryGetValue(date, out double v) ? v : 0.0;
            builder[date] = sign > 0.0 ? current + value : current - value;
        }

        return new TimeBasket(builder.ToImmutable());
    }

    private static int LowerBound(Date[] sorted, Date date)
    {
        int lo = 0, hi = sorted.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (sorted[mid] < date)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }
}
