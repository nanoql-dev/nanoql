// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Numerics;

/// <summary>
/// A double as an <see cref="IRealArithmetic{T}"/>: each operation the IEEE double operation QuantLib's expression
/// performs, so an expression evaluated here gives QuantLib's bits.
/// </summary>
/// <param name="Value">The double.</param>
internal readonly record struct RealDouble(double Value) : IRealArithmetic<RealDouble>
{
    /// <inheritdoc/>
    public static RealDouble Of(double x) => new(x);

    /// <inheritdoc/>
    public static RealDouble Sqrt(RealDouble x) => new(Math.Sqrt(x.Value));

    /// <inheritdoc/>
    public static RealDouble Exp(RealDouble x) => new(Math.Exp(x.Value));

    /// <inheritdoc/>
    public static RealDouble Log(RealDouble x) => new(Math.Log(x.Value));

    /// <inheritdoc/>
    public static RealDouble Pow(RealDouble x, int n) => new(Math.Pow(x.Value, n));

    /// <inheritdoc/>
    public static RealDouble Pow(RealDouble x, double y) => new(Math.Pow(x.Value, y));

    /// <inheritdoc/>
    public static RealDouble operator +(RealDouble a, RealDouble b) => new(a.Value + b.Value);

    /// <inheritdoc/>
    public static RealDouble operator +(RealDouble a, double b) => new(a.Value + b);

    /// <inheritdoc/>
    public static RealDouble operator +(double a, RealDouble b) => new(a + b.Value);

    /// <inheritdoc/>
    public static RealDouble operator -(RealDouble a, RealDouble b) => new(a.Value - b.Value);

    /// <inheritdoc/>
    public static RealDouble operator -(RealDouble a, double b) => new(a.Value - b);

    /// <inheritdoc/>
    public static RealDouble operator -(double a, RealDouble b) => new(a - b.Value);

    /// <inheritdoc/>
    public static RealDouble operator -(RealDouble a) => new(-a.Value);

    /// <inheritdoc/>
    public static RealDouble operator *(RealDouble a, RealDouble b) => new(a.Value * b.Value);

    /// <inheritdoc/>
    public static RealDouble operator *(RealDouble a, double b) => new(a.Value * b);

    /// <inheritdoc/>
    public static RealDouble operator *(double a, RealDouble b) => new(a * b.Value);

    /// <inheritdoc/>
    public static RealDouble operator /(RealDouble a, RealDouble b) => new(a.Value / b.Value);

    /// <inheritdoc/>
    public static RealDouble operator /(RealDouble a, double b) => new(a.Value / b);

    /// <inheritdoc/>
    public static RealDouble operator /(double a, RealDouble b) => new(a / b.Value);
}
