// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2014 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/triplebandlinearop.hpp ql/methods/finitedifferences/operators/triplebandlinearop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// A general triple-band operator in one direction of a multi-dimensional layout: each point's row reads the point and
/// its two neighbours in that direction, reflected at the grid's edges.
/// </summary>
/// <remarks>
/// <para>
/// A working type: mutable, owned by one computation and not thread-safe.
/// <see cref="Apply(ReadOnlySpan{double}, Span{double})"/> and
/// <see cref="SolveSplitting(ReadOnlySpan{double}, double, double, Span{double})"/> write into the caller's span, which
/// mustn't overlap the input, through a workspace the operator holds; <see cref="Axpyb"/> rewrites the operator in
/// place. The composition methods <see cref="Mult"/>, <see cref="MultR"/> and the two
/// <see cref="Add(TripleBandLinearOp)"/> forms return new operators, as QuantLib's do, since they run while an operator
/// is built.
/// </para>
/// <para>
/// An extension point, as in QuantLib: the derivative operators derive from it and fill the protected bands in their
/// constructors. QuantLib's <c>swap</c> and move members are C++ value semantics: <see cref="Clone"/> and references
/// replace them.
/// </para>
/// </remarks>
[WorkingType]
public class TripleBandLinearOp : FdmLinearOp
{
    /// <summary>The direction the operator acts in.</summary>
    protected readonly int direction;

    /// <summary>Each point's lower neighbour's index.</summary>
    protected readonly int[] i0;

    /// <summary>Each point's upper neighbour's index.</summary>
    protected readonly int[] i2;

    /// <summary>The storage index of each position in the direction-first order the solve walks.</summary>
    protected readonly int[] reverseIndex;

    /// <summary>The lower band, one coefficient per point.</summary>
    protected readonly double[] lower;

    /// <summary>The diagonal, one coefficient per point.</summary>
    protected readonly double[] diag;

    /// <summary>The upper band, one coefficient per point.</summary>
    protected readonly double[] upper;

    /// <summary>The mesher.</summary>
    protected readonly FdmMesher mesher;

    private readonly double[] temp;

    /// <summary>Initializes a new instance of the <see cref="TripleBandLinearOp"/> class, its bands zero.</summary>
    /// <param name="direction">The direction.</param>
    /// <param name="mesher">The mesher.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mesher"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The direction isn't one of the layout's.</exception>
    /// <remarks>The bands are zero, where QuantLib leaves them uninitialized.</remarks>
    public TripleBandLinearOp(int direction, FdmMesher mesher)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        FdmLinearOpLayout layout = mesher.Layout;
        ArgumentOutOfRangeException.ThrowIfNegative(direction);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(direction, layout.Dim.Length);
        int n = layout.Size;
        this.direction = direction;
        this.mesher = mesher;
        i0 = new int[n];
        i2 = new int[n];
        reverseIndex = new int[n];
        lower = new double[n];
        diag = new double[n];
        upper = new double[n];
        temp = new double[n];

        int[] newDim = [.. layout.Dim];
        (newDim[0], newDim[direction]) = (newDim[direction], newDim[0]);
        int[] newSpacing = [.. new FdmLinearOpLayout(newDim).Spacing];
        (newSpacing[0], newSpacing[direction]) = (newSpacing[direction], newSpacing[0]);

