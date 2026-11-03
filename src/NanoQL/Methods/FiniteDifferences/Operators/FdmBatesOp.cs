// SPDX-FileCopyrightText: Copyright (C) 2010 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmbatesop.hpp ql/methods/finitedifferences/operators/fdmbatesop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The Bates backward operator: the Heston operator on the dividend curve spreaded by the jumps' compensator λm, and
/// the jumps' integral λ(E[V(x + J)] − V(x)), J normal with mean ν and standard deviation δ, by Gauss-Hermite
/// quadrature over each variance row's linear interpolation in the log spot.
/// </summary>
/// <remarks>
/// <para>
/// A working type: the Heston part steps in place; the jump integral, in the apply and the mixed apply, reads each row
/// into held buffers and interpolates it in place, by QuantLib's <c>LinearInterpolation</c> arithmetic (the slopes from
/// the row, the value by its located interval), so a step allocates nothing. Its boundary conditions must be
/// Dirichlet's, as QuantLib's. Beyond the grid the integral extrapolates each row linearly, QuantLib's approximation.
/// </para>
/// <para>
/// The operator's times are <see cref="FdmHestonOp.Clock"/>'s. QuantLib adds the compensator λm to the dividend curve's
/// zero rate in the dividend curve's own day count (<c>fdmbatesop.cpp:45-58</c>), so on a dividend curve on another day
/// counter than the risk-free curve's the jumps' compensator, an intensity per unit of the model's time, is read off
/// its clock. Here, where the dividend curve shares the engine's clock, QuantLib's spreaded curve and its bits;
/// otherwise the dividend curve read on its own clock and λm added per unit of the engine's time, as QuantLib's
/// operator on the market restated on one clock (<c>Divergences/FdHestonMismatchedTimesTests.cs</c>).
/// </para>
/// </remarks>
[WorkingType]
public sealed class FdmBatesOp : FdmLinearOpComposite
{
    private readonly double lambda;
    private readonly double delta;
    private readonly double nu;
    private readonly ImmutableArray<BoundaryCondition<FdmLinearOp>> bcSet;
    private readonly FdmHestonOp hestonOp;

    // the quadrature's nodes and weights, the grid's log spots, each row's values and slopes, and the integral, held
    private readonly double[] nodes;
    private readonly double[] weights;
    private readonly double[] x;
    private readonly double[] f;
    private readonly double[] s;
    private readonly double[] integral;
    private readonly int dim0;
    private readonly int dim1;
    private readonly int[] row;
    private readonly int[] column;

    /// <summary>Initializes a new instance of the <see cref="FdmBatesOp"/> class.</summary>
    /// <param name="mesher">The mesher: the log spot in direction 0, the variance in direction 1.</param>
    /// <param name="batesProcess">The Bates process.</param>
    /// <param name="bcSet">The boundary conditions, Dirichlet's.</param>
    /// <param name="integroIntegrationOrder">The order of the jump integral's Gauss-Hermite quadrature.</param>
    /// <param name="quantoHelper">The quanto helper, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException">The mesher or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The boundary conditions are the default array.</exception>
    /// <exception cref="NanoQLException">The layout isn't two-dimensional ("invalid layout dimension").</exception>
    public FdmBatesOp(
        FdmMesher mesher,
        BatesProcess batesProcess,
        ImmutableArray<BoundaryCondition<FdmLinearOp>> bcSet,
        int integroIntegrationOrder,
        FdmQuantoHelper? quantoHelper = null)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(batesProcess);
        if (bcSet.IsDefault)
        {
            throw new ArgumentException("default(ImmutableArray) is not a boundary set; an empty one is []", nameof(bcSet));
        }

        lambda = batesProcess.Lambda;
        delta = batesProcess.Delta;
        nu = batesProcess.Nu;
        double m = Math.Exp(nu + (0.5 * delta * delta)) - 1.0;
        var gaussHermiteIntegration = new GaussHermiteIntegration(integroIntegrationOrder);
        this.bcSet = bcSet;

        // the Heston operator on the dividend curve spreaded by lambda*m, on the Bates process's clock: QuantLib's
        // spreaded curve where the dividend curve shares the clock's times; otherwise the dividend curve read on its own
        // clock and the compensator added per unit of the engine's time, the jumps' intensity being per unit of it
        TermStructure clock = FdmHestonOp.Clock(batesProcess, quantoHelper);
        YieldTermStructure dividends = batesProcess.DividendYield;
        hestonOp = dividends.SharesTimes(clock)
            ? new FdmHestonOp(
                mesher,
                batesProcess,
                new ZeroSpreadedTermStructure(dividends, lambda * m, Compounding.Continuous),
                quantoHelper,
                null,
                1.0,
                clock,
                0.0)
            : new FdmHestonOp(mesher, batesProcess, dividends, quantoHelper, null, 1.0, clock, lambda * m);

        if (!(mesher.Layout.Dim.Length == 2))
        {
            throw new NanoQLException("invalid layout dimension");
        }

