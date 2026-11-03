// SPDX-FileCopyrightText: Copyright (C) 2018 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmsabrop.hpp ql/methods/finitedifferences/operators/fdmsabrop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The backward operator of the SABR model dF = α·F^β·dW₁, dα = ν·α·dW₂, d⟨W₁, W₂⟩ = ρ·dt, on a mesher of the forward
/// in direction 0 and the log volatility x = ln α in direction 1: the forward's diffusion ½e^{2x}F^{2β}, the log
/// volatility's drift −½ν² and diffusion ½ν², the correlation term ρνe^xF^β, each direction discounting at half the
/// curve's forward rate.
/// </summary>
/// <remarks>
/// A working type: the parts are built at construction; <see cref="SetTime"/> rewrites the two directions' operators in
/// place through buffers it holds, and the apply, splitting solves and preconditioner write into the caller's spans
/// through buffers it holds, so a step allocates nothing. The forward rate is the curve's over its own times, read
/// through <see cref="YieldTermStructure"/>'s internal <c>ContinuousForwardRate</c>, its bits <c>ForwardRate</c>'s.
/// </remarks>
[WorkingType]
public sealed class FdmSabrOp : FdmLinearOpComposite
{
    private readonly YieldTermStructure rTS;
    private readonly TripleBandLinearOp dffMap;
    private readonly TripleBandLinearOp dxMap;
    private readonly TripleBandLinearOp dxxMap;
    private readonly NinePointLinearOp correlationMap;
    private readonly TripleBandLinearOp mapF;
    private readonly TripleBandLinearOp mapA;

    // the step's Array(), Array(1, 1.0) and Array(1, -0.5*r), held
    private readonly Vector none = new(0);
    private readonly Vector one = new(1, 1.0);
    private readonly Vector minusHalfR = new(1);

    // apply's two direction terms and the preconditioner's first solve, held
    private readonly double[] tempF;
    private readonly double[] tempA;

    /// <summary>Initializes a new instance of the <see cref="FdmSabrOp"/> class.</summary>
    /// <param name="mesher">The mesher: the forward in direction 0, the log volatility in direction 1.</param>
    /// <param name="rTS">The discount curve.</param>
    /// <param name="f0">The forward's value today, which QuantLib's operator doesn't read.</param>
    /// <param name="alpha">The initial volatility α, which QuantLib's operator doesn't read.</param>
    /// <param name="beta">The elasticity β.</param>
    /// <param name="nu">The volatility of volatility ν.</param>
    /// <param name="rho">The correlation ρ.</param>
    /// <exception cref="ArgumentNullException">The mesher or the curve is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The mesher has fewer than two directions.</exception>
    public FdmSabrOp(FdmMesher mesher, YieldTermStructure rTS, double f0, double alpha, double beta, double nu, double rho)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(rTS);
        _ = f0;
        _ = alpha;
        this.rTS = rTS;
        int n = mesher.Layout.Size;
        Vector f = mesher.Locations(0), x = mesher.Locations(1);
        dffMap = new SecondDerivativeOp(0, mesher)
            .Mult(0.5 * ArrayFunctions.Exp(2.0 * x) * ArrayFunctions.Pow(f, 2.0 * beta));
        dxMap = new FirstDerivativeOp(1, mesher).Mult(new Vector(n, -0.5 * nu * nu));
        dxxMap = new SecondDerivativeOp(1, mesher).Mult(new Vector(n, 0.5 * nu * nu));
        correlationMap = new SecondOrderMixedDerivativeOp(0, 1, mesher)
            .Mult(rho * nu * ArrayFunctions.Exp(x) * ArrayFunctions.Pow(f, beta));
        mapF = new TripleBandLinearOp(0, mesher);
        mapA = new TripleBandLinearOp(1, mesher);
        tempF = new double[n];
        tempA = new double[n];
    }

    /// <inheritdoc/>
    public override int Size => 2;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// <paramref name="t2"/> is before <paramref name="t1"/>, or the curve can't be read between them.
    /// </exception>
    public override void SetTime(double t1, double t2)
    {
        double r = rTS.ContinuousForwardRate(t1, t2);
        minusHalfR[0] = -0.5 * r;

        mapF.Axpyb(none, dffMap, dffMap, minusHalfR);
        mapA.Axpyb(one, dxMap, dxxMap, minusHalfR);
    }

    /// <inheritdoc/>
    public override void Apply(ReadOnlySpan<double> r, Span<double> result)
    {
        mapF.Apply(r, tempF);
        mapA.Apply(r, tempA);
        correlationMap.Apply(r, result);

        // mapF_.apply(u) + mapA_.apply(u) + correlationMap_.apply(u)
        for (int i = 0; i < r.Length; ++i)
        {
            result[i] = tempF[i] + tempA[i] + result[i];
        }
    }

    /// <inheritdoc/>
    public override void ApplyMixed(ReadOnlySpan<double> r, Span<double> result) => correlationMap.Apply(r, result);

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException">The direction is above 1, with QuantLib's message.</exception>
    public override void ApplyDirection(int direction, ReadOnlySpan<double> r, Span<double> result)
    {
        if (direction == 0)
        {
            mapF.Apply(r, result);
        }
        else if (direction == 1)
        {
            mapA.Apply(r, result);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction too large");
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException">The direction is above 1, with QuantLib's message.</exception>
    public override void SolveSplitting(int direction, ReadOnlySpan<double> r, double s, Span<double> result)
    {
        if (direction == 0)
        {
            mapF.SolveSplitting(r, s, 1.0, result);
        }
        else if (direction == 1)
        {
            mapA.SolveSplitting(r, s, 1.0, result);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction too large");
        }
    }

    /// <inheritdoc/>
    public override void Preconditioner(ReadOnlySpan<double> r, double s, Span<double> result)
    {
        // solve_splitting(1, solve_splitting(0, r, dt), dt)
        SolveSplitting(0, r, s, tempF);
        SolveSplitting(1, tempF, s, result);
    }

    /// <inheritdoc/>
    public override ImmutableArray<SparseMatrix> ToMatrixDecomp() =>
        [mapA.ToMatrix(), mapF.ToMatrix(), correlationMap.ToMatrix()];
}
