// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/evolver.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;

namespace NanoQL.Models;

/// <summary>
/// A market-model evolver: the gritty work of evolving the rates from one evolution time to the next, path by path.
/// </summary>
/// <remarks>
/// A working type (the S-mm plan's MR1 (ii)): it keeps its state between steps, owned by one computation, and isn't
/// thread-safe. Its <see cref="CurrentState"/> is its own curve state, read only by contract and valid until its next
/// step, as QuantLib's <c>const CurveState&amp;</c> is; a caller that keeps it calls <see cref="CurveState.Clone"/>.
/// An extension point, as in QuantLib.
/// </remarks>
[WorkingType]
public abstract class MarketModelEvolver
{
    /// <summary>Initializes a new instance of the <see cref="MarketModelEvolver"/> class.</summary>
    protected MarketModelEvolver()
    {
    }

    /// <summary>Gets the numeraires, one per step.</summary>
    public abstract ImmutableArray<int> Numeraires { get; }

    /// <summary>Gets the step the next <see cref="AdvanceStep"/> takes.</summary>
    public abstract int CurrentStep { get; }

    /// <summary>Gets the curve state after the last step: the evolver's own, valid until its next step.</summary>
    public abstract CurveState CurrentState { get; }

    /// <summary>Starts a new path.</summary>
    /// <returns>The path's weight.</returns>
    public abstract double StartNewPath();

    /// <summary>Evolves the rates over the current step.</summary>
    /// <returns>The step's weight.</returns>
    public abstract double AdvanceStep();

    /// <summary>Sets the state each path starts from.</summary>
    /// <param name="cs">The curve state whose rates each path starts from.</param>
    public abstract void SetInitialState(CurveState cs);

    /// <summary>
    /// Checks an evolver's initial step: QuantLib's evolvers create their generator for the steps from it on and read
    /// its step's drift calculator, so one at or past the steps is refused, as QuantLib's generators refuse no steps.
    /// </summary>
    /// <param name="initialStep">The initial step.</param>
    /// <param name="steps">The number of steps.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialStep"/> isn't a step.</exception>
    private protected static void CheckInitialStep(int initialStep, int steps)
    {
        if (!(initialStep >= 0 && initialStep < steps))
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialStep),
                string.Create(
                    CultureInfo.InvariantCulture, $"initial step ({initialStep}) must be less than the number of steps ({steps})"));
        }
    }
}
