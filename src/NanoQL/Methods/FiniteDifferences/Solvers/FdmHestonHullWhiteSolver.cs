// SPDX-FileCopyrightText: Copyright (C) 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/solvers/fdmhestonhullwhitesolver.hpp ql/methods/finitedifferences/solvers/fdmhestonhullwhitesolver.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Processes;

namespace NanoQL.Methods;

/// <summary>The Heston-Hull-White finite-difference solver: <see cref="FdmHestonHullWhiteOp"/> under
/// <see cref="Fdm3DimSolver"/>, queried at a spot, a variance and a short-rate state.</summary>
/// <remarks>
/// Immutable: the solve runs in the constructor, where QuantLib's runs lazily on the first query (the S-hes plan's
/// HD8). The delta and gamma are central differences in the spot, not the model-implied ones (Mercurio and Morini,
/// <i>A Note on Hedging with Local and Stochastic Volatility Models</i>).
/// </remarks>
public sealed class FdmHestonHullWhiteSolver
{
    private readonly Fdm3DimSolver solver;

    /// <summary>Initializes a new instance of the <see cref="FdmHestonHullWhiteSolver"/> class, and solves.</summary>
    /// <param name="hestonProcess">The Heston process.</param>
    /// <param name="hwProcess">The Hull-White process.</param>
    /// <param name="corrEquityShortRate">The equity-rate correlation.</param>
    /// <param name="solverDesc">The problem.</param>
    /// <param name="schemeDesc">The scheme, or <see langword="null"/> for Hundsdorfer's.</param>
    /// <exception cref="ArgumentNullException">A process or the problem is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The correlations' squares sum past 1.</exception>
    public FdmHestonHullWhiteSolver(
        HestonProcess hestonProcess,
        HullWhiteProcess hwProcess,
        double corrEquityShortRate,
        FdmSolverDesc solverDesc,
        FdmSchemeDesc? schemeDesc = null)
    {
        ArgumentNullException.ThrowIfNull(hestonProcess);
        ArgumentNullException.ThrowIfNull(hwProcess);
        ArgumentNullException.ThrowIfNull(solverDesc);

        var op = new FdmHestonHullWhiteOp(solverDesc.Mesher, hestonProcess, hwProcess, corrEquityShortRate);
        solver = new Fdm3DimSolver(solverDesc, schemeDesc ?? FdmSchemeDesc.Hundsdorfer(), op);
    }

    /// <summary>The value at a spot, a variance and a short-rate state.</summary>
    /// <param name="s">The spot.</param>
    /// <param name="v">The variance.</param>
    /// <param name="r">The short rate's state.</param>
    /// <returns>The value.</returns>
    public double ValueAt(double s, double v, double r) => solver.InterpolateAt(Math.Log(s), v, r);

    /// <summary>The theta at a spot, a variance and a short-rate state.</summary>
    /// <param name="s">The spot.</param>
    /// <param name="v">The variance.</param>
    /// <param name="r">The short rate's state.</param>
    /// <returns>The theta, or <see langword="null"/> where the first stopping time is the start.</returns>
    public double? ThetaAt(double s, double v, double r) => solver.ThetaAt(Math.Log(s), v, r);

    /// <summary>The first derivative in the spot by central differences.</summary>
    /// <param name="s">The spot.</param>
    /// <param name="v">The variance.</param>
    /// <param name="r">The short rate's state.</param>
    /// <param name="eps">The spot's shift.</param>
    /// <returns>The delta.</returns>
    public double DeltaAt(double s, double v, double r, double eps) => (ValueAt(s + eps, v, r) - ValueAt(s - eps, v, r)) / (2 * eps);

    /// <summary>The second derivative in the spot by central differences.</summary>
    /// <param name="s">The spot.</param>
    /// <param name="v">The variance.</param>
    /// <param name="r">The short rate's state.</param>
    /// <param name="eps">The spot's shift.</param>
    /// <returns>The gamma.</returns>
    public double GammaAt(double s, double v, double r, double eps) =>
        (ValueAt(s + eps, v, r) + ValueAt(s - eps, v, r) - (2 * ValueAt(s, v, r))) / (eps * eps);
}
