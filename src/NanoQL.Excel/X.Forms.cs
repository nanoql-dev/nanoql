// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Numerics;
using NanoQL.Time;

namespace NanoQL.Excel;

/// <summary>
/// The forms beyond a cell's value (XLR2, XLR17): complex numbers as Excel's text, nullable elements keeping a range's
/// shape, jagged rows, maps as two columns, tuples across a row, generators' snapshots and the bound engine.
/// </summary>
public static partial class X
{
    /// <summary>A complex number as Excel's own text (<c>COMPLEX</c>, <c>IMREAL</c>): "3+4i", "3-i", "i", "0"; a number is real.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The number.</returns>
    public static Complex Cplx(object a, string p)
    {
        object x = One(a);
        if (x is double d)
        {
            return new Complex(d, 0);
        }

        if (x is string s && TryComplex(s.Trim(), out Complex c))
        {
            return c;
        }

        throw Bad(p, "a complex number such as \"3+4i\"", a);
    }

    /// <summary>A nullable complex number.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The number, or none.</returns>
    public static Complex? OptCplx(object a, string p) => IsMissing(One(a)) ? null : Cplx(a, p);

    /// <summary>Complex numbers from a range.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The numbers.</returns>
    public static Complex[] Cplxs(object a, string p) => Cells(a, p).Select(x => Cplx(x, p)).ToArray();

    /// <summary>A complex number's text as Excel writes it, its parts with round-trip digits; a zero imaginary part as Excel writes it.</summary>
    /// <param name="c">The number.</param>
    /// <returns>The text.</returns>
    public static string CplxText(Complex c)
    {
        static string R(double d) => d.ToString("R", CultureInfo.InvariantCulture);
        if (c.Imaginary == 0)
        {
            return R(c.Real);
        }

        string im = c.Imaginary == 1 ? string.Empty : c.Imaginary == -1 ? "-" : R(c.Imaginary);
        return c.Real == 0 ? im + "i" : R(c.Real) + (c.Imaginary > 0 ? "+" : string.Empty) + im + "i";
    }

    /// <summary>Nullable numbers keeping the range's shape: an empty cell is none, nothing trimmed (XLR2).</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The numbers.</returns>
    public static double?[] OptDbls(object a, string p) => Shape(a, p).Select(x => IsMissing(x) ? (double?)null : Dbl(x, p)).ToArray();

    /// <summary>Nullable whole numbers keeping the range's shape.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The numbers.</returns>
    public static int?[] OptInts(object a, string p) => Shape(a, p).Select(x => IsMissing(x) ? (int?)null : Int(x, p)).ToArray();

    /// <summary>Nullable dates keeping the range's shape.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The dates.</returns>
    public static Date?[] OptDates(object a, string p) => Shape(a, p).Select(x => IsMissing(x) ? (Date?)null : Date(x, p)).ToArray();

    /// <summary>A jagged array: a row per inner array, each row's trailing empty cells trimmed and an inner one refused.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The rows.</returns>
    public static double[][] Jagged(object a, string p)
    {
        if (a is not object[,] m)
        {
            return IsMissing(a) ? [] : [[Dbl(Cell(a, p), p)]];
        }

        var rows = new List<double[]>();
        for (int i = 0; i < m.GetLength(0); i++)
        {
            int n = m.GetLength(1);
            while (n > 0 && IsMissing(m[i, n - 1]))
            {
                n--;
            }

            var r = new double[n];
            for (int j = 0; j < n; j++)
            {
                r[j] = IsMissing(m[i, j])
                    ? throw new ArgumentException($"{p}: an empty cell inside row {(i + 1).ToString(CultureInfo.InvariantCulture)}; only a row's trailing empty cells are trimmed", p)
                    : Dbl(Cell(m[i, j], p), p);
            }

            rows.Add(r);
        }

        while (rows.Count > 0 && rows[^1].Length == 0)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return [.. rows];
    }

