// SPDX-FileCopyrightText: Copyright (C) 2009 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2009, 2017 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/schemes/impliciteulerscheme.hpp ql/methods/finitedifferences/schemes/impliciteulerscheme.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The implicit Euler scheme: solves (1 − θ·dt·A) a' = a for a', directly for a one-directional operator and by an
/// iterative solver otherwise.
/// </summary>
/// <remarks>
/// A working type: <see cref="Step(Span{double}, double)"/> steps the caller's values in place through a buffer the
/// scheme holds, so a one-directional step allocates nothing after the first; the iterative solvers allocate their
/// vectors, as QuantLib's do.
/// </remarks>
[WorkingType]
public sealed class ImplicitEulerScheme : IFdmScheme, IFdmSetOperatorScheme
{
    private const double NullReal = 3.4028234663852886e38;

    private readonly double relTol;
    private readonly FdmLinearOpComposite map;
    private readonly BoundaryConditionSchemeHelper bcSet;
    private readonly ImplicitEulerSchemeSolverType solverType;
    private double dt = NullReal;
    private int iterations;
    private double[] work = [];

    /// <summary>Initializes a new instance of the <see cref="ImplicitEulerScheme"/> class.</summary>
    /// <param name="map">The operator.</param>
    /// <param name="bcSet">The boundary conditions; <c>default</c> or empty for none.</param>
    /// <param name="relTol">The iterative solver's relative tolerance.</param>
    /// <param name="solverType">The iterative solver.</param>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A boundary condition is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="solverType"/> isn't a member of its
    /// enum.</exception>
    public ImplicitEulerScheme(
        FdmLinearOpComposite map,
        ImmutableArray<BoundaryCondition<FdmLinearOp>> bcSet = default,
        double relTol = 1e-8,
        ImplicitEulerSchemeSolverType solverType = ImplicitEulerSchemeSolverType.BiCGstab)
    {
        ArgumentNullException.ThrowIfNull(map);
        EnumArguments.Check(solverType, nameof(solverType));
        this.relTol = relTol;
        this.map = map;
        this.bcSet = new BoundaryConditionSchemeHelper(bcSet);
        this.solverType = solverType;
    }

    /// <summary>Gets the iterative solver's iterations so far.</summary>
    public int NumberOfIterations => iterations;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The step would end before time 0.</exception>
    /// <exception cref="NanoQLException">The system can't be solved.</exception>
    public void Step(Span<double> a, double t) => Step(a, t, 1.0);

    /// <inheritdoc/>
    public void SetStep(double dt) => this.dt = dt;

    /// <inheritdoc/>
    void IFdmSetOperatorScheme.StepOnSetOperator(Span<double> a, double t) => StepCore(a, t, 1.0, false);

    /// <summary>The step scaled by θ, as Crank-Nicolson takes it.</summary>
    /// <param name="a">The values.</param>
    /// <param name="t">The time the step starts from.</param>
    /// <param name="theta">The scale.</param>
    internal void Step(Span<double> a, double t, double theta) => StepCore(a, t, theta, true);

    /// <summary>The θ-scaled step on an operator TR-BDF2 has already set.</summary>
    /// <param name="a">The values.</param>
    /// <param name="t">The time the step starts from.</param>
    /// <param name="theta">The scale.</param>
    internal void StepOnSetOperator(Span<double> a, double t, double theta) => StepCore(a, t, theta, false);

    private void StepCore(Span<double> a, double t, double theta, bool setTime)
    {
        if (!(t - dt > -1e-8))
        {
            throw new ArgumentException("a step towards negative time given", nameof(t));
        }

        if (setTime)
        {
            map.SetTime(Max0(t - dt), t);
        }

        bcSet.SetTime(Max0(t - dt));

        bcSet.ApplyBeforeSolving(map, a);

        if (map.Size == 1)
        {
            if (work.Length != a.Length)
            {
                work = new double[a.Length];
            }

            map.SolveSplitting(0, a, -theta * dt, work);
            work.AsSpan().CopyTo(a);
        }
        else
        {
            SolveIteratively(a, theta);
        }

        bcSet.ApplyAfterSolving(a);
    }

    // The several-direction solve, by QuantLib's Krylov solvers on vectors; a method of its own, so that its closures
    // are allocated only where it runs, never on the one-direction step
    private void SolveIteratively(Span<double> a, double theta)
    {
        double s = -theta * dt;
        double scale = theta * dt;
        Vector Preconditioner(Vector x) => map.Preconditioner(x, s);
        Vector ApplyF(Vector r) => r - (scale * map.Apply(r));
        var rhs = new Vector(a);

        if (solverType == ImplicitEulerSchemeSolverType.BiCGstab)
        {
            BiCGStabResult result =
                new BiCGstab(ApplyF, Math.Max(10, a.Length), relTol, Preconditioner).Solve(rhs, rhs);

            iterations += result.Iterations;
            result.X.AsSpan().CopyTo(a);
        }
        else if (solverType == ImplicitEulerSchemeSolverType.GMRES)
        {
            GMRESResult result =
                new GMRES(ApplyF, Math.Max(10, a.Length / 10), relTol, Preconditioner).Solve(rhs, rhs);

            iterations += result.Errors.Length;
            result.X.AsSpan().CopyTo(a);
        }
        else
        {
            throw new NanoQLException("unknown/illegal solver type");
        }
    }

    // std::max(0.0, x)
    private static double Max0(double x) => 0.0 < x ? x : 0.0;
}
