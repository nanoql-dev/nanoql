// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/timegrid.hpp ql/timegrid.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL;

/// <summary>A discrete time grid: increasing times from 0, which contain the mandatory times it was built with.</summary>
/// <remarks>
/// <para>
/// Immutable. The grid starts at 0 and refuses negative times; QuantLib's own note asks whether negative times could
/// be allowed, and keeps the restriction for the time being.
/// </para>
/// <para>
/// <see cref="GetEnumerator"/> is QuantLib's <c>begin()</c> and <c>end()</c>. Its reverse iterators,
/// <c>rbegin()</c> and <c>rend()</c>, are an indexed loop run backwards: <c>for (int i = grid.Size - 1; i &gt;= 0; i--)</c>.
/// </para>
/// </remarks>
public sealed class TimeGrid
{
    private readonly ImmutableArray<double> times;
    private readonly ImmutableArray<double> dt;
    private readonly ImmutableArray<double> mandatoryTimes;

    /// <summary>Initializes a new instance of the <see cref="TimeGrid"/> class with no times.</summary>
    public TimeGrid()
    {
        times = [];
        dt = [];
        mandatoryTimes = [];
    }

    /// <summary>Initializes a new instance of the <see cref="TimeGrid"/> class, regularly spaced from 0 to an end.</summary>
    /// <param name="end">The last time, positive.</param>
    /// <param name="steps">The number of steps.</param>
    /// <remarks>
    /// With no steps the grid holds one time, 0 times the infinite step: NaN, as QuantLib's does. The mandatory time is
    /// <paramref name="end"/>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="end"/> isn't positive, or <paramref name="steps"/> is negative.</exception>
    public TimeGrid(double end, int steps)
    {
        // We seem to assume that the grid begins at 0.
        // Let's enforce the assumption for the time being
        // (even though I'm not sure that I agree.)
        if (!(end > 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(end), end, "negative times not allowed");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(steps);
        double step = end / steps;
        var t = new double[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            t[i] = step * i;
        }

        times = [.. t];
        mandatoryTimes = [end];
        var d = new double[steps];
        Array.Fill(d, step);
        dt = [.. d];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeGrid"/> class with mandatory time points, which it copies: the
    /// grid holds them, and 0 before them, and no other point.
    /// </summary>
    /// <param name="times">The mandatory times, in any order; times close enough to one another count once.</param>
    /// <exception cref="ArgumentException"><paramref name="times"/> is empty or holds a negative time.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="times"/> holds a NaN, which QuantLib's sort leaves undefined.
    /// </exception>
    public TimeGrid(ReadOnlySpan<double> times)
    {
        double[] mandatory = Mandatory(times);
        var t = new List<double>(mandatory.Length + 1);
        if (mandatory[0] > 0.0)
        {
            t.Add(0.0);
        }

        t.AddRange(mandatory);
        this.times = [.. t];
        dt = Steps(this.times);
        mandatoryTimes = [.. mandatory];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeGrid"/> class with mandatory time points, which it copies, and
    /// further points regularly spaced between each pair of them to reach about the given number of steps.
    /// </summary>
    /// <param name="times">The mandatory times, in any order; times close enough to one another count once.</param>
    /// <param name="steps">
    /// The number of steps over the whole grid, from which the longest step is the last mandatory time divided by
    /// it; with 0, the longest step is the shortest distance between the mandatory times, 0 aside.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="times"/> is empty or holds a negative time, or, with no steps, holds no time but 0.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="steps"/> is negative; or <paramref name="times"/> holds a NaN, or gives a period whose number
    /// of steps isn't a finite <see cref="int"/> (an infinite time, or no steps and a shortest distance too small),
    /// which QuantLib leaves undefined.
    /// </exception>
    public TimeGrid(ReadOnlySpan<double> times, int steps)
    {
        double[] mandatory = Mandatory(times);
        ArgumentOutOfRangeException.ThrowIfNegative(steps);

        double last = mandatory[^1];
        double dtMax;

        // The resulting timegrid have points at times listed in the input
        // list. Between these points, there are inner-points which are
        // regularly spaced.
        if (steps == 0)
        {
            // std::adjacent_difference: the first time, then each difference
            var diff = new List<double>(mandatory.Length) { mandatory[0] };
            for (int i = 1; i < mandatory.Length; i++)
            {
                diff.Add(mandatory[i] - mandatory[i - 1]);
            }

            if (diff.Count == 0)
            {
                throw new ArgumentException("at least two distinct points required in time grid", nameof(times));
            }

            if (diff[0] == 0.0)
            {
                diff.RemoveAt(0);
            }

            if (diff.Count == 0)
            {
                throw new ArgumentException("not enough distinct points in time grid", nameof(times));
            }

            // std::min_element: the first of the smallest
            dtMax = diff[0];
            for (int i = 1; i < diff.Count; i++)
            {
                if (diff[i] < dtMax)
                {
                    dtMax = diff[i];
                }
            }
        }
        else
        {
            dtMax = last / steps;
        }

        double periodBegin = 0.0;
        var t = new List<double> { periodBegin };
        foreach (double periodEnd in mandatory)
        {
            if (periodEnd != 0.0)
            {
                // the nearest integer, at least 1. QuantLib's std::lround is undefined for a NaN or a value beyond its
                // 32-bit long (an infinite time, or a tiny shortest distance), so such a count is refused.
                double rounded = Math.Round((periodEnd - periodBegin) / dtMax, MidpointRounding.AwayFromZero);
                if (!(rounded <= int.MaxValue))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(times),
                        $"the number of steps between times {Show(periodBegin)} and {Show(periodEnd)} is not a finite integer");
                }

                int nSteps = Math.Max((int)rounded, 1);
                double step = (periodEnd - periodBegin) / nSteps;
                for (int n = 1; n <= nSteps; ++n)
                {
                    t.Add(periodBegin + (n * step));
                }
            }

            periodBegin = periodEnd;
        }

        this.times = [.. t];
        dt = Steps(this.times);
        mandatoryTimes = [.. mandatory];
    }

    /// <summary>Gets the mandatory times, sorted, with times close enough to one another counted once.</summary>
    public ImmutableArray<double> MandatoryTimes => mandatoryTimes;

    /// <summary>Gets the number of times.</summary>
    public int Size => times.Length;

    /// <summary>Gets a value indicating whether the grid has no times.</summary>
    public bool Empty => times.IsEmpty;

    /// <summary>Gets the time at an index.</summary>
    /// <param name="i">The index.</param>
    /// <returns>The time.</returns>
    public double this[int i] => times[i];

    /// <summary>The index of a time on the grid: the closest time's, if <paramref name="t"/> is close enough to it.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The index.</returns>
    /// <exception cref="NanoQLException">
    /// No time on the grid is close enough to <paramref name="t"/>; or the grid is empty or its first time is NaN,
    /// where QuantLib is undefined.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="t"/> is NaN, for which QuantLib is undefined.
    /// </exception>
    public int Index(double t)
    {
        // QuantLib reads times_[closestIndex(t)] of an empty grid, and for a NaN, which is neither before nor after
        // the grid, reaches its last branch with closestIndex 0 and reads times_[-1].
        ThrowIfEmpty("no time on an empty time grid");
        if (double.IsNaN(t))
        {
            throw new ArgumentOutOfRangeException(nameof(t), t, "NaN time not allowed");
        }

        // Only the regular constructor gives a NaN time, its first, 0 times an infinite step (an infinite end, or no
        // steps). QuantLib's index then takes closestIndex 0, finds t neither before nor after that node, and reads
        // times_[Size(-1)] (timegrid.cpp:62-75), whatever t is.
        if (double.IsNaN(times[0]))
        {
            throw new NanoQLException("using inadequate time grid: its first node is NaN");
        }

        int i = ClosestIndex(t);
        if (ComparisonFunctions.CloseEnough(t, times[i]))
        {
            return i;
        }

        if (t < times[0])
        {
            throw new NanoQLException(
                $"using inadequate time grid: all nodes are later than the required time t = {Show(t)} (earliest node is t1 = {Show(times[0])})");
        }

        if (t > times[^1])
        {
            throw new NanoQLException(
                $"using inadequate time grid: all nodes are earlier than the required time t = {Show(t)} (latest node is t1 = {Show(times[^1])})");
        }

        int j, k;
        if (t > times[i])
        {
            j = i;
            k = i + 1;
        }
        else
        {
            j = i - 1;
            k = i;
        }

        throw new NanoQLException(
            $"using inadequate time grid: the nodes closest to the required time t = {Show(t)} are t1 = {Show(times[j])} and t2 = {Show(times[k])}");
    }

    /// <summary>The index of the time on the grid closest to <paramref name="t"/>; of the earlier of two equally close.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The index; 0 for an empty grid.</returns>
    public int ClosestIndex(double t)
    {
        int begin = 0, end = times.Length;
        int result = LowerBound(t);
        if (result == begin)
        {
            return 0;
        }

        if (result == end)
        {
            return Size - 1;
        }

        double dt1 = times[result] - t;
        double dt2 = t - times[result - 1];
        return dt1 < dt2 ? result : result - 1;
    }

    /// <summary>The time on the grid closest to <paramref name="t"/>.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The time.</returns>
    /// <exception cref="NanoQLException">The grid is empty, where QuantLib is undefined.</exception>
    public double ClosestTime(double t)
    {
        ThrowIfEmpty("no closest time on an empty time grid");
        return times[ClosestIndex(t)];
    }

    /// <summary>The length of the step that starts at index <paramref name="i"/>.</summary>
    /// <param name="i">The index, from 0 to <see cref="Size"/> − 2.</param>
    /// <returns>The step's length.</returns>
    public double Dt(int i) => dt[i];

    /// <summary>The time at an index, checked.</summary>
    /// <param name="i">The index.</param>
    /// <returns>The time.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is out of range.</exception>
    public double At(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(i, times.Length);
        return times[i];
    }

    /// <summary>The first time.</summary>
    /// <returns>The first time.</returns>
    /// <exception cref="NanoQLException">The grid is empty, where QuantLib is undefined.</exception>
    public double Front()
    {
        ThrowIfEmpty("no front time for empty time grid");
        return times[0];
    }

    /// <summary>The last time.</summary>
    /// <returns>The last time.</returns>
    /// <exception cref="NanoQLException">The grid is empty, where QuantLib is undefined.</exception>
    public double Back()
    {
        ThrowIfEmpty("no back time for empty time grid");
        return times[^1];
    }

    /// <summary>Returns an enumerator over the times, so that <c>foreach</c> works.</summary>
    /// <returns>The enumerator.</returns>
    public ImmutableArray<double>.Enumerator GetEnumerator() => times.GetEnumerator();

    // The mandatory times: copied, sorted, checked, and with the times close enough to the last one kept dropped, as
    // std::unique with close_enough does.
    private static double[] Mandatory(ReadOnlySpan<double> times)
    {
        if (times.IsEmpty)
        {
            throw new ArgumentException("empty time sequence", nameof(times));
        }

        // std::sort needs a strict weak ordering, which a NaN breaks: QuantLib's grid is undefined then.
        foreach (double t in times)
        {
            if (double.IsNaN(t))
            {
                throw new ArgumentOutOfRangeException(nameof(times), "NaN time not allowed");
            }
        }

        double[] m = times.ToArray();
        Array.Sort(m);

        // We seem to assume that the grid begins at 0.
        // Let's enforce the assumption for the time being
        // (even though I'm not sure that I agree.)
        if (!(m[0] >= 0.0))
        {
            throw new ArgumentException("negative times not allowed", nameof(times));
        }

        int kept = 0;
        for (int i = 1; i < m.Length; i++)
        {
            if (!ComparisonFunctions.CloseEnough(m[kept], m[i]) && ++kept != i)
            {
                m[kept] = m[i];
            }
        }

        Array.Resize(ref m, kept + 1);
        return m;
    }

    // The steps, as std::adjacent_difference from the second time: the second time itself, then each difference.
    private static ImmutableArray<double> Steps(ImmutableArray<double> times)
    {
        if (times.Length < 2)
        {
            return [];
        }

        var d = new double[times.Length - 1];
        d[0] = times[1];
        for (int i = 1; i < d.Length; i++)
        {
            d[i] = times[i + 1] - times[i];
        }

        return [.. d];
    }

    private static string Show(double x) => x.ToString("G12", CultureInfo.InvariantCulture);

    // The empty grid's element reads (times_.front(), times_.back(), times_[0]) are undefined in QuantLib.
    private void ThrowIfEmpty(string message)
    {
        if (times.IsEmpty)
        {
            throw new NanoQLException(message);
        }
    }

    // std::lower_bound: the first index whose time isn't less than t.
    private int LowerBound(double t)
    {
        int first = 0, count = times.Length;
        while (count > 0)
        {
            int step = count / 2;
            int it = first + step;
            if (times[it] < t)
            {
                first = it + 1;
                count -= step + 1;
            }
            else
            {
                count = step;
            }
        }

        return first;
    }
}
