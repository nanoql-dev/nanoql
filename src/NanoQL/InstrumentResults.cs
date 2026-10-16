// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instrument.hpp ql/instrument.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Equality-tested: tests/NanoQL.Tests/Conventions/ResultsEqualityTests.cs

using System.Collections;
using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL;

/// <summary>The results an engine returns for an instrument.</summary>
/// <remarks>
/// A result QuantLib leaves null is <see langword="null"/>: where QuantLib throws "not provided" on reading it, the
/// caller checks for the null. Equality compares every member by value, the additional results key by key, and a
/// collection among them element by element.
/// </remarks>
public record InstrumentResults
{
    /// <summary>Gets the net present value.</summary>
    public double? NPV { get; init; }

    /// <summary>Gets the error estimate on the NPV, when the engine gives one.</summary>
    public double? ErrorEstimate { get; init; }

    /// <summary>Gets the date the NPV refers to.</summary>
    /// <exception cref="ArgumentException">Set to <c>default(Date)</c>, which is not a valid date.</exception>
    public Date? ValuationDate
    {
        get;
        init
        {
            Date.ThrowIfDefault(value, nameof(ValuationDate));
            field = value;
        }
    }

    /// <summary>Gets any additional results the engine returns, by tag.</summary>
    public ImmutableSortedDictionary<string, object> AdditionalResults { get; init; } =
        ImmutableSortedDictionary<string, object>.Empty;

    /// <inheritdoc/>
    public virtual bool Equals(InstrumentResults? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null
            && EqualityContract == other.EqualityContract
            && EqualityComparer<double?>.Default.Equals(NPV, other.NPV)
            && EqualityComparer<double?>.Default.Equals(ErrorEstimate, other.ErrorEstimate)
            && EqualityComparer<Date?>.Default.Equals(ValuationDate, other.ValuationDate)
            && AdditionalResults.Count == other.AdditionalResults.Count
            && AdditionalResults.All(kv =>
                other.AdditionalResults.TryGetValue(kv.Key, out object? value) && ValueEquals(kv.Value, value));
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(EqualityContract);
        hash.Add(NPV);
        hash.Add(ErrorEstimate);
        hash.Add(ValuationDate);
        foreach ((string key, object value) in AdditionalResults)
        {
            hash.Add(key);
            hash.Add(ValueHash(value));
        }

        return hash.ToHashCode();
    }

    /// <summary>Compares two arrays of per-leg results element by element.</summary>
    /// <param name="x">The first array.</param>
    /// <param name="y">The second array.</param>
    /// <returns><see langword="true"/> when the arrays hold equal elements in the same order.</returns>
    private protected static bool SequenceEquals(ImmutableArray<double?> x, ImmutableArray<double?> y) =>
        x.IsDefaultOrEmpty ? y.IsDefaultOrEmpty : !y.IsDefault && x.SequenceEqual(y);

    /// <summary>Hashes an array of per-leg results element by element.</summary>
    /// <param name="hash">The hash to add to.</param>
    /// <param name="x">The array.</param>
    private protected static void AddSequence(ref HashCode hash, ImmutableArray<double?> x)
    {
        hash.Add(x.IsDefault ? 0 : x.Length);
        if (!x.IsDefault)
        {
            foreach (double? v in x)
            {
                hash.Add(v);
            }
        }
    }

    private static bool ValueEquals(object? x, object? y)
    {
        if (x is IEnumerable xs and not string && y is IEnumerable ys and not string)
        {
            IEnumerator ex = xs.GetEnumerator(), ey = ys.GetEnumerator();
            while (true)
            {
                bool hasX = ex.MoveNext(), hasY = ey.MoveNext();
                if (hasX != hasY)
                {
                    return false;
                }

                if (!hasX)
                {
                    return true;
                }

                if (!ValueEquals(ex.Current, ey.Current))
                {
                    return false;
                }
            }
        }

        return Equals(x, y);
    }

    private static int ValueHash(object? x)
    {
        if (x is IEnumerable xs and not string)
        {
            var hash = new HashCode();
            foreach (object? item in xs)
            {
                hash.Add(ValueHash(item));
            }

            return hash.ToHashCode();
        }

        return x?.GetHashCode() ?? 0;
    }
}
