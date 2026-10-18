// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/indexmanager.hpp ql/indexes/indexmanager.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// The stored fixings of indexes, by index name: QuantLib's <c>IndexManager</c>, as an immutable value that
/// <see cref="EvaluationContext.Fixings"/> holds.
/// </summary>
/// <remarks>
/// <para>
/// Every change returns a new store, as .NET's immutable collections do. The writers take the index, whose name keys
/// its history and whose <see cref="FinancialIndex.IsValidFixingDate"/> and
/// <see cref="FinancialIndex.AllowsNativeFixings"/> QuantLib's checks read; an index reads its fixings through
/// <see cref="FinancialIndex.PastFixing"/>, <see cref="FinancialIndex.HasHistoricalFixing"/> and
/// <see cref="FinancialIndex.TimeSeries"/>.
/// </para>
/// <para>
/// Names compare as QuantLib's do: ASCII letters upper-cased, every other character as it is, so "Euribor6M" and
/// "EURIBOR6M" share a history. The first spelling stored is the one <see cref="Histories"/> gives, in the order of
/// that comparison.
/// </para>
/// <para>
/// Two stores are equal when they hold the same names, by that comparison, each with an equal history; which spelling
/// was kept doesn't count. The hash combines one hash per history, each computed when its series was built.
/// </para>
/// <para>
/// Three divergences from QuantLib: a refused batch changes nothing, where QuantLib stores its valid fixings, and a
/// null entry at the refused date, before it throws; reading never writes, where QuantLib's reads insert null entries
/// and empty histories; and a fixing equal to QuantLib's null sentinel <c>Null&lt;Real&gt;()</c>
/// (3.4028234663852886e38) is stored as a value, where QuantLib reads it as no fixing.
/// </para>
/// </remarks>
public sealed class IndexFixings : IEquatable<IndexFixings>
{
    private readonly ImmutableSortedDictionary<string, Entry> data;
    private readonly int hash;

    private IndexFixings(ImmutableSortedDictionary<string, Entry> data)
    {
        this.data = data;
        var h = default(HashCode);
        h.Add(data.Count);
        foreach (KeyValuePair<string, Entry> kv in data)
        {
            h.Add(kv.Key, StringComparer.Ordinal);
            h.Add(kv.Value.History);
        }

        hash = h.ToHashCode();
    }

    /// <summary>Gets the store with no fixings, QuantLib's state after <c>clearHistories()</c>.</summary>
    public static IndexFixings Empty { get; } =
        new(ImmutableSortedDictionary.Create<string, Entry>(StringComparer.Ordinal));

    /// <summary>The names of the indexes whose fixings are stored, as first spelt, in QuantLib's order.</summary>
    /// <returns>The names.</returns>
    public ImmutableArray<string> Histories() => [.. data.Values.Select(e => e.Name)];

    /// <summary>A store with one more fixing.</summary>
    /// <param name="index">The index the fixing is for.</param>
    /// <param name="fixingDate">The actual calendar date of the fixing, with no settlement days applied.</param>
    /// <param name="fixing">The fixing.</param>
    /// <param name="forceOverwrite">Whether a fixing already stored on that date is replaced.</param>
    /// <returns>The new store; this one is unchanged.</returns>
    /// <exception cref="ArgumentException">The date isn't a valid fixing date, or a different fixing is stored on it
    /// and <paramref name="forceOverwrite"/> is <see langword="false"/>.</exception>
    /// <exception cref="NanoQLException">The index allows no native fixings.</exception>
    public IndexFixings AddFixing(FinancialIndex index, Date fixingDate, double fixing, bool forceOverwrite = false)
    {
        ArgumentNullException.ThrowIfNull(index);
        Date.ThrowIfDefault(fixingDate, nameof(fixingDate));
        CheckNativeFixingsAllowed(index);
        return Add(index, [fixingDate], [fixing], forceOverwrite, nameof(fixingDate));
    }

    /// <summary>A store with more fixings, taken in pairs from two sequences.</summary>
    /// <param name="index">The index the fixings are for.</param>
    /// <param name="fixingDates">The actual calendar dates of the fixings, with no settlement days applied.</param>
    /// <param name="fixings">The fixings, one per date; values beyond the last date are ignored, as in QuantLib.</param>
    /// <param name="forceOverwrite">Whether fixings already stored on those dates are replaced.</param>
    /// <returns>The new store; this one is unchanged. With no dates, it holds an empty history for the index, as
    /// QuantLib's does.</returns>
    /// <exception cref="ArgumentException">There are fewer fixings than dates; or a date isn't a valid fixing date, or
    /// a different fixing is stored on it and <paramref name="forceOverwrite"/> is <see langword="false"/>. Nothing is
    /// stored then.</exception>
    /// <exception cref="NanoQLException">The index allows no native fixings.</exception>
    public IndexFixings AddFixings(
        FinancialIndex index,
        IEnumerable<Date> fixingDates,
        IEnumerable<double> fixings,
        bool forceOverwrite = false)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(fixingDates);
        ArgumentNullException.ThrowIfNull(fixings);
        CheckNativeFixingsAllowed(index);
        Date[] dates = [.. fixingDates];
        foreach (Date d in dates)
        {
            Date.ThrowIfDefault(d, nameof(fixingDates));
        }

        var values = new double[dates.Length];
        int n = 0;
        foreach (double v in fixings)
        {
            if (n == values.Length)
            {
                break;
            }

            values[n++] = v;
        }

        if (n < dates.Length)
        {
            // QuantLib reads past the end of the values here, which is undefined.
            throw new ArgumentException("fewer values than dates", nameof(fixings));
        }

