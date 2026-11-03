// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009, 2011, 2014, 2015 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/solvers/fdmhestonsolver.hpp ql/methods/finitedifferences/solvers/fdmhestonsolver.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The finite-difference Heston solver: <see cref="FdmHestonOp"/> on the problem's grid in the logarithm of the
/// underlying by the variance, rolled back by the two-dimensional solver, read at a spot and a variance.
/// </summary>
/// <remarks>
/// <para>
/// Computed at construction, where QuantLib's lazy object computes at the first read, and immutable after (<see
/// cref="Fdm2DimSolver"/>'s form). The problem's times are <see cref="FdmHestonOp.Clock"/>'s.
/// </para>
/// <para>
/// The delta and gamma are the first and second derivatives with respect to the spot, not the model-implied ones (Fabio
/// Mercurio, Massimo Morini, "A Note on Hedging with Local and Stochastic Volatility Models"); the mean-variance delta
/// and gamma add the variance's moves along its correlation with the spot.
/// </para>
/// </remarks>
public sealed class FdmHestonSolver
{
    private readonly HestonProcess process;
    private readonly Fdm2DimSolver solver;

    /// <summary>Initializes a new instance of the <see cref="FdmHestonSolver"/> class, and solves the problem.</summary>
    /// <param name="process">The Heston process.</param>
    /// <param name="solverDesc">The problem, on <see cref="FdmHestonOp.Clock"/>'s times.</param>
    /// <param name="schemeDesc">The scheme; Hundsdorfer's by default, QuantLib's.</param>
    /// <param name="quantoHelper">The quanto helper, or <see langword="null"/> for none.</param>
    /// <param name="leverageFct">The leverage function, or <see langword="null"/> for none.</param>
    /// <param name="mixingFactor">The factor scaling the variance's volatility σ.</param>
    /// <exception cref="ArgumentNullException">The process or the problem is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The rollback's arguments are refused.</exception>
    /// <exception cref="NanoQLException">A step or the spline fails.</exception>
    public FdmHestonSolver(
        HestonProcess process,
        FdmSolverDesc solverDesc,
        FdmSchemeDesc? schemeDesc = null,
        FdmQuantoHelper? quantoHelper = null,
        LocalVolTermStructure? leverageFct = null,
        double mixingFactor = 1.0)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(solverDesc);
        this.process = process;

        var op = new FdmHestonOp(solverDesc.Mesher, process, quantoHelper, leverageFct, mixingFactor);

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

    /// <summary>The mean-variance delta at a spot and a variance: the delta plus ρσ/s times the derivative with
    /// respect to the variance.</summary>
    /// <param name="s">The spot.</param>
    /// <param name="v">The variance.</param>
    /// <returns>The mean-variance delta.</returns>
    public double MeanVarianceDeltaAt(double s, double v)
    {
        double alpha = process.Rho * process.Sigma / s;
        return DeltaAt(s, v) + (alpha * solver.DerivativeY(Math.Log(s), v));
    }

    /// <summary>The mean-variance gamma at a spot and a variance.</summary>
    /// <param name="s">The spot.</param>
    /// <param name="v">The variance.</param>
    /// <returns>The mean-variance gamma.</returns>
    public double MeanVarianceGammaAt(double s, double v)
    {
        double x = Math.Log(s);
        double alpha = process.Rho * process.Sigma / s;
        return GammaAt(s, v)
            + (solver.DerivativeYY(x, v) * alpha * alpha)
            + (2 * solver.DerivativeXY(x, v) * alpha / s);
    }
}
