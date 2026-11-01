// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdmzabrop.hpp ql/experimental/finitedifferences/fdmzabrop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The ZABR pricing operator on a mesher whose first direction is the forward F and second the volatility v:
/// ½v²F<sup>2β</sup>·∂²/∂F² + ½ν²v<sup>2γ</sup>·∂²/∂v² + νρ|v|<sup>γ+1</sup>F<sup>β</sup>·∂²/∂F∂v; γ = 1 is SABR.
/// </summary>
/// <remarks>
/// A working type (the S-eq plan's SEQR1): its bands are built at construction and never change, and the sum
/// <see cref="Apply"/> forms is written through buffers it holds, so a step allocates nothing (the S-g1d plan's G1D11);
/// not thread-safe. Experimental, as in QuantLib.
/// </remarks>
[Experimental("NQL0001")]
[WorkingType]
public sealed class FdmZabrOp : FdmLinearOpComposite
{
    private readonly NinePointLinearOp dxyMap;
    private readonly FdmZabrUnderlyingPart dxMap;
    private readonly FdmZabrVolatilityPart dyMap;

    // apply's two direction terms, held
    private readonly double[] tempX;
    private readonly double[] tempY;

    /// <summary>Initializes a new instance of the <see cref="FdmZabrOp"/> class.</summary>
    /// <param name="mesher">The mesher: the forward, then the volatility.</param>
    /// <param name="beta">β.</param>
    /// <param name="nu">ν.</param>
    /// <param name="rho">ρ.</param>
    /// <param name="gamma">γ; 1 recovers the classic SABR model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mesher"/> is null.</exception>
    public FdmZabrOp(FdmMesher mesher, double beta, double nu, double rho, double gamma = 1.0)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        Vector volatilityValues = mesher.Locations(1);
        Vector forwardValues = mesher.Locations(0);
        dxyMap = new SecondOrderMixedDerivativeOp(0, 1, mesher)
            .Mult(nu * rho * Pow(Abs(volatilityValues), gamma + 1.0) * Pow(forwardValues, beta));
        dxMap = new FdmZabrUnderlyingPart(mesher, beta, nu, rho, gamma);
        dyMap = new FdmZabrVolatilityPart(mesher, beta, nu, rho, gamma);
        tempX = new double[mesher.Layout.Size];
        tempY = new double[mesher.Layout.Size];
    }

    /// <inheritdoc/>
    public override int Size => 2;

    /// <inheritdoc/>
    public override void SetTime(double t1, double t2)
    {
        dxMap.SetTime(t1, t2);
        dyMap.SetTime(t1, t2);
    }

    /// <inheritdoc/>
    public override void Apply(ReadOnlySpan<double> r, Span<double> result)
    {
        dyMap.Map.Apply(r, tempY);
        dxMap.Map.Apply(r, tempX);
        dxyMap.Apply(r, result);

        // dyMap_.getMap().apply(u) + dxMap_.getMap().apply(u) + dxyMap_.apply(u)
        for (int i = 0; i < r.Length; ++i)
        {
            result[i] = tempY[i] + tempX[i] + result[i];
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> is neither 0 nor 1.</exception>
    public override void ApplyDirection(int direction, ReadOnlySpan<double> r, Span<double> result)
    {
        if (direction == 0)
        {
            dxMap.Map.Apply(r, result);
        }
        else if (direction == 1)
        {
            dyMap.Map.Apply(r, result);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction too large");
        }
    }

    /// <inheritdoc/>
    public override void ApplyMixed(ReadOnlySpan<double> r, Span<double> result) => dxyMap.Apply(r, result);

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> is neither 0 nor 1.</exception>
    public override void SolveSplitting(int direction, ReadOnlySpan<double> r, double s, Span<double> result)
    {
        if (direction == 0)
        {
            dxMap.Map.SolveSplitting(r, s, 1.0, result);
        }
        else if (direction == 1)
        {
            dyMap.Map.SolveSplitting(r, s, 1.0, result);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction too large");
        }
    }

    /// <inheritdoc/>
    public override void Preconditioner(ReadOnlySpan<double> r, double s, Span<double> result) => SolveSplitting(0, r, s, result);

    /// <inheritdoc/>
    public override ImmutableArray<SparseMatrix> ToMatrixDecomp() => [dxMap.Map.ToMatrix(), dyMap.Map.ToMatrix(), dxyMap.ToMatrix()];

    // QuantLib's Pow(Array, Real), elementwise.
    internal static Vector Pow(Vector v, double alpha)
    {
        var result = new Vector(v.Size);
        for (int i = 0; i < v.Size; ++i)
        {
            result[i] = Math.Pow(v[i], alpha);
        }

        return result;
    }

    // QuantLib's Abs(Array), elementwise.
    private static Vector Abs(Vector v)
    {
        var result = new Vector(v.Size);
        for (int i = 0; i < v.Size; ++i)
        {
            result[i] = Math.Abs(v[i]);
        }

        return result;
    }
}