    /// <summary>A map: two columns, the keys on the left; the empty rows at its end trimmed, an inner one and a duplicate key refused.</summary>
    /// <typeparam name="TKey">The key's type.</typeparam>
    /// <typeparam name="TValue">The value's type.</typeparam>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <param name="key">The key's conversion.</param>
    /// <param name="value">The value's conversion.</param>
    /// <returns>The map, in the range's order.</returns>
    public static Dictionary<TKey, TValue> Map<TKey, TValue>(object a, string p, Func<object, string, TKey> key, Func<object, string, TValue> value)
        where TKey : notnull
    {
        var d = new Dictionary<TKey, TValue>();
        if (IsMissing(a))
        {
            return d;
        }

        if (a is not object[,] m || m.GetLength(1) != 2)
        {
            throw Bad(p, "two columns, the keys and the values", a);
        }

        int rows = m.GetLength(0);
        while (rows > 0 && IsMissing(m[rows - 1, 0]) && IsMissing(m[rows - 1, 1]))
        {
            rows--;
        }

        for (int i = 0; i < rows; i++)
        {
            TKey k = key(Cell(m[i, 0], p), p + " key");
            if (!d.TryAdd(k, value(Cell(m[i, 1], p), p + " value")))
            {
                throw new ArgumentException($"{p}: the key {Show(m[i, 0])} appears twice", p);
            }
        }

        return d;
    }

    /// <summary>A pair: a row of its two cells, or a column, each through its item's conversion (XLR17).</summary>
    /// <typeparam name="T1">The first item's type.</typeparam>
    /// <typeparam name="T2">The second item's type.</typeparam>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <param name="c1">The first item's conversion.</param>
    /// <param name="c2">The second item's conversion.</param>
    /// <returns>The pair.</returns>
    public static (T1, T2) Tuple<T1, T2>(object a, string p, Func<object, string, T1> c1, Func<object, string, T2> c2)
    {
        object[] c = TupleCells(a, p, 2);
        return (c1(c[0], p + " item 1"), c2(c[1], p + " item 2"));
    }

    /// <summary>A triple: a row of its three cells, or a column.</summary>
    /// <typeparam name="T1">The first item's type.</typeparam>
    /// <typeparam name="T2">The second item's type.</typeparam>
    /// <typeparam name="T3">The third item's type.</typeparam>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <param name="c1">The first item's conversion.</param>
    /// <param name="c2">The second item's conversion.</param>
    /// <param name="c3">The third item's conversion.</param>
    /// <returns>The triple.</returns>
    public static (T1, T2, T3) Tuple<T1, T2, T3>(object a, string p, Func<object, string, T1> c1, Func<object, string, T2> c2, Func<object, string, T3> c3)
    {
        object[] c = TupleCells(a, p, 3);
        return (c1(c[0], p + " item 1"), c2(c[1], p + " item 2"), c3(c[2], p + " item 3"));
    }

    /// <summary>An array of pairs: a row per element and a column per item, the empty rows at its end trimmed.</summary>
    /// <typeparam name="T1">The first item's type.</typeparam>
    /// <typeparam name="T2">The second item's type.</typeparam>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <param name="c1">The first item's conversion.</param>
    /// <param name="c2">The second item's conversion.</param>
    /// <returns>The pairs.</returns>
    public static (T1, T2)[] Tuples<T1, T2>(object a, string p, Func<object, string, T1> c1, Func<object, string, T2> c2)
        => TupleRows(a, p, 2).Select((r, i) => (c1(r[0], RowItem(p, i, 1)), c2(r[1], RowItem(p, i, 2)))).ToArray();

    /// <summary>An array of triples: a row per element and a column per item.</summary>
    /// <typeparam name="T1">The first item's type.</typeparam>
    /// <typeparam name="T2">The second item's type.</typeparam>
    /// <typeparam name="T3">The third item's type.</typeparam>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <param name="c1">The first item's conversion.</param>
    /// <param name="c2">The second item's conversion.</param>
    /// <param name="c3">The third item's conversion.</param>
    /// <returns>The triples.</returns>
    public static (T1, T2, T3)[] Tuples<T1, T2, T3>(object a, string p, Func<object, string, T1> c1, Func<object, string, T2> c2, Func<object, string, T3> c3)
        => TupleRows(a, p, 3).Select((r, i) => (c1(r[0], RowItem(p, i, 1)), c2(r[1], RowItem(p, i, 2)), c3(r[2], RowItem(p, i, 3)))).ToArray();

    /// <summary>A generator by its snapshot's handle; a member taking one gets a copy, the snapshot never drawn from (BIR3's R3).</summary>
    /// <typeparam name="T">The generator's type.</typeparam>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>A copy of the generator, or none.</returns>
    public static T? Generator<T>(object a, string p)
        where T : class
    {
        if (IsMissing(One(a)))
        {
            return null;
        }

        object g = Obj<XlSnapshot>(a, p).Generator;
        return CloneGenerator(g) as T ?? throw new ArgumentException($"{p}: a {g.GetType().Name} snapshot where a {typeof(T).Name} is expected", p);
    }

