// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/array.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections;
using System.Runtime.CompilerServices;
using System.Text;
using NanoQL.Utilities;

namespace NanoQL.Numerics;

/// <summary>
/// A 1-D array used in linear algebra: QuantLib's <c>Array</c>, renamed. It implements the concept of a vector as used
/// in linear algebra, and is not meant to be used as a container.
/// </summary>
/// <remarks>
/// <para>
/// A working type: mutable, owned by one computation and not thread-safe, as an array is. The indexer sets elements,
/// and the compound assignments (<c>v += w</c>, <c>v *= x</c>, ...) change the vector in place, so every reference to
/// it sees the change; the binary and unary operators return new vectors. <see cref="Clone"/> is the deep copy, where
/// QuantLib copies an <c>Array</c> by value. <c>v += x</c> and the other operations with a scalar apply it to every
/// element; <c>v *= w</c> and the other operations with two vectors work element by element, and need vectors of the
/// same size.
/// </para>
/// <para>
/// A collection expression builds one, as QuantLib's initializer list does: <c>Vector v = [1, 2, 3];</c>.
/// QuantLib's iterators are <see cref="AsSpan"/> and <see cref="GetEnumerator"/>, and its reverse iterators
/// (<c>rbegin()</c>, <c>rend()</c>) indexed loops run backwards.
/// </para>
/// </remarks>
[CollectionBuilder(typeof(Vector), nameof(Create))]
public sealed class Vector : IEquatable<Vector>, IEnumerable<double>
{
    private double[] data;

    /// <summary>Initializes a new instance of the <see cref="Vector"/> class with size 0.</summary>
    public Vector()
        : this(0)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Vector"/> class with the given size.</summary>
    /// <param name="size">The size.</param>
    /// <remarks>The elements are zero, where QuantLib leaves them uninitialized.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The size is negative.</exception>
    public Vector(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        data = size != 0 ? new double[size] : [];
    }

