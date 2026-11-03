// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/exercisestrategy.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Methods;

/// <summary>
/// An early-exercise strategy stepped along a path: at each of its relevant times it is told the state, and at each
/// exercise time it says whether to exercise.
/// </summary>
/// <remarks>
/// A working type and an extension point: <see cref="Reset"/> and <see cref="NextStep"/> change it,
/// <see cref="Exercise"/> may change its scratch, and it isn't thread-safe; a holder steps its own
/// <see cref="Clone"/>.
/// </remarks>
/// <typeparam name="TState">The state the strategy reads.</typeparam>
[WorkingType]
public abstract class ExerciseStrategy<TState>
{
    /// <summary>The times at which the strategy can exercise.</summary>
    /// <returns>The exercise times.</returns>
    public abstract ImmutableArray<double> ExerciseTimes();

    /// <summary>The times at which the strategy is told the state: the exercise times and any it reads.</summary>
    /// <returns>The relevant times.</returns>
    public abstract ImmutableArray<double> RelevantTimes();

    /// <summary>Puts the strategy at the start of a path.</summary>
    public abstract void Reset();

    /// <summary>Whether to exercise in the given state, at the current exercise time.</summary>
    /// <param name="currentState">The state.</param>
    /// <returns><see langword="true"/> to exercise.</returns>
    public abstract bool Exercise(TState currentState);

    /// <summary>Moves the strategy to its next relevant time, in the given state.</summary>
    /// <param name="currentState">The state.</param>
    public abstract void NextStep(TState currentState);

    /// <summary>A deep copy of the strategy, its state included.</summary>
    /// <returns>The copy.</returns>
    public abstract ExerciseStrategy<TState> Clone();
}