        nodes = [.. gaussHermiteIntegration.X().AsSpan()];
        weights = [.. gaussHermiteIntegration.Weights().AsSpan()];
        dim0 = mesher.Layout.Dim[0];
        dim1 = mesher.Layout.Dim[1];
        int n = mesher.Layout.Size;
        x = new double[dim0];
        f = new double[dim1 * dim0];
        s = new double[dim1 * dim0];
        integral = new double[n];
        row = new int[n];
        column = new int[n];

        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int k = 0; k < n; ++k, iter.Increment())
        {
            int i = iter.Coordinates[0];
            int j = iter.Coordinates[1];
            x[i] = mesher.Location(iter, 0);
            row[iter.Index] = j;
            column[iter.Index] = i;
        }

        // FdmBatesOp can only deal with Dirichlet boundary conditions, checked where QuantLib's integrand checks
        foreach (BoundaryCondition<FdmLinearOp> bc in bcSet)
        {
            if (bc is not FdmDirichletBoundary)
            {
                throw new NanoQLException("FdmBatesOp can only deal with Dirichlet boundary conditions.");
            }
        }
    }

    /// <inheritdoc/>
    public override int Size => hestonOp.Size;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// <paramref name="t2"/> is before <paramref name="t1"/>, or a curve can't be read.
    /// </exception>
    /// <exception cref="NanoQLException">A curve or the quanto helper's volatility can't be read.</exception>
    public override void SetTime(double t1, double t2) => hestonOp.SetTime(t1, t2);

    /// <inheritdoc/>
    public override void Apply(ReadOnlySpan<double> r, Span<double> result)
    {
        hestonOp.Apply(r, result);
        AddIntegro(r, result);
    }

    /// <inheritdoc/>
    public override void ApplyMixed(ReadOnlySpan<double> r, Span<double> result)
    {
        hestonOp.ApplyMixed(r, result);
        AddIntegro(r, result);
    }

    /// <inheritdoc/>
    public override void ApplyDirection(int direction, ReadOnlySpan<double> r, Span<double> result) =>
        hestonOp.ApplyDirection(direction, r, result);

    /// <inheritdoc/>
    public override void SolveSplitting(int direction, ReadOnlySpan<double> r, double s, Span<double> result) =>
        hestonOp.SolveSplitting(direction, r, s, result);

    /// <inheritdoc/>
    public override void Preconditioner(ReadOnlySpan<double> r, double s, Span<double> result) =>
        hestonOp.Preconditioner(r, s, result);

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always: QuantLib's operator has no matrix decomposition ("not
    /// implemented").</exception>
    public override ImmutableArray<SparseMatrix> ToMatrixDecomp() => throw new NanoQLException("not implemented");

    // result += lambda*(integral - r), QuantLib's integro(r) added to the Heston part
    private void AddIntegro(ReadOnlySpan<double> r, Span<double> result)
    {
        // the rows of r by variance, f[j][i], and each row's linear interpolation's slopes
        for (int k = 0; k < r.Length; ++k)
        {
            f[(row[k] * dim0) + column[k]] = r[k];
        }

        for (int j = 0; j < dim1; ++j)
        {
            int offset = j * dim0;
            for (int i = 1; i < dim0; ++i)
            {
                double dx = x[i] - x[i - 1];
                s[offset + i - 1] = (f[offset + i] - f[offset + i - 1]) / dx;
            }
        }

        for (int k = 0; k < r.Length; ++k)
        {
            integral[k] = MathConstants.M_1_SQRTPI * GaussHermite(row[k] * dim0, x[column[k]]);
        }

        for (int k = 0; k < r.Length; ++k)
        {
            result[k] += lambda * (integral[k] - r[k]);
        }
    }

    // the Gauss-Hermite sum, from the last node to the first, of QuantLib's IntegroIntegrand on a row
    private double GaussHermite(int offset, double xi)
    {
        double sum = 0.0;
        for (int k = nodes.Length - 1; k >= 0; --k)
        {
            double y = nodes[k];
            double xj = xi + (MathConstants.M_SQRT2 * delta * y) + nu;
            double valueOfDerivative = Interpolate(offset, xj);

            foreach (BoundaryCondition<FdmLinearOp> bc in bcSet)
            {
                valueOfDerivative = ((FdmDirichletBoundary)bc).ApplyAfterApplying(xj, valueOfDerivative);
            }

            sum += weights[k] * (Math.Exp(-y * y) * valueOfDerivative);
        }

        return sum;
    }

    // LinearInterpolation's value with extrapolation: the located interval's y + (x - x_i)·s_i
    private double Interpolate(int offset, double xv)
    {
        int i;
        if (xv < x[0])
        {
            i = 0;
        }
        else if (xv > x[^1])
        {
            i = x.Length - 2;
        }
        else
        {
            i = Interpolation.UpperBound(x.AsSpan(0, x.Length - 1), xv) - 1;
        }

        return f[offset + i] + ((xv - x[i]) * s[offset + i]);
    }
}
