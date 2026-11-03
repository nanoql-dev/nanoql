// SPDX-FileCopyrightText: Copyright (C) 2019 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/schemes/cranknicolsonscheme.hpp ql/methods/finitedifferences/schemes/cranknicolsonscheme.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Methods;

/// <summary>
/// The Crank-Nicolson scheme: an explicit step scaled by 1 − θ, then an implicit one scaled by θ. In one dimension it
/// is the Douglas scheme; in more, operator-splitting schemes such as Craig-Sneyd or Hundsdorfer-Verwer usually beat
/// it.
/// </summary>
/// <remarks>A working type, stepping in place through its two schemes.</remarks>
[WorkingType]
public sealed class CrankNicolsonScheme : IFdmScheme, IFdmSetOperatorScheme
{
    private const double NullReal = 3.4028234663852886e38;

    private readonly double theta;
    private readonly ExplicitEulerScheme explicitScheme;
    private readonly ImplicitEulerScheme implicitScheme;
    private double dt = NullReal;

    /// <summary>Initializes a new instance of the <see cref="CrankNicolsonScheme"/> class.</summary>
    /// <param name="theta">The implicit step's weight.</param>
    /// <param name="map">The operator.</param>
    /// <param name="bcSet">The boundary conditions; <c>default</c> or empty for none.</param>
    /// <param name="relTol">The implicit step's iterative solver's relative tolerance.</param>
    /// <param name="solverType">The implicit step's iterative solver.</param>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A boundary condition is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="solverType"/> isn't a member of its
    /// enum.</exception>
    public CrankNicolsonScheme(
        double theta,
        FdmLinearOpComposite map,
        ImmutableArray<BoundaryCondition<FdmLinearOp>> bcSet = default,
        double relTol = 1e-8,
        ImplicitEulerSchemeSolverType solverType = ImplicitEulerSchemeSolverType.BiCGstab)
    {
        this.theta = theta;
        explicitScheme = new ExplicitEulerScheme(map, bcSet);
        implicitScheme = new ImplicitEulerScheme(map, bcSet, relTol, solverType);
    }

    /// <summary>Gets the implicit step's iterative solver's iterations so far.</summary>
    public int NumberOfIterations => implicitScheme.NumberOfIterations;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The step would end before time 0.</exception>
    /// <exception cref="NanoQLException">The implicit system can't be solved.</exception>
    public void Step(Span<double> a, double t) => StepCore(a, t, true);

    /// <inheritdoc/>
    public void SetStep(double dt)
    {
        this.dt = dt;
        explicitScheme.SetStep(this.dt);
        implicitScheme.SetStep(this.dt);
    }

    /// <inheritdoc/>
    void IFdmSetOperatorScheme.StepOnSetOperator(Span<double> a, double t) => StepCore(a, t, false);

    private void StepCore(Span<double> a, double t, bool setTime)
    {
        if (!(t - dt > -1e-8))
        {
            throw new ArgumentException("a step towards negative time given", nameof(t));
        }

        if (theta != 1.0)
        {
            if (setTime)
            {
                explicitScheme.Step(a, t, 1.0 - theta);
            }
            else
            {
                explicitScheme.StepOnSetOperator(a, t, 1.0 - theta);
            }
        }

        if (theta != 0.0)
        {
            if (setTime)
            {
                implicitScheme.Step(a, t, theta);
            }
            else
            {
                implicitScheme.StepOnSetOperator(a, t, theta);
            }
        }
    }
}