        return Add(index, dates, values, forceOverwrite, nameof(fixingDates));
    }

    /// <summary>A store with more fixings, taken from a series.</summary>
    /// <param name="index">The index the fixings are for.</param>
    /// <param name="fixings">The fixings, on the actual calendar dates of the fixings.</param>
    /// <param name="forceOverwrite">Whether fixings already stored on those dates are replaced.</param>
    /// <returns>The new store; this one is unchanged.</returns>
    /// <exception cref="ArgumentException">A date isn't a valid fixing date, or a different fixing is stored on it and
    /// <paramref name="forceOverwrite"/> is <see langword="false"/>. Nothing is stored then.</exception>
    /// <exception cref="NanoQLException">The index allows no native fixings.</exception>
    public IndexFixings AddFixings(FinancialIndex index, TimeSeries<double> fixings, bool forceOverwrite = false)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(fixings);
        CheckNativeFixingsAllowed(index);
        return Add(index, [.. fixings.Dates()], [.. fixings.Values()], forceOverwrite, nameof(fixings));
    }

    /// <summary>A store without the index's fixings.</summary>
    /// <param name="index">The index.</param>
    /// <returns>The new store; this one is unchanged.</returns>
    /// <exception cref="NanoQLException">The index allows no native fixings.</exception>
    public IndexFixings ClearFixings(FinancialIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);
        CheckNativeFixingsAllowed(index);
        string key = Key(index.Name);
        return data.ContainsKey(key) ? new IndexFixings(data.Remove(key)) : this;
    }

    /// <inheritdoc/>
    public bool Equals(IndexFixings? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || hash != other.hash || data.Count != other.data.Count)
        {
            return false;
        }

        foreach (KeyValuePair<string, Entry> kv in data)
        {
            if (!other.data.TryGetValue(kv.Key, out Entry? e) || !kv.Value.History.Equals(e.History))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is IndexFixings other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => hash;

    /// <summary>Whether a fixing is stored for a name on a date; reading inserts nothing.</summary>
    /// <param name="name">The index's name.</param>
    /// <param name="fixingDate">The date.</param>
    /// <returns><see langword="true"/> when one is stored.</returns>
    internal bool HasHistoricalFixing(string name, Date fixingDate) =>
        data.TryGetValue(Key(name), out Entry? e) && e.History.TryGetValue(fixingDate, out _);

    /// <summary>The history stored for a name; empty, and not inserted, when there is none.</summary>
    /// <param name="name">The index's name.</param>
    /// <returns>The history.</returns>
    internal TimeSeries<double> GetHistory(string name) =>
        data.TryGetValue(Key(name), out Entry? e) ? e.History : TimeSeries<double>.None;

    /// <summary>
    /// The key a name is stored under: its ASCII letters upper-cased, every other character as it is, so that the
    /// ordinal order of keys is QuantLib's <c>CaseInsensitiveCompare</c> order (for UTF-16 code units).
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The key.</returns>
    private static string Key(string name) =>
        string.Create(name.Length, name, static (span, s) =>
        {
            for (int i = 0; i < s.Length; ++i)
            {
                char c = s[i];
                span[i] = c is >= 'a' and <= 'z' ? (char)(c - ('a' - 'A')) : c;
            }
        });

    private static void CheckNativeFixingsAllowed(FinancialIndex index)
    {
        if (!index.AllowsNativeFixings)
        {
            throw new NanoQLException(
                $"native fixings not allowed for {index.Name}; refer to underlying indices instead");
        }
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// QuantLib's <c>IndexManager::addFixings</c>, run on a copy of the index's history and kept only when no fixing is
    /// refused.
    /// </summary>
    private IndexFixings Add(FinancialIndex index, Date[] dates, double[] values, bool forceOverwrite, string paramName)
    {
        string name = index.Name;
        string key = Key(name);
        bool existing = data.TryGetValue(key, out Entry? entry);
        var h = new SortedDictionary<Date, double>();
        if (existing)
        {
            foreach (KeyValuePair<Date, double> kv in entry!.History)
            {
                h[kv.Key] = kv.Value;
            }
        }

        bool noInvalidFixing = true, noDuplicatedFixing = true;
        Date invalidDate = default, duplicatedDate = default;
        double invalidValue = 0.0, duplicatedValue = 0.0;
        for (int i = 0; i < dates.Length; ++i)
        {
            Date d = dates[i];
            double v = values[i];
            if (index.IsValidFixingDate(d))
            {
                if (forceOverwrite || !h.TryGetValue(d, out double currentValue))
                {
                    h[d] = v;
                }
                else if (!ComparisonFunctions.Close(currentValue, v))
                {
                    noDuplicatedFixing = false;
                    duplicatedDate = d;
                    duplicatedValue = v;
                }
            }
            else
            {
                noInvalidFixing = false;
                invalidDate = d;
                invalidValue = v;
            }
        }

        if (!noInvalidFixing)
        {
            throw new ArgumentException(
                $"At least one invalid fixing provided: {WeekdayFunctions.LongWeekday(invalidDate.Weekday)} "
                + $"{invalidDate}, {Show(invalidValue)}",
                paramName);
        }

        if (!noDuplicatedFixing)
        {
            throw new ArgumentException(
                $"At least one duplicated fixing provided: {duplicatedDate}, {Show(duplicatedValue)} while "
                + $"{Show(h[duplicatedDate])} value is already present",
                paramName);
        }

        var history = new TimeSeries<double>(h.Keys, h.Values);
        return new IndexFixings(data.SetItem(key, new Entry(existing ? entry!.Name : name, history)));
    }

    /// <summary>A stored history, with the name as first spelt.</summary>
    private sealed record Entry(string Name, TimeSeries<double> History);
}
