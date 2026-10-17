// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/path.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// A single-factor random walk: QuantLib's <c>Path</c>, renamed. The path includes the initial asset value as its
/// first point, so it has one value per time of its <see cref="TimeGrid"/>.
/// </summary>
/// <remarks>
/// <para>
/// Read-only to its users. A path built from a time grid and values copies them, and is immutable. A path a
/// <see cref="PathGenerator{TGsg}"/> returns is a view of the generator's own path, valid until the generator's next
/// draw, which changes it; a caller that keeps one copies it, as <c>new MonteCarloPath(path.TimeGrid, [.. path])</c>.
/// </para>
/// <para>
/// <see cref="GetEnumerator"/> is QuantLib's <c>begin()</c> and <c>end()</c>. Its reverse iterators,
/// <c>rbegin()</c> and <c>rend()</c>, are an indexed loop run backwards: <c>for (int i = path.Length - 1; i &gt;= 0; i--)</c>.
/// </para>
/// </remarks>
public sealed class MonteCarloPath
{
    private readonly TimeGrid timeGrid;
    private readonly double[] values;

    /// <summary>Initializes a new instance of the <see cref="MonteCarloPath"/> class from a time grid and values, which it copies.</summary>
    /// <param name="timeGrid">The time grid.</param>
    /// <param name="values">
    /// The asset values, one per time of the grid; when <see langword="null"/> or empty, as many zeros, where QuantLib
    /// leaves them uninitialized.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="values"/> has another size than the grid.</exception>
    public MonteCarloPath(TimeGrid timeGrid, Vector? values = null)
    {
        ArgumentNullException.ThrowIfNull(timeGrid);
        this.timeGrid = timeGrid;
        this.values = values is null || values.Empty ? new double[timeGrid.Size] : values.AsSpan().ToArray();
        if (!(this.values.Length == timeGrid.Size))
        {
            throw new ArgumentException("different number of times and asset values", nameof(values));
        }
    }

    private MonteCarloPath(TimeGrid timeGrid)
    {
        this.timeGrid = timeGrid;
        values = new double[timeGrid.Size];
    }

    /// <summary>Gets a value indicating whether the path has no points, as its time grid has no times.</summary>
    public bool Empty => timeGrid.Empty;

    /// <summary>Gets the number of points, the time grid's size.</summary>
    public int Length => timeGrid.Size;

    /// <summary>Gets the time grid.</summary>
    public TimeGrid TimeGrid => timeGrid;

    // The values, which only the path generator that owns this path writes.
    internal Span<double> Buffer => values;

    /// <summary>Gets the asset value at the <paramref name="i"/>-th point.</summary>
    /// <param name="i">The index.</param>
    /// <returns>The value.</returns>
    public double this[int i] => values[i];

    /// <summary>The asset value at the <paramref name="i"/>-th point, checked.</summary>
    /// <param name="i">The index.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is out of range.</exception>
    public double At(int i)
    {
        // QuantLib's Path::at is Array::at's check.
        if (!(i >= 0 && i < values.Length))
        {
            throw new ArgumentOutOfRangeException(
                nameof(i),
                $"index ({i}) must be less than {values.Length}: array access out of range");
        }

        return values[i];
    }

    /// <summary>The asset value at the <paramref name="i"/>-th point.</summary>
    /// <param name="i">The index.</param>
    /// <returns>The value.</returns>
    public double Value(int i) => values[i];

    /// <summary>The time at the <paramref name="i"/>-th point.</summary>
    /// <param name="i">The index.</param>
    /// <returns>The time.</returns>
    public double Time(int i) => timeGrid[i];

    /// <summary>The initial asset value.</summary>
    /// <returns>The first value.</returns>
    /// <exception cref="NanoQLException">The path is empty, where QuantLib is undefined.</exception>
    public double Front()
    {
        // QuantLib's values_[0] of an empty Array is undefined.
        if (values.Length == 0)
        {
            throw new NanoQLException("no front value for empty path");
        }

        return values[0];
    }

    /// <summary>The final asset value.</summary>
    /// <returns>The last value.</returns>
    /// <exception cref="NanoQLException">The path is empty, where QuantLib is undefined.</exception>
    public double Back()
    {
        // QuantLib's values_[values_.size()-1] of an empty Array is undefined.
        if (values.Length == 0)
        {
            throw new NanoQLException("no back value for empty path");
        }

        return values[^1];
    }

    /// <summary>Returns an enumerator over the asset values, so that <c>foreach</c> works.</summary>
    /// <returns>The enumerator.</returns>
    public ReadOnlySpan<double>.Enumerator GetEnumerator() => new ReadOnlySpan<double>(values).GetEnumerator();

    // A path over a time grid, of zeros, for a path generator to own and write through Buffer.
    internal static MonteCarloPath ForGenerator(TimeGrid timeGrid) => new(timeGrid);
}
