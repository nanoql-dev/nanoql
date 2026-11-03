// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/constrainedevolver.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>
/// A constrained market-model evolver: an evolver whose rates can be fixed by importance sampling, for the Fries-Joshi
/// proxy simulation approach to Greeks.
/// </summary>
/// <remarks>A working type. An extension point, as in QuantLib.</remarks>
[WorkingType]
public abstract class ConstrainedEvolver : MarketModelEvolver
{
    /// <summary>Sets, once, the swap rates each step constrains.</summary>
    /// <param name="startIndexOfSwapRate">Each step's swap's first rate.</param>
    /// <param name="endIndexOfSwapRate">Each step's swap's end, after its last rate.</param>
    public abstract void SetConstraintType(ReadOnlySpan<int> startIndexOfSwapRate, ReadOnlySpan<int> endIndexOfSwapRate);

    /// <summary>Sets, before each path, the value each step constrains its swap rate to, and which steps are active.</summary>
    /// <param name="rateConstraints">Each step's constrained value.</param>
    /// <param name="isConstraintActive">Whether each step's constraint is active.</param>
    public abstract void SetThisConstraint(ReadOnlySpan<double> rateConstraints, ReadOnlySpan<bool> isConstraintActive);
}
