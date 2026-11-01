// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Numerics;
using NanoQL.Numerics;
using NanoQL.Time;
using Vector = NanoQL.Numerics.Vector;

namespace NanoQL.Bind.Tests.Fixtures;

/// <summary>A member per kind and per naming rule, whose C declarations the tests read.</summary>
public static class Kinds
{
    /// <summary>Dates in, dates out.</summary>
    /// <param name="x">The dates.</param>
    /// <returns>The dates.</returns>
    public static ImmutableArray<Date> Dates(IReadOnlyList<Date> x) => [.. x];

    /// <summary>A nullable date and a nullable double.</summary>
    /// <param name="d">The date.</param>
    /// <param name="x">The double.</param>
    /// <returns>The double.</returns>
    public static double? Nullables(Date? d, double? x) => d.HasValue ? x : null;

    /// <summary>A period and an enum.</summary>
    /// <param name="p">The period.</param>
    /// <param name="u">The unit.</param>
    /// <returns>The period.</returns>
    public static Period PeriodAndEnum(Period p, TimeUnit u) => new(p.Length, u);

    /// <summary>A matrix and a vector.</summary>
    /// <param name="m">The matrix.</param>
    /// <param name="v">The vector.</param>
    /// <returns>The vector.</returns>
    public static Vector MatrixAndVector(Matrix m, Vector v) => v;

    /// <summary>A jagged array.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The rows.</returns>
    public static ImmutableArray<ImmutableArray<double>> Jagged(IReadOnlyList<IReadOnlyList<double>> rows) => [.. rows.Select(r => r.ToImmutableArray())];

    /// <summary>A dictionary.</summary>
    /// <param name="m">The entries.</param>
    /// <returns>The entries.</returns>
    public static ImmutableSortedDictionary<Date, double> Map(IReadOnlyDictionary<Date, double> m) => m.ToImmutableSortedDictionary();

    /// <summary>A tuple, its elements named.</summary>
    /// <param name="x">The value.</param>
    /// <returns>Its parts.</returns>
    public static (int Whole, double Fraction) Split(double x) => ((int)x, x - (int)x);

    /// <summary>A complex number.</summary>
    /// <param name="z">The value.</param>
    /// <returns>The value.</returns>
    public static Complex Conjugate(Complex z) => Complex.Conjugate(z);

    /// <summary>Parameters named as C, C++ and the generated code's own names.</summary>
    /// <param name="default">A keyword.</param>
    /// <param name="result">The result's name.</param>
    /// <param name="self">The instance's name.</param>
    /// <returns>Their sum.</returns>
    public static double Keywords(double @default, double result, double self) => @default + result + self;

    /// <summary>A member nanoql deprecates.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The value.</returns>
    [Obsolete("use Kinds.Dates")]
    public static double Old(double x) => x;

    /// <summary>An element of a fixed array.</summary>
    /// <param name="i">The index.</param>
    /// <returns>The element.</returns>
    /// <exception cref="IndexOutOfRangeException">The index is outside the array.</exception>
    public static double Element(int i) => new[] { 1.0 }[i];
}
