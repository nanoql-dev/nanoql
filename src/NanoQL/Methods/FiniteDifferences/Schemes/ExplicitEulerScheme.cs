// SPDX-FileCopyrightText: Copyright (C) 2009 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/schemes/expliciteulerscheme.hpp ql/methods/finitedifferences/schemes/expliciteulerscheme.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Methods;

/// <summary>The explicit Euler scheme: a ← a + θ·dt·A a, the operator set over the step.</summary>
/// <remarks>
/// A working type: <see cref="Step(Span{double}, double)"/> steps the caller's values in place through a buffer the
/// scheme holds, so a step allocates nothing after the first.
/// </remarks>
[WorkingType]
public sealed class ExplicitEulerScheme : IFdmScheme, IFdmSetOperatorScheme
{
    private const double NullReal = 3.4028234663852886e38;

    private readonly FdmLinearOpComposite map;
    private readonly BoundaryConditionSchemeHelper bcSet;
    private double dt = NullReal;
    private double[] work = [];

    /// <summary>Initializes a new instance of the <see cref="ExplicitEulerScheme"/> class.</summary>
    /// <param name="map">The operator.</param>
    /// <param name="bcSet">The boundary conditions; <c>default</c> or empty for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A boundary condition is <see langword="null"/>.</exception>
    public ExplicitEulerScheme(FdmLinearOpComposite map, ImmutableArray<BoundaryCondition<FdmLinearOp>> bcSet = default)
    {
        ArgumentNullException.ThrowIfNull(map);
        this.map = map;
        this.bcSet = new BoundaryConditionSchemeHelper(bcSet);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The step would end before time 0.</exception>
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

        bcSet.ApplyBeforeApplying(map);
        if (work.Length != a.Length)
        {
            work = new double[a.Length];
        }

        map.Apply(a, work);
        double f = theta * dt;
        for (int k = 0; k < a.Length; ++k)
        {
            a[k] += f * work[k];
        }

        bcSet.ApplyAfterApplying(a);
    }

    // std::max(0.0, x)
    private static double Max0(double x) => 0.0 < x ? x : 0.0;
}
