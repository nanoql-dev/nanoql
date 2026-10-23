// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/solvers/fdmhullwhitesolver.hpp ql/methods/finitedifferences/solvers/fdmhullwhitesolver.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Models;

namespace NanoQL.Methods;

/// <summary>The Hull-White model's finite-difference solver: a <see cref="Fdm1DimSolver"/> on a <see cref="FdmHullWhiteOp"/> in direction 0.</summary>
/// <remarks>
/// Computed at construction, where QuantLib's lazy object computes at the first read (S-eq's solvers' form), and
/// immutable after. The model is an argument, where QuantLib's solver holds a handle and recalculates when it changes.
/// </remarks>
public sealed class FdmHullWhiteSolver
{
    private readonly Fdm1DimSolver solver;

    /// <summary>Initializes a new instance of the <see cref="FdmHullWhiteSolver"/> class, and rolls the problem back.</summary>
    /// <param name="model">The Hull-White model.</param>
    /// <param name="solverDesc">The problem.</param>
    /// <param name="schemeDesc">The scheme; <see langword="null"/> for QuantLib's default, <see cref="FdmSchemeDesc.Hundsdorfer"/>.</param>
    /// <exception cref="ArgumentNullException">The model, the problem or one of its parts is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A number of steps is negative.</exception>
    /// <exception cref="ArgumentException">The rollback's arguments are refused.</exception>
    /// <exception cref="NanoQLException">A step or the spline fails.</exception>
    public FdmHullWhiteSolver(HullWhite model, FdmSolverDesc solverDesc, FdmSchemeDesc? schemeDesc = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(solverDesc);

        // QuantLib's performCalculations
        var op = new FdmHullWhiteOp(solverDesc.Mesher, model, 0);

        solver = new Fdm1DimSolver(solverDesc, schemeDesc ?? FdmSchemeDesc.Hundsdorfer(), op);
    }

    /// <summary>The value at a value of the factor.</summary>
    /// <param name="r">The factor.</param>
    /// <returns>The interpolated value.</returns>
    /// <exception cref="NanoQLException">The point is outside the grid.</exception>
    public double ValueAt(double r) => solver.InterpolateAt(r);
}
