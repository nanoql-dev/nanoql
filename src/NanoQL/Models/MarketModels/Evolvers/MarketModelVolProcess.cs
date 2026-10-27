// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/evolvers/marketmodelvolprocess.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>
/// A volatility process for a displaced-diffusion LIBOR market model with an uncorrelated stochastic volatility, the
/// "shifted BGM" with Heston volatility of Brace's "Engineering BGM": it scales each evolution step's variance.
/// </summary>
/// <remarks>
/// A working type (the S-mm plan's MR1 (ii)): it keeps its state along a path, owned by one computation, and isn't
/// thread-safe. <see cref="Clone"/>, a nanoql addition, is deep: an evolver holding a process owns a copy of it
/// (MR1 (iv)). An extension point, as in QuantLib.
/// </remarks>
public abstract class MarketModelVolProcess
{
    /// <summary>Initializes a new instance of the <see cref="MarketModelVolProcess"/> class.</summary>
    protected MarketModelVolProcess()
    {
    }

    /// <summary>Gets the number of variates each evolution step draws.</summary>
    public abstract int VariatesPerStep { get; }

    /// <summary>Gets the number of evolution steps a path takes.</summary>
    public abstract int NumberSteps { get; }

    /// <summary>Gets the number of state variables.</summary>
    public abstract int NumberStateVariables { get; }

    /// <summary>Starts a new path.</summary>
    public abstract void NextPath();

    /// <summary>Evolves the process over the next evolution step.</summary>
    /// <param name="variates">The step's variates, one per variate per step.</param>
    /// <returns>The step's weight.</returns>
    public abstract double Nextstep(ReadOnlySpan<double> variates);

    /// <summary>The multiplier of the last step's standard deviation.</summary>
    /// <returns>The multiplier.</returns>
    public abstract double StepSd();

    /// <summary>The process's state: a view of its buffer, valid until its next step.</summary>
    /// <returns>The state variables.</returns>
    public abstract ReadOnlySpan<double> StateVariables();

    /// <summary>A deep copy: the process's parameters and its state along the current path.</summary>
    /// <returns>The copy, of the receiver's type.</returns>
    public abstract MarketModelVolProcess Clone();
}
