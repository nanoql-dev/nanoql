// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/callability/exercisevalue.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// The value of exercising a market-model callable product: stepped along a path at its evolution's times, it gives at
/// each exercise time the cash flow exercising pays.
/// </summary>
/// <remarks>
/// A working type and an extension point (U13; the S-mmp plan's MMPR1 (i), (vi)): <see cref="Reset"/> and
/// <see cref="NextStep"/> change it, and it isn't thread-safe; a holder steps its own <see cref="Clone"/>.
/// </remarks>
[WorkingType]
public abstract class MarketModelExerciseValue
{
    /// <summary>Gets the number of exercises.</summary>
    public abstract int NumberOfExercises { get; }

    /// <summary>Gets the evolution: its rate times and the times it is stepped at, its exercise times included.</summary>
    public abstract EvolutionDescription Evolution { get; }

    /// <summary>The times the exercise can pay at, which its cash flows' time indices index.</summary>
    /// <returns>The times.</returns>
    public abstract ImmutableArray<double> PossibleCashFlowTimes();

    /// <summary>Moves to the next evolution time, in the given state.</summary>
    /// <param name="currentState">The state.</param>
    public abstract void NextStep(CurveState currentState);

    /// <summary>Puts the exercise value at the start of a path.</summary>
    public abstract void Reset();

    /// <summary>Whether each evolution time is an exercise time.</summary>
    /// <returns>One flag an evolution time.</returns>
    public abstract ImmutableArray<bool> IsExerciseTime();

    /// <summary>The cash flow exercising pays at the current exercise time, in the given state.</summary>
    /// <param name="currentState">The state.</param>
    /// <returns>The cash flow.</returns>
    public abstract MarketModelMultiProductCashFlow Value(CurveState currentState);

    /// <summary>A deep copy of the exercise value, its state included.</summary>
    /// <returns>The copy.</returns>
    public abstract MarketModelExerciseValue Clone();
}