    /// <summary>The generator behind a snapshot's handle, never drawn from itself.</summary>
    /// <typeparam name="T">The generator's type.</typeparam>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The generator.</returns>
    public static T SnapshotOf<T>(object a, string p)
        where T : class
    {
        object g = Obj<XlSnapshot>(a, p).Generator;
        return g as T ?? throw new ArgumentException($"{p}: a {g.GetType().Name} snapshot where a {typeof(T).Name} is expected", p);
    }

    /// <summary>A generator's snapshot, behind a handle: a copy, so the caller's generator stays its own.</summary>
    /// <param name="g">The generator, or none.</param>
    /// <returns>The snapshot.</returns>
    public static object? NewSnapshot(object? g) => g == null ? null : new XlSnapshot(CloneGenerator(g));

    /// <summary>A bound engine by its handle; none where the argument is left out.</summary>
    /// <param name="a">The Excel value.</param>
    /// <param name="p">The argument's name.</param>
    /// <returns>The engine, or none.</returns>
    public static BoundEngine? Engine(object a, string p) => IsMissing(One(a)) ? null : Obj<BoundEngine>(a, p);

    private static bool TryComplex(string s, out Complex c)
    {
        c = default;
        if (s.Length == 0)
        {
            return false;
        }

        var inv = CultureInfo.InvariantCulture;
        if (s[^1] is not ('i' or 'j'))
        {
            bool real = double.TryParse(s, NumberStyles.Float, inv, out double re0);
            c = new Complex(re0, 0);
            return real;
        }

        string body = s[..^1];

        // The imaginary part starts at the last sign that isn't an exponent's.
        int k = -1;
        for (int i = body.Length - 1; i > 0; i--)
        {
            if (body[i] is '+' or '-' && body[i - 1] is not ('e' or 'E'))
            {
                k = i;
                break;
            }
        }

        string reText = k < 0 ? "0" : body[..k], imText = k < 0 ? body : body[k..];
        double im = imText is "" or "+" ? 1 : imText == "-" ? -1 : double.TryParse(imText, NumberStyles.Float, inv, out double v) ? v : double.NaN;
        if (double.IsNaN(im) || !double.TryParse(reText, NumberStyles.Float, inv, out double re))
        {
            return false;
        }

        c = new Complex(re, im);
        return true;
    }

    // A range of nullable elements: its shape is the array, an empty cell none, nothing trimmed (XLR2).
    private static IEnumerable<object> Shape(object a, string p)
    {
        if (a is object[,] m)
        {
            foreach (object x in m)
            {
                yield return IsMissing(x) ? x : Cell(x, p);
            }
        }
        else if (!IsMissing(a))
        {
            yield return Cell(a, p);
        }
    }

    private static object[] TupleCells(object a, string p, int n)
    {
        if (a is object[,] m && ((m.GetLength(0) == 1 && m.GetLength(1) == n) || (m.GetLength(1) == 1 && m.GetLength(0) == n)))
        {
            return m.Cast<object>().Select(x => Cell(x, p)).ToArray();
        }

        throw Bad(p, $"a row of {n} cells", a);
    }

    private static List<object[]> TupleRows(object a, string p, int n)
    {
        var rows = new List<object[]>();
        if (IsMissing(a))
        {
            return rows;
        }

        if (a is not object[,] m || m.GetLength(1) != n)
        {
            throw Bad(p, $"a range of {n} columns, a row per element", a);
        }

        for (int i = 0; i < m.GetLength(0); i++)
        {
            var r = new object[n];
            for (int j = 0; j < n; j++)
            {
                r[j] = Cell(m[i, j], p);
            }

            rows.Add(r);
        }

        while (rows.Count > 0 && rows[^1].All(IsMissing))
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return rows;
    }

    private static string RowItem(string p, int i, int j) => $"{p} row {(i + 1).ToString(CultureInfo.InvariantCulture)}, item {j.ToString(CultureInfo.InvariantCulture)}";

    private static object CloneGenerator(object g) => g switch
    {
        NanoQL.Numerics.IRng r => r.Clone(),
        NanoQL.Numerics.IRsg s => s.Clone(),
        _ => throw new ArgumentException($"a {g.GetType().Name} is not a generator"),
    };
}
