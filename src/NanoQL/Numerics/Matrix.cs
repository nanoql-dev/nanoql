// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Michael von den Driesch
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrix.hpp ql/math/matrix.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Text;
using NanoQL.Utilities;

namespace NanoQL.Numerics;

/// <summary>
/// A matrix used in linear algebra, stored by rows. It implements the concept of a matrix as used in linear algebra,
/// and is not meant to be used as a container.
/// </summary>
/// <remarks>
/// <para>
/// A working type: mutable, owned by one computation and not thread-safe, as an array is. The indexer sets elements,
/// and the compound assignments (<c>m += n</c>, <c>m *= x</c>, ...) change the matrix in place, so every reference to
/// it sees the change; the binary and unary operators return new matrices. <see cref="Clone"/> is the deep copy, where
/// QuantLib copies a <c>Matrix</c> by value. The matrices of a sum or a difference need the same size.
/// </para>
/// <para>
/// QuantLib's iterators over the whole storage are <see cref="AsSpan"/> and <see cref="GetEnumerator"/>; its row
/// iterators are <see cref="Row"/>, a span over a row's elements; its column iterators are <see cref="Column"/>, a copy
/// of a column, and indexed loops; its reverse iterators (<c>rbegin()</c>, <c>rend()</c>, over the storage, a row or a
/// column) are indexed loops run backwards. <c>m[i][j]</c> and <c>m(i, j)</c> are both <c>m[i, j]</c>.
/// </para>
/// </remarks>
public sealed class Matrix : IEquatable<Matrix>
{
    private double[] data;
    private int rows;
    private int columns;

