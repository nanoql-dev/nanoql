// SPDX-FileCopyrightText: Copyright (C) 2008 Andrea Odetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/mcbasket/pathpayoff.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// A payoff on the assets' whole paths at the fixing dates: its payments and early-termination values, and the state
/// on which a Longstaff-Schwartz regression decides an exercise.
/// </summary>
/// <remarks>
/// An extension point, as in QuantLib: a product derives it (or <see cref="AdaptedPathPayoff"/>). QuantLib's visitor
/// isn't ported (master §2.2's visitor row).
/// </remarks>
[Experimental("NQL0001")]
public abstract class PathPayoff
{
    /// <summary>Gets the payoff's name, for output and comparison, not for switching on its type.</summary>
    public abstract string Name { get; }

    /// <summary>Gets the payoff's description.</summary>
    public abstract string Description { get; }

    /// <summary>
    /// Gets the dimension of the basis functions: the size of every state <see cref="Value"/> writes.
    /// </summary>
    public abstract int BasisSystemDimension { get; }

    /// <summary>
    /// The payoff and early-termination payments of one path. If the option is cancelled at time i, every payment on and
    /// before i counts, plus the exercise value at i: cancellation at i doesn't cancel the payment at i.
    /// </summary>
    /// <param name="path">The assets' values at the fixing times, one row per asset.</param>
    /// <param name="forwardTermStructures">The yield curve at each fixing date.</param>
    /// <param name="payments">The payments at each fixing time, written.</param>
    /// <param name="exercises">The exercise values at each fixing time, written; empty where no exercise is possible.</param>
    /// <param name="states">The regression state at each fixing time; empty where no exercise is possible, and an
    /// empty state signals that no exercise is possible at its time.</param>
    public abstract void Value(
        Matrix path,
        ImmutableArray<YieldTermStructure> forwardTermStructures,
        Vector payments,
        Vector exercises,
        Vector[] states);
}
