// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmlinearop.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>A linear operator of a multi-dimensional finite-difference problem.</summary>
/// <remarks>
/// <para>
/// A working type (the S-eq plan's SEQR1): mutable, owned by one computation and not thread-safe. An extension point,
/// as in QuantLib: later units derive their operators from it.
/// </para>
/// <para>
/// <see cref="Apply(ReadOnlySpan{double}, Span{double})"/> writes into the caller's span, where QuantLib returns a
/// new array, so that a time step allocates nothing; the result mustn't overlap the input, as PETSc's
/// <c>MatMult</c> requires. <see cref="Apply(Vector)"/> is QuantLib's value-returning form.
/// </para>
/// </remarks>
[WorkingType]
public abstract class FdmLinearOp
{
    /// <summary>Applies the operator to <paramref name="r"/>, writing the result into the caller's span.</summary>
    /// <param name="r">The values, one per point.</param>
    /// <param name="result">The result, of <paramref name="r"/>'s length; it mustn't overlap
    /// <paramref name="r"/>.</param>
    public abstract void Apply(ReadOnlySpan<double> r, Span<double> result);

    /// <summary>Applies the operator to <paramref name="r"/>: QuantLib's <c>apply</c>.</summary>
    /// <param name="r">The values, one per point.</param>
    /// <returns>A new vector of the result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="r"/> is <see langword="null"/>.</exception>
    public Vector Apply(Vector r)
    {
        ArgumentNullException.ThrowIfNull(r);
        var result = new Vector(r.Size);
        Apply(r.AsSpan(), result.AsSpan());
        return result;
    }

    /// <summary>The operator as a sparse matrix.</summary>
    /// <returns>A new matrix.</returns>
    public abstract SparseMatrix ToMatrix();
}
