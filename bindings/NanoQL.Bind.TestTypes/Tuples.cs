// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Time;
using Complex = System.Numerics.Complex;

namespace NanoQL.Bind.TestTypes;

/// <summary>
/// The tuple forms both ways (docs/bindings.md, "Values"): a tuple parameter, a collection of tuples in, and one
/// returned, a nullable tuple's among them. Each function returns what it is given, or a text of what crossed.
/// </summary>
public static class Tuples
{
    /// <summary>A tuple of nullable doubles, C#'s default (both null) when left out.</summary>
    /// <param name="cPoint">The tuple.</param>
    /// <returns>The tuple.</returns>
    public static (double? Point, double? Density) Single((double? point, double? density) cPoint = default) => cPoint;

    /// <summary>A tuple of an integer, a long integer and a flag, as text.</summary>
    /// <param name="x">The tuple.</param>
    /// <returns>Its elements, each as invariant text, joined by <c>|</c>.</returns>
    public static string Scalars((int count, long total, bool flag) x) =>
        string.Join("|", x.count.ToString(CultureInfo.InvariantCulture), x.total.ToString(CultureInfo.InvariantCulture), x.flag ? "true" : "false");

    /// <summary>A tuple of a string, a date and a period, as text; a null string as <c>null</c>.</summary>
    /// <param name="x">The tuple.</param>
    /// <returns>Its elements joined by <c>|</c>: the string, the date's serial number and the period.</returns>
    public static string Labels((string? name, Date when, Period tenor) x) =>
        string.Join("|", x.name ?? "null", x.when.SerialNumber.ToString(CultureInfo.InvariantCulture), x.tenor.ToString());

    /// <summary>A tuple of an enum value and a complex number, as text.</summary>
    /// <param name="x">The tuple.</param>
    /// <returns>Its elements joined by <c>|</c>: the enum's value and the number's parts.</returns>
    public static string Kinds((Weekday day, Complex z) x) =>
        string.Join("|", ((int)x.day).ToString(CultureInfo.InvariantCulture), x.z.Real.ToString("R", CultureInfo.InvariantCulture), x.z.Imaginary.ToString("R", CultureInfo.InvariantCulture));

    /// <summary>A collection of objects with weights, returned as given.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The rows.</returns>
    public static IReadOnlyList<(EchoObject Item, double Weight)> Rows(IReadOnlyList<(EchoObject item, double weight)> rows) => rows;

    /// <summary>A collection of tuples of nullable elements, as text: <c>null</c> for a null collection.</summary>
    /// <param name="rows">The rows, or null.</param>
    /// <returns>Each row's elements joined by <c>:</c>, a null one as <c>-</c>, the rows by <c>;</c>.</returns>
    public static string Describe(IReadOnlyList<(double? second, int? third, Date? when)>? rows) =>
        rows is null ? "null" : string.Join(";", rows.Select(r => string.Join(
            ":",
            r.second?.ToString("R", CultureInfo.InvariantCulture) ?? "-",
            r.third?.ToString(CultureInfo.InvariantCulture) ?? "-",
            r.when?.SerialNumber.ToString(CultureInfo.InvariantCulture) ?? "-")));

    /// <summary>A collection of names and weekdays, returned as given.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The rows.</returns>
    public static ImmutableArray<(string Name, Weekday Day)> Days(IEnumerable<(string name, Weekday day)> rows) => [.. rows];

    /// <summary>A collection of periods, long integers and flags, returned as given.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The rows.</returns>
    public static IReadOnlyList<(Period Tenor, long Count, bool Flag)> Terms(IReadOnlyList<(Period tenor, long count, bool flag)> rows) => rows;

    /// <summary>A collection of complex numbers and dates, returned as given.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The rows.</returns>
    public static IReadOnlyList<(Complex Z, Date When)> Numbers(IReadOnlyList<(Complex z, Date when)> rows) => rows;

    /// <summary>Pairs returned with every <paramref name="every"/>-th one null, from the first: a nullable tuple's collection.</summary>
    /// <param name="pairs">The pairs.</param>
    /// <param name="every">How often a pair is null; 0 for never.</param>
    /// <returns>The pairs, some null.</returns>
    public static IReadOnlyList<(int Left, int Right)?> Gaps(IReadOnlyList<(int left, int right)> pairs, int every) =>
        [.. pairs.Select((p, i) => every > 0 && i % every == 0 ? ((int, int)?)null : p)];
}
