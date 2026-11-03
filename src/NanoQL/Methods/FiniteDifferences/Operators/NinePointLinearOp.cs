// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/ninepointlinearop.hpp ql/methods/finitedifferences/operators/ninepointlinearop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// A nine-point operator in two directions of a multi-dimensional layout: each point's row reads the point and its
/// eight neighbours in the plane of the two directions, reflected at the grid's edges.
/// </summary>
/// <remarks>
/// <para>
/// A working type: mutable, owned by one computation and not thread-safe.
/// <see cref="Apply(ReadOnlySpan{double}, Span{double})"/> writes into the caller's span, which mustn't overlap the
/// input; <see cref="Mult"/> returns a new operator, as QuantLib's does, since it runs while an operator is built.
/// </para>
/// <para>
/// An extension point, as in QuantLib: the mixed second-derivative operator derives from it and fills the protected
/// coefficients in its constructor. A bare operator's coefficients are zero, where QuantLib leaves them uninitialized.
/// QuantLib's <c>swap</c> and move members are C++ value semantics: <see cref="Clone"/> and references replace them.
/// </para>
/// </remarks>
[WorkingType]
public class NinePointLinearOp : FdmLinearOp
{
    /// <summary>The first direction.</summary>
    protected readonly int d0;

    /// <summary>The second direction.</summary>
    protected readonly int d1;

    /// <summary>The neighbours' indices: <c>iXY</c> is the point X − 1 steps along the first direction and Y − 1 along the second.</summary>
    protected readonly int[] i00, i10, i20, i01, i21, i02, i12, i22;

    /// <summary>The coefficients, one per point for each neighbour, <c>a11</c> the point's own.</summary>
    protected readonly double[] a00, a10, a20, a01, a11, a21, a02, a12, a22;

    /// <summary>The mesher.</summary>
    protected readonly FdmMesher mesher;

    // Whether a point's neighbour lies outside the grid, as on a direction of one point, which QuantLib's
    // reflection sends past it: the operator stays built, as QuantLib's is, and refuses what would read there.
    private readonly bool neighbourOutside;

    /// <summary>Initializes a new instance of the <see cref="NinePointLinearOp"/> class, its coefficients zero.</summary>
    /// <param name="d0">The first direction.</param>
    /// <param name="d1">The second direction.</param>
    /// <param name="mesher">The mesher.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mesher"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The directions are equal, or one of them isn't the layout's: QuantLib's "inconsistent derivative directions".
    /// </exception>
    public NinePointLinearOp(int d0, int d1, FdmMesher mesher)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        FdmLinearOpLayout layout = mesher.Layout;
        int n = layout.Size;
        this.d0 = d0;
        this.d1 = d1;
        this.mesher = mesher;
        i00 = new int[n];
        i10 = new int[n];
        i20 = new int[n];
        i01 = new int[n];
        i21 = new int[n];
        i02 = new int[n];
        i12 = new int[n];
        i22 = new int[n];
        a00 = new double[n];
        a10 = new double[n];
        a20 = new double[n];
        a01 = new double[n];
        a11 = new double[n];
        a21 = new double[n];
        a02 = new double[n];
        a12 = new double[n];
        a22 = new double[n];

        // QuantLib's Size directions can't be negative
        if (!(d0 != d1 && d0 >= 0 && d1 >= 0 && d0 < layout.Dim.Length && d1 < layout.Dim.Length))
        {
            throw new ArgumentException("inconsistent derivative directions", nameof(d1));
        }

        FdmLinearOpIterator iter = layout.Begin();
        for (int k = 0; k < n; ++k, iter.Increment())
        {
            int i = iter.Index;

            i10[i] = layout.Neighbourhood(iter, d1, -1);
            i01[i] = layout.Neighbourhood(iter, d0, -1);
            i21[i] = layout.Neighbourhood(iter, d0, 1);
            i12[i] = layout.Neighbourhood(iter, d1, 1);
            i00[i] = layout.Neighbourhood(iter, d0, -1, d1, -1);
            i20[i] = layout.Neighbourhood(iter, d0, 1, d1, -1);
            i02[i] = layout.Neighbourhood(iter, d0, -1, d1, 1);
            i22[i] = layout.Neighbourhood(iter, d0, 1, d1, 1);
        }

