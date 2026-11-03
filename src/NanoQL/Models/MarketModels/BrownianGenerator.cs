// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/browniangenerator.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>
/// An incremental generator of Brownian increments: each path's draws, step by step, for a number of factors.
/// </summary>
/// <remarks>
/// A working type: each draw changes it, it is owned by one computation and it is not thread-safe. An extension point,
/// as in QuantLib: the Mersenne-twister and Sobol generators derive it.
/// </remarks>
[WorkingType]
public abstract class BrownianGenerator
{
    /// <summary>Initializes a new instance of the <see cref="BrownianGenerator"/> class.</summary>
    protected BrownianGenerator()
    {
    }

    /// <summary>Gets the number of factors each step draws.</summary>
    public abstract int NumberOfFactors { get; }

    /// <summary>Gets the number of steps each path draws.</summary>
    public abstract int NumberOfSteps { get; }

    /// <summary>Draws the current path's next step.</summary>
    /// <param name="output">The step's increments, one per factor.</param>
    /// <returns>The step's weight.</returns>
    public abstract double NextStep(Span<double> output);

    /// <summary>Starts a new path.</summary>
    /// <returns>The path's weight.</returns>
    public abstract double NextPath();
}
