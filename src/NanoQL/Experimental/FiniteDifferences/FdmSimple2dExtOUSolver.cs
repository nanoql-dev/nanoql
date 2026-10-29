// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdmsimple2dextousolver.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// A two-dimensional finite-difference solver on an <see cref="ExtendedOrnsteinUhlenbeckProcess"/>'s operator in
/// direction 0, the second direction carried by the step conditions (a storage level, say).
/// </summary>
/// <remarks>Solved at construction: QuantLib's lazy calculation, run once, as its process doesn't change.</remarks>
[Experimental("NQL0001")]
public sealed class FdmSimple2dExtOUSolver
{
    private readonly Fdm2DimSolver solver;

    /// <summary>Initializes a new instance of the <see cref="FdmSimple2dExtOUSolver"/> class.</summary>
    /// <param name="process">The process.</param>
    /// <param name="rTS">The discount curve.</param>
    /// <param name="solverDesc">The mesher, boundary conditions, step conditions, payoff and time grid.</param>
    /// <param name="schemeDesc">The scheme, or <see langword="null"/> for Hundsdorfer's.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public FdmSimple2dExtOUSolver(
        ExtendedOrnsteinUhlenbeckProcess process, YieldTermStructure rTS, FdmSolverDesc solverDesc, FdmSchemeDesc? schemeDesc = null)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(rTS);
        ArgumentNullException.ThrowIfNull(solverDesc);
        var op = new FdmExtendedOrnsteinUhlenbeckOp(solverDesc.Mesher, process, rTS, solverDesc.BcSet);
        solver = new Fdm2DimSolver(solverDesc, schemeDesc ?? FdmSchemeDesc.Hundsdorfer(), op);
    }

    /// <summary>The value at the two coordinates, interpolated on the solution's grid.</summary>
    /// <param name="x">The process's coordinate.</param>
    /// <param name="y">The second coordinate.</param>
    /// <returns>The value.</returns>
    public double ValueAt(double x, double y) => solver.InterpolateAt(x, y);
}
