// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Numerics;

/// <summary>
/// The arithmetic in which an expression of QuantLib's written once over it evaluates: <see cref="RealDouble"/>,
/// QuantLib's own double operations and bits; <see cref="DoubleDouble"/>, about 31 digits, where double cancels; and
/// <see cref="ExtendedDouble"/>, double's mantissa with an unbounded exponent, where double overflows (the S-hes plan's
/// HR6 and HR11).
/// </summary>
/// <typeparam name="T">The implementing type.</typeparam>
/// <remarks>
/// A mixed operation with a <see cref="double"/> is the operation on the double converted, as C++ converts an
/// expression's integer and double literals.
/// </remarks>
internal interface IRealArithmetic<T>
    where T : struct, IRealArithmetic<T>
{
    /// <summary>Gets the value rounded to a double.</summary>
    double Value { get; }

    /// <summary>The value a double holds.</summary>
    /// <param name="x">The double.</param>
    /// <returns>The value.</returns>
    static abstract T Of(double x);

    /// <summary>The square root.</summary>
    /// <param name="x">The argument.</param>
    /// <returns>The square root.</returns>
    static abstract T Sqrt(T x);

    /// <summary>The exponential.</summary>
    /// <param name="x">The argument.</param>
    /// <returns>The exponential.</returns>
    static abstract T Exp(T x);

    /// <summary>The natural logarithm.</summary>
    /// <param name="x">The argument.</param>
    /// <returns>The logarithm.</returns>
    static abstract T Log(T x);

    /// <summary>An integer power, <c>std::pow(x, n)</c>.</summary>
    /// <param name="x">The base.</param>
    /// <param name="n">The exponent.</param>
    /// <returns>The power.</returns>
    static abstract T Pow(T x, int n);

    /// <summary>A real power, <c>std::pow(x, y)</c>; the expressions read y = 1.5 only.</summary>
    /// <param name="x">The base.</param>
    /// <param name="y">The exponent.</param>
    /// <returns>The power.</returns>
    static abstract T Pow(T x, double y);

    /// <summary>The sum.</summary>
    /// <param name="a">The first term.</param>
    /// <param name="b">The second term.</param>
    /// <returns>The sum.</returns>
    static abstract T operator +(T a, T b);

    /// <summary>The sum.</summary>
    /// <param name="a">The first term.</param>
    /// <param name="b">The second term.</param>
    /// <returns>The sum.</returns>
    static abstract T operator +(T a, double b);

    /// <summary>The sum.</summary>
    /// <param name="a">The first term.</param>
    /// <param name="b">The second term.</param>
    /// <returns>The sum.</returns>
    static abstract T operator +(double a, T b);

    /// <summary>The difference.</summary>
    /// <param name="a">The minuend.</param>
    /// <param name="b">The subtrahend.</param>
    /// <returns>The difference.</returns>
    static abstract T operator -(T a, T b);

    /// <summary>The difference.</summary>
    /// <param name="a">The minuend.</param>
    /// <param name="b">The subtrahend.</param>
    /// <returns>The difference.</returns>
    static abstract T operator -(T a, double b);

    /// <summary>The difference.</summary>
    /// <param name="a">The minuend.</param>
    /// <param name="b">The subtrahend.</param>
    /// <returns>The difference.</returns>
    static abstract T operator -(double a, T b);

    /// <summary>The negation.</summary>
    /// <param name="a">The argument.</param>
    /// <returns>The negation.</returns>
    static abstract T operator -(T a);

    /// <summary>The product.</summary>
    /// <param name="a">The first factor.</param>
    /// <param name="b">The second factor.</param>
    /// <returns>The product.</returns>
    static abstract T operator *(T a, T b);

    /// <summary>The product.</summary>
    /// <param name="a">The first factor.</param>
    /// <param name="b">The second factor.</param>
    /// <returns>The product.</returns>
    static abstract T operator *(T a, double b);

    /// <summary>The product.</summary>
    /// <param name="a">The first factor.</param>
    /// <param name="b">The second factor.</param>
    /// <returns>The product.</returns>
    static abstract T operator *(double a, T b);

    /// <summary>The quotient.</summary>
    /// <param name="a">The dividend.</param>
    /// <param name="b">The divisor.</param>
    /// <returns>The quotient.</returns>
    static abstract T operator /(T a, T b);

    /// <summary>The quotient.</summary>
    /// <param name="a">The dividend.</param>
    /// <param name="b">The divisor.</param>
    /// <returns>The quotient.</returns>
    static abstract T operator /(T a, double b);

    /// <summary>The quotient.</summary>
    /// <param name="a">The dividend.</param>
    /// <param name="b">The divisor.</param>
    /// <returns>The quotient.</returns>
    static abstract T operator /(double a, T b);
}
