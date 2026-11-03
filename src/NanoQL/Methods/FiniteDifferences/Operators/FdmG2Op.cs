// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmg2op.hpp ql/methods/finitedifferences/operators/fdmg2op.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The G2 model's backward operator on two directions of a mesher: each factor's drift −ax and diffusion ½σ²,
/// discounting at half the short rate x + y + φ in each direction, and the factors' correlation ρση through the mixed
/// derivative.
/// </summary>
/// <remarks>
/// <para>
/// A working type: mutable, owned by one computation and not thread-safe. Its directions' operators and the mixed one
/// are built at construction; <see cref="SetTime"/> rewrites the directions' operators in place with the step's φ, at
/// the step's two ends' mean, and allocates nothing.
/// </para>
/// <para>
/// The model's dynamics are built once, at construction, where QuantLib's <c>setTime</c> calls
/// <c>model->dynamics()</c> each step: the model is immutable, so they are the same each time.
/// </para>
/// </remarks>
[WorkingType]
public sealed class FdmG2Op : FdmLinearOpComposite
{
    private readonly int direction1;
    private readonly int direction2;
    private readonly double[] x;
    private readonly double[] y;
    private readonly TripleBandLinearOp dxMap;
    private readonly TripleBandLinearOp dyMap;
    private readonly NinePointLinearOp corrMap;
    private readonly TripleBandLinearOp mapX;
    private readonly TripleBandLinearOp mapY;
    private readonly TwoFactorModelShortRateDynamics dynamics;

    // the step's diagonal and apply's two terms, held
    private readonly double[] hr;
    private readonly double[] tempX;
    private readonly double[] tempY;

    /// <summary>Initializes a new instance of the <see cref="FdmG2Op"/> class.</summary>
    /// <param name="mesher">The mesher.</param>
    /// <param name="model">The G2 model.</param>
    /// <param name="direction1">The first factor's direction.</param>
    /// <param name="direction2">The second factor's direction.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A direction isn't one of the layout's.</exception>
    /// <exception cref="ArgumentException">The directions are equal.</exception>
    public FdmG2Op(FdmMesher mesher, G2 model, int direction1, int direction2)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(model);
        this.direction1 = direction1;
        this.direction2 = direction2;
        Vector xs = mesher.Locations(direction1);
        Vector ys = mesher.Locations(direction2);
        x = [.. xs.AsSpan()];
        y = [.. ys.AsSpan()];
        int n = mesher.Layout.Size;

        double a = model.A(), b = model.B(), sigma = model.Sigma, eta = model.Eta, rho = model.Rho;
        dxMap = new FirstDerivativeOp(direction1, mesher).Mult(Scaled(x, -1.0, a))
            .Add(new SecondDerivativeOp(direction1, mesher).Mult(Filled(n, 0.5 * sigma * sigma * 1.0)));
        dyMap = new FirstDerivativeOp(direction2, mesher).Mult(Scaled(y, -1.0, b))
            .Add(new SecondDerivativeOp(direction2, mesher).Mult(Filled(n, 0.5 * eta * eta * 1.0)));
        corrMap = new SecondOrderMixedDerivativeOp(direction1, direction2, mesher).Mult(Filled(n, rho * sigma * eta));
        mapX = new TripleBandLinearOp(direction1, mesher);
        mapY = new TripleBandLinearOp(direction2, mesher);
        dynamics = model.Dynamics();

        hr = new double[n];
        tempX = new double[n];
        tempY = new double[n];
    }

    /// <inheritdoc/>
    public override int Size => 2;

    /// <inheritdoc/>
    public override void SetTime(double t1, double t2)
    {
        double phi = 0.5 * (dynamics.ShortRate(t1, 0.0, 0.0) + dynamics.ShortRate(t2, 0.0, 0.0));

        // -0.5*(x_ + y_ + phi)
        for (int i = 0; i < hr.Length; ++i)
        {
            hr[i] = -0.5 * (x[i] + y[i] + phi);
        }

        mapX.SetSumWithDiagonal(dxMap, hr);
        mapY.SetSumWithDiagonal(dyMap, hr);
    }

    /// <inheritdoc/>
    public override void Apply(ReadOnlySpan<double> r, Span<double> result)
    {
        mapX.Apply(r, tempX);
        mapY.Apply(r, tempY);
        corrMap.Apply(r, result);

        // mapX_.apply(r) + mapY_.apply(r) + apply_mixed(r)
        for (int i = 0; i < r.Length; ++i)
        {
            result[i] = tempX[i] + tempY[i] + result[i];
        }
    }

    /// <inheritdoc/>
    public override void ApplyMixed(ReadOnlySpan<double> r, Span<double> result) => corrMap.Apply(r, result);

    /// <inheritdoc/>
    public override void ApplyDirection(int direction, ReadOnlySpan<double> r, Span<double> result)
    {
        if (direction == direction1)
        {
            mapX.Apply(r, result);
        }
        else if (direction == direction2)
        {
            mapY.Apply(r, result);
        }
        else
        {
            result[..r.Length].Clear();
        }
    }

    /// <inheritdoc/>
    /// <remarks>Another direction's solution is zero, as QuantLib's.</remarks>
    public override void SolveSplitting(int direction, ReadOnlySpan<double> r, double s, Span<double> result)
    {
        if (direction == direction1)
        {
            mapX.SolveSplitting(r, s, 1.0, result);
        }
        else if (direction == direction2)
        {
            mapY.SolveSplitting(r, s, 1.0, result);
        }
        else
        {
            result[..r.Length].Clear();
        }
    }

    /// <inheritdoc/>
    public override void Preconditioner(ReadOnlySpan<double> r, double s, Span<double> result) =>
        SolveSplitting(direction1, r, s, result);

    /// <inheritdoc/>
    public override ImmutableArray<SparseMatrix> ToMatrixDecomp() => [mapX.ToMatrix(), mapY.ToMatrix(), corrMap.ToMatrix()];

    // -x_*a: each location negated, then scaled
    internal static Vector Scaled(double[] locations, double sign, double scale)
    {
        var v = new Vector(locations.Length);
        for (int i = 0; i < locations.Length; ++i)
        {
            v[i] = sign * locations[i] * scale;
        }

        return v;
    }

    // Array(size, value)
    internal static Vector Filled(int size, double value)
    {
        var v = new Vector(size);
        for (int i = 0; i < size; ++i)
        {
            v[i] = value;
        }

        return v;
    }
}
