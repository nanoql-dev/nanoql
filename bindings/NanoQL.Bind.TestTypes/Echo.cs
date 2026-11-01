// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using Complex = System.Numerics.Complex;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Bind.TestTypes;

/// <summary>Each kind of the C ABI both ways: every function returns what it is given, or what it computes from it plainly.</summary>
public static class Echo
{
    /// <summary>A double.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The value.</returns>
    public static double Double(double x) => x;

    /// <summary>An integer.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The value.</returns>
    public static int Int(int x) => x;

    /// <summary>A long integer.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The value.</returns>
    public static long Long(long x) => x;

    /// <summary>A boolean.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The value.</returns>
    public static bool Bool(bool x) => x;

    /// <summary>A string; null stays null.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The value.</returns>
    public static string? String(string? x) => x;

    /// <summary>A string's length in UTF-16 code units, to show what crossed.</summary>
    /// <param name="x">The value.</param>
    /// <returns>Its length; -1 for null.</returns>
    public static int StringLength(string? x) => x?.Length ?? -1;

    /// <summary>A date.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The value.</returns>
    public static Date Date(Date x) => x;

    /// <summary>A nullable date.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The value.</returns>
    public static Date? OptDate(Date? x) => x;

    /// <summary>A period.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The value.</returns>
    public static Period Period(Period x) => x;

    /// <summary>A nullable period.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The value.</returns>
    public static Period? OptPeriod(Period? x) => x;

    /// <summary>An enum.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The value.</returns>
    public static TimeUnit Enum(TimeUnit x) => x;

    /// <summary>A nullable enum.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The value.</returns>
    public static TimeUnit? OptEnum(TimeUnit? x) => x;

    /// <summary>A complex number.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The value.</returns>
    public static Complex Complex(Complex x) => x;

    /// <summary>A nullable double.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The value.</returns>
    public static double? OptDouble(double? x) => x;

    /// <summary>A nullable integer.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The value.</returns>
    public static int? OptInt(int? x) => x;

    /// <summary>A nullable boolean.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The value.</returns>
    public static bool? OptBool(bool? x) => x;

    /// <summary>A nullable long integer.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The value.</returns>
    public static long? OptLong(long? x) => x;

    /// <summary>A nullable complex number.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The value.</returns>
    public static Complex? OptComplex(Complex? x) => x;

    /// <summary>A nanoql struct behind a handle.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The value.</returns>
    public static Money Money(Money x) => x;

    /// <summary>A nanoql struct behind a handle, or none.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The value.</returns>
    public static Money? OptMoney(Money? x) => x;

    /// <summary>Doubles read in place from the caller's buffer.</summary>
    /// <param name="x">The values.</param>
    /// <returns>A copy.</returns>
    public static ImmutableArray<double> Doubles(ReadOnlySpan<double> x) => [.. x];

    /// <summary>Doubles copied in; null gives -1.</summary>
    /// <param name="x">The values.</param>
    /// <returns>Their count.</returns>
    public static int DoublesCount(IEnumerable<double>? x) => x?.Count() ?? -1;

    /// <summary>Integers.</summary>
    /// <param name="x">The values.</param>
    /// <returns>The values.</returns>
    public static int[] Ints(IReadOnlyList<int> x) => [.. x];

    /// <summary>Booleans.</summary>
    /// <param name="x">The values.</param>
    /// <returns>The values.</returns>
    public static ImmutableArray<bool> Bools(IEnumerable<bool> x) => [.. x];

    /// <summary>Dates.</summary>
    /// <param name="x">The values.</param>
    /// <returns>The values.</returns>
    public static ImmutableArray<Date> Dates(ImmutableArray<Date> x) => x;

    /// <summary>Nullable doubles: values and has-value flags.</summary>
    /// <param name="x">The values.</param>
    /// <returns>The values.</returns>
    public static ImmutableArray<double?> OptDoubles(IEnumerable<double?> x) => [.. x];

    /// <summary>Periods.</summary>
    /// <param name="x">The values.</param>
    /// <returns>The values.</returns>
    public static ImmutableArray<Period> Periods(IReadOnlyList<Period> x) => [.. x];

    /// <summary>Enum values.</summary>
    /// <param name="x">The values.</param>
    /// <returns>The values.</returns>
    public static ImmutableArray<TimeUnit> Enums(IEnumerable<TimeUnit> x) => [.. x];

    /// <summary>Strings.</summary>
    /// <param name="x">The values.</param>
    /// <returns>The values.</returns>
    public static ImmutableArray<string> Strings(IEnumerable<string> x) => [.. x];

    /// <summary>Complex numbers.</summary>
    /// <param name="x">The values.</param>
    /// <returns>The values.</returns>
    public static ImmutableArray<Complex> Complexes(IReadOnlyList<Complex> x) => [.. x];

    /// <summary>Objects: an array of handles in, of new handles out.</summary>
    /// <param name="x">The objects.</param>
    /// <returns>The objects.</returns>
    public static ImmutableArray<EchoObject> Objects(ImmutableArray<EchoObject> x) => x;

    /// <summary>A sorted set of dates, as an array in its order.</summary>
    /// <param name="x">The dates.</param>
    /// <returns>The set.</returns>
    public static ImmutableSortedSet<Date> DateSet(IEnumerable<Date> x) => [.. x];

    /// <summary>A jagged array: a flat array and the rows' lengths.</summary>
    /// <param name="x">The rows.</param>
    /// <returns>The rows.</returns>
    public static ImmutableArray<ImmutableArray<double>> Jagged(IReadOnlyList<IReadOnlyList<double>> x) => [.. x.Select(r => r.ToImmutableArray())];

    /// <summary>A dictionary: keys and values.</summary>
    /// <param name="x">The entries.</param>
    /// <returns>The entries, sorted by date.</returns>
    public static ImmutableSortedDictionary<Date, double> Map(IReadOnlyDictionary<Date, double> x) => x.ToImmutableSortedDictionary();

    /// <summary>Key–value pairs, a curve's nodes' form.</summary>
    /// <param name="dates">The keys.</param>
    /// <param name="values">The values.</param>
    /// <returns>The pairs.</returns>
    public static ImmutableArray<KeyValuePair<Date, double>> Nodes(IReadOnlyList<Date> dates, IReadOnlyList<double> values) =>
        [.. dates.Zip(values, (d, v) => new KeyValuePair<Date, double>(d, v))];

    /// <summary>A tuple: one out-parameter per element.</summary>
    /// <param name="x">The value.</param>
    /// <returns>Its floor, as an integer, and its fraction.</returns>
    public static (int Whole, double Fraction) Split(double x) => ((int)Math.Floor(x), x - Math.Floor(x));

    /// <summary>A matrix in and out by rows: its transpose.</summary>
    /// <param name="m">The matrix.</param>
    /// <returns>Its transpose.</returns>
    public static Matrix Transpose(Matrix m) => MatrixFunctions.Transpose(m);

    /// <summary>A vector in and out: each element doubled.</summary>
    /// <param name="v">The vector.</param>
    /// <returns>The doubled vector.</returns>
    public static Vector Twice(Vector v) => v * 2.0;

    /// <summary>A writable span: filled in place.</summary>
    /// <param name="x">The span.</param>
    /// <param name="value">The value.</param>
    public static void Fill(Span<double> x, double value) => x.Fill(value);
}
