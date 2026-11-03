// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/callability/nodedataprovider.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// The data a market-model callable product's exercise decision reads at each exercise: stepped along a path at its
/// evolution's times, it fills the current exercise's values.
/// </summary>
/// <remarks>
/// A working type and an extension point: <see cref="Reset"/> and <see cref="NextStep"/> change it, and it isn't
/// thread-safe. <see cref="Values"/> fills a span of the current exercise's count of data, which QuantLib's resizes its
/// caller's vector to.
/// </remarks>
[WorkingType]
public abstract class MarketModelNodeDataProvider
{
    /// <summary>Gets the number of exercises.</summary>
    public abstract int NumberOfExercises { get; }

    /// <summary>Gets the evolution: its rate times and the times it is stepped at, its exercise times included.</summary>
    public abstract EvolutionDescription Evolution { get; }

    /// <summary>The number of data at each exercise, possibly different for each.</summary>
    /// <returns>One count an exercise.</returns>
    public abstract ImmutableArray<int> NumberOfData();

    /// <summary>Moves to the next evolution time, in the given state.</summary>
    /// <param name="currentState">The state.</param>
    public abstract void NextStep(CurveState currentState);

    /// <summary>Puts the provider at the start of a path.</summary>
    public abstract void Reset();

    /// <summary>Whether each evolution time is an exercise time.</summary>
    /// <returns>One flag an evolution time.</returns>
    public abstract ImmutableArray<bool> IsExerciseTime();

    /// <summary>Writes the current exercise's data in the given state.</summary>
    /// <param name="currentState">The state.</param>
    /// <param name="results">The data written, the current exercise's count of them.</param>
    public abstract void Values(CurveState currentState, Span<double> results);
}
