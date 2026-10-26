// SPDX-FileCopyrightText: Copyright (C) 2010 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/solvers/fdm2dblackscholessolver.hpp ql/methods/finitedifferences/solvers/fdm2dblackscholessolver.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Processes;

namespace NanoQL.Methods;

/// <summary>
/// The two-dimensional Black-Scholes solver: a problem on two assets' log prices rolled back on
/// <see cref="Fdm2dBlackScholesOp"/>, its values and Greeks read at the assets' prices.
/// </summary>
/// <remarks>
/// Immutable and safe to share across threads: the operator is built and the rollback runs in the constructor, where
/// QuantLib's lazy object runs them at the first query (master §2.2's finite-difference row).
/// </remarks>
public sealed class Fdm2dBlackScholesSolver
{
    private readonly Fdm2DimSolver solver;

    /// <summary>
    /// Initializes a new instance of the <see cref="Fdm2dBlackScholesSolver"/> class, and solves the problem.
    /// </summary>
    /// <param name="p1">The first asset's process.</param>
    /// <param name="p2">The second asset's process.</param>
    /// <param name="correlation">The assets' correlation.</param>
    /// <param name="solverDesc">The problem.</param>
    /// <param name="schemeDesc">The scheme; Hundsdorfer's by default.</param>
    /// <param name="localVol">Whether to use the processes' local volatilities.</param>
    /// <param name="illegalLocalVolOverwrite">
    /// The volatility to use where a local volatility can't be computed; <see langword="null"/> or a negative value
    /// to let the failure through.
    /// </param>
    /// <exception cref="ArgumentNullException">A process or the problem is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The rollback's arguments are refused.</exception>
    /// <exception cref="NanoQLException">A step or the spline fails.</exception>
    public Fdm2dBlackScholesSolver(
        GeneralizedBlackScholesProcess p1,
        GeneralizedBlackScholesProcess p2,
        double correlation,
        FdmSolverDesc solverDesc,
        FdmSchemeDesc? schemeDesc = null,
        bool localVol = false,
        double? illegalLocalVolOverwrite = null)
    {
        ArgumentNullException.ThrowIfNull(p1);
        ArgumentNullException.ThrowIfNull(p2);
        ArgumentNullException.ThrowIfNull(solverDesc);

        var op = new Fdm2dBlackScholesOp(
            solverDesc.Mesher, p1, p2, correlation, solverDesc.Maturity, localVol, illegalLocalVolOverwrite);

        solver = new Fdm2DimSolver(solverDesc, schemeDesc ?? FdmSchemeDesc.Hundsdorfer(), op);
    }

    /// <summary>The value at the assets' prices.</summary>
    /// <param name="u">The first asset's price.</param>
    /// <param name="v">The second asset's price.</param>
    /// <returns>The value.</returns>
    /// <exception cref="NanoQLException">A price is outside the grid.</exception>
    public double ValueAt(double u, double v) => solver.InterpolateAt(Math.Log(u), Math.Log(v));

    /// <summary>The theta at the assets' prices.</summary>
    /// <param name="u">The first asset's price.</param>
    /// <param name="v">The second asset's price.</param>
    /// <returns>The theta, or <see langword="null"/> when the problem's first stopping time is 0.</returns>
    /// <exception cref="NanoQLException">A price is outside the grid.</exception>
    public double? ThetaAt(double u, double v) => solver.ThetaAt(Math.Log(u), Math.Log(v));

    /// <summary>The delta in the first asset's price.</summary>
    /// <param name="u">The first asset's price.</param>
    /// <param name="v">The second asset's price.</param>
    /// <returns>The delta.</returns>
    /// <exception cref="NanoQLException">A price is outside the grid.</exception>
    public double DeltaXat(double u, double v) => solver.DerivativeX(Math.Log(u), Math.Log(v)) / u;

    /// <summary>The delta in the second asset's price.</summary>
    /// <param name="u">The first asset's price.</param>
    /// <param name="v">The second asset's price.</param>
    /// <returns>The delta.</returns>
    /// <exception cref="NanoQLException">A price is outside the grid.</exception>
    public double DeltaYat(double u, double v) => solver.DerivativeY(Math.Log(u), Math.Log(v)) / v;

    /// <summary>The gamma in the first asset's price.</summary>
    /// <param name="u">The first asset's price.</param>
    /// <param name="v">The second asset's price.</param>
    /// <returns>The gamma.</returns>
    /// <exception cref="NanoQLException">A price is outside the grid.</exception>
    public double GammaXat(double u, double v)
    {
        double x = Math.Log(u);
        double y = Math.Log(v);
        return (solver.DerivativeXX(x, y) - solver.DerivativeX(x, y)) / (u * u);
    }

    /// <summary>The gamma in the second asset's price.</summary>
    /// <param name="u">The first asset's price.</param>
    /// <param name="v">The second asset's price.</param>
    /// <returns>The gamma.</returns>
    /// <exception cref="NanoQLException">A price is outside the grid.</exception>
    public double GammaYat(double u, double v)
    {
        double x = Math.Log(u);
        double y = Math.Log(v);
        return (solver.DerivativeYY(x, y) - solver.DerivativeY(x, y)) / (v * v);
    }

    /// <summary>The cross gamma in the two assets' prices.</summary>
    /// <param name="u">The first asset's price.</param>
    /// <param name="v">The second asset's price.</param>
    /// <returns>The cross gamma.</returns>
    /// <exception cref="NanoQLException">A price is outside the grid.</exception>
    public double GammaXYat(double u, double v) => solver.DerivativeXY(Math.Log(u), Math.Log(v)) / (u * v);
}
