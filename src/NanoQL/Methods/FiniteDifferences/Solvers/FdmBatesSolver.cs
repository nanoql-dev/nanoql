// SPDX-FileCopyrightText: Copyright (C) 2010 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2010, 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/solvers/fdmbatessolver.hpp ql/methods/finitedifferences/solvers/fdmbatessolver.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Processes;

namespace NanoQL.Methods;

/// <summary>
/// The finite-difference Bates solver: <see cref="FdmBatesOp"/> on the problem's grid in the logarithm of the underlying
/// by the variance, rolled back by the two-dimensional solver, read at a spot and a variance.
/// </summary>
/// <remarks>
/// Computed at construction, where QuantLib's lazy object computes at the first read, and immutable after. The
/// problem's times are <see cref="FdmHestonOp.Clock"/>'s. The delta and gamma are the derivatives with respect to the
/// spot, not the model-implied ones.
/// </remarks>
public sealed class FdmBatesSolver
{
    private readonly Fdm2DimSolver solver;

    /// <summary>Initializes a new instance of the <see cref="FdmBatesSolver"/> class, and solves the problem.</summary>
    /// <param name="process">The Bates process.</param>
    /// <param name="solverDesc">The problem, on <see cref="FdmHestonOp.Clock"/>'s times.</param>
    /// <param name="schemeDesc">The scheme; Hundsdorfer's by default, QuantLib's.</param>
    /// <param name="integroIntegrationOrder">The order of the jump integral's Gauss-Hermite quadrature.</param>
    /// <param name="quantoHelper">The quanto helper, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException">The process or the problem is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The rollback's arguments are refused.</exception>
    /// <exception cref="NanoQLException">The boundary conditions aren't Dirichlet's, or a step or the spline
    /// fails.</exception>
    public FdmBatesSolver(
        BatesProcess process,
        FdmSolverDesc solverDesc,
        FdmSchemeDesc? schemeDesc = null,
        int integroIntegrationOrder = 12,
        FdmQuantoHelper? quantoHelper = null)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(solverDesc);

        var op = new FdmBatesOp(solverDesc.Mesher, process, solverDesc.BcSet, integroIntegrationOrder, quantoHelper);

        solver = new Fdm2DimSolver(solverDesc, schemeDesc ?? FdmSchemeDesc.Hundsdorfer(), op);
    }

    /// <summary>The value at a spot and a variance.</summary>
    /// <param name="s">The spot.</param>
    /// <param name="v">The variance.</param>
    /// <returns>The value.</returns>
    public double ValueAt(double s, double v) => solver.InterpolateAt(Math.Log(s), v);

    /// <summary>The theta at a spot and a variance.</summary>
    /// <param name="s">The spot.</param>
    /// <param name="v">The variance.</param>
    /// <returns>The theta; <see langword="null"/> when the problem's first stopping time is time 0.</returns>
    public double? ThetaAt(double s, double v) => solver.ThetaAt(Math.Log(s), v);

    /// <summary>The first derivative with respect to the spot at a spot and a variance.</summary>
    /// <param name="s">The spot.</param>
    /// <param name="v">The variance.</param>
    /// <returns>The delta.</returns>
    public double DeltaAt(double s, double v) => solver.DerivativeX(Math.Log(s), v) / s;

    /// <summary>The second derivative with respect to the spot at a spot and a variance.</summary>
    /// <param name="s">The spot.</param>
    /// <param name="v">The variance.</param>
    /// <returns>The gamma.</returns>
    public double GammaAt(double s, double v)
    {
        double x = Math.Log(s);
        return (solver.DerivativeXX(x, v) - solver.DerivativeX(x, v)) / (s * s);
    }
}
