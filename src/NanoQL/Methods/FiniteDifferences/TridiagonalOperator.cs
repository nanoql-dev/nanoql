// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/tridiagonaloperator.hpp ql/methods/finitedifferences/tridiagonaloperator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The base implementation of a tridiagonal operator: its lower, main and upper diagonals, applied to a vector or
/// solved for a right-hand side.
/// </summary>
/// <remarks>
/// <para>
/// A working type: mutable, owned by one computation and not thread-safe. The row setters and
/// <see cref="SetTime"/> change it; the operators return new operators. <see cref="Clone"/> is the deep copy, where
/// QuantLib copies an operator by value.
/// </para>
/// <para>
/// An extension point, as in QuantLib: the finite-difference operators derive from it. A derived operator reads and
/// sets the diagonals through the protected fields, and makes itself time-dependent by setting
/// <see cref="timeSetter"/>. To use real time-dependent algebra, a time-dependent derived operator overloads the
/// operators, which here build operators without a time setter.
/// </para>
/// </remarks>
public class TridiagonalOperator
{
    /// <summary>The size: the length of the main diagonal.</summary>
    protected int n;

    /// <summary>The main diagonal, of size <see cref="n"/>.</summary>
    protected Vector diagonal;

    /// <summary>The lower diagonal, of size <see cref="n"/> − 1: element i is row i + 1's.</summary>
    protected Vector lowerDiagonal;

    /// <summary>The upper diagonal, of size <see cref="n"/> − 1: element i is row i's.</summary>
    protected Vector upperDiagonal;

    /// <summary>The time-setting logic, or <see langword="null"/> for an operator that doesn't depend on time.</summary>
    protected TridiagonalOperatorTimeSetter? timeSetter;

    /// <summary>Initializes a new instance of the <see cref="TridiagonalOperator"/> class of the given size.</summary>
    /// <param name="size">The size: 0, or at least 2.</param>
    /// <remarks>The diagonals' elements are zero, where QuantLib leaves them uninitialized.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The size is negative.</exception>
    /// <exception cref="NanoQLException">The size is 1.</exception>
    public TridiagonalOperator(int size = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        if (size >= 2)
        {
            n = size;
            diagonal = new Vector(size);
            lowerDiagonal = new Vector(size - 1);
            upperDiagonal = new Vector(size - 1);
        }
        else if (size == 0)
        {
            n = 0;
            diagonal = new Vector(0);
            lowerDiagonal = new Vector(0);
            upperDiagonal = new Vector(0);
        }
        else
        {
            throw new NanoQLException($"invalid size ({size}) for tridiagonal operator (must be null or >= 2)");
        }
    }

