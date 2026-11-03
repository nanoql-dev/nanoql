// SPDX-FileCopyrightText: Copyright (C) 2018 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/schemes/trbdf2scheme.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The TR-BDF2 scheme: a trapezoidal sub-step of length α·dt, then a BDF2 stage to the end of the step.
/// </summary>
/// <typeparam name="TTrapezoidalScheme">The trapezoidal sub-scheme (QuantLib's template parameter).</typeparam>
/// <remarks>
/// <para>
/// Both stages run on the operator set once over the whole step, [t − dt, t], the interval QuantLib's other schemes set
/// for their own steps: the sub-scheme steps without setting its operator's time. QuantLib's sub-step sets the operator
/// over [t − α·dt, t] and its BDF2 stage solves with that operator, so with time-dependent coefficients QuantLib's
/// scheme is first order or stalls; its own cases are all flat, where both stages' last bits move with the fix
/// (<c>Divergences/TrBdf2TimeDependentTests.cs</c>). The boundary conditions keep QuantLib's times: the sub-step sets
/// them to t − α·dt, and the stage to t − dt.
/// </para>
/// <para>
/// The fix holds over nanoql's seven one-step schemes, which can step on the set operator. A trapezoidal scheme of
/// one's own steps as it always does, setting its operator over [t − α·dt, t], so the stage solves with that operator,
/// as QuantLib's does.
/// </para>
/// <para>
/// A working type: <see cref="Step"/> steps the caller's values in place through buffers the scheme holds, so a
/// one-directional step allocates nothing after the first.
/// </para>
/// </remarks>
[WorkingType]
public sealed class TrBDF2Scheme<TTrapezoidalScheme> : IFdmScheme
    where TTrapezoidalScheme : class, IFdmScheme
{
    private const double NullReal = 3.4028234663852886e38;

    private readonly double alpha;
    private readonly FdmLinearOpComposite map;
    private readonly TTrapezoidalScheme trapezoidalScheme;
    private readonly BoundaryConditionSchemeHelper bcSet;
    private readonly double relTol;
    private readonly TrBDF2SchemeSolverType solverType;
    private double dt = NullReal;
    private double beta = NullReal;
    private int iterations;
    private double[] fStar = [], f = [], work = [];

    /// <summary>Initializes a new instance of the <see cref="TrBDF2Scheme{TTrapezoidalScheme}"/> class.</summary>
    /// <param name="alpha">The trapezoidal sub-step's share of the step.</param>
    /// <param name="map">The operator.</param>
    /// <param name="trapezoidalScheme">The trapezoidal sub-scheme, built on the same operator.</param>
    /// <param name="bcSet">The boundary conditions; <c>default</c> or empty for none.</param>
    /// <param name="relTol">The BDF2 stage's iterative solver's relative tolerance.</param>
    /// <param name="solverType">The BDF2 stage's iterative solver.</param>
    /// <exception cref="ArgumentNullException">The operator or the sub-scheme is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A boundary condition is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="solverType"/> isn't a member of its
    /// enum.</exception>
    public TrBDF2Scheme(
        double alpha,
        FdmLinearOpComposite map,
        TTrapezoidalScheme trapezoidalScheme,
        ImmutableArray<BoundaryCondition<FdmLinearOp>> bcSet = default,
        double relTol = 1e-8,
        TrBDF2SchemeSolverType solverType = TrBDF2SchemeSolverType.BiCGstab)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(trapezoidalScheme);
        EnumArguments.Check(solverType, nameof(solverType));
        this.alpha = alpha;
        this.map = map;
        this.trapezoidalScheme = trapezoidalScheme;
        this.bcSet = new BoundaryConditionSchemeHelper(bcSet);
        this.relTol = relTol;
        this.solverType = solverType;
    }

    /// <summary>Gets the BDF2 stage's iterative solver's iterations so far.</summary>
    public int NumberOfIterations => iterations;

    /// <inheritdoc/>
    public void SetStep(double dt)
    {
        this.dt = dt;
        beta = (1.0 - alpha) / (2.0 - alpha) * this.dt;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The step would end before time 0.</exception>
    /// <exception cref="NanoQLException">A system can't be solved.</exception>
    public void Step(Span<double> fn, double t)
    {
        if (!(t - dt > -1e-8))
        {
            throw new ArgumentException("a step towards negative time given", nameof(t));
        }

        // The operator set once over the whole step, for both stages
        map.SetTime(Max0(t - dt), t);

        double intermediateTimeStep = dt * alpha;

        int n = fn.Length;
        if (fStar.Length != n)
        {
            fStar = new double[n];
            f = new double[n];
            work = new double[n];
        }

        fn.CopyTo(fStar);
        trapezoidalScheme.SetStep(intermediateTimeStep);
        if (trapezoidalScheme is IFdmSetOperatorScheme onSetOperator)
        {
            onSetOperator.StepOnSetOperator(fStar, t);
        }
        else
        {
            // a scheme of one's own sets its operator's time, as QuantLib's TR-BDF2 lets its sub-step do
            trapezoidalScheme.Step(fStar, t);
        }

        bcSet.SetTime(Max0(t - dt));
        bcSet.ApplyBeforeSolving(map, fn);

        double c1 = 1 / alpha;
        double c2 = (1 - alpha) * (1 - alpha) / alpha;
        double c3 = 2 - alpha;
        for (int k = 0; k < n; ++k)
        {
            f[k] = ((c1 * fStar[k]) - (c2 * fn[k])) / c3;
        }

        if (map.Size == 1)
        {
            map.SolveSplitting(0, f, -beta, work);
            work.AsSpan().CopyTo(fn);
        }
        else
        {
            SolveIteratively(fn);
        }

        bcSet.ApplyAfterSolving(fn);
    }

    // The several-direction solve of f into fn, by QuantLib's Krylov solvers on vectors; a method of its own, so that
    // its closures are allocated only where it runs, never on the one-direction step
    private void SolveIteratively(Span<double> fn)
    {
        int n = fn.Length;
        double s = -beta;
        double scale = beta;
        Vector Preconditioner(Vector x) => map.Preconditioner(x, s);
        Vector ApplyF(Vector r) => r - (scale * map.Apply(r));
        var rhs = new Vector(f);

        if (solverType == TrBDF2SchemeSolverType.BiCGstab)
        {
            BiCGStabResult result =
                new BiCGstab(ApplyF, Math.Max(10, n), relTol, Preconditioner).Solve(rhs, rhs);

            iterations += result.Iterations;
            result.X.AsSpan().CopyTo(fn);
        }
        else if (solverType == TrBDF2SchemeSolverType.GMRES)
        {
            GMRESResult result =
                new GMRES(ApplyF, Math.Max(10, n / 10), relTol, Preconditioner).Solve(rhs, rhs);

            iterations += result.Errors.Length;
            result.X.AsSpan().CopyTo(fn);
        }
        else
        {
            throw new NanoQLException("unknown/illegal solver type");
        }
    }

    // std::max(0.0, x)
    private static double Max0(double x) => 0.0 < x ? x : 0.0;
}
