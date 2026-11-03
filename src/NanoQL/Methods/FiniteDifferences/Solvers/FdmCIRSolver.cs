// SPDX-FileCopyrightText: Copyright (C) 2020 Lew Wei Hao
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/solvers/fdmcirsolver.hpp ql/methods/finitedifferences/solvers/fdmcirsolver.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Processes;

namespace NanoQL.Methods;

/// <summary>
/// The finite-difference solver of a Black-Scholes equity under a Cox-Ingersoll-Ross short rate: <see cref="FdmCIROp"/>
/// on the problem's grid in the logarithm of the underlying by the short rate, rolled back by the two-dimensional
/// solver, read at a spot and a rate.
/// </summary>
/// <remarks>
/// A working type built per call: computed at construction, where QuantLib's lazy object computes at the first read,
/// and immutable after. With a quanto helper the operator applies its adjustment, where QuantLib's solver takes none
/// (<see cref="FdmCIROp"/>'s remarks).
/// </remarks>
[WorkingType]
public sealed class FdmCIRSolver
{
    private readonly Fdm2DimSolver solver;

    /// <summary>Initializes a new instance of the <see cref="FdmCIRSolver"/> class, and solves the problem.</summary>
    /// <param name="cirProcess">The short rate's process.</param>
    /// <param name="bsProcess">The equity's process.</param>
    /// <param name="solverDesc">The problem, on <see cref="FdmBlackScholesOp.Clock"/>'s times.</param>
    /// <param name="schemeDesc">The scheme; Hundsdorfer's by default, QuantLib's.</param>
    /// <param name="rho">The correlation of the two factors.</param>
    /// <param name="strike">The strike the Black forward variance is read at.</param>
    /// <param name="quantoHelper">The quanto helper, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException">A process or the problem is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The rollback's arguments are refused.</exception>
    /// <exception cref="NanoQLException">A step or the spline fails.</exception>
    public FdmCIRSolver(
        CoxIngersollRossProcess cirProcess,
        GeneralizedBlackScholesProcess bsProcess,
        FdmSolverDesc solverDesc,
        FdmSchemeDesc? schemeDesc = null,
        double rho = 1.0,
        double strike = 1.0,
        FdmQuantoHelper? quantoHelper = null)
    {
        ArgumentNullException.ThrowIfNull(cirProcess);
        ArgumentNullException.ThrowIfNull(bsProcess);
        ArgumentNullException.ThrowIfNull(solverDesc);

        var op = new FdmCIROp(solverDesc.Mesher, cirProcess, bsProcess, rho, strike, quantoHelper);

        solver = new Fdm2DimSolver(solverDesc, schemeDesc ?? FdmSchemeDesc.Hundsdorfer(), op);
    }

    /// <summary>The value at a spot and a short rate.</summary>
    /// <param name="s">The spot.</param>
    /// <param name="r">The short rate.</param>
    /// <returns>The value.</returns>
    public double ValueAt(double s, double r) => solver.InterpolateAt(Math.Log(s), r);

    /// <summary>The delta at a spot and a short rate.</summary>
    /// <param name="s">The spot.</param>
    /// <param name="r">The short rate.</param>
    /// <returns>The delta.</returns>
    public double DeltaAt(double s, double r) => solver.DerivativeX(Math.Log(s), r) / s;

    /// <summary>The gamma at a spot and a short rate.</summary>
    /// <param name="s">The spot.</param>
    /// <param name="r">The short rate.</param>
    /// <returns>The gamma.</returns>
    public double GammaAt(double s, double r)
    {
        double x = Math.Log(s);
        return (solver.DerivativeXX(x, r) - solver.DerivativeX(x, r)) / (s * s);
    }

    /// <summary>The theta at a spot and a short rate.</summary>
    /// <param name="s">The spot.</param>
    /// <param name="r">The short rate.</param>
    /// <returns>The theta; <see langword="null"/> when the problem's first stopping time is time 0.</returns>
    public double? ThetaAt(double s, double r) => solver.ThetaAt(Math.Log(s), r);
}
