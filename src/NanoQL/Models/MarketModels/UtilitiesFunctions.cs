// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2006 Marco Bianchetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/utilities.hpp ql/models/marketmodels/utilities.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Utilities;

namespace NanoQL.Models;

/// <summary>The market models' time utilities: merging sets of times, subset tests and the times' checks.</summary>
public static class UtilitiesFunctions
{
    /// <summary>
    /// The sorted union of sets of times, and for each set which of the merged times it holds.
    /// </summary>
    /// <param name="times">The sets, each sorted.</param>
    /// <returns>The merged times, each once, and each set's presence over them.</returns>
    /// <exception cref="ArgumentException">A set holds a NaN, or isn't sorted.</exception>
    /// <remarks>
    /// QuantLib's <c>mergeTimes</c> appends the merged times to the caller's vector and sizes each set's presence to
    /// the times before their duplicates are removed, its tail read past the merged ones; here the result is the merged
    /// times alone and each presence their size, which changes no value QuantLib's readers see, since they read it only
    /// below the merged count (the S-mm plan's SD7). QuantLib's sort of a NaN and its binary search of an unsorted set
    /// are undefined, and refused (<c>Divergences/MarketModelUndefinedReadsTests.cs</c>).
    /// </remarks>
    public static MergedTimes MergeTimes(ReadOnlySpan<ImmutableArray<double>> times)
    {
        var allTimes = new List<double>();
        for (int i = 0; i < times.Length; ++i)
        {
            ImmutableArray<double> set = times[i];
            for (int j = 0; j < set.Length; ++j)
            {
                if (double.IsNaN(set[j]))
                {
                    throw new ArgumentException(
                        string.Create(CultureInfo.InvariantCulture, $"{DataFormattersFunctions.Ordinal(i + 1)} set of times holds a NaN"),
                        nameof(times));
                }

                if (j > 0 && set[j] < set[j - 1])
                {
                    throw new ArgumentException(
                        string.Create(CultureInfo.InvariantCulture, $"{DataFormattersFunctions.Ordinal(i + 1)} set of times isn't sorted"),
                        nameof(times));
                }
            }

            allTimes.AddRange(set);
        }

        // ...sort and compact the vector of merged times...
        double[] sorted = [.. allTimes.OrderBy(t => t)];
        var merged = new List<double>(sorted.Length);
        foreach (double t in sorted)
        {
            if (merged.Count == 0 || !(merged[^1] == t))
            {
                merged.Add(t);
            }
        }

        var isPresent = ImmutableArray.CreateBuilder<ImmutableArray<bool>>(times.Length);
        for (int i = 0; i < times.Length; i++)
        {
            bool[] present = new bool[merged.Count];
            for (int j = 0; j < merged.Count; j++)
            {
                present[j] = times[i].AsSpan().BinarySearch(merged[j]) >= 0;
            }

            isPresent.Add(ImmutableArray.Create(present));
        }

        return new MergedTimes([.. merged], isPresent.MoveToImmutable());
    }

    /// <summary>Which elements of a set are in a subset.</summary>
    /// <param name="set">The set, strictly increasing.</param>
    /// <param name="subset">The subset, strictly increasing, no larger than the set.</param>
    /// <returns>For each element of the set, whether the subset holds it.</returns>
    /// <exception cref="ArgumentException">The subset, not empty, is larger than the set.</exception>
    public static ImmutableArray<bool> IsInSubset(ReadOnlySpan<double> set, ReadOnlySpan<double> subset)
    {
        bool[] result = new bool[set.Length];
        int dimsubSet = subset.Length;
        if (dimsubSet == 0)
        {
            return ImmutableArray.Create(result);
        }

        int dimSet = set.Length;
        if (!(dimSet >= dimsubSet))
        {
            throw new ArgumentException("set is required to be larger or equal than subset", nameof(subset));
        }

        for (int i = 0; i < dimSet; ++i)
        {
            // loop in set
            int j = 0;
            double setElement = set[i];
            for (; ;)
            {
                // loop in subset
                double subsetElement = subset[j];
                result[i] = false;

                // if smaller no hope, leave false and go to next i
                if (setElement < subsetElement)
                {
                    break;
                }

                // if match, set result[i] to true and go to next i
                if (setElement == subsetElement)
                {
                    result[i] = true;
                    break;
                }

                // if larger, leave false if at the end or go to next j
                if (j == dimsubSet - 1)
                {
                    break;
                }

                ++j;
            }
        }

        return ImmutableArray.Create(result);
    }

    /// <summary>Checks that times are strictly increasing, the first greater than zero.</summary>
    /// <param name="times">The times, at least one.</param>
    /// <exception cref="ArgumentException">There is no time, or the times aren't strictly increasing positive times.</exception>
    public static void CheckIncreasingTimes(ReadOnlySpan<double> times)
    {
        int nTimes = times.Length;
        if (!(nTimes > 0))
        {
            throw new ArgumentException("at least one time is required", nameof(times));
        }

        if (!(times[0] > 0.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"first time ({times[0]}) must be greater than zero"), nameof(times));
        }

        for (int i = 0; i < nTimes - 1; ++i)
        {
            if (!(times[i + 1] - times[i] > 0))
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"non increasing rate times: times[{i}]={times[i]}, times[{i + 1}]={times[i + 1]}"),
                    nameof(times));
            }
        }
    }

    /// <summary>
    /// Checks that times are strictly increasing, the first greater than zero, and returns their differences.
    /// </summary>
    /// <param name="times">The times, at least two.</param>
    /// <returns>The differences of consecutive times, one fewer than the times.</returns>
    /// <exception cref="ArgumentException">There are fewer than two times, or they aren't strictly increasing positive times.</exception>
    /// <remarks>QuantLib's writes the differences into a vector it resizes; here they are returned.</remarks>
    public static ImmutableArray<double> CheckIncreasingTimesAndCalculateTaus(ReadOnlySpan<double> times)
    {
        double[] taus = new double[Math.Max(times.Length - 1, 0)];
        CheckIncreasingTimesAndCalculateTaus(times, taus);
        return ImmutableArray.Create(taus);
    }

    // QuantLib's checkIncreasingTimesAndCalculateTaus, into a buffer of the times' count less one.
    internal static void CheckIncreasingTimesAndCalculateTaus(ReadOnlySpan<double> times, Span<double> taus)
    {
        int nTimes = times.Length;
        if (!(nTimes > 1))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"at least two times are required, {nTimes} provided"), nameof(times));
        }

        if (!(times[0] > 0.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"first time ({times[0]}) must be greater than zero"), nameof(times));
        }

        for (int i = 0; i < nTimes - 1; ++i)
        {
            taus[i] = times[i + 1] - times[i];
            if (!(taus[i] > 0))
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"non increasing rate times: times[{i}]={times[i]}, times[{i + 1}]={times[i + 1]}"),
                    nameof(times));
            }
        }
    }
}