        neighbourOutside = Outside(i00) || Outside(i10) || Outside(i20) || Outside(i01) || Outside(i21)
            || Outside(i02) || Outside(i12) || Outside(i22);
    }

    /// <summary>Initializes a new instance of the <see cref="NinePointLinearOp"/> class as a copy of another.</summary>
    /// <param name="m">The operator copied, its coefficients and indices copied, its mesher shared.</param>
    /// <exception cref="ArgumentNullException"><paramref name="m"/> is <see langword="null"/>.</exception>
    public NinePointLinearOp(NinePointLinearOp m)
    {
        ArgumentNullException.ThrowIfNull(m);
        d0 = m.d0;
        d1 = m.d1;
        mesher = m.mesher;
        i00 = (int[])m.i00.Clone();
        i10 = (int[])m.i10.Clone();
        i20 = (int[])m.i20.Clone();
        i01 = (int[])m.i01.Clone();
        i21 = (int[])m.i21.Clone();
        i02 = (int[])m.i02.Clone();
        i12 = (int[])m.i12.Clone();
        i22 = (int[])m.i22.Clone();
        a00 = (double[])m.a00.Clone();
        a10 = (double[])m.a10.Clone();
        a20 = (double[])m.a20.Clone();
        a01 = (double[])m.a01.Clone();
        a11 = (double[])m.a11.Clone();
        a21 = (double[])m.a21.Clone();
        a02 = (double[])m.a02.Clone();
        a12 = (double[])m.a12.Clone();
        a22 = (double[])m.a22.Clone();
        neighbourOutside = m.neighbourOutside;
    }

    // The operator's size: the layout's number of points.
    private int N => a11.Length;

    // Whether an index of a point's neighbour lies outside the grid.
    private static bool Outside(int[] index)
    {
        foreach (int k in index)
        {
            if (k < 0 || k >= index.Length)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A copy of the operator: QuantLib's copy constructor.</summary>
    /// <returns>The copy.</returns>
    public NinePointLinearOp Clone() => new(this);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="r"/>'s length isn't the layout's size.</exception>
    public override void Apply(ReadOnlySpan<double> r, Span<double> result)
    {
        if (!(r.Length == N))
        {
            throw new ArgumentException($"inconsistent length of r {r.Length} vs {N}", nameof(r));
        }

        if (neighbourOutside)
        {
            throw new ArgumentException(
                "a nine-point apply needs at least two points in each of its directions, not 1", nameof(r));
        }

        for (int i = 0; i < N; ++i)
        {
            result[i] = (a00[i] * r[i00[i]])
                + (a01[i] * r[i01[i]])
                + (a02[i] * r[i02[i]])
                + (a10[i] * r[i10[i]])
                + (a11[i] * r[i])
                + (a12[i] * r[i12[i]])
                + (a20[i] * r[i20[i]])
                + (a21[i] * r[i21[i]])
                + (a22[i] * r[i22[i]]);
        }
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">
    /// A direction of the operator has one point, whose neighbours QuantLib writes past the matrix.
    /// </exception>
    public override SparseMatrix ToMatrix()
    {
        if (neighbourOutside)
        {
            throw new InvalidOperationException(
                "a nine-point matrix needs at least two points in each of its directions, not 1");
        }

        int n = N;

        var retVal = new SparseMatrix(n, n, 9 * n);
        for (int i = 0; i < n; ++i)
        {
            retVal[i, i00[i]] += a00[i];
            retVal[i, i01[i]] += a01[i];
            retVal[i, i02[i]] += a02[i];
            retVal[i, i10[i]] += a10[i];
            retVal[i, i] += a11[i];
            retVal[i, i12[i]] += a12[i];
            retVal[i, i20[i]] += a20[i];
            retVal[i, i21[i]] += a21[i];
            retVal[i, i22[i]] += a22[i];
        }

        return retVal;
    }

    /// <summary>The operator with each row scaled: diag(u)·A.</summary>
    /// <param name="u">The rows' scales, one per point.</param>
    /// <returns>A new operator.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="u"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="u"/>'s size isn't the layout's, where QuantLib reads a shorter one past its end.
    /// </exception>
    public NinePointLinearOp Mult(Vector u)
    {
        ArgumentNullException.ThrowIfNull(u);

        // QuantLib reads u[i] for every point, unchecked
        if (!(u.Size == N))
        {
            throw new ArgumentException("inconsistent size of rhs", nameof(u));
        }

        var retVal = new NinePointLinearOp(d0, d1, mesher);

        for (int i = 0; i < N; ++i)
        {
            double s = u[i];
            retVal.a11[i] = a11[i] * s;
            retVal.a00[i] = a00[i] * s;
            retVal.a01[i] = a01[i] * s;
            retVal.a02[i] = a02[i] * s;
            retVal.a10[i] = a10[i] * s;
            retVal.a20[i] = a20[i] * s;
            retVal.a21[i] = a21[i] * s;
            retVal.a12[i] = a12[i] * s;
            retVal.a22[i] = a22[i] * s;
        }

        return retVal;
    }

    /// <summary>
    /// Sets this operator's coefficients, in place, to another's scaled by one value: QuantLib's
    /// <c>NinePointLinearOp op(m.mult(Array(size, s))); swap(op)</c>, by the same arithmetic, so that a time step
    /// allocates nothing.
    /// </summary>
    /// <param name="m">The operator scaled, on this one's directions and mesher.</param>
    /// <param name="s">The scale.</param>
    internal void SetMult(NinePointLinearOp m, double s)
    {
        for (int i = 0; i < N; ++i)
        {
            a11[i] = m.a11[i] * s;
            a00[i] = m.a00[i] * s;
            a01[i] = m.a01[i] * s;
            a02[i] = m.a02[i] * s;
            a10[i] = m.a10[i] * s;
            a20[i] = m.a20[i] * s;
            a21[i] = m.a21[i] * s;
            a12[i] = m.a12[i] * s;
            a22[i] = m.a22[i] * s;
        }
    }

    /// <summary>
    /// Sets this operator's coefficients, in place, to another's with each row scaled: QuantLib's
    /// <c>op = m.mult(u)</c>, by the same arithmetic, so that a time step allocates nothing.
    /// </summary>
    /// <param name="m">The operator scaled, on this one's directions and mesher.</param>
    /// <param name="u">The rows' scales, one per point.</param>
    internal void SetMult(NinePointLinearOp m, ReadOnlySpan<double> u)
    {
        for (int i = 0; i < N; ++i)
        {
            double s = u[i];
            a11[i] = m.a11[i] * s;
            a00[i] = m.a00[i] * s;
            a01[i] = m.a01[i] * s;
            a02[i] = m.a02[i] * s;
            a10[i] = m.a10[i] * s;
            a20[i] = m.a20[i] * s;
            a21[i] = m.a21[i] * s;
            a12[i] = m.a12[i] * s;
            a22[i] = m.a22[i] * s;
        }
    }
}
