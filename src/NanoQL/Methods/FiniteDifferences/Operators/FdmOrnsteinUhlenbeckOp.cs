// SPDX-FileCopyrightText: Copyright (C) 2016 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmornsteinuhlenbeckop.hpp ql/methods/finitedifferences/operators/fdmornsteinuhlenbeckop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The backward operator of an Ornstein-Uhlenbeck process dx = a(μ − x)dt + σ dW in one direction of a mesher: the
/// drift a(μ − x), the diffusion ½σ², discounting at the curve's forward rate.
/// </summary>
/// <remarks>
/// A working type: <see cref="SetTime"/> rewrites the step's operator in place through a buffer it holds, and the apply
/// and splitting solves write into the caller's spans, so a step allocates nothing. The forward rate is the curve's
/// over its own times, read through <see cref="YieldTermStructure"/>'s internal <c>ContinuousForwardRate</c>, its
/// bits <c>ForwardRate</c>'s. A splitting solve in another direction returns its input, as QuantLib's.
/// </remarks>
[WorkingType]
public sealed class FdmOrnsteinUhlenbeckOp : FdmLinearOpComposite
{
    private readonly FdmMesher mesher;
    private readonly YieldTermStructure rTS;
    private readonly int direction;
    private readonly TripleBandLinearOp m;
    private readonly TripleBandLinearOp mapX;

    // the step's diagonal, -r, held
    private readonly double[] minusR = new double[1];

    /// <summary>Initializes a new instance of the <see cref="FdmOrnsteinUhlenbeckOp"/> class.</summary>
    /// <param name="mesher">The mesher.</param>
    /// <param name="process">The Ornstein-Uhlenbeck process, its drift read at time 0.</param>
    /// <param name="rTS">The discounting curve.</param>
    /// <param name="direction">The process's direction.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> isn't one of the mesher's.</exception>
    public FdmOrnsteinUhlenbeckOp(
        FdmMesher mesher, OrnsteinUhlenbeckProcess process, YieldTermStructure rTS, int direction = 0)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(rTS);
        this.mesher = mesher;
        this.rTS = rTS;
        this.direction = direction;
        m = new TripleBandLinearOp(direction, mesher);
        mapX = new TripleBandLinearOp(direction, mesher);

        int n = mesher.Layout.Size;
        var drift = new Vector(n);
        Vector x = mesher.Locations(direction);

        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int k = 0; k < n; ++k, iter.Increment())
        {
            int i = iter.Index;
            drift[i] = process.Drift(0.0, x[i]);
        }

        double volatility = process.Volatility;
        m.Axpyb(
            drift,
            new FirstDerivativeOp(direction, mesher),
            new SecondDerivativeOp(direction, mesher).Mult(0.5 * (volatility * volatility) * new Vector(n, 1.0)),
            new Vector(0));
    }

    /// <inheritdoc/>
    public override int Size => mesher.Layout.Dim.Length;

    /// <inheritdoc/>
    public override void SetTime(double t1, double t2)
    {
        double r = rTS.ContinuousForwardRate(t1, t2);
        minusR[0] = -r;
        mapX.SetSumWithDiagonal(m, minusR);
    }

    /// <inheritdoc/>
    public override void Apply(ReadOnlySpan<double> r, Span<double> result) => mapX.Apply(r, result);

    /// <inheritdoc/>
    public override void ApplyMixed(ReadOnlySpan<double> r, Span<double> result) => result[..r.Length].Clear();

    /// <inheritdoc/>
    public override void ApplyDirection(int direction, ReadOnlySpan<double> r, Span<double> result)
    {
        if (direction == this.direction)
        {
            mapX.Apply(r, result);
        }
        else
        {
            result[..r.Length].Clear();
        }
    }

    /// <inheritdoc/>
    public override void SolveSplitting(int direction, ReadOnlySpan<double> r, double s, Span<double> result)
    {
        if (direction == this.direction)
        {
            mapX.SolveSplitting(r, s, 1.0, result);
        }
        else
        {
            r.CopyTo(result);
        }
    }

    /// <inheritdoc/>
    public override void Preconditioner(ReadOnlySpan<double> r, double s, Span<double> result) =>
        SolveSplitting(direction, r, s, result);

    /// <inheritdoc/>
    public override ImmutableArray<SparseMatrix> ToMatrixDecomp() => [mapX.ToMatrix()];
}
