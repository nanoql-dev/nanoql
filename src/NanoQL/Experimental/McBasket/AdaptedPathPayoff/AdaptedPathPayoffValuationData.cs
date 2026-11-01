// SPDX-FileCopyrightText: Copyright (C) 2009 Andrea Odetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/mcbasket/adaptedpathpayoff.hpp ql/experimental/mcbasket/adaptedpathpayoff.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// One path's data for an <see cref="AdaptedPathPayoff"/>, QuantLib's <c>AdaptedPathPayoff::ValuationData</c>: the
/// fixings and curves it reads and the payments and exercise data it writes, refusing a write earlier than the latest
/// time read.
/// </summary>
/// <remarks>A working type, owned by one path's valuation.</remarks>
[Experimental("NQL0001")]
[WorkingType]
public sealed class AdaptedPathPayoffValuationData
{
    private readonly Matrix path;
    private readonly ImmutableArray<YieldTermStructure> forwardTermStructures;
    private readonly Vector payments;
    private readonly Vector exercises;
    private readonly Vector[] states;

    // Initializing maximumTimeRead_ to -1 would make more sense, but it is unsigned and 0 has exactly the same behaviour.
    private int maximumTimeRead;

    internal AdaptedPathPayoffValuationData(
        Matrix path,
        ImmutableArray<YieldTermStructure> forwardTermStructures,
        Vector payments,
        Vector exercises,
        Vector[] states)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(payments);
        ArgumentNullException.ThrowIfNull(exercises);
        ArgumentNullException.ThrowIfNull(states);
        this.path = path;
        this.forwardTermStructures = forwardTermStructures;
        this.payments = payments;
        this.exercises = exercises;
        this.states = states;
    }

    /// <summary>Gets the number of fixing times.</summary>
    public int NumberOfTimes => path.Columns;

    /// <summary>Gets the number of assets.</summary>
    public int NumberOfAssets => path.Rows;

    /// <summary>An asset's value at a fixing time, which the payments and exercises written later may read.</summary>
    /// <param name="time">The fixing time's index.</param>
    /// <param name="asset">The asset's index.</param>
    /// <returns>The value.</returns>
    public double GetAssetValue(int time, int asset)
    {
        maximumTimeRead = Math.Max(maximumTimeRead, time);
        return path[asset, time];
    }

    /// <summary>The yield curve at a fixing date, which the payments and exercises written later may read.</summary>
    /// <param name="time">The fixing time's index.</param>
    /// <returns>The curve.</returns>
    public YieldTermStructure GetYieldTermStructure(int time)
    {
        maximumTimeRead = Math.Max(maximumTimeRead, time);
        return forwardTermStructures[time];
    }

    /// <summary>The payment at a fixing time.</summary>
    /// <param name="time">The fixing time's index, at least the latest read.</param>
    /// <param name="value">The payment.</param>
    /// <exception cref="NanoQLException">A later time was read ("not adapted payoff: looking into the future").</exception>
    public void SetPayoffValue(int time, double value)
    {
        // This is to ensure the payoff is an adapted function. We prevent payments to depend on future fixings.
        if (!(time >= maximumTimeRead))
        {
            throw new NanoQLException("not adapted payoff: looking into the future");
        }

        payments[time] = value;
    }

    /// <summary>
    /// The exercise value and the regression state at a fixing time; the state is exchanged with the stored one, as
    /// QuantLib's swap.
    /// </summary>
    /// <param name="time">The fixing time's index, at least the latest read.</param>
    /// <param name="exercise">The exercise value.</param>
    /// <param name="state">The regression state, exchanged with the stored one.</param>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">A later time was read ("not adapted payoff: looking into the future").</exception>
    public void SetExerciseData(int time, double exercise, ref Vector state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // This is to ensure the payoff is an adapted function. We prevent payments to depend on future fixings.
        if (!(time >= maximumTimeRead))
        {
            throw new NanoQLException("not adapted payoff: looking into the future");
        }

        if (!exercises.Empty)
        {
            exercises[time] = exercise;
        }

        if (states.Length != 0)
        {
            (states[time], state) = (state, states[time]);
        }
    }
}
