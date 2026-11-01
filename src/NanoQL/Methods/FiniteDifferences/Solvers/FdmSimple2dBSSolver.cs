// SPDX-FileCopyrightText: Copyright (C) 2009 Ralph Schreyer
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/solvers/fdmsimple2dbssolver.hpp ql/methods/finitedifferences/solvers/fdmsimple2dbssolver.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Processes;

namespace NanoQL.Methods;

/// <summary>
/// The two-dimensional Black-Scholes finite-difference solver: the Black-Scholes operator in direction 0 of a mesher in
/// the logarithm of the underlying by a second direction the operator leaves alone, such as the swing option's rights
/// used, rolled back by the two-dimensional solver, read at a spot and a second coordinate.
/// </summary>
/// <remarks>
/// A working type built per call (the S-van plan's VD11): computed at construction, where QuantLib's lazy object computes
/// at the first read, and immutable after. The second coordinate is read at its logarithm, as QuantLib reads it: a
/// value at 1 is the value at the second direction's location 0.
/// </remarks>
[WorkingType]
public sealed class FdmSimple2dBSSolver
{
    private readonly Fdm2DimSolver solver;

    /// <summary>Initializes a new instance of the <see cref="FdmSimple2dBSSolver"/> class, and solves the problem.</summary>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="strike">The strike the Black forward variance is read at.</param>
    /// <param name="desc">The problem, on <see cref="FdmBlackScholesOp.Clock"/>'s times.</param>
    /// <param name="schemeDesc">The scheme; Douglas's by default, QuantLib's.</param>
    /// <exception cref="ArgumentNullException">The process or the problem is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The rollback's arguments are refused.</exception>
    /// <exception cref="NanoQLException">A step or the spline fails.</exception>
    public FdmSimple2dBSSolver(
        GeneralizedBlackScholesProcess process, double strike, FdmSolverDesc desc, FdmSchemeDesc? schemeDesc = null)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(desc);

        var op = new FdmBlackScholesOp(desc.Mesher, process, strike);

        solver = new Fdm2DimSolver(desc, schemeDesc ?? FdmSchemeDesc.Douglas(), op);
    }

    /// <summary>The value at a spot and a second coordinate.</summary>
    /// <param name="s">The spot.</param>
    /// <param name="a">The second coordinate, read at its logarithm.</param>
    /// <returns>The value.</returns>
    public double ValueAt(double s, double a) => solver.InterpolateAt(Math.Log(s), Math.Log(a));

    /// <summary>The delta at a spot and a second coordinate, by central differences.</summary>
    /// <param name="s">The spot.</param>
    /// <param name="a">The second coordinate.</param>
    /// <param name="eps">The spot's bump.</param>
    /// <returns>The delta.</returns>
    public double DeltaAt(double s, double a, double eps) => (ValueAt(s + eps, a) - ValueAt(s - eps, a)) / (2 * eps);

    /// <summary>The gamma at a spot and a second coordinate, by central differences.</summary>
    /// <param name="s">The spot.</param>
    /// <param name="a">The second coordinate.</param>
    /// <param name="eps">The spot's bump.</param>
    /// <returns>The gamma.</returns>
    public double GammaAt(double s, double a, double eps) =>
        (ValueAt(s + eps, a) + ValueAt(s - eps, a) - (2 * ValueAt(s, a))) / (eps * eps);

    /// <summary>The theta at a spot and a second coordinate.</summary>
    /// <param name="s">The spot.</param>
    /// <param name="a">The second coordinate, read at its logarithm.</param>
    /// <returns>The theta; <see langword="null"/> when the problem's first stopping time is time 0.</returns>
    public double? ThetaAt(double s, double a) => solver.ThetaAt(Math.Log(s), Math.Log(a));
}