        FdmLinearOpIterator iter = layout.Begin();
        for (int k = 0; k < n; ++k, iter.Increment())
        {
            int i = iter.Index;

            i0[i] = layout.Neighbourhood(iter, direction, -1);
            i2[i] = layout.Neighbourhood(iter, direction, 1);

            ReadOnlySpan<int> coordinates = iter.Coordinates;
            int newIndex = 0;
            for (int j = 0; j < coordinates.Length; ++j)
            {
                newIndex += coordinates[j] * newSpacing[j];
            }

            reverseIndex[newIndex] = i;
        }
    }

    /// <summary>Initializes a new instance of the <see cref="TripleBandLinearOp"/> class as a copy of
    /// another.</summary>
    /// <param name="m">The operator copied, its bands and indices copied, its mesher shared.</param>
    /// <exception cref="ArgumentNullException"><paramref name="m"/> is <see langword="null"/>.</exception>
    public TripleBandLinearOp(TripleBandLinearOp m)
    {
        ArgumentNullException.ThrowIfNull(m);
        direction = m.direction;
        mesher = m.mesher;
        i0 = (int[])m.i0.Clone();
        i2 = (int[])m.i2.Clone();
        reverseIndex = (int[])m.reverseIndex.Clone();
        lower = (double[])m.lower.Clone();
        diag = (double[])m.diag.Clone();
        upper = (double[])m.upper.Clone();
        temp = new double[m.temp.Length];
    }

    /// <summary>Gets the operator's size: the layout's number of points.</summary>
    private int N => lower.Length;

    /// <summary>A copy of the operator: QuantLib's copy constructor.</summary>
    /// <returns>The copy.</returns>
    public TripleBandLinearOp Clone() => new(this);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="r"/>'s length isn't the layout's size.</exception>
    public override void Apply(ReadOnlySpan<double> r, Span<double> result)
    {
        if (!(r.Length == N))
        {
            throw new ArgumentException("inconsistent length of r", nameof(r));
        }

        for (int i = 0; i < N; ++i)
        {
            result[i] = (r[i0[i]] * lower[i]) + (r[i] * diag[i]) + (r[i2[i]] * upper[i]);
        }
    }

    /// <summary>
    /// Solves (<paramref name="a"/>·A + <paramref name="b"/>) x = <paramref name="r"/> for x by the Thomas algorithm,
    /// writing it into the caller's span.
    /// </summary>
    /// <param name="r">The right-hand side, one value per point.</param>
    /// <param name="a">The operator's scale.</param>
    /// <param name="b">The identity's scale.</param>
    /// <param name="result">The solution, of the layout's size; it mustn't overlap <paramref name="r"/>.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="r"/>'s length isn't the layout's size, or the layout has one point, where QuantLib's
    /// back-substitution reads past its arrays.
    /// </exception>
    /// <exception cref="NanoQLException">A pivot is zero.</exception>
    public void SolveSplitting(ReadOnlySpan<double> r, double a, double b, Span<double> result)
    {
        int size = N;
        if (!(r.Length == size))
        {
            throw new ArgumentException("inconsistent size of rhs", nameof(r));
        }

        // QuantLib's size-2 wraps at size 1, and the back-substitution reads past temp_ and the result
        if (size < 2)
        {
            throw new ArgumentException($"a triple-band solve needs at least two points, not {size}", nameof(r));
        }

        if (direction == 0)
        {
            Solve(r, a, b, result, null);
        }
        else
        {
            Solve(r, a, b, result, reverseIndex);
        }
    }

    /// <summary>Solves (<paramref name="a"/>·A + <paramref name="b"/>) x = <paramref name="r"/>: QuantLib's
    /// <c>solve_splitting</c>.</summary>
    /// <param name="r">The right-hand side, one value per point.</param>
    /// <param name="a">The operator's scale.</param>
    /// <param name="b">The identity's scale.</param>
    /// <returns>A new vector of the solution.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="r"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="r"/>'s length isn't the layout's size, or the layout has one point.
    /// </exception>
    /// <exception cref="NanoQLException">A pivot is zero.</exception>
    public Vector SolveSplitting(Vector r, double a, double b = 1.0)
    {
        ArgumentNullException.ThrowIfNull(r);
        var result = new Vector(r.Size);
        SolveSplitting(r.AsSpan(), a, b, result.AsSpan());
        return result;
    }

    /// <summary>The operator with each row scaled: diag(u)·A.</summary>
    /// <param name="u">The rows' scales, one per point.</param>
    /// <returns>A new operator.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="u"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="u"/>'s size isn't the layout's, where QuantLib reads a shorter one past its end.
    /// </exception>
    public TripleBandLinearOp Mult(Vector u)
    {
        ArgumentNullException.ThrowIfNull(u);

        // QuantLib reads u[i] for every point, unchecked
        CheckSize(u.Size, nameof(u));
        var retVal = new TripleBandLinearOp(direction, mesher);

        for (int i = 0; i < N; ++i)
        {
            double s = u[i];
            retVal.lower[i] = lower[i] * s;
            retVal.diag[i] = diag[i] * s;
            retVal.upper[i] = upper[i] * s;
        }

        return retVal;
    }

    /// <summary>The operator with each column scaled: A·diag(u).</summary>
    /// <param name="u">The columns' scales, one per point.</param>
    /// <returns>A new operator.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="u"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="u"/>'s size isn't the layout's.</exception>
    public TripleBandLinearOp MultR(Vector u)
    {
        ArgumentNullException.ThrowIfNull(u);
        int size = N;
        CheckSize(u.Size, nameof(u));
        var retVal = new TripleBandLinearOp(direction, mesher);

        for (int i = 0; i < size; ++i)
        {
            double sm1 = i > 0 ? u[i - 1] : 1.0;
            double s0 = u[i];
            double sp1 = i < size - 1 ? u[i + 1] : 1.0;
            retVal.lower[i] = lower[i] * sm1;
            retVal.diag[i] = diag[i] * s0;
            retVal.upper[i] = upper[i] * sp1;
        }

        return retVal;
    }

    /// <summary>The sum of two operators in the same direction, on the same mesher.</summary>
    /// <param name="m">The other operator.</param>
    /// <returns>A new operator.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="m"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The other operator's size isn't this one's.</exception>
    public TripleBandLinearOp Add(TripleBandLinearOp m)
    {
        ArgumentNullException.ThrowIfNull(m);
        CheckSize(m.N, nameof(m));
        var retVal = new TripleBandLinearOp(direction, mesher);
        for (int i = 0; i < N; ++i)
        {
            retVal.lower[i] = lower[i] + m.lower[i];
            retVal.diag[i] = diag[i] + m.diag[i];
            retVal.upper[i] = upper[i] + m.upper[i];
        }

        return retVal;
    }

    /// <summary>The operator plus a diagonal matrix.</summary>
    /// <param name="u">The diagonal, one value per point.</param>
    /// <returns>A new operator.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="u"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="u"/>'s size isn't the layout's, where QuantLib reads a shorter one past its end.
    /// </exception>
    public TripleBandLinearOp Add(Vector u)
    {
        ArgumentNullException.ThrowIfNull(u);

        // QuantLib reads u[i] for every point, unchecked
        CheckSize(u.Size, nameof(u));
        var retVal = new TripleBandLinearOp(direction, mesher);

        for (int i = 0; i < N; ++i)
        {
            retVal.lower[i] = lower[i];
            retVal.upper[i] = upper[i];
            retVal.diag[i] = diag[i] + u[i];
        }

        return retVal;
    }

    /// <summary>
    /// Sets this operator, in place, to diag(<paramref name="a"/>)·<paramref name="x"/> + <paramref name="y"/> +
    /// diag(<paramref name="b"/>).
    /// </summary>
    /// <param name="a">The scales of <paramref name="x"/>'s rows: empty for none, one value for all, or one per
    /// point.</param>
    /// <param name="x">The operator scaled.</param>
    /// <param name="y">The operator added.</param>
    /// <param name="b">The diagonal added: empty for none, one value for all, or one per point.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="a"/> or <paramref name="b"/> has more than one value but not one per point, or an operator's
    /// size isn't this one's, where QuantLib reads a shorter one past its end.
    /// </exception>
    public void Axpyb(Vector a, TripleBandLinearOp x, TripleBandLinearOp y, Vector b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        ArgumentNullException.ThrowIfNull(b);

        // QuantLib reads a, b, x and y for every point, a and b broadcast at one value, unchecked otherwise
        if (a.Size > 1)
        {
            CheckSize(a.Size, nameof(a));
        }

        if (b.Size > 1)
        {
            CheckSize(b.Size, nameof(b));
        }

        CheckSize(x.N, nameof(x));
        CheckSize(y.N, nameof(y));

        int size = N;
        if (a.Size == 0)
        {
            if (b.Size == 0)
            {
                for (int i = 0; i < size; ++i)
                {
                    diag[i] = y.diag[i];
                    lower[i] = y.lower[i];
                    upper[i] = y.upper[i];
                }
            }
            else
            {
                int binc = (b.Size > 1) ? 1 : 0;
                for (int i = 0; i < size; ++i)
                {
                    diag[i] = y.diag[i] + b[i * binc];
                    lower[i] = y.lower[i];
                    upper[i] = y.upper[i];
                }
            }
        }
        else if (b.Size == 0)
        {
            int ainc = (a.Size > 1) ? 1 : 0;
            for (int i = 0; i < size; ++i)
            {
                double s = a[i * ainc];
                diag[i] = y.diag[i] + (s * x.diag[i]);
                lower[i] = y.lower[i] + (s * x.lower[i]);
                upper[i] = y.upper[i] + (s * x.upper[i]);
            }
        }
        else
        {
            int binc = (b.Size > 1) ? 1 : 0;
            int ainc = (a.Size > 1) ? 1 : 0;
            for (int i = 0; i < size; ++i)
            {
                double s = a[i * ainc];
                diag[i] = y.diag[i] + (s * x.diag[i]) + b[i * binc];
                lower[i] = y.lower[i] + (s * x.lower[i]);
                upper[i] = y.upper[i] + (s * x.upper[i]);
            }
        }
    }

    /// <inheritdoc/>
    public override SparseMatrix ToMatrix()
    {
        int n = N;

        var retVal = new SparseMatrix(n, n, 3 * n);
        for (int i = 0; i < n; ++i)
        {
            retVal[i, i0[i]] += lower[i];
            retVal[i, i] += diag[i];
            retVal[i, i2[i]] += upper[i];
        }

        return retVal;
    }

    /// <summary>
    /// Sets this operator, in place, to diag(<paramref name="a"/>)·<paramref name="x"/> +
    /// diag(<paramref name="u"/>)·<paramref name="y"/> + <paramref name="b"/>: QuantLib's
    /// <c>axpyb(a, x, y.mult(u), Array(1, b))</c> with <c>mult</c>'s temporary operator written straight into this
    /// one, by the same arithmetic in the same order, so that a time step allocates nothing.
    /// </summary>
    /// <param name="a">The scales of <paramref name="x"/>'s rows: one value for all, or one per point.</param>
    /// <param name="x">The operator scaled by <paramref name="a"/>.</param>
    /// <param name="y">The operator scaled by <paramref name="u"/>.</param>
    /// <param name="u">The scales of <paramref name="y"/>'s rows: one value for all, or one per point.</param>
    /// <param name="b">The value added to the diagonal.</param>
    internal void SetAxpybOfMult(
        ReadOnlySpan<double> a, TripleBandLinearOp x, TripleBandLinearOp y, ReadOnlySpan<double> u, double b)
    {
        int size = N;
        int ainc = (a.Length > 1) ? 1 : 0;
        int uinc = (u.Length > 1) ? 1 : 0;
        for (int i = 0; i < size; ++i)
        {
            double s = a[i * ainc];
            double h = u[i * uinc];
            diag[i] = (y.diag[i] * h) + (s * x.diag[i]) + b;
            lower[i] = (y.lower[i] * h) + (s * x.lower[i]);
            upper[i] = (y.upper[i] * h) + (s * x.upper[i]);
        }
    }

    /// <summary>
    /// Sets this operator, in place, to diag(<paramref name="a"/>)·<paramref name="x"/> +
    /// diag(<paramref name="u"/>)·<paramref name="y"/> + diag(<paramref name="b"/>): QuantLib's
    /// <c>axpyb(a, x, y.mult(u), b)</c> with <c>mult</c>'s temporary operator written straight into this one, by the
    /// same arithmetic in the same order, so that a time step allocates nothing.
    /// </summary>
    /// <param name="a">The scales of <paramref name="x"/>'s rows: one value for all, or one per point.</param>
    /// <param name="x">The operator scaled by <paramref name="a"/>.</param>
    /// <param name="y">The operator scaled by <paramref name="u"/>.</param>
    /// <param name="u">The scales of <paramref name="y"/>'s rows: one value for all, or one per point.</param>
    /// <param name="b">The diagonal added: one value for all, or one per point.</param>
    internal void SetAxpybOfMult(
        ReadOnlySpan<double> a, TripleBandLinearOp x, TripleBandLinearOp y, ReadOnlySpan<double> u, ReadOnlySpan<double> b)
    {
        int size = N;
        int ainc = (a.Length > 1) ? 1 : 0;
        int uinc = (u.Length > 1) ? 1 : 0;
        int binc = (b.Length > 1) ? 1 : 0;
        for (int i = 0; i < size; ++i)
        {
            double s = a[i * ainc];
            double h = u[i * uinc];
            diag[i] = (y.diag[i] * h) + (s * x.diag[i]) + b[i * binc];
            lower[i] = (y.lower[i] * h) + (s * x.lower[i]);
            upper[i] = (y.upper[i] * h) + (s * x.upper[i]);
        }
    }

    /// <summary>
    /// Sets this operator, in place, to <paramref name="x"/>·diag(<paramref name="ux"/>) +
    /// <paramref name="y"/>·diag(<paramref name="uy"/>): QuantLib's
    /// <c>axpyb(Array(1, 1.0), x.multR(ux), y.multR(uy), Array(1, 0.0))</c> with <c>multR</c>'s temporary operators
    /// written straight into this one, by the same arithmetic in the same order, so that a time step allocates nothing.
    /// </summary>
    /// <param name="x">The first operator.</param>
    /// <param name="ux">The scales of <paramref name="x"/>'s columns, one per point.</param>
    /// <param name="y">The second operator.</param>
    /// <param name="uy">The scales of <paramref name="y"/>'s columns, one per point.</param>
    internal void SetSumOfMultR(TripleBandLinearOp x, ReadOnlySpan<double> ux, TripleBandLinearOp y, ReadOnlySpan<double> uy)
    {
        const double S = 1.0;
        double b = 0.0;
        int size = N;
        for (int i = 0; i < size; ++i)
        {
            double xm1 = i > 0 ? ux[i - 1] : 1.0, x0 = ux[i], xp1 = i < size - 1 ? ux[i + 1] : 1.0;
            double ym1 = i > 0 ? uy[i - 1] : 1.0, y0 = uy[i], yp1 = i < size - 1 ? uy[i + 1] : 1.0;

            // axpyb's form for one scale and one diagonal value: y + s·x + b
            diag[i] = (y.diag[i] * y0) + (S * (x.diag[i] * x0)) + b;
            lower[i] = (y.lower[i] * ym1) + (S * (x.lower[i] * xm1));
            upper[i] = (y.upper[i] * yp1) + (S * (x.upper[i] * xp1));
        }
    }

    /// <summary>
    /// Sets this operator, in place, to <paramref name="y"/> + diag(<paramref name="b"/>): QuantLib's
    /// <c>axpyb(Array(), x, y, b)</c>, whose x it doesn't read, by the same arithmetic, so that a time step allocates
    /// nothing.
    /// </summary>
    /// <param name="y">The operator added, of this one's size.</param>
    /// <param name="b">The diagonal added: one value for all, or one per point.</param>
    internal void SetSumWithDiagonal(TripleBandLinearOp y, ReadOnlySpan<double> b)
    {
        int size = N;
        int binc = (b.Length > 1) ? 1 : 0;
        for (int i = 0; i < size; ++i)
        {
            diag[i] = y.diag[i] + b[i * binc];
            lower[i] = y.lower[i];
            upper[i] = y.upper[i];
        }
    }

    // QuantLib's Thomas algorithm, the index the storage order (direction 0) or the reverse index
    private void Solve(ReadOnlySpan<double> r, double a, double b, Span<double> result, int[]? index)
    {
        int size = N;

        int previous = Index(index, 0);
        double beta = (a * diag[previous]) + b;
        if (!(beta != 0.0))
        {
            throw new ArgumentException("division by zero", nameof(a));
        }

        beta = 1.0 / beta;
        result[previous] = r[previous] * beta;

        for (int j = 1; j < size; ++j)
        {
            int current = Index(index, j);
            temp[j] = a * upper[previous] * beta;

            beta = b + (a * (diag[current] - (temp[j] * lower[current])));
            if (!(beta != 0.0))
            {
                throw new NanoQLException("division by zero");
            }

            beta = 1.0 / beta;

            result[current] = (r[current] - (a * lower[current] * result[previous])) * beta;
            previous = current;
        }

        for (int j = size - 2; j > 0; --j)
        {
            result[Index(index, j)] -= temp[j + 1] * result[Index(index, j + 1)];
        }

        result[Index(index, 0)] -= temp[1] * result[Index(index, 1)];
    }

    private static int Index(int[]? index, int i) => index is null ? i : index[i];

    private void CheckSize(int size, string paramName)
    {
        if (!(size == N))
        {
            throw new ArgumentException("inconsistent size of rhs", paramName);
        }
    }
}