    /// <summary>Initializes a new instance of the <see cref="Vector"/> class, every element set to a value.</summary>
    /// <param name="size">The size.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentOutOfRangeException">The size is negative.</exception>
    public Vector(int size, double value)
        : this(size)
    {
        Array.Fill(data, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Vector"/> class filled as a₀ = <paramref name="value"/>,
    /// aᵢ = aᵢ₋₁ + <paramref name="increment"/>.
    /// </summary>
    /// <param name="size">The size.</param>
    /// <param name="value">The first element.</param>
    /// <param name="increment">The increment between successive elements, added one at a time.</param>
    /// <exception cref="ArgumentOutOfRangeException">The size is negative.</exception>
    public Vector(int size, double value, double increment)
        : this(size)
    {
        for (int i = 0; i < data.Length; ++i, value += increment)
        {
            data[i] = value;
        }
    }

    /// <summary>Initializes a new instance of the <see cref="Vector"/> class from a sequence, which it copies.</summary>
    /// <param name="data">The elements.</param>
    public Vector(ReadOnlySpan<double> data)
    {
        this.data = data.ToArray();
    }

    /// <summary>Gets the size of the vector.</summary>
    public int Size => data.Length;

    /// <summary>Gets a value indicating whether the vector is empty.</summary>
    public bool Empty => data.Length == 0;

    /// <summary>Gets or sets the element at the given index.</summary>
    /// <param name="i">The index.</param>
    /// <returns>The element.</returns>
    public double this[int i]
    {
        get => data[i];
        set => data[i] = value;
    }

    /// <summary>Builds a vector from a collection expression's elements, which it copies.</summary>
    /// <param name="values">The elements.</param>
    /// <returns>The vector.</returns>
    public static Vector Create(ReadOnlySpan<double> values) => new(values);

    /// <summary>The vector itself, a copy: QuantLib's unary <c>+</c>.</summary>
    /// <param name="v">The vector.</param>
    /// <returns>A copy.</returns>
    public static Vector operator +(Vector v)
    {
        ArgumentNullException.ThrowIfNull(v);
        return v.Clone();
    }

    /// <summary>The vector negated, element by element.</summary>
    /// <param name="v">The vector.</param>
    /// <returns>A new vector.</returns>
    public static Vector operator -(Vector v)
    {
        ArgumentNullException.ThrowIfNull(v);
        var result = new Vector(v.Size);
        for (int i = 0; i < v.data.Length; ++i)
        {
            result.data[i] = -v.data[i];
        }

        return result;
    }

    /// <summary>The element-by-element sum.</summary>
    /// <param name="v1">The first vector.</param>
    /// <param name="v2">The second vector.</param>
    /// <returns>A new vector.</returns>
    /// <exception cref="ArgumentException">The sizes differ.</exception>
    public static Vector operator +(Vector v1, Vector v2)
    {
        RequireSameSize(v1, v2, "added");
        var result = new Vector(v1.Size);
        for (int i = 0; i < v1.data.Length; ++i)
        {
            result.data[i] = v1.data[i] + v2.data[i];
        }

        return result;
    }

    /// <summary>The vector with a scalar added to every element.</summary>
    /// <param name="v1">The vector.</param>
    /// <param name="a">The scalar.</param>
    /// <returns>A new vector.</returns>
    public static Vector operator +(Vector v1, double a)
    {
        ArgumentNullException.ThrowIfNull(v1);
        var result = new Vector(v1.Size);
        for (int i = 0; i < v1.data.Length; ++i)
        {
            result.data[i] = v1.data[i] + a;
        }

        return result;
    }

    /// <summary>A scalar added to every element of the vector.</summary>
    /// <param name="a">The scalar.</param>
    /// <param name="v2">The vector.</param>
    /// <returns>A new vector.</returns>
    public static Vector operator +(double a, Vector v2)
    {
        ArgumentNullException.ThrowIfNull(v2);
        var result = new Vector(v2.Size);
        for (int i = 0; i < v2.data.Length; ++i)
        {
            result.data[i] = a + v2.data[i];
        }

        return result;
    }

    /// <summary>The element-by-element difference.</summary>
    /// <param name="v1">The first vector.</param>
    /// <param name="v2">The second vector.</param>
    /// <returns>A new vector.</returns>
    /// <exception cref="ArgumentException">The sizes differ.</exception>
    public static Vector operator -(Vector v1, Vector v2)
    {
        RequireSameSize(v1, v2, "subtracted");
        var result = new Vector(v1.Size);
        for (int i = 0; i < v1.data.Length; ++i)
        {
            result.data[i] = v1.data[i] - v2.data[i];
        }

        return result;
    }

    /// <summary>The vector with a scalar subtracted from every element.</summary>
    /// <param name="v1">The vector.</param>
    /// <param name="a">The scalar.</param>
    /// <returns>A new vector.</returns>
    public static Vector operator -(Vector v1, double a)
    {
        ArgumentNullException.ThrowIfNull(v1);
        var result = new Vector(v1.Size);
        for (int i = 0; i < v1.data.Length; ++i)
        {
            result.data[i] = v1.data[i] - a;
        }

        return result;
    }

    /// <summary>Every element of the vector subtracted from a scalar.</summary>
    /// <param name="a">The scalar.</param>
    /// <param name="v2">The vector.</param>
    /// <returns>A new vector.</returns>
    public static Vector operator -(double a, Vector v2)
    {
        ArgumentNullException.ThrowIfNull(v2);
        var result = new Vector(v2.Size);
        for (int i = 0; i < v2.data.Length; ++i)
        {
            result.data[i] = a - v2.data[i];
        }

        return result;
    }

    /// <summary>The element-by-element product.</summary>
    /// <param name="v1">The first vector.</param>
    /// <param name="v2">The second vector.</param>
    /// <returns>A new vector.</returns>
    /// <exception cref="ArgumentException">The sizes differ.</exception>
    public static Vector operator *(Vector v1, Vector v2)
    {
        RequireSameSize(v1, v2, "multiplied");
        var result = new Vector(v1.Size);
        for (int i = 0; i < v1.data.Length; ++i)
        {
            result.data[i] = v1.data[i] * v2.data[i];
        }

        return result;
    }

    /// <summary>The vector with every element multiplied by a scalar.</summary>
    /// <param name="v1">The vector.</param>
    /// <param name="a">The scalar.</param>
    /// <returns>A new vector.</returns>
    public static Vector operator *(Vector v1, double a)
    {
        ArgumentNullException.ThrowIfNull(v1);
        var result = new Vector(v1.Size);
        for (int i = 0; i < v1.data.Length; ++i)
        {
            result.data[i] = v1.data[i] * a;
        }

        return result;
    }

    /// <summary>A scalar multiplied by every element of the vector.</summary>
    /// <param name="a">The scalar.</param>
    /// <param name="v2">The vector.</param>
    /// <returns>A new vector.</returns>
    public static Vector operator *(double a, Vector v2)
    {
        ArgumentNullException.ThrowIfNull(v2);
        var result = new Vector(v2.Size);
        for (int i = 0; i < v2.data.Length; ++i)
        {
            result.data[i] = a * v2.data[i];
        }

        return result;
    }

    /// <summary>The element-by-element quotient.</summary>
    /// <param name="v1">The first vector.</param>
    /// <param name="v2">The second vector.</param>
    /// <returns>A new vector.</returns>
    /// <exception cref="ArgumentException">The sizes differ.</exception>
    public static Vector operator /(Vector v1, Vector v2)
    {
        RequireSameSize(v1, v2, "divided");
        var result = new Vector(v1.Size);
        for (int i = 0; i < v1.data.Length; ++i)
        {
            result.data[i] = v1.data[i] / v2.data[i];
        }

        return result;
    }

    /// <summary>The vector with every element divided by a scalar.</summary>
    /// <param name="v1">The vector.</param>
    /// <param name="a">The scalar.</param>
    /// <returns>A new vector.</returns>
    public static Vector operator /(Vector v1, double a)
    {
        ArgumentNullException.ThrowIfNull(v1);
        var result = new Vector(v1.Size);
        for (int i = 0; i < v1.data.Length; ++i)
        {
            result.data[i] = v1.data[i] / a;
        }

        return result;
    }

    /// <summary>A scalar divided by every element of the vector.</summary>
    /// <param name="a">The scalar.</param>
    /// <param name="v2">The vector.</param>
    /// <returns>A new vector.</returns>
    public static Vector operator /(double a, Vector v2)
    {
        ArgumentNullException.ThrowIfNull(v2);
        var result = new Vector(v2.Size);
        for (int i = 0; i < v2.data.Length; ++i)
        {
            result.data[i] = a / v2.data[i];
        }

        return result;
    }

    /// <summary>
    /// Whether two vectors have the same size and equal elements, compared with <c>==</c> as QuantLib does: a NaN
    /// element makes them unequal.
    /// </summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    /// <returns><see langword="true"/> when equal; two null references are equal.</returns>
    public static bool operator ==(Vector? left, Vector? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (left.data.Length != right.data.Length)
        {
            return false;
        }

        for (int i = 0; i < left.data.Length; ++i)
        {
            if (!(left.data[i] == right.data[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether two vectors differ: the negation of <c>==</c>.</summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    public static bool operator !=(Vector? left, Vector? right) => !(left == right);

    /// <summary>Adds another vector to this one, element by element, in place.</summary>
    /// <param name="v">The vector to add.</param>
    /// <exception cref="ArgumentException">The sizes differ.</exception>
    public void operator +=(Vector v)
    {
        RequireSameSize(this, v, "added");
        for (int i = 0; i < data.Length; ++i)
        {
            data[i] = data[i] + v.data[i];
        }
    }

    /// <summary>Adds a scalar to every element, in place.</summary>
    /// <param name="x">The scalar.</param>
    public void operator +=(double x)
    {
        for (int i = 0; i < data.Length; ++i)
        {
            data[i] = data[i] + x;
        }
    }

    /// <summary>Subtracts another vector from this one, element by element, in place.</summary>
    /// <param name="v">The vector to subtract.</param>
    /// <exception cref="ArgumentException">The sizes differ.</exception>
    public void operator -=(Vector v)
    {
        RequireSameSize(this, v, "subtracted");
        for (int i = 0; i < data.Length; ++i)
        {
            data[i] = data[i] - v.data[i];
        }
    }

    /// <summary>Subtracts a scalar from every element, in place.</summary>
    /// <param name="x">The scalar.</param>
    public void operator -=(double x)
    {
        for (int i = 0; i < data.Length; ++i)
        {
            data[i] = data[i] - x;
        }
    }

    /// <summary>Multiplies this vector by another, element by element, in place.</summary>
    /// <param name="v">The other vector.</param>
    /// <exception cref="ArgumentException">The sizes differ.</exception>
    public void operator *=(Vector v)
    {
        RequireSameSize(this, v, "multiplied");
        for (int i = 0; i < data.Length; ++i)
        {
            data[i] = data[i] * v.data[i];
        }
    }

    /// <summary>Multiplies every element by a scalar, in place.</summary>
    /// <param name="x">The scalar.</param>
    public void operator *=(double x)
    {
        for (int i = 0; i < data.Length; ++i)
        {
            data[i] = data[i] * x;
        }
    }

    /// <summary>Divides this vector by another, element by element, in place.</summary>
    /// <param name="v">The other vector.</param>
    /// <exception cref="ArgumentException">The sizes differ.</exception>
    public void operator /=(Vector v)
    {
        RequireSameSize(this, v, "divided");
        for (int i = 0; i < data.Length; ++i)
        {
            data[i] = data[i] / v.data[i];
        }
    }

    /// <summary>Divides every element by a scalar, in place.</summary>
    /// <param name="x">The scalar.</param>
    public void operator /=(double x)
    {
        for (int i = 0; i < data.Length; ++i)
        {
            data[i] = data[i] / x;
        }
    }

    /// <summary>The element at the given index, checked: QuantLib's <c>at</c>, which can also set it.</summary>
    /// <param name="i">The index.</param>
    /// <returns>A reference to the element.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is out of range.</exception>
    public ref double At(int i)
    {
        if (!(i >= 0 && i < data.Length))
        {
            throw new ArgumentOutOfRangeException(
                nameof(i),
                $"index ({i}) must be less than {data.Length}: array access out of range");
        }

        return ref data[i];
    }

    /// <summary>The first element, which can also be set.</summary>
    /// <returns>A reference to the first element.</returns>
    /// <exception cref="IndexOutOfRangeException">The vector is empty.</exception>
    public ref double Front() => ref data[0];

    /// <summary>The last element, which can also be set.</summary>
    /// <returns>A reference to the last element.</returns>
    /// <exception cref="IndexOutOfRangeException">The vector is empty.</exception>
    public ref double Back() => ref data[^1];

    /// <summary>
    /// Changes the size, keeping the elements that fit; new elements are zero, where QuantLib leaves them
    /// uninitialized.
    /// </summary>
    /// <param name="n">The new size.</param>
    /// <remarks>
    /// Shrinking moves the elements to new storage, where QuantLib keeps its storage, so a span from
    /// <see cref="AsSpan"/> taken before a resize no longer reads the vector.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The size is negative.</exception>
    public void Resize(int n)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        if (n != data.Length)
        {
            Array.Resize(ref data, n);
        }
    }

    /// <summary>Exchanges the contents of this vector with another's.</summary>
    /// <param name="from">The other vector.</param>
    public void Swap(Vector from)
    {
        ArgumentNullException.ThrowIfNull(from);
        (data, from.data) = (from.data, data);
    }

    /// <summary>A deep copy: QuantLib's copy constructor and assignment.</summary>
    /// <returns>A new vector with the same elements.</returns>
    public Vector Clone() => new(data);

    /// <summary>
    /// A span over the elements, through which an algorithm reads and writes them: QuantLib's <c>begin()</c> and
    /// <c>end()</c>. It reads this vector until the next <see cref="Resize"/> or <see cref="Swap"/>.
    /// </summary>
    /// <returns>The span.</returns>
    public Span<double> AsSpan() => data;

    /// <summary>Returns an enumerator over the elements, so that <c>foreach</c> works.</summary>
    /// <returns>The enumerator, whose elements can also be set (<c>foreach (ref double x in v)</c>).</returns>
    public Span<double>.Enumerator GetEnumerator() => data.AsSpan().GetEnumerator();

    /// <inheritdoc/>
    IEnumerator<double> IEnumerable<double>.GetEnumerator() => ((IEnumerable<double>)data).GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => data.GetEnumerator();

    /// <summary>
    /// Whether another vector has the same size and equal elements, each compared as <see cref="double.Equals(double)"/>
    /// does: a NaN equals a NaN, so a vector equals itself. <c>==</c> compares as QuantLib does, as <see cref="double"/>'s
    /// own <c>==</c> and <see cref="double.Equals(double)"/> differ.
    /// </summary>
    /// <param name="other">The other vector.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    public bool Equals(Vector? other)
    {
        if (other is null)
        {
            return false;
        }

        if (data.Length != other.data.Length)
        {
            return false;
        }

        for (int i = 0; i < data.Length; ++i)
        {
            if (!data[i].Equals(other.data[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as Vector);

    /// <summary>A hash of the elements, which changes when they do.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(data.Length);
        foreach (double x in data)
        {
            hash.Add(x);
        }

        return hash.ToHashCode();
    }

    /// <summary>The vector as QuantLib writes it, e.g. "[ 1; 2.5; 3 ]", each element as a C++ stream writes it.</summary>
    /// <returns>The text.</returns>
    public override string ToString()
    {
        var text = new StringBuilder("[ ");
        if (data.Length > 0)
        {
            for (int n = 0; n < data.Length - 1; ++n)
            {
                text.Append(DataFormattersFunctions.General(data[n])).Append("; ");
            }

            text.Append(DataFormattersFunctions.General(data[^1]));
        }

        return text.Append(" ]").ToString();
    }

    private static void RequireSameSize(
        Vector v1,
        Vector v2,
        string operation,
        [CallerArgumentExpression(nameof(v1))] string? name1 = null,
        [CallerArgumentExpression(nameof(v2))] string? name2 = null)
    {
        ArgumentNullException.ThrowIfNull(v1, name1);
        ArgumentNullException.ThrowIfNull(v2, name2);
        if (!(v1.data.Length == v2.data.Length))
        {
            throw new ArgumentException(
                $"arrays with different sizes ({v1.data.Length}, {v2.data.Length}) cannot be {operation}",
                name2);
        }
    }
}