    /// <summary>Initializes a new instance of the <see cref="TridiagonalOperator"/> class from its diagonals, which it copies.</summary>
    /// <param name="low">The lower diagonal, one shorter than the main one.</param>
    /// <param name="mid">The main diagonal.</param>
    /// <param name="high">The upper diagonal, one shorter than the main one.</param>
    /// <exception cref="ArgumentException">A diagonal has the wrong size.</exception>
    public TridiagonalOperator(Vector low, Vector mid, Vector high)
    {
        ArgumentNullException.ThrowIfNull(low);
        ArgumentNullException.ThrowIfNull(mid);
        ArgumentNullException.ThrowIfNull(high);
        n = mid.Size;
        diagonal = mid.Clone();
        lowerDiagonal = low.Clone();
        upperDiagonal = high.Clone();

        // QuantLib's n_ - 1 is a Size, so for an empty main diagonal it wraps to 2^64 - 1, which no size equals.
        ulong expected = unchecked((ulong)(n - 1));
        if (!(low.Size == n - 1))
        {
            throw new ArgumentException($"low diagonal vector of size {low.Size} instead of {expected}", nameof(low));
        }

        if (!(high.Size == n - 1))
        {
            throw new ArgumentException($"high diagonal vector of size {high.Size} instead of {expected}", nameof(high));
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TridiagonalOperator"/> class as a copy of another: its diagonals
    /// copied, and its time setter, which is shared, kept.
    /// </summary>
    /// <param name="from">The operator to copy.</param>
    protected TridiagonalOperator(TridiagonalOperator from)
    {
        ArgumentNullException.ThrowIfNull(from);
        n = from.n;
        diagonal = from.diagonal.Clone();
        lowerDiagonal = from.lowerDiagonal.Clone();
        upperDiagonal = from.upperDiagonal.Clone();
        timeSetter = from.timeSetter;
    }

    /// <summary>Gets the size: the number of rows.</summary>
    public int Size => n;

    /// <summary>Gets a value indicating whether the operator has a time setter, which <see cref="SetTime"/> calls.</summary>
    public bool IsTimeDependent => timeSetter is not null;

    /// <summary>The operator itself, a copy that keeps its time setter: QuantLib's unary <c>+</c>.</summary>
    /// <param name="D">The operator.</param>
    /// <returns>A copy.</returns>
    public static TridiagonalOperator operator +(TridiagonalOperator D)
    {
        ArgumentNullException.ThrowIfNull(D);
        return D.Clone();
    }

    /// <summary>The operator negated, with no time setter.</summary>
    /// <param name="D">The operator.</param>
    /// <returns>A new operator.</returns>
    public static TridiagonalOperator operator -(TridiagonalOperator D)
    {
        ArgumentNullException.ThrowIfNull(D);
        Vector low = -D.lowerDiagonal, mid = -D.diagonal, high = -D.upperDiagonal;
        return new TridiagonalOperator(low, mid, high);
    }

    /// <summary>The sum, diagonal by diagonal, with no time setter.</summary>
    /// <param name="D1">The first operator.</param>
    /// <param name="D2">The second operator, of the same size.</param>
    /// <returns>A new operator.</returns>
    /// <exception cref="ArgumentException">The sizes differ.</exception>
    public static TridiagonalOperator operator +(TridiagonalOperator D1, TridiagonalOperator D2)
    {
        ArgumentNullException.ThrowIfNull(D1);
        ArgumentNullException.ThrowIfNull(D2);
        Vector low = D1.lowerDiagonal + D2.lowerDiagonal,
            mid = D1.diagonal + D2.diagonal,
            high = D1.upperDiagonal + D2.upperDiagonal;
        return new TridiagonalOperator(low, mid, high);
    }

    /// <summary>The difference, diagonal by diagonal, with no time setter.</summary>
    /// <param name="D1">The first operator.</param>
    /// <param name="D2">The second operator, of the same size.</param>
    /// <returns>A new operator.</returns>
    /// <exception cref="ArgumentException">The sizes differ.</exception>
    public static TridiagonalOperator operator -(TridiagonalOperator D1, TridiagonalOperator D2)
    {
        ArgumentNullException.ThrowIfNull(D1);
        ArgumentNullException.ThrowIfNull(D2);
        Vector low = D1.lowerDiagonal - D2.lowerDiagonal,
            mid = D1.diagonal - D2.diagonal,
            high = D1.upperDiagonal - D2.upperDiagonal;
        return new TridiagonalOperator(low, mid, high);
    }

    /// <summary>A scalar times the operator, with no time setter.</summary>
    /// <param name="a">The scalar.</param>
    /// <param name="D">The operator.</param>
    /// <returns>A new operator, each diagonal element multiplied by <paramref name="a"/>.</returns>
    public static TridiagonalOperator operator *(double a, TridiagonalOperator D)
    {
        ArgumentNullException.ThrowIfNull(D);
        Vector low = D.lowerDiagonal * a, mid = D.diagonal * a, high = D.upperDiagonal * a;
        return new TridiagonalOperator(low, mid, high);
    }

    /// <summary>The operator times a scalar, with no time setter.</summary>
    /// <param name="D">The operator.</param>
    /// <param name="a">The scalar.</param>
    /// <returns>A new operator, each diagonal element multiplied by <paramref name="a"/>.</returns>
    public static TridiagonalOperator operator *(TridiagonalOperator D, double a)
    {
        ArgumentNullException.ThrowIfNull(D);
        Vector low = D.lowerDiagonal * a, mid = D.diagonal * a, high = D.upperDiagonal * a;
        return new TridiagonalOperator(low, mid, high);
    }

    /// <summary>The operator divided by a scalar, with no time setter.</summary>
    /// <param name="D">The operator.</param>
    /// <param name="a">The scalar.</param>
    /// <returns>A new operator, each diagonal element divided by <paramref name="a"/>.</returns>
    public static TridiagonalOperator operator /(TridiagonalOperator D, double a)
    {
        ArgumentNullException.ThrowIfNull(D);
        Vector low = D.lowerDiagonal / a, mid = D.diagonal / a, high = D.upperDiagonal / a;
        return new TridiagonalOperator(low, mid, high);
    }

    /// <summary>The identity operator of the given size.</summary>
    /// <param name="size">The size.</param>
    /// <returns>An operator whose main diagonal is 1 and whose other diagonals are 0.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The size is less than 1.</exception>
    public static TridiagonalOperator Identity(int size) =>
        new(
            new Vector(size - 1, 0.0), // lower diagonal
            new Vector(size, 1.0), // diagonal
            new Vector(size - 1, 0.0)); // upper diagonal

    /// <summary>Applies the operator to a vector.</summary>
    /// <param name="v">The vector, of the operator's size.</param>
    /// <returns>The product, a new vector.</returns>
    /// <exception cref="NanoQLException">The operator has size 0, or size 1, where QuantLib reads past its arrays.</exception>
    /// <exception cref="ArgumentException">The vector has the wrong size.</exception>
    public Vector ApplyTo(Vector v)
    {
        ArgumentNullException.ThrowIfNull(v);
        if (!(n != 0))
        {
            throw new NanoQLException("uninitialized TridiagonalOperator");
        }

        RequireSizeTwo("applyTo");

        if (!(v.Size == n))
        {
            throw new ArgumentException($"vector of the wrong size {v.Size} instead of {n}", nameof(v));
        }

        var result = new Vector(n);
        for (int i = 0; i < n; ++i)
        {
            result[i] = diagonal[i] * v[i];
        }

        // matricial product
        result[0] += upperDiagonal[0] * v[1];
        for (int j = 1; j <= n - 2; j++)
        {
            result[j] += (lowerDiagonal[j - 1] * v[j - 1]) + (upperDiagonal[j] * v[j + 1]);
        }

        result[n - 1] += lowerDiagonal[n - 2] * v[n - 2];

        return result;
    }

    /// <summary>Solves the linear system for a right-hand side.</summary>
    /// <param name="rhs">The right-hand side, of the operator's size.</param>
    /// <returns>The solution, a new vector.</returns>
    /// <exception cref="NanoQLException">The operator has size 0, or a pivot is close to zero.</exception>
    /// <exception cref="ArgumentException">The right-hand side has the wrong size.</exception>
    public Vector SolveFor(Vector rhs)
    {
        ArgumentNullException.ThrowIfNull(rhs);
        var result = new Vector(rhs.Size);
        SolveFor(rhs, result);
        return result;
    }

    /// <summary>
    /// Solves the linear system for a right-hand side into a given vector, which may be the right-hand side itself,
    /// which then changes.
    /// </summary>
    /// <param name="rhs">The right-hand side, of the operator's size.</param>
    /// <param name="result">The vector the solution is written into, at least of the operator's size.</param>
    /// <exception cref="NanoQLException">The operator has size 0, or size 1, where QuantLib reads past its arrays, or a
    /// pivot is close to zero.</exception>
    /// <exception cref="ArgumentException">The right-hand side has the wrong size.</exception>
    public void SolveFor(Vector rhs, Vector result)
    {
        ArgumentNullException.ThrowIfNull(rhs);
        ArgumentNullException.ThrowIfNull(result);
        if (!(n != 0))
        {
            throw new NanoQLException("uninitialized TridiagonalOperator");
        }

        RequireSizeTwo("solveFor");

        if (!(rhs.Size == n))
        {
            throw new ArgumentException($"rhs vector of size {rhs.Size} instead of {n}", nameof(rhs));
        }

        double bet = diagonal[0];
        if (ComparisonFunctions.Close(bet, 0.0))
        {
            throw new NanoQLException($"diagonal's first element ({Show(bet)}) cannot be close to zero");
        }

        // QuantLib's mutable temp_ member, a local here.
        var temp = new double[n];
        result[0] = rhs[0] / bet;
        for (int j = 1; j <= n - 1; ++j)
        {
            temp[j] = upperDiagonal[j - 1] / bet;
            bet = diagonal[j] - (lowerDiagonal[j - 1] * temp[j]);
            if (ComparisonFunctions.Close(bet, 0.0))
            {
                throw new NanoQLException("division by zero");
            }

            result[j] = (rhs[j] - (lowerDiagonal[j - 1] * result[j - 1])) / bet;
        }

        for (int j = n - 2; j > 0; --j)
        {
            result[j] -= temp[j + 1] * result[j + 1];
        }

        result[0] -= temp[1] * result[1];
    }

    /// <summary>Solves the linear system for a right-hand side by successive over-relaxation.</summary>
    /// <param name="rhs">The right-hand side, of the operator's size, which is also the first guess.</param>
    /// <param name="tol">The tolerance on the sum of the squared corrections of a sweep.</param>
    /// <returns>The solution, a new vector.</returns>
    /// <exception cref="NanoQLException">The operator has size 0, or size 1, where QuantLib reads past its arrays, or
    /// 100,000 sweeps don't reach the tolerance.</exception>
    /// <exception cref="ArgumentException">The right-hand side has the wrong size.</exception>
    public Vector SOR(Vector rhs, double tol)
    {
        ArgumentNullException.ThrowIfNull(rhs);
        if (!(n != 0))
        {
            throw new NanoQLException("uninitialized TridiagonalOperator");
        }

        RequireSizeTwo("SOR");

        if (!(rhs.Size == n))
        {
            throw new ArgumentException($"rhs vector of size {rhs.Size} instead of {n}", nameof(rhs));
        }

        // initial guess
        Vector result = rhs.Clone();

        // solve tridiagonal system with SOR technique
        double omega = 1.5;
        double err = 2.0 * tol;
        double temp;
        for (int sorIteration = 0; err > tol; ++sorIteration)
        {
            if (!(sorIteration < 100000))
            {
                throw new NanoQLException(
                    $"tolerance ({Show(tol)}) not reached in {sorIteration} iterations. The error still is {Show(err)}");
            }

            temp = omega * (rhs[0] - (upperDiagonal[0] * result[1]) - (diagonal[0] * result[0])) / diagonal[0];
            err = temp * temp;
            result[0] += temp;
            int i;
            for (i = 1; i < n - 1; ++i)
            {
                temp = omega * (rhs[i] - (upperDiagonal[i] * result[i + 1]) - (diagonal[i] * result[i])
                    - (lowerDiagonal[i - 1] * result[i - 1])) / diagonal[i];
                err += temp * temp;
                result[i] += temp;
            }

            temp = omega * (rhs[i] - (diagonal[i] * result[i]) - (lowerDiagonal[i - 1] * result[i - 1])) / diagonal[i];
            err += temp * temp;
            result[i] += temp;
        }

        return result;
    }

    /// <summary>A copy of the lower diagonal.</summary>
    /// <returns>A new vector, of size <see cref="Size"/> − 1.</returns>
    public Vector LowerDiagonal() => lowerDiagonal.Clone();

    /// <summary>A copy of the main diagonal.</summary>
    /// <returns>A new vector, of size <see cref="Size"/>.</returns>
    public Vector Diagonal() => diagonal.Clone();

    /// <summary>A copy of the upper diagonal.</summary>
    /// <returns>A new vector, of size <see cref="Size"/> − 1.</returns>
    public Vector UpperDiagonal() => upperDiagonal.Clone();

    /// <summary>Sets the first row's main and upper elements.</summary>
    /// <param name="valB">The main diagonal's element.</param>
    /// <param name="valC">The upper diagonal's element.</param>
    public void SetFirstRow(double valB, double valC)
    {
        diagonal[0] = valB;
        upperDiagonal[0] = valC;
    }

    /// <summary>Sets an inner row's three elements.</summary>
    /// <param name="i">The row, from 1 to <see cref="Size"/> − 2.</param>
    /// <param name="valA">The lower diagonal's element.</param>
    /// <param name="valB">The main diagonal's element.</param>
    /// <param name="valC">The upper diagonal's element.</param>
    /// <exception cref="ArgumentOutOfRangeException">The row is the first, the last or out of range.</exception>
    public void SetMidRow(int i, double valA, double valB, double valC)
    {
        if (!(i >= 1 && i <= n - 2))
        {
            throw new ArgumentOutOfRangeException(nameof(i), "out of range in TridiagonalSystem::setMidRow");
        }

        lowerDiagonal[i - 1] = valA;
        diagonal[i] = valB;
        upperDiagonal[i] = valC;
    }

    /// <summary>Sets every inner row's three elements to the same values.</summary>
    /// <param name="valA">The lower diagonal's element.</param>
    /// <param name="valB">The main diagonal's element.</param>
    /// <param name="valC">The upper diagonal's element.</param>
    public void SetMidRows(double valA, double valB, double valC)
    {
        for (int i = 1; i <= n - 2; i++)
        {
            lowerDiagonal[i - 1] = valA;
            diagonal[i] = valB;
            upperDiagonal[i] = valC;
        }
    }

    /// <summary>Sets the last row's lower and main elements.</summary>
    /// <param name="valA">The lower diagonal's element.</param>
    /// <param name="valB">The main diagonal's element.</param>
    public void SetLastRow(double valA, double valB)
    {
        lowerDiagonal[n - 2] = valA;
        diagonal[n - 1] = valB;
    }

    /// <summary>Sets the time, through the time setter; does nothing for an operator without one.</summary>
    /// <param name="t">The time.</param>
    public void SetTime(double t)
    {
        timeSetter?.SetTime(t, this);
    }

    /// <summary>
    /// Exchanges the contents of this operator with another's, the time setters included: QuantLib's member
    /// <c>swap</c> and its free <c>swap(TridiagonalOperator&amp;, TridiagonalOperator&amp;)</c>.
    /// </summary>
    /// <param name="from">The other operator.</param>
    public void Swap(TridiagonalOperator from)
    {
        ArgumentNullException.ThrowIfNull(from);
        (n, from.n) = (from.n, n);
        (diagonal, from.diagonal) = (from.diagonal, diagonal);
        (lowerDiagonal, from.lowerDiagonal) = (from.lowerDiagonal, lowerDiagonal);
        (upperDiagonal, from.upperDiagonal) = (from.upperDiagonal, upperDiagonal);
        (timeSetter, from.timeSetter) = (from.timeSetter, timeSetter);
    }

    /// <summary>A deep copy of the diagonals, sharing the time setter: QuantLib's copy constructor and assignment.</summary>
    /// <returns>A new operator.</returns>
    public TridiagonalOperator Clone() => new(this);

    // A divergence: QuantLib accepts an operator of size 1 (identity(1), or three diagonals of sizes 0, 1, 0), but its
    // applyTo, solveFor and SOR then read past the arrays (upperDiagonal_[0], v[1], temp_[1]; solveFor's n_-2
    // wraps), a result C++ leaves undefined. nanoql refuses it.
    private void RequireSizeTwo(string operation)
    {
        if (n == 1)
        {
            throw new NanoQLException($"{operation} needs a tridiagonal operator of size 2 or more, not 1");
        }
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);
}
