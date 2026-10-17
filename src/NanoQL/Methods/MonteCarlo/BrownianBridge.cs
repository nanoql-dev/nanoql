// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Bojan Nikolic
// SPDX-FileCopyrightText: Copyright (C) 2002 Peter Jäckel "Monte Carlo Methods in Finance".
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause AND LicenseRef-Jaeckel
// Upstream: ql/methods/montecarlo/brownianbridge.hpp ql/methods/montecarlo/brownianbridge.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// ===========================================================================
// NOTE: The following copyright notice applies to the original code,
//
// Copyright (C) 2002 Peter Jäckel "Monte Carlo Methods in Finance".
// All rights reserved.
//
// Permission to use, copy, modify, and distribute this software is freely
// granted, provided that this notice is preserved.
// ===========================================================================

using System.Collections.Immutable;

namespace NanoQL.Methods;

/// <summary>
/// Builds Wiener process paths using Gaussian variates: it generates normalized (unit-variance) paths as sequences of
/// variations. To obtain the actual path of the underlying, multiply the returned variations by the integrated
/// variance (including time) over the corresponding time step.
/// </summary>
/// <remarks>Immutable: it copies the times it is given, and its accessors return the arrays it holds.</remarks>
public sealed class BrownianBridge
{
    private readonly int size;
    private readonly ImmutableArray<double> t;
    private readonly double[] sqrtdt;
    private readonly ImmutableArray<int> bridgeIndex;
    private readonly ImmutableArray<int> leftIndex;
    private readonly ImmutableArray<int> rightIndex;
    private readonly ImmutableArray<double> leftWeight;
    private readonly ImmutableArray<double> rightWeight;
    private readonly ImmutableArray<double> stdDev;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrownianBridge"/> class over steps of unit length: its times are
    /// 1, 2, …, <paramref name="steps"/>.
    /// </summary>
    /// <param name="steps">The number of steps in the path, at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="steps"/> is less than 1, for which QuantLib's bridge is undefined.
    /// </exception>
    public BrownianBridge(int steps)
    {
        // QuantLib's initialize() reads sqrtdt_[0] and t_[size_-1], undefined with no step.
        if (!(steps >= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(steps), steps, $"a Brownian bridge needs at least one step, not {steps}");
        }

        var times = new double[steps];
        for (int i = 0; i < steps; ++i)
        {
            times[i] = i + 1;
        }

        size = steps;
        t = [.. times];
        sqrtdt = new double[size];
        (bridgeIndex, leftIndex, rightIndex, leftWeight, rightWeight, stdDev) = Initialize(size, t, sqrtdt);
    }