    /// <summary>Initializes a new instance of the <see cref="Matrix"/> class: a null matrix, with no rows or columns.</summary>
    public Matrix()
        : this(0, 0)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Matrix"/> class with the given dimensions.</summary>
    /// <param name="rows">The number of rows.</param>
    /// <param name="columns">The number of columns.</param>
    /// <remarks>The elements are zero, where QuantLib leaves them uninitialized.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is negative.</exception>
    public Matrix(int rows, int columns)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rows);
        ArgumentOutOfRangeException.ThrowIfNegative(columns);
        data = new double[checked(rows * columns)];
        this.rows = rows;
        this.columns = columns;
    }

    /// <summary>Initializes a new instance of the <see cref="Matrix"/> class, every element set to a value.</summary>
    /// <param name="rows">The number of rows.</param>
    /// <param name="columns">The number of columns.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is negative.</exception>
    public Matrix(int rows, int columns, double value)
        : this(rows, columns)
    {
        Array.Fill(data, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Matrix"/> class from a range of elements, row by row, which it
    /// copies: QuantLib's iterator-range constructor.
    /// </summary>
    /// <param name="rows">The number of rows.</param>
    /// <param name="columns">The number of columns.</param>
    /// <param name="data">The elements, row by row; elements past its end are zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is negative.</exception>
    /// <exception cref="ArgumentException">The range is larger than the matrix.</exception>
    public Matrix(int rows, int columns, ReadOnlySpan<double> data)
        : this(rows, columns)
    {
        data.CopyTo(this.data);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Matrix"/> class from its rows, which it copies: QuantLib's
    /// initializer list, <c>new Matrix(new double[,] { { 1, 2, 3 }, { 4, 5, 6 } })</c>.
    /// </summary>
    /// <param name="data">The elements; a matrix with no rows has no columns.</param>
    public Matrix(double[,] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        rows = data.GetLength(0);
        columns = rows == 0 ? 0 : data.GetLength(1);
        this.data = new double[rows * columns];
        for (int i = 0; i < rows; ++i)
        {
            for (int j = 0; j < columns; ++j)
            {
                this.data[(i * columns) + j] = data[i, j];
            }
        }
    }

    /// <summary>Gets the number of rows.</summary>
    public int Rows => rows;

    /// <summary>Gets the number of columns.</summary>
    public int Columns => columns;

    /// <summary>Gets the number of rows, as QuantLib's <c>size1()</c>.</summary>
    public int Size1 => rows;

    /// <summary>Gets the number of columns, as QuantLib's <c>size2()</c>.</summary>
    public int Size2 => columns;

    /// <summary>Gets a value indicating whether the matrix has no rows or no columns.</summary>
    public bool Empty => rows == 0 || columns == 0;

    /// <summary>Gets or sets the element in row <paramref name="i"/> and column <paramref name="j"/>.</summary>
    /// <param name="i">The row.</param>
    /// <param name="j">The column.</param>
    /// <returns>The element.</returns>
    /// <remarks>As in QuantLib, the element is the one at <c>i * Columns + j</c> in the row-by-row storage.</remarks>
    public double this[int i, int j]
    {
        get => data[(i * columns) + j];
        set => data[(i * columns) + j] = value;
    }

    /// <summary>The matrix negated, element by element.</summary>
    /// <param name="m1">The matrix.</param>
    /// <returns>A new matrix.</returns>
    public static Matrix operator -(Matrix m1)
    {
        ArgumentNullException.ThrowIfNull(m1);
        var temp = new Matrix(m1.rows, m1.columns);
        for (int k = 0; k < m1.data.Length; ++k)
        {
            temp.data[k] = -m1.data[k];
        }

        return temp;
    }

    /// <summary>The element-by-element sum.</summary>
    /// <param name="m1">The first matrix.</param>
    /// <param name="m2">The second matrix.</param>
    /// <returns>A new matrix.</returns>
    /// <exception cref="ArgumentException">The sizes differ.</exception>
    public static Matrix operator +(Matrix m1, Matrix m2)
    {
        RequireSameSize(m1, m2, "added");
        var temp = new Matrix(m1.rows, m1.columns);
        for (int k = 0; k < m1.data.Length; ++k)
        {
            temp.data[k] = m1.data[k] + m2.data[k];
        }

        return temp;
    }

    /// <summary>The element-by-element difference.</summary>
    /// <param name="m1">The first matrix.</param>
    /// <param name="m2">The second matrix.</param>
    /// <returns>A new matrix.</returns>
    /// <exception cref="ArgumentException">The sizes differ.</exception>
    public static Matrix operator -(Matrix m1, Matrix m2)
    {
        RequireSameSize(m1, m2, "subtracted");
        var temp = new Matrix(m1.rows, m1.columns);
        for (int k = 0; k < m1.data.Length; ++k)
        {
            temp.data[k] = m1.data[k] - m2.data[k];
        }

        return temp;
    }

    /// <summary>The matrix with every element multiplied by a scalar.</summary>
    /// <param name="m">The matrix.</param>
    /// <param name="x">The scalar.</param>
    /// <returns>A new matrix.</returns>
    public static Matrix operator *(Matrix m, double x)
    {
        ArgumentNullException.ThrowIfNull(m);
        var temp = new Matrix(m.rows, m.columns);
        for (int k = 0; k < m.data.Length; ++k)
        {
            temp.data[k] = m.data[k] * x;
        }

        return temp;
    }

    /// <summary>A scalar multiplied by every element of the matrix.</summary>
    /// <param name="x">The scalar.</param>
    /// <param name="m">The matrix.</param>
    /// <returns>A new matrix.</returns>
    public static Matrix operator *(double x, Matrix m)
    {
        ArgumentNullException.ThrowIfNull(m);
        var temp = new Matrix(m.rows, m.columns);
        for (int k = 0; k < m.data.Length; ++k)
        {
            temp.data[k] = x * m.data[k];
        }

        return temp;
    }

    /// <summary>The matrix with every element divided by a scalar.</summary>
    /// <param name="m">The matrix.</param>
    /// <param name="x">The scalar.</param>
    /// <returns>A new matrix.</returns>
    public static Matrix operator /(Matrix m, double x)
    {
        ArgumentNullException.ThrowIfNull(m);
        var temp = new Matrix(m.rows, m.columns);
        for (int k = 0; k < m.data.Length; ++k)
        {
            temp.data[k] = m.data[k] / x;
        }

        return temp;
    }

    /// <summary>The row vector <paramref name="v"/> times the matrix.</summary>
    /// <param name="v">The vector, of the matrix's row count.</param>
    /// <param name="m">The matrix.</param>
    /// <returns>A new vector of the matrix's column count, each element the dot product of the vector and a column.</returns>
    /// <exception cref="ArgumentException">The sizes don't match.</exception>
    public static Vector operator *(Vector v, Matrix m)
    {
        ArgumentNullException.ThrowIfNull(v);
        ArgumentNullException.ThrowIfNull(m);
        if (!(v.Size == m.rows))
        {
            throw new ArgumentException(
                $"vectors and matrices with different sizes ({v.Size}, {m.rows}x{m.columns}) cannot be multiplied",
                nameof(m));
        }

        var result = new Vector(m.columns);
        ReadOnlySpan<double> x = v.AsSpan();
        for (int i = 0; i < result.Size; i++)
        {
            double sum = 0.0;
            for (int k = 0; k < x.Length; ++k)
            {
                sum = sum + (x[k] * m.data[(k * m.columns) + i]);
            }

            result[i] = sum;
        }

        return result;
    }

    /// <summary>The matrix times the column vector <paramref name="v"/>.</summary>
    /// <param name="m">The matrix.</param>
    /// <param name="v">The vector, of the matrix's column count.</param>
    /// <returns>A new vector of the matrix's row count, each element the dot product of the vector and a row.</returns>
    /// <exception cref="ArgumentException">The sizes don't match.</exception>
    public static Vector operator *(Matrix m, Vector v)
    {
        ArgumentNullException.ThrowIfNull(m);
        ArgumentNullException.ThrowIfNull(v);
        if (!(v.Size == m.columns))
        {
            throw new ArgumentException(
                $"vectors and matrices with different sizes ({v.Size}, {m.rows}x{m.columns}) cannot be multiplied",
                nameof(v));
        }

        var result = new Vector(m.rows);
        ReadOnlySpan<double> x = v.AsSpan();
        for (int i = 0; i < result.Size; i++)
        {
            double sum = 0.0;
            for (int k = 0; k < x.Length; ++k)
            {
                sum = sum + (x[k] * m.data[(i * m.columns) + k]);
            }

            result[i] = sum;
        }

        return result;
    }

    /// <summary>The matrix product.</summary>
    /// <param name="m1">The first matrix.</param>
    /// <param name="m2">The second matrix, with as many rows as the first has columns.</param>
    /// <returns>A new matrix.</returns>
    /// <exception cref="ArgumentException">The sizes don't match.</exception>
    public static Matrix operator *(Matrix m1, Matrix m2)
    {
        ArgumentNullException.ThrowIfNull(m1);
        ArgumentNullException.ThrowIfNull(m2);
        if (!(m1.columns == m2.rows))
        {
            throw new ArgumentException(
                $"matrices with different sizes ({m1.rows}x{m1.columns}, {m2.rows}x{m2.columns}) cannot be multiplied",
                nameof(m2));
        }

        var result = new Matrix(m1.rows, m2.columns, 0.0);
        for (int i = 0; i < result.rows; ++i)
        {
            for (int k = 0; k < m1.columns; ++k)
            {
                for (int j = 0; j < result.columns; ++j)
                {
                    result.data[(i * result.columns) + j] += m1.data[(i * m1.columns) + k] * m2.data[(k * m2.columns) + j];
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Whether two matrices have the same dimensions and equal elements, compared with <c>==</c> as QuantLib does: a
    /// NaN element makes them unequal.
    /// </summary>
    /// <param name="left">The first matrix.</param>
    /// <param name="right">The second matrix.</param>
    /// <returns><see langword="true"/> when equal; two null references are equal.</returns>
    public static bool operator ==(Matrix? left, Matrix? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (!(left.rows == right.rows && left.columns == right.columns))
        {
            return false;
        }

        for (int k = 0; k < left.data.Length; ++k)
        {
            if (!(left.data[k] == right.data[k]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether two matrices differ: the negation of <c>==</c>.</summary>
    /// <param name="left">The first matrix.</param>
    /// <param name="right">The second matrix.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    public static bool operator !=(Matrix? left, Matrix? right) => !(left == right);

    /// <summary>Adds another matrix to this one, element by element, in place.</summary>
    /// <param name="m">The matrix to add.</param>
    /// <exception cref="ArgumentException">The sizes differ.</exception>
    public void operator +=(Matrix m)
    {
        RequireSameSizeInPlace(m, "added");
        for (int k = 0; k < data.Length; ++k)
        {
            data[k] = data[k] + m.data[k];
        }
    }

    /// <summary>Subtracts another matrix from this one, element by element, in place.</summary>
    /// <param name="m">The matrix to subtract.</param>
    /// <exception cref="ArgumentException">The sizes differ.</exception>
    public void operator -=(Matrix m)
    {
        RequireSameSizeInPlace(m, "subtracted");
        for (int k = 0; k < data.Length; ++k)
        {
            data[k] = data[k] - m.data[k];
        }
    }

    /// <summary>Multiplies every element by a scalar, in place.</summary>
    /// <param name="x">The scalar.</param>
    public void operator *=(double x)
    {
        for (int k = 0; k < data.Length; ++k)
        {
            data[k] = data[k] * x;
        }
    }

    /// <summary>Divides every element by a scalar, in place.</summary>
    /// <param name="x">The scalar.</param>
    public void operator /=(double x)
    {
        for (int k = 0; k < data.Length; ++k)
        {
            data[k] = data[k] / x;
        }
    }

    /// <summary>A span over row <paramref name="i"/>'s elements, through which they can be read and set.</summary>
    /// <param name="i">The row.</param>
    /// <returns>The span, which reads this matrix until the next <see cref="Swap"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The row is outside the storage.</exception>
    public Span<double> Row(int i) => data.AsSpan(i * columns, columns);

    /// <summary>Row <paramref name="i"/>, checked: QuantLib's <c>at</c>.</summary>
    /// <param name="i">The row.</param>
    /// <returns>A span over the row's elements.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The row is out of range.</exception>
    public Span<double> At(int i)
    {
        if (!(i >= 0 && i < rows))
        {
            throw new ArgumentOutOfRangeException(nameof(i), "matrix access out of range");
        }

        return Row(i);
    }

    /// <summary>A copy of column <paramref name="j"/>.</summary>
    /// <param name="j">The column.</param>
    /// <returns>A new vector of the matrix's row count.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The column is out of range.</exception>
    public Vector Column(int j)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(j);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(j, columns);
        var result = new Vector(rows);
        for (int i = 0; i < rows; ++i)
        {
            result[i] = data[(i * columns) + j];
        }

        return result;
    }

    /// <summary>A copy of the diagonal, of the smaller of the two dimensions.</summary>
    /// <returns>A new vector.</returns>
    public Vector Diagonal()
    {
        int arraySize = Math.Min(rows, columns);
        var tmp = new Vector(arraySize);
        for (int i = 0; i < arraySize; i++)
        {
            tmp[i] = data[(i * columns) + i];
        }

        return tmp;
    }

    /// <summary>Exchanges the contents of this matrix with another's.</summary>
    /// <param name="from">The other matrix.</param>
    public void Swap(Matrix from)
    {
        ArgumentNullException.ThrowIfNull(from);
        (data, from.data) = (from.data, data);
        (rows, from.rows) = (from.rows, rows);
        (columns, from.columns) = (from.columns, columns);
    }

    /// <summary>A deep copy: QuantLib's copy constructor and assignment.</summary>
    /// <returns>A new matrix with the same dimensions and elements.</returns>
    public Matrix Clone() => new(rows, columns, data);

    /// <summary>
    /// Whether another matrix has the same dimensions and equal elements, each compared as
    /// <see cref="double.Equals(double)"/> does: a NaN equals a NaN, so a matrix equals itself. <c>==</c> compares as
    /// QuantLib does, as <see cref="double"/>'s own <c>==</c> and <see cref="double.Equals(double)"/> differ.
    /// </summary>
    /// <param name="other">The other matrix.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    public bool Equals(Matrix? other)
    {
        if (other is null || !(rows == other.rows && columns == other.columns))
        {
            return false;
        }

        for (int k = 0; k < data.Length; ++k)
        {
            if (!data[k].Equals(other.data[k]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as Matrix);

    /// <summary>A hash of the dimensions and the elements, which changes when they do.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(rows);
        hash.Add(columns);
        foreach (double x in data)
        {
            hash.Add(x);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// The matrix as QuantLib writes it, one line per row, e.g. "| 1 2 |\n| 3 4 |\n", each element as a C++ stream
    /// writes it.
    /// </summary>
    /// <returns>The text.</returns>
    public override string ToString()
    {
        var text = new StringBuilder();
        for (int i = 0; i < rows; i++)
        {
            text.Append("| ");
            for (int j = 0; j < columns; j++)
            {
                text.Append(DataFormattersFunctions.General(data[(i * columns) + j])).Append(' ');
            }

            text.Append("|\n");
        }

        return text.ToString();
    }

    /// <summary>
    /// Gets a span over the elements, row by row: QuantLib's <c>begin()</c> and <c>end()</c>, for algorithms that walk a
    /// matrix's storage whole. Writing through it changes the matrix.
    /// </summary>
    /// <returns>The span.</returns>
    public Span<double> AsSpan() => data;

    /// <summary>Enumerates the elements, row by row, so that <c>foreach</c> works (QuantLib's <c>begin()</c> to <c>end()</c>).</summary>
    /// <returns>The enumerator.</returns>
    public Span<double>.Enumerator GetEnumerator() => data.AsSpan().GetEnumerator();

    private static void RequireSameSize(Matrix m1, Matrix m2, string operation)
    {
        ArgumentNullException.ThrowIfNull(m1);
        ArgumentNullException.ThrowIfNull(m2);
        if (!(m1.rows == m2.rows && m1.columns == m2.columns))
        {
            throw new ArgumentException(
                $"matrices with different sizes ({m1.rows}x{m1.columns}, {m2.rows}x{m2.columns}) cannot be {operation}",
                nameof(m2));
        }
    }

    // The compound assignments' check, whose message gives the argument's size first, as QuantLib's does.
    private void RequireSameSizeInPlace(Matrix m, string operation)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (!(rows == m.rows && columns == m.columns))
        {
            throw new ArgumentException(
                $"matrices with different sizes ({m.rows}x{m.columns}, {rows}x{columns}) cannot be {operation}",
                nameof(m));
        }
    }
}
