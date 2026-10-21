// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/solvers/fdmblackscholessolver.hpp ql/methods/finitedifferences/solvers/fdmblackscholessolver.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Processes;

namespace NanoQL.Methods;

/// <summary>
/// The Black-Scholes finite-difference solver: the Black-Scholes operator on the problem's grid in the logarithm of
/// the underlying, rolled back by the one-dimensional solver, read at a spot.
/// </summary>
/// <remarks>
/// Computed at construction, where QuantLib's lazy object computes at the first read, and immutable after. QuantLib's
/// <c>thetaAt</c> reads its solver without computing it first, so it fails before another read; here the solver is
/// always there.
/// </remarks>
public sealed class FdmBlackScholesSolver
{
    private readonly Fdm1DimSolver solver;

    /// <summary>Initializes a new instance of the <see cref="FdmBlackScholesSolver"/> class, and solves the
    /// problem.</summary>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="strike">The strike the Black forward variance is read at.</param>
    /// <param name="solverDesc">The problem.</param>
    /// <param name="schemeDesc">The scheme; Douglas's by default.</param>
    /// <param name="localVol">Whether to use the process's local volatility.</param>
    /// <param name="illegalLocalVolOverwrite">
    /// The volatility to use where the local volatility can't be computed; <see langword="null"/> or a negative value
    /// to let the failure through.
    /// </param>
    /// <param name="quantoHelper">The quanto helper, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException">The process or the problem is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The rollback's arguments are refused.</exception>
    /// <exception cref="NanoQLException">A step or the spline fails.</exception>
    public FdmBlackScholesSolver(
        GeneralizedBlackScholesProcess process,
        double strike,
        FdmSolverDesc solverDesc,
        FdmSchemeDesc? schemeDesc = null,
        bool localVol = false,
        double? illegalLocalVolOverwrite = null,
        FdmQuantoHelper? quantoHelper = null)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(solverDesc);

        var op = new FdmBlackScholesOp(
            solverDesc.Mesher, process, strike, localVol, illegalLocalVolOverwrite, 0, quantoHelper);

        solver = new Fdm1DimSolver(solverDesc, schemeDesc ?? FdmSchemeDesc.Douglas(), op);
    }

    /// <summary>The value at a spot.</summary>
    /// <param name="s">The spot.</param>
    /// <returns>The value.</returns>
    /// <exception cref="NanoQLException">The spot is outside the grid.</exception>
    public double ValueAt(double s) => solver.InterpolateAt(Math.Log(s));

    /// <summary>The delta at a spot.</summary>
    /// <param name="s">The spot.</param>
    /// <returns>The delta.</returns>
    /// <exception cref="NanoQLException">The spot is outside the grid.</exception>
    public double DeltaAt(double s) => solver.DerivativeX(Math.Log(s)) / s;

    /// <summary>The gamma at a spot.</summary>
    /// <param name="s">The spot.</param>
    /// <returns>The gamma.</returns>
    /// <exception cref="NanoQLException">The spot is outside the grid.</exception>
    public double GammaAt(double s) =>
        (solver.DerivativeXX(Math.Log(s)) - solver.DerivativeX(Math.Log(s))) / (s * s);

    /// <summary>The theta at a spot.</summary>
    /// <param name="s">The spot.</param>
    /// <returns>The theta, or <see langword="null"/> when the problem's first stopping time is 0.</returns>
    /// <exception cref="NanoQLException">The spot is outside the grid.</exception>
    public double? ThetaAt(double s) => solver.ThetaAt(Math.Log(s));
}
