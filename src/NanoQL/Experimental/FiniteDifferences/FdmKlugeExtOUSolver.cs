// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdmklugeextousolver.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// The finite-difference solver of a <see cref="KlugeExtOUProcess"/> on a mesher of three or more dimensions, its jump
/// integral at a Gauss-Laguerre order of 16, as QuantLib's; directions past the third carried by the step conditions.
/// </summary>
/// <remarks>
/// Solved at construction: QuantLib's lazy calculation, run once, as its process doesn't change. QuantLib's template
/// parameter N, the mesher's dimension, is the mesher's own here, checked at three or more.
/// </remarks>
[Experimental("NQL0001")]
public sealed class FdmKlugeExtOUSolver
{
    private const int IntegroIntegrationOrder = 16;

    private readonly FdmNdimSolver solver;

    /// <summary>Initializes a new instance of the <see cref="FdmKlugeExtOUSolver"/> class.</summary>
    /// <param name="klugeOUProcess">The process.</param>
    /// <param name="rTS">The discount curve.</param>
    /// <param name="solverDesc">The mesher, boundary conditions, step conditions, payoff and time grid.</param>
    /// <param name="schemeDesc">The scheme, or <see langword="null"/> for Hundsdorfer's.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The mesher has fewer than three dimensions.</exception>
    public FdmKlugeExtOUSolver(
        KlugeExtOUProcess klugeOUProcess, YieldTermStructure rTS, FdmSolverDesc solverDesc, FdmSchemeDesc? schemeDesc = null)
    {
        ArgumentNullException.ThrowIfNull(klugeOUProcess);
        ArgumentNullException.ThrowIfNull(rTS);
        ArgumentNullException.ThrowIfNull(solverDesc);
        if (!(solverDesc.Mesher.Layout.Dim.Length >= 3))
        {
            throw new ArgumentException(
                $"a Kluge solver needs a mesher of three or more dimensions, not {solverDesc.Mesher.Layout.Dim.Length}",
                nameof(solverDesc));
        }

        var op = new FdmKlugeExtOUOp(solverDesc.Mesher, klugeOUProcess, rTS, solverDesc.BcSet, IntegroIntegrationOrder);
        solver = new FdmNdimSolver(solverDesc, schemeDesc ?? FdmSchemeDesc.Hundsdorfer(), op);
    }

    /// <summary>The value at a point, interpolated on the solution's grid.</summary>
    /// <param name="x">The point, one coordinate per direction.</param>
    /// <returns>The value.</returns>
    public double ValueAt(ReadOnlySpan<double> x) => solver.InterpolateAt(x);
}
