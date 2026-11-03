// SPDX-FileCopyrightText: Copyright (C) 2018 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/schemes/methodoflinesscheme.hpp ql/methods/finitedifferences/schemes/methodoflinesscheme.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The method-of-lines scheme: integrates the values' ordinary differential equation over each step with an adaptive
/// Runge-Kutta solver.
/// </summary>
/// <remarks>
/// A working type, outside the finite-difference core's allocation pin: each step runs
/// <see cref="AdaptiveRungeKutta{T}"/>, which allocates its state as QuantLib's does.
/// </remarks>
[WorkingType]
public sealed class MethodOfLinesScheme : IFdmScheme
{
    private const double NullReal = 3.4028234663852886e38;

    private readonly double eps;
    private readonly double relInitStepSize;
    private readonly FdmLinearOpComposite map;
    private readonly BoundaryConditionSchemeHelper bcSet;
    private double dt = NullReal;

    /// <summary>Initializes a new instance of the <see cref="MethodOfLinesScheme"/> class.</summary>
    /// <param name="eps">The solver's tolerance.</param>
    /// <param name="relInitStepSize">The solver's first step, relative to the scheme's step.</param>
    /// <param name="map">The operator.</param>
    /// <param name="bcSet">The boundary conditions; <c>default</c> or empty for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A boundary condition is <see langword="null"/>.</exception>
    public MethodOfLinesScheme(
        double eps,
        double relInitStepSize,
        FdmLinearOpComposite map,
        ImmutableArray<BoundaryCondition<FdmLinearOp>> bcSet = default)
    {
        ArgumentNullException.ThrowIfNull(map);
        this.eps = eps;
        this.relInitStepSize = relInitStepSize;
        this.map = map;
        this.bcSet = new BoundaryConditionSchemeHelper(bcSet);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The step would end before time 0.</exception>
    /// <exception cref="NanoQLException">The solver fails.</exception>
    public void Step(Span<double> a, double t)
    {
        if (!(t - dt > -1e-8))
        {
            throw new ArgumentException("a step towards negative time given", nameof(t));
        }

        ImmutableArray<double> v = new AdaptiveRungeKutta<double>(eps, relInitStepSize * dt).Evaluate(
            Apply, a, t, Max0(t - dt));

        double[] y = [.. v];

        bcSet.ApplyAfterSolving(y);

        y.AsSpan().CopyTo(a);
    }

    /// <inheritdoc/>
    public void SetStep(double dt) => this.dt = dt;

    private ImmutableArray<double> Apply(double t, ImmutableArray<double> u)
    {
        map.SetTime(t, t + 0.0001);
        bcSet.ApplyBeforeApplying(map);

        double[] dxdt = new double[u.Length];
        map.Apply(u.AsSpan(), dxdt);
        for (int i = 0; i < dxdt.Length; ++i)
        {
            dxdt[i] = -dxdt[i];
        }

        return [.. dxdt];
    }

    // std::max(0.0, x)
    private static double Max0(double x) => 0.0 < x ? x : 0.0;
}
