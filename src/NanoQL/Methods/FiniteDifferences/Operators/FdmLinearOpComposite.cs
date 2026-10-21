// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2012 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmlinearopcomposite.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// A finite-difference operator composed of one operator per direction and a mixed part, as the operator-splitting
/// schemes step it: time-dependent, applied whole, by direction or in its mixed part, and solved one direction at a
/// time.
/// </summary>
/// <remarks>
/// <para>
/// A working type (the S-eq plan's SEQR1): <see cref="SetTime"/> rewrites the operator in buffers it holds, where
/// QuantLib builds new bands per call, and the applies and solves write into the caller's span, which mustn't overlap
/// the input. The value-returning forms are QuantLib's. An extension point, as in QuantLib: later units derive their
/// operators from it.
/// </para>
/// </remarks>
public abstract class FdmLinearOpComposite : FdmLinearOp
{
    /// <summary>Gets the number of directions.</summary>
    public abstract int Size { get; }

    /// <summary>Sets the operator for the interval [<paramref name="t1"/>, <paramref name="t2"/>].</summary>
    /// <param name="t1">The start time.</param>
    /// <param name="t2">The end time, not before <paramref name="t1"/>.</param>
    public abstract void SetTime(double t1, double t2);

    /// <summary>Applies the operator's mixed part, writing the result into the caller's span.</summary>
    /// <param name="r">The values, one per point.</param>
    /// <param name="result">The result, of <paramref name="r"/>'s length.</param>
    public abstract void ApplyMixed(ReadOnlySpan<double> r, Span<double> result);

    /// <summary>Applies the operator's part in one direction, writing the result into the caller's span.</summary>
    /// <param name="direction">The direction.</param>
    /// <param name="r">The values, one per point.</param>
    /// <param name="result">The result, of <paramref name="r"/>'s length.</param>
    public abstract void ApplyDirection(int direction, ReadOnlySpan<double> r, Span<double> result);

    /// <summary>
    /// Solves (1 + <paramref name="s"/>·A<sub>direction</sub>) x = <paramref name="r"/> for x, writing it into the
    /// caller's span.
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <param name="r">The right-hand side, one value per point.</param>
    /// <param name="s">The scale of the direction's operator.</param>
    /// <param name="result">The solution, of <paramref name="r"/>'s length.</param>
    public abstract void SolveSplitting(int direction, ReadOnlySpan<double> r, double s, Span<double> result);

    /// <summary>Applies the preconditioner of (1 + <paramref name="s"/>·A), writing the result into the caller's
    /// span.</summary>
    /// <param name="r">The values, one per point.</param>
    /// <param name="s">The scale of the operator.</param>
    /// <param name="result">The result, of <paramref name="r"/>'s length.</param>
    public abstract void Preconditioner(ReadOnlySpan<double> r, double s, Span<double> result);

    /// <summary>Applies the operator's mixed part: QuantLib's <c>apply_mixed</c>.</summary>
    /// <param name="r">The values, one per point.</param>
    /// <returns>A new vector of the result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="r"/> is <see langword="null"/>.</exception>
    public Vector ApplyMixed(Vector r)
    {
        ArgumentNullException.ThrowIfNull(r);
        var result = new Vector(r.Size);
        ApplyMixed(r.AsSpan(), result.AsSpan());
        return result;
    }

    /// <summary>Applies the operator's part in one direction: QuantLib's <c>apply_direction</c>.</summary>
    /// <param name="direction">The direction.</param>
    /// <param name="r">The values, one per point.</param>
    /// <returns>A new vector of the result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="r"/> is <see langword="null"/>.</exception>
    public Vector ApplyDirection(int direction, Vector r)
    {
        ArgumentNullException.ThrowIfNull(r);
        var result = new Vector(r.Size);
        ApplyDirection(direction, r.AsSpan(), result.AsSpan());
        return result;
    }

    /// <summary>Solves one direction's system: QuantLib's <c>solve_splitting</c>.</summary>
    /// <param name="direction">The direction.</param>
    /// <param name="r">The right-hand side, one value per point.</param>
    /// <param name="s">The scale of the direction's operator.</param>
    /// <returns>A new vector of the solution.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="r"/> is <see langword="null"/>.</exception>
    public Vector SolveSplitting(int direction, Vector r, double s)
    {
        ArgumentNullException.ThrowIfNull(r);
        var result = new Vector(r.Size);
        SolveSplitting(direction, r.AsSpan(), s, result.AsSpan());
        return result;
    }

    /// <summary>Applies the preconditioner: QuantLib's <c>preconditioner</c>.</summary>
    /// <param name="r">The values, one per point.</param>
    /// <param name="s">The scale of the operator.</param>
    /// <returns>A new vector of the result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="r"/> is <see langword="null"/>.</exception>
    public Vector Preconditioner(Vector r, double s)
    {
        ArgumentNullException.ThrowIfNull(r);
        var result = new Vector(r.Size);
        Preconditioner(r.AsSpan(), s, result.AsSpan());
        return result;
    }

    /// <summary>The operator as sparse matrices, one per part.</summary>
    /// <returns>The matrices.</returns>
    /// <exception cref="NanoQLException">The operator has no sparse representation.</exception>
    public virtual ImmutableArray<SparseMatrix> ToMatrixDecomp() =>
        throw new NanoQLException(" ublas representation is not implemented");

    /// <summary>The operator as one sparse matrix: the sum of <see cref="ToMatrixDecomp"/>'s parts.</summary>
    /// <returns>A new matrix.</returns>
    /// <exception cref="NanoQLException">The operator has no sparse representation.</exception>
    public override SparseMatrix ToMatrix()
    {
        ImmutableArray<SparseMatrix> dcmp = ToMatrixDecomp();

        // std::accumulate from a copy of the first part
        SparseMatrix result = dcmp[0].Clone();
        for (int i = 1; i < dcmp.Length; ++i)
        {
            result = result + dcmp[i];
        }

        return result;
    }
}
