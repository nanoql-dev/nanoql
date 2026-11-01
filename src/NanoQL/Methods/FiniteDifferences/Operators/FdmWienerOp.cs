// SPDX-FileCopyrightText: Copyright (C) 2024 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmwienerop.hpp ql/methods/finitedifferences/operators/fdmwienerop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The operator of independent Wiener processes, one per direction with its own variance rate, discounted on a
/// risk-free curve: the n-dimensional Black-Scholes engine's operator in the basket's principal components.
/// </summary>
/// <remarks>
/// <para>
/// A working type (master §2.2's finite-difference row): mutable, owned by one rollback and not thread-safe. Its time
/// step rewrites only its rate, so a step on Douglas's scheme, the engine's default, allocates nothing.
/// </para>
/// <para>
/// A direction past the operator's count is refused, where QuantLib reads its operators' vector past its end
/// (<c>Divergences/FdmWienerOpDirectionTests.cs</c>).
/// </para>
/// </remarks>
[WorkingType]
public sealed class FdmWienerOp : FdmLinearOpComposite
{
    private readonly YieldTermStructure? rTS;
    private readonly TermStructure? clock;
    private readonly TripleBandLinearOp[] ops;
    private readonly double[] partial;
    private double r;

    /// <summary>Initializes a new instance of the <see cref="FdmWienerOp"/> class.</summary>
    /// <param name="mesher">The mesher, one direction per variance rate.</param>
    /// <param name="rTS">The risk-free curve, or <see langword="null"/> for no discounting.</param>
    /// <param name="lambdas">The variance rates, one per direction.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mesher"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The mesher's dimension isn't the number of variance rates.</exception>
    public FdmWienerOp(FdmMesher mesher, YieldTermStructure? rTS, ReadOnlySpan<double> lambdas)
        : this(mesher, rTS, lambdas, rTS)
    {
    }

    // The operator on an engine's clock (the S-exo plan's ER5 (i)): the curve's rate read over the instants the
    // clock's times denote; QuantLib's read where the clock is the curve itself.
    internal FdmWienerOp(FdmMesher mesher, YieldTermStructure? rTS, ReadOnlySpan<double> lambdas, TermStructure? clock)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        this.rTS = rTS;
        this.clock = clock;

        if (!(mesher.Layout.Dim.Length == lambdas.Length))
        {
            throw new ArgumentException("mesher and lambdas need to be of the same dimension", nameof(lambdas));
        }

        int n = mesher.Layout.Size;
        ops = new TripleBandLinearOp[lambdas.Length];
        for (int i = 0; i < lambdas.Length; ++i)
        {
            ops[i] = new SecondDerivativeOp(i, mesher).Mult(new Vector(n, 0.5 * lambdas[i]));
        }

        partial = new double[n];
    }

    /// <summary>Gets the number of directions.</summary>
    public override int Size => ops.Length;

    /// <summary>Sets the step's continuously compounded forward rate on the risk-free curve, if there is one.</summary>
    /// <param name="t1">The step's start.</param>
    /// <param name="t2">The step's end.</param>
    public override void SetTime(double t1, double t2)
    {
        if (rTS is not null)
        {
            r = rTS.ContinuousForwardRateAt(clock!, t1, t2);
        }
    }

    /// <inheritdoc/>
    public override void Apply(ReadOnlySpan<double> r, Span<double> result)
    {
        // QuantLib's Array y(-r_*x); y += op->apply(x) for each op
        double minusR = -this.r;
        for (int i = 0; i < r.Length; ++i)
        {
            result[i] = minusR * r[i];
        }

        Span<double> a = partial.AsSpan(0, r.Length);
        foreach (TripleBandLinearOp op in ops)
        {
            op.Apply(r, a);
            for (int i = 0; i < r.Length; ++i)
            {
                result[i] = result[i] + a[i];
            }
        }
    }

    /// <inheritdoc/>
    public override void ApplyMixed(ReadOnlySpan<double> r, Span<double> result) => result[..r.Length].Clear();

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> isn't one of the
    /// operator's.</exception>
    public override void ApplyDirection(int direction, ReadOnlySpan<double> r, Span<double> result) =>
        Op(direction).Apply(r, result);

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> isn't one of the
    /// operator's.</exception>
    public override void SolveSplitting(int direction, ReadOnlySpan<double> r, double s, Span<double> result) =>
        Op(direction).SolveSplitting(r, s, 1.0, result);

    /// <inheritdoc/>
    public override void Preconditioner(ReadOnlySpan<double> r, double s, Span<double> result) =>
        SolveSplitting(0, r, s, result);

    /// <inheritdoc/>
    public override ImmutableArray<SparseMatrix> ToMatrixDecomp()
    {
        var retVal = ImmutableArray.CreateBuilder<SparseMatrix>(ops.Length);
        foreach (TripleBandLinearOp op in ops)
        {
            retVal.Add(op.ToMatrix());
        }

        return retVal.MoveToImmutable();
    }

    // QuantLib's ops_[direction], unchecked
    private TripleBandLinearOp Op(int direction)
    {
        if (direction < 0 || direction >= ops.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(direction), direction, $"direction {direction} out of the operator's {ops.Length}");
        }

        return ops[direction];
    }
}
