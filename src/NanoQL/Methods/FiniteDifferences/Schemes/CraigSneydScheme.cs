// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/schemes/craigsneydscheme.hpp ql/methods/finitedifferences/schemes/craigsneydscheme.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Methods;

/// <summary>
/// The Craig-Sneyd operator-splitting scheme: the Douglas step, then a second predictor on the operator's mixed part
/// weighted by μ and a second round of implicit corrections.
/// </summary>
/// <remarks>
/// A working type (the S-eq plan's SEQR1): <see cref="Step"/> steps the caller's values in place through buffers the
/// scheme holds, so a step allocates nothing after the first. TR-BDF2 drives it as its trapezoidal sub-scheme, on an
/// operator set over TR-BDF2's whole step (the S-eq plan's SEQR9).
/// </remarks>
[WorkingType]
public sealed class CraigSneydScheme : IFdmScheme, IFdmSetOperatorScheme
{
    private const double NullReal = 3.4028234663852886e38;

    private readonly double theta;
    private readonly double mu;
    private readonly FdmLinearOpComposite map;
    private readonly BoundaryConditionSchemeHelper bcSet;
    private double dt = NullReal;
    private double[] y = [], y0 = [], yt = [], work = [], rhs = [], diff = [];

    /// <summary>Initializes a new instance of the <see cref="CraigSneydScheme"/> class.</summary>
    /// <param name="theta">The implicit corrections' weight.</param>
    /// <param name="mu">The second predictor's weight.</param>
    /// <param name="map">The operator.</param>
    /// <param name="bcSet">The boundary conditions; <c>default</c> or empty for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A boundary condition is <see langword="null"/>.</exception>
    public CraigSneydScheme(
        double theta,
        double mu,
        FdmLinearOpComposite map,
        ImmutableArray<BoundaryCondition<FdmLinearOp>> bcSet = default)
    {
        ArgumentNullException.ThrowIfNull(map);
        this.theta = theta;
        this.mu = mu;
        this.map = map;
        this.bcSet = new BoundaryConditionSchemeHelper(bcSet);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The step would end before time 0.</exception>
    /// <exception cref="NanoQLException">A direction's system can't be solved.</exception>
    public void Step(Span<double> a, double t) => StepCore(a, t, true);

    /// <inheritdoc/>
    public void SetStep(double dt) => this.dt = dt;

    /// <inheritdoc/>
    void IFdmSetOperatorScheme.StepOnSetOperator(Span<double> a, double t) => StepCore(a, t, false);

    private void StepCore(Span<double> a, double t, bool setTime)
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

        int n = a.Length;
        if (y.Length != n)
        {
            y = new double[n];
            y0 = new double[n];
            yt = new double[n];
            work = new double[n];
            rhs = new double[n];
            diff = new double[n];
        }

        bcSet.ApplyBeforeApplying(map);
        map.Apply(a, work);
        for (int k = 0; k < n; ++k)
        {
            y[k] = a[k] + (dt * work[k]);
        }

        bcSet.ApplyAfterApplying(y);

        y.AsSpan().CopyTo(y0);

        double f = theta * dt;
        for (int i = 0; i < map.Size; ++i)
        {
            map.ApplyDirection(i, a, work);
            for (int k = 0; k < n; ++k)
            {
                rhs[k] = y[k] - (f * work[k]);
            }

            map.SolveSplitting(i, rhs, -theta * dt, y);
        }

        bcSet.ApplyBeforeApplying(map);
        for (int k = 0; k < n; ++k)
        {
            diff[k] = y[k] - a[k];
        }

        map.ApplyMixed(diff, work);
        double g = mu * dt;
        for (int k = 0; k < n; ++k)
        {
            yt[k] = y0[k] + (g * work[k]);
        }

        bcSet.ApplyAfterApplying(yt);

        for (int i = 0; i < map.Size; ++i)
        {
            map.ApplyDirection(i, a, work);
            for (int k = 0; k < n; ++k)
            {
                rhs[k] = yt[k] - (f * work[k]);
            }

            map.SolveSplitting(i, rhs, -theta * dt, yt);
        }

        bcSet.ApplyAfterSolving(yt);

        yt.AsSpan().CopyTo(a);
    }

    // std::max(0.0, x)
    private static double Max0(double x) => 0.0 < x ? x : 0.0;
}
