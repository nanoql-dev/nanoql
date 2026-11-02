// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using ExcelDna.Integration;
using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Excel;

/// <summary>
/// The conversions out (XLR2): a value as Excel shows it, a non-finite number #NUM! and none #N/A; arrays spilled down a
/// column, a matrix by rows, a tuple across a row, a map as two columns, a jagged array padded with #N/A, a distribution
/// under a header row; an object as its handle, one inside a collection keyed by the call and its place.
/// </summary>
public static partial class X
{
    // A nanoql object, which a cell shows by its handle.
    private static bool IsObject(object? r) => r != null && r is not Matrix && r is not NanoQL.Numerics.Vector && r is not string && r is not double && r is not int
        && r is not long && r is not bool && r is not NanoQL.Time.Date && r is not Period && r is not System.Enum && r is not Array && r is not System.Collections.IEnumerable
        && r.GetType().Assembly.GetName().Name!.StartsWith("NanoQL", StringComparison.Ordinal);

    private static object Out(object? r)
    {
        if (OutForm(r, out object? form))
        {
            return form!;
        }

        switch (r)
        {
            case null: return ExcelError.ExcelErrorNA;
            case double d: return Num(d);
            case int i: return (double)i;
            case long l: return (double)l;
            case bool b: return b;
            case string s: return s;
            case NanoQL.Time.Date dt: return (double)(dt.SerialNumber - DateShift());
            case Period p: return PeriodFunctions.ShortPeriod(p);
            case System.Enum e: return e.ToString();
            case Matrix m when m.Rows == 0 || m.Columns == 0:
                return ExcelError.ExcelErrorNA;
            case Matrix m:
            {
                var o = new object[m.Rows, m.Columns];
                for (int i = 0; i < m.Rows; i++)
                {
                    for (int j = 0; j < m.Columns; j++)
                    {
                        o[i, j] = Num(m[i, j]);
                    }
                }

                return o;
            }

            case System.Collections.IEnumerable seq:
            {
                var items = seq.Cast<object>().ToList();
                if (items.Count == 0)
                {
                    return ExcelError.ExcelErrorNA;
                }

                var o = new object[items.Count, 1];
                for (int i = 0; i < items.Count; i++)
                {
                    o[i, 0] = IsObject(items[i]) ? Handle(items[i], i) : Out(items[i]);
                }

                return o;
            }
        }

        return IsObject(r) ? Handle(r) : r.ToString() ?? string.Empty;
    }

    private static object Num(double d) => double.IsFinite(d) ? d : ExcelError.ExcelErrorNum;

    // The forms beyond a plain value; false leaves the rest to Out.
    private static bool OutForm(object? r, out object? o)
    {
        o = null;
        switch (r)
        {
            case Complex c:
                o = double.IsFinite(c.Real) && double.IsFinite(c.Imaginary) ? CplxText(c) : ExcelError.ExcelErrorNum;
                return true;
            case double[,] m2 when m2.Length == 0:
                o = ExcelError.ExcelErrorNA;
                return true;
            case double[,] m2:
            {
                var t = new object[m2.GetLength(0), m2.GetLength(1)];
                for (int i = 0; i < m2.GetLength(0); i++)
                {
                    for (int j = 0; j < m2.GetLength(1); j++)
                    {
                        t[i, j] = Num(m2[i, j]);
                    }
                }

                o = t;
                return true;
            }

            case double?[] nd:
                o = Column(nd.Select(x => x.HasValue ? Num(x.Value) : ExcelError.ExcelErrorNA).ToArray());
                return true;
            case int?[] ni:
                o = Column(ni.Select(x => x.HasValue ? (object)(double)x.Value : ExcelError.ExcelErrorNA).ToArray());
                return true;
            case Distribution dist:
                o = DistributionTable(dist);
                return true;
            case ITuple tup:
                o = TupleRow(tup);
                return true;
        }

        if (r is System.Collections.IEnumerable tuples and not string && TupleWidth(r.GetType()) is int width)
        {
            o = TupleTable(tuples, width);
            return true;
        }

        if (r is System.Collections.IEnumerable pairs and not string && IsMap(r.GetType()))
        {
            o = MapTable(pairs);
            return true;
        }

        if (r is System.Collections.IEnumerable outer and not string && r is not Matrix && r is not NanoQL.Numerics.Vector)
        {
            var items = outer.Cast<object>().ToList();
            if (items.Count > 0 && items.All(x => x is IEnumerable<double>))
            {
                // Jagged: a row per inner array, padded with #N/A as Excel's HSTACK pads.
                var rows = items.Select(x => ((IEnumerable<double>)x).ToArray()).ToList();
                int w = Math.Max(1, rows.Max(x => x.Length));
                var t = new object[rows.Count, w];
                for (int i = 0; i < rows.Count; i++)
                {
                    for (int j = 0; j < w; j++)
                    {
                        t[i, j] = j < rows[i].Length ? Num(rows[i][j]) : ExcelError.ExcelErrorNA;
                    }
                }

                o = t;
                return true;
            }
        }

        return false;
    }