    /// <summary>Initializes a new instance of the <see cref="BrownianBridge"/> class over the times of its steps, which it copies.</summary>
    /// <param name="times">
    /// The times at which the steps occur, which also give the number of steps. The path's starting time is assumed
    /// to be 0 and must not be included. At least one.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="times"/> is empty, for which QuantLib's bridge is undefined.
    /// </exception>
    public BrownianBridge(ReadOnlySpan<double> times)
    {
        // QuantLib's initialize() reads sqrtdt_[0] and t_[size_-1], undefined with no step.
        if (times.IsEmpty)
        {
            throw new ArgumentOutOfRangeException(nameof(times), "a Brownian bridge needs at least one step time, not none");
        }

        size = times.Length;
        t = [.. times];
        sqrtdt = new double[size];
        (bridgeIndex, leftIndex, rightIndex, leftWeight, rightWeight, stdDev) = Initialize(size, t, sqrtdt);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BrownianBridge"/> class over a time grid: the steps occur at its
    /// times after the first.
    /// </summary>
    /// <param name="timeGrid">The time grid, of at least two times.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="timeGrid"/> has fewer than two times, for which QuantLib's bridge is undefined.
    /// </exception>
    public BrownianBridge(TimeGrid timeGrid)
    {
        ArgumentNullException.ThrowIfNull(timeGrid);

        // QuantLib's size_(timeGrid.size()-1) is 0 for a grid of one time, whose initialize() is undefined, and wraps
        // for the empty grid.
        if (!(timeGrid.Size >= 2))
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeGrid),
                $"a Brownian bridge needs a time grid of at least two times, not {timeGrid.Size}");
        }

        size = timeGrid.Size - 1;
        var times = new double[size];
        for (int i = 0; i < size; ++i)
        {
            times[i] = timeGrid[i + 1];
        }

        t = [.. times];
        sqrtdt = new double[size];
        (bridgeIndex, leftIndex, rightIndex, leftWeight, rightWeight, stdDev) = Initialize(size, t, sqrtdt);
    }

    /// <summary>Gets the number of steps.</summary>
    public int Size => size;

    /// <summary>Gets the times at which the steps occur.</summary>
    public ImmutableArray<double> Times => t;

    /// <summary>Gets, for each variate in turn, the index of the path point it constructs.</summary>
    public ImmutableArray<int> BridgeIndex => bridgeIndex;

    /// <summary>Gets, for each variate in turn, the index after the constructed point to its left (0 for the start).</summary>
    public ImmutableArray<int> LeftIndex => leftIndex;

    /// <summary>Gets, for each variate in turn, the index of the constructed point to its right.</summary>
    public ImmutableArray<int> RightIndex => rightIndex;

    /// <summary>Gets, for each variate in turn, the weight of the point to its left.</summary>
    public ImmutableArray<double> LeftWeight => leftWeight;

    /// <summary>Gets, for each variate in turn, the weight of the point to its right.</summary>
    public ImmutableArray<double> RightWeight => rightWeight;

    /// <summary>Gets, for each variate in turn, the standard deviation it is multiplied by.</summary>
    public ImmutableArray<double> StdDeviation => stdDev;

    /// <summary>
    /// The Brownian-bridge generator function: transforms a sequence of random variates into the sequence of
    /// variations of a Brownian-bridge path.
    /// </summary>
    /// <param name="input">The variates, one per step.</param>
    /// <param name="output">
    /// The variations, written into its first <see cref="Size"/> elements; it must not overlap <paramref name="input"/>.
    /// </param>
    /// <remarks>
    /// To get the canonical Brownian bridge, which starts and finishes at the same value, the first element of the
    /// input sequence must be zero. Conversely, to get a sloped bridge, set the first element to a non-zero value. In
    /// this case, the final value in the bridge will be sqrt(last time point) * (first element of input sequence).
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="input"/> has another size than the bridge, or <paramref name="output"/> is shorter.
    /// </exception>
    public void Transform(ReadOnlySpan<double> input, Span<double> output)
    {
        // QuantLib's other check, that the input's end isn't before its begin, holds for every span.
        if (!(input.Length == size))
        {
            throw new ArgumentException("incompatible sequence size", nameof(input));
        }

        // QuantLib writes output[0] to output[size_-1] unchecked, undefined past a shorter output.
        if (!(output.Length >= size))
        {
            throw new ArgumentException(
                $"output sequence of size {output.Length} shorter than the bridge's {size}",
                nameof(output));
        }

        // We use output to store the path...
        output[size - 1] = stdDev[0] * input[0];
        for (int i = 1; i < size; ++i)
        {
            int j = leftIndex[i];
            int k = rightIndex[i];
            int l = bridgeIndex[i];
            if (j != 0)
            {
                output[l] = (leftWeight[i] * output[j - 1]) + (rightWeight[i] * output[k]) + (stdDev[i] * input[i]);
            }
            else
            {
                output[l] = (rightWeight[i] * output[k]) + (stdDev[i] * input[i]);
            }
        }

        // ...after which, we calculate the variations and
        // normalize to unit times
        for (int i = size - 1; i >= 1; --i)
        {
            output[i] -= output[i - 1];
            output[i] /= sqrtdt[i];
        }

        output[0] /= sqrtdt[0];
    }

    private static (ImmutableArray<int> BridgeIndex, ImmutableArray<int> LeftIndex, ImmutableArray<int> RightIndex,
        ImmutableArray<double> LeftWeight, ImmutableArray<double> RightWeight, ImmutableArray<double> StdDev)
        Initialize(int size, ImmutableArray<double> t, double[] sqrtdt)
    {
        var bridgeIndex = new int[size];
        var leftIndex = new int[size];
        var rightIndex = new int[size];
        var leftWeight = new double[size];
        var rightWeight = new double[size];
        var stdDev = new double[size];

        sqrtdt[0] = Math.Sqrt(t[0]);
        for (int i = 1; i < size; ++i)
        {
            sqrtdt[i] = Math.Sqrt(t[i] - t[i - 1]);
        }

        // map is used to indicate which points are already constructed.
        // If map[i] is zero, path point i is yet unconstructed.
        // map[i]-1 is the index of the variate that constructs
        // the path point # i.
        var map = new int[size];

        //  The first point in the construction is the global step.
        map[size - 1] = 1;

        //  The global step is constructed from the first variate.
        bridgeIndex[0] = size - 1;

        //  The variance of the global step
        stdDev[0] = Math.Sqrt(t[size - 1]);

        //  The global step to the last point in time is special.
        leftWeight[0] = rightWeight[0] = 0.0;
        for (int j = 0, i = 1; i < size; ++i)
        {
            // Find the next unpopulated entry in the map.
            while (map[j] != 0)
            {
                ++j;
            }

            int k = j;

            // Find the next populated entry in the map from there.
            while (map[k] == 0)
            {
                ++k;
            }

            // l-1 is now the index of the point to be constructed next.
            int l = j + ((k - 1 - j) >> 1);
            map[l] = i;

            // The i-th Gaussian variate will be used to set point l-1.
            bridgeIndex[i] = l;
            leftIndex[i] = j;
            rightIndex[i] = k;
            if (j != 0)
            {
                leftWeight[i] = (t[k] - t[l]) / (t[k] - t[j - 1]);
                rightWeight[i] = (t[l] - t[j - 1]) / (t[k] - t[j - 1]);
                stdDev[i] = Math.Sqrt((t[l] - t[j - 1]) * (t[k] - t[l]) / (t[k] - t[j - 1]));
            }
            else
            {
                leftWeight[i] = (t[k] - t[l]) / t[k];
                rightWeight[i] = t[l] / t[k];
                stdDev[i] = Math.Sqrt(t[l] * (t[k] - t[l]) / t[k]);
            }

            j = k + 1;
            if (j >= size)
            {
                j = 0; //  wrap around
            }
        }

        return ([.. bridgeIndex], [.. leftIndex], [.. rightIndex], [.. leftWeight], [.. rightWeight], [.. stdDev]);
    }
}
