// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdmsimple3dextoujumpsolver.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// A three-dimensional finite-difference solver on Kluge's <see cref="ExtOUWithJumpsProcess"/> operator in directions 0
/// and 1, its jump integral at a Gauss-Laguerre order of 32, the third direction carried by the step conditions (a
/// swing option's exercises, say).
/// </summary>
/// <remarks>Solved at construction: QuantLib's lazy calculation, run once, as its process doesn't change.</remarks>
[Experimental("NQL0001")]
public sealed class FdmSimple3dExtOUJumpSolver
{
    private const int IntegroIntegrationOrder = 32;

    private readonly Fdm3DimSolver solver;

    /// <summary>Initializes a new instance of the <see cref="FdmSimple3dExtOUJumpSolver"/> class.</summary>
    /// <param name="process">The process.</param>
    /// <param name="rTS">The discount curve.</param>
    /// <param name="solverDesc">The mesher, boundary conditions, step conditions, payoff and time grid.</param>
    /// <param name="schemeDesc">The scheme, or <see langword="null"/> for Hundsdorfer's.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public FdmSimple3dExtOUJumpSolver(
        ExtOUWithJumpsProcess process, YieldTermStructure rTS, FdmSolverDesc solverDesc, FdmSchemeDesc? schemeDesc = null)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(rTS);
        ArgumentNullException.ThrowIfNull(solverDesc);
        var op = new FdmExtOUJumpOp(solverDesc.Mesher, process, rTS, solverDesc.BcSet, IntegroIntegrationOrder);
        solver = new Fdm3DimSolver(solverDesc, schemeDesc ?? FdmSchemeDesc.Hundsdorfer(), op);
    }

    /// <summary>The value at the three coordinates, interpolated on the solution's grid.</summary>
    /// <param name="x">X.</param>
    /// <param name="y">The jump component Y.</param>
    /// <param name="z">The third coordinate.</param>
    /// <returns>The value.</returns>
    public double ValueAt(double x, double y, double z) => solver.InterpolateAt(x, y, z);
}