    // A loss distribution: six columns under a header row, normalized, as the bindings give it (BIR3).
    private static object[,] DistributionTable(Distribution dist)
    {
        dist.Normalize();
        int k = dist.Size;
        var t = new object[k + 1, 6];
        string[] head = ["x", "dx", "density", "cumulative", "excess", "average"];
        for (int j = 0; j < 6; j++)
        {
            t[0, j] = head[j];
        }

        for (int i = 0; i < k; i++)
        {
            t[i + 1, 0] = Num(dist.X(i));
            t[i + 1, 1] = Num(dist.Dx(i));
            t[i + 1, 2] = Num(dist.Density(i));
            t[i + 1, 3] = Num(dist.Cumulative(i));
            t[i + 1, 4] = Num(dist.Excess(i));
            t[i + 1, 5] = Num(dist.Average(i));
        }

        return t;
    }

    // A tuple across a row; an element that is itself an array takes a column of its own, padded with #N/A.
    private static object[,] TupleRow(ITuple tup)
    {
        var cols = new List<object[]>();
        for (int i = 0; i < tup.Length; i++)
        {
            object e = InSlot("slot:" + i.ToString(CultureInfo.InvariantCulture), () => Out(tup[i]));
            if (e is object[,] m)
            {
                var col = new object[m.GetLength(0)];
                for (int j = 0; j < col.Length; j++)
                {
                    col[j] = m[j, 0];
                }

                cols.Add(col);
            }
            else
            {
                cols.Add([e]);
            }
        }

        int rows = cols.Max(c => c.Length);
        var t = new object[rows, cols.Count];
        for (int j = 0; j < cols.Count; j++)
        {
            for (int i = 0; i < rows; i++)
            {
                t[i, j] = i < cols[j].Length ? cols[j][i] : ExcelError.ExcelErrorNA;
            }
        }

        return t;
    }

    // A collection of tuples: a row per element and a column per item; a null element a row of #N/A (XLR17).
    private static object TupleTable(System.Collections.IEnumerable tuples, int width)
    {
        var items = tuples.Cast<object>().ToList();
        if (items.Count == 0)
        {
            return ExcelError.ExcelErrorNA;
        }

        var t = new object[items.Count, width];
        for (int i = 0; i < items.Count; i++)
        {
            for (int j = 0; j < width; j++)
            {
                if (items[i] is not ITuple tu)
                {
                    t[i, j] = ExcelError.ExcelErrorNA;
                    continue;
                }

                object e = InSlot($"row:{i.ToString(CultureInfo.InvariantCulture)}/slot:{j.ToString(CultureInfo.InvariantCulture)}", () => Out(tu[j]));
                t[i, j] = e is object[,] ? ExcelError.ExcelErrorValue : e;
            }
        }

        return t;
    }

    // A map: two columns, keys and values, in the map's own order; an object value keyed by its key (F5-2).
    private static object MapTable(System.Collections.IEnumerable map)
    {
        var pairs = map.Cast<object>().ToList();
        if (pairs.Count == 0)
        {
            return ExcelError.ExcelErrorNA;
        }

        var t = new object[pairs.Count, 2];
        for (int i = 0; i < pairs.Count; i++)
        {
            Type pt = pairs[i].GetType();
            t[i, 0] = Out(pt.GetProperty("Key")!.GetValue(pairs[i]));
            object? v = pt.GetProperty("Value")!.GetValue(pairs[i]);
            t[i, 1] = InSlot("key:" + Convert.ToString(t[i, 0], CultureInfo.InvariantCulture), () => Out(v));
        }

        return t;
    }

    private static object InSlot(string slot, Func<object> body)
    {
        string? outer = slotPath;
        slotPath = (outer == null ? string.Empty : outer + "/") + slot;
        try
        {
            return body();
        }
        finally
        {
            slotPath = outer;
        }
    }

    // The item count of a collection of tuples (a nullable tuple's included), or null for any other type.
    private static int? TupleWidth(Type type)
    {
        foreach (Type i in type.GetInterfaces().Prepend(type))
        {
            if (!i.IsGenericType || i.GetGenericTypeDefinition() != typeof(IEnumerable<>))
            {
                continue;
            }

            Type e = i.GetGenericArguments()[0];
            e = Nullable.GetUnderlyingType(e) ?? e;
            if (e.IsGenericType && e.FullName!.StartsWith("System.ValueTuple`", StringComparison.Ordinal))
            {
                return e.GetGenericArguments().Length;
            }
        }

        return null;
    }

    private static bool IsMap(Type type) => type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)
        && i.GetGenericArguments()[0].IsGenericType && i.GetGenericArguments()[0].GetGenericTypeDefinition() == typeof(KeyValuePair<,>));

    private static object[,] Column(object[] v)
    {
        if (v.Length == 0)
        {
            return new object[,] { { ExcelError.ExcelErrorNA } };
        }

        var o = new object[v.Length, 1];
        for (int i = 0; i < v.Length; i++)
        {
            o[i, 0] = v[i];
        }

        return o;
    }
}
