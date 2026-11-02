// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using NanoQL.Time;

namespace NanoQL.Bind.Tests.Fixtures;

/// <summary>The tuple forms: a tuple of every element kind in, collections of tuples in and out, and the ones withheld.</summary>
public static class TupleKinds
{
    /// <summary>A tuple of nullable doubles, defaulted.</summary>
    /// <param name="cPoint">The tuple.</param>
    /// <returns>Its first element, or 0.</returns>
    public static double Single((double? point, double? density) cPoint = default) => cPoint.point ?? 0;

    /// <summary>A tuple of an integer, a long integer and a flag.</summary>
    /// <param name="x">The tuple.</param>
    /// <returns>Its count.</returns>
    public static int Scalars((int count, long total, bool flag) x) => x.count;

    /// <summary>A tuple of a string, a date and a period.</summary>
    /// <param name="x">The tuple.</param>
    /// <returns>The string's length.</returns>
    public static int Values((string name, Date when, Period tenor) x) => x.name.Length;

    /// <summary>A tuple of an enum value and a complex number.</summary>
    /// <param name="x">The tuple.</param>
    /// <returns>The enum's value.</returns>
    public static int Others((TimeUnit unit, Complex z) x) => (int)x.unit;

    /// <summary>A collection of tuples of nullable elements.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>Their count.</returns>
    public static int Rows(IReadOnlyList<(double? second, int? third, Date? when)> rows) => rows.Count;

    /// <summary>A collection of tuples holding objects, without names.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>Their count.</returns>
    public static int Objects(IEnumerable<(Calendar, double)> rows) => rows.Count();

    /// <summary>A collection of tuples returned.</summary>
    /// <returns>The pairs.</returns>
    public static IReadOnlyList<(int First, string Second)> Pairs() => [(1, "one")];

    /// <summary>A collection of tuples of a double, a flag and a date returned.</summary>
    /// <returns>The rows.</returns>
    public static IReadOnlyList<(double X, bool Flag, Date When)> Measures() => [];

    /// <summary>A collection of tuples of an enum value, a complex number and an object returned.</summary>
    /// <returns>The rows.</returns>
    public static IReadOnlyList<(TimeUnit Unit, Complex Z, Calendar Item)> Things() => [];

    /// <summary>A collection of nullable tuples returned.</summary>
    /// <returns>The pairs, some null.</returns>
    public static IReadOnlyList<(int Left, int Right)?> Gaps() => [null, (1, 2)];

    /// <summary>A collection of tuples whose element named count takes an underscore beside the collection's count.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The rows.</returns>
    public static IReadOnlyList<(Period Tenor, long Count)> Counted(IReadOnlyList<(Period tenor, long count)> rows) => rows;

    /// <summary>A returned collection's tuple with a nullable element: withheld.</summary>
    /// <returns>The rows.</returns>
    public static IReadOnlyList<(int A, double? B)> NullableOut() => [];

    /// <summary>A collection of nullable tuples in: withheld.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>Their count.</returns>
    public static int NullableIn(IReadOnlyList<(int a, int b)?> rows) => rows.Count;

    /// <summary>A tuple holding a tuple: withheld.</summary>
    /// <param name="x">The tuple.</param>
    /// <returns>Its first element.</returns>
    public static int Nested((int a, (int b, int c) d) x) => x.a;
}
