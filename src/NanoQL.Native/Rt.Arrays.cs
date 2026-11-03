// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using Complex = System.Numerics.Complex;
using System.Runtime.InteropServices;
using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Native;

/// <summary>
/// Collections across the C ABI. In: a pointer and a count, copied (a <c>ReadOnlySpan</c> of numbers reads the caller's
/// buffer in place); a NULL pointer is a null collection. Out: memory nanoql allocates, freed with <c>nql_free</c>. A
/// matrix crosses by rows with its row and column counts.
/// </summary>
internal static unsafe partial class Rt
{
    /// <summary>A copy of the caller's numbers.</summary>
    /// <typeparam name="T">The number's type.</typeparam>
    /// <param name="p">The numbers, or NULL.</param>
    /// <param name="n">Their count.</param>
    /// <returns>The copy, or null.</returns>
    public static T[] Copy<T>(T* p, int n)
        where T : unmanaged => p == null ? null! : new ReadOnlySpan<T>(p, Count(n)).ToArray();

    /// <summary>The caller's booleans.</summary>
    /// <param name="p">The booleans, or NULL.</param>
    /// <param name="n">Their count.</param>
    /// <returns>The array, or null.</returns>
    public static bool[] Bools(byte* p, int n) => Map(p, n, b => b != 0);

    /// <summary>The caller's dates, by serial; 0 is QuantLib's <c>Date()</c>.</summary>
    /// <param name="p">The serials, or NULL.</param>
    /// <param name="n">Their count.</param>
    /// <returns>The dates, or null.</returns>
    public static Date[] Dates(int* p, int n) => Map(p, n, Date);

    /// <summary>The caller's nullable dates, by serial; 0 is none.</summary>
    /// <param name="p">The serials, or NULL.</param>
    /// <param name="n">Their count.</param>
    /// <returns>The dates, or null.</returns>
    public static Date?[] OptDates(int* p, int n) => Map(p, n, OptDate);

    /// <summary>The caller's periods.</summary>
    /// <param name="p">The periods, or NULL.</param>
    /// <param name="n">Their count.</param>
    /// <returns>The periods, or null.</returns>
    public static Period[] Periods(NqlPeriod* p, int n) => Map(p, n, x => x.ToPeriod());

    /// <summary>The caller's complex numbers.</summary>
    /// <param name="p">The numbers, or NULL.</param>
    /// <param name="n">Their count.</param>
    /// <returns>The numbers, or null.</returns>
    public static Complex[] Complexes(NqlComplex* p, int n) => Map(p, n, x => x.ToComplex());

    /// <summary>The caller's enum values.</summary>
    /// <typeparam name="T">The enum.</typeparam>
    /// <param name="p">The values, or NULL.</param>
    /// <param name="n">Their count.</param>
    /// <returns>The values, or null.</returns>
    public static T[] Enums<T>(int* p, int n)
        where T : struct, Enum => Map(p, n, x => (T)Enum.ToObject(typeof(T), x));

    /// <summary>The caller's strings.</summary>
    /// <param name="p">The strings, or NULL.</param>
    /// <param name="n">Their count.</param>
    /// <returns>The strings, or null.</returns>
    public static string[] Strs(byte** p, int n)
    {
        if (p == null)
        {
            return null!;
        }

        var a = new string[Count(n)];
        for (int i = 0; i < a.Length; i++)
        {
            a[i] = Str(p[i]);
        }

        return a;
    }

    /// <summary>The objects the caller's handles refer to.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="p">The handles, or NULL.</param>
    /// <param name="n">Their count.</param>
    /// <param name="param">The parameter's name.</param>
    /// <returns>The objects, or null.</returns>
    public static T[] Objects<T>(nint* p, int n, string param)
        where T : class
    {
        if (p == null)
        {
            return null!;
        }

        var a = new T[Count(n)];
        for (int i = 0; i < a.Length; i++)
        {
            a[i] = Get<T>(p[i], param);
        }

        return a;
    }

    /// <summary>The caller's nullable doubles: values and has-value flags.</summary>
    /// <param name="p">The values, or NULL.</param>
    /// <param name="has">The flags.</param>
    /// <param name="n">Their count.</param>
    /// <returns>The values, or null.</returns>
    public static double?[] OptDoubles(double* p, byte* has, int n)
    {
        if (p == null)
        {
            return null!;
        }

        var a = new double?[Count(n)];
        for (int i = 0; i < a.Length; i++)
        {
            a[i] = has == null || has[i] != 0 ? p[i] : null;
        }

        return a;
    }

    /// <summary>The caller's nullable integers: values and has-value flags.</summary>
    /// <param name="p">The values, or NULL.</param>
    /// <param name="has">The flags.</param>
    /// <param name="n">Their count.</param>
    /// <returns>The values, or null.</returns>
    public static int?[] OptInts(int* p, byte* has, int n)
    {
        if (p == null)
        {
            return null!;
        }

        var a = new int?[Count(n)];
        for (int i = 0; i < a.Length; i++)
        {
            a[i] = has == null || has[i] != 0 ? p[i] : null;
        }

        return a;
    }

    /// <summary>A jagged array from its values, flat by rows, and its rows' lengths.</summary>
    /// <param name="p">The values, or NULL.</param>
    /// <param name="count">Their count.</param>
    /// <param name="lengths">The rows' lengths.</param>
    /// <param name="rows">The rows' count.</param>
    /// <returns>The rows, or null.</returns>
    public static double[][] Jagged(double* p, int count, int* lengths, int rows)
    {
        if (p == null)
        {
            return null!;
        }

        var a = new double[Count(rows)][];
        long at = 0;
        for (int r = 0; r < a.Length; r++)
        {
            int len = Count(lengths[r]);
            if (at + len > count)
            {
                throw new ArgumentException("the rows' lengths sum past the values' count");
            }

            a[r] = new ReadOnlySpan<double>(p + at, len).ToArray();
            at += len;
        }

        return at == count ? a : throw new ArgumentException("the rows' lengths don't sum to the values' count");
    }

    /// <summary>An immutable array; null is empty.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="a">The array.</param>
    /// <returns>The immutable array.</returns>
    public static ImmutableArray<T> Immutable<T>(T[] a) => a is null ? [] : ImmutableCollectionsMarshal.AsImmutableArray(a);

    /// <summary>A list; null stays null.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="a">The elements.</param>
    /// <returns>The list.</returns>
    public static List<T> List<T>(T[] a) => a is null ? null! : [.. a];

    /// <summary>A sorted set; null stays null.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="a">The elements.</param>
    /// <returns>The set.</returns>
    public static ImmutableSortedSet<T> SortedSet<T>(T[] a) => a is null ? null! : ImmutableSortedSet.Create(a);

    /// <summary>A hash set; null stays null.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="a">The elements.</param>
    /// <returns>The set.</returns>
    public static ImmutableHashSet<T> HashSet<T>(T[] a) => a is null ? null! : ImmutableHashSet.Create(a);

    /// <summary>A read-only set; null stays null.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="a">The elements.</param>
    /// <returns>The set.</returns>
    public static IReadOnlySet<T> ReadOnlySet<T>(T[] a) => a is null ? null! : new HashSet<T>(a);

    /// <summary>An immutable list; null stays null.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="a">The elements.</param>
    /// <returns>The list.</returns>
    public static ImmutableList<T> ImmutableList<T>(T[] a) => a is null ? null! : System.Collections.Immutable.ImmutableList.Create(a);

    /// <summary>A dictionary from keys and values in step; null keys give null.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="keys">The keys.</param>
    /// <param name="values">The values.</param>
    /// <returns>The dictionary.</returns>
    public static Dictionary<TKey, TValue> Dictionary<TKey, TValue>(TKey[] keys, TValue[] values)
        where TKey : notnull
    {
        if (keys is null)
        {
            return null!;
        }

        var d = new Dictionary<TKey, TValue>(keys.Length);
        for (int i = 0; i < keys.Length; i++)
        {
            d.Add(keys[i], values[i]);
        }

        return d;
    }

    /// <summary>An immutable dictionary; null stays null.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="d">The entries.</param>
    /// <returns>The dictionary.</returns>
    public static ImmutableDictionary<TKey, TValue> ImmutableDictionary<TKey, TValue>(Dictionary<TKey, TValue> d)
        where TKey : notnull => d is null ? null! : d.ToImmutableDictionary();

    /// <summary>An immutable sorted dictionary; null stays null.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="d">The entries.</param>
    /// <returns>The dictionary.</returns>
    public static ImmutableSortedDictionary<TKey, TValue> ImmutableSortedDictionary<TKey, TValue>(Dictionary<TKey, TValue> d)
        where TKey : notnull => d is null ? null! : d.ToImmutableSortedDictionary();

    /// <summary>A frozen dictionary; null stays null.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="d">The entries.</param>
    /// <returns>The dictionary.</returns>
    public static System.Collections.Frozen.FrozenDictionary<TKey, TValue> FrozenDictionary<TKey, TValue>(Dictionary<TKey, TValue> d)
        where TKey : notnull => d is null ? null! : System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(d);

    /// <summary>
    /// Pairs from their elements' arrays in step: a null first array gives null, the collection a <c>Bind</c> leaves
    /// open; a later one null where the first isn't is refused.
    /// </summary>
    /// <typeparam name="T1">The first element's type.</typeparam>
    /// <typeparam name="T2">The second element's type.</typeparam>
    /// <param name="a">The first elements.</param>
    /// <param name="b">The second elements.</param>
    /// <param name="name">The parameter, for the refusal.</param>
    /// <returns>The pairs.</returns>
    public static (T1, T2)[] Zip<T1, T2>(T1[] a, T2[] b, string name)
    {
        if (a is null)
        {
            return null!;
        }

        Present(b, a.Length, name);
        var r = new (T1, T2)[a.Length];
        for (int i = 0; i < r.Length; i++)
        {
            r[i] = (a[i], b[i]);
        }

        return r;
    }

    /// <summary>Triples from their elements' arrays in step, as <see cref="Zip{T1, T2}"/>.</summary>
    /// <typeparam name="T1">The first element's type.</typeparam>
    /// <typeparam name="T2">The second element's type.</typeparam>
    /// <typeparam name="T3">The third element's type.</typeparam>
    /// <param name="a">The first elements.</param>
    /// <param name="b">The second elements.</param>
    /// <param name="c">The third elements.</param>
    /// <param name="name">The parameter, for the refusal.</param>
    /// <returns>The triples.</returns>
    public static (T1, T2, T3)[] Zip<T1, T2, T3>(T1[] a, T2[] b, T3[] c, string name)
    {
        if (a is null)
        {
            return null!;
        }

        Present(b, a.Length, name);
        Present(c, a.Length, name);
        var r = new (T1, T2, T3)[a.Length];
        for (int i = 0; i < r.Length; i++)
        {
            r[i] = (a[i], b[i], c[i]);
        }

        return r;
    }

    /// <summary>Key–value pairs from keys and values in step; null keys give null.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="keys">The keys.</param>
    /// <param name="values">The values.</param>
    /// <returns>The pairs.</returns>
    public static KeyValuePair<TKey, TValue>[] Pairs<TKey, TValue>(TKey[] keys, TValue[] values)
    {
        if (keys is null)
        {
            return null!;
        }

        var a = new KeyValuePair<TKey, TValue>[keys.Length];
        for (int i = 0; i < a.Length; i++)
        {
            a[i] = new(keys[i], values[i]);
        }

        return a;
    }

    // A tuple element's array, beside a first one that is there: NULL is refused unless the collection is empty.
    private static void Present<T>(T[] array, int count, string name)
    {
        if (array is null && count > 0)
        {
            throw new ArgumentNullException(name, "a tuple element's array is NULL");
        }
    }

    /// <summary>A matrix from the caller's values by rows.</summary>
    /// <param name="p">The values, or NULL.</param>
    /// <param name="rows">The rows.</param>
    /// <param name="columns">The columns.</param>
    /// <returns>The matrix, or null.</returns>
    public static Matrix Matrix(double* p, int rows, int columns)
    {
        if (p == null)
        {
            return null!;
        }

        var m = new Matrix(Count(rows), Count(columns));
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                m[i, j] = p[((long)i * columns) + j];
            }
        }

        return m;
    }

    /// <summary>A vector from the caller's values.</summary>
    /// <param name="p">The values, or NULL.</param>
    /// <param name="n">Their count.</param>
    /// <returns>The vector, or null.</returns>
    public static Vector Vector(double* p, int n) => p == null ? null! : new Vector(new ReadOnlySpan<double>(p, Count(n)));

    /// <summary>A vector the member wrote into, copied back into the caller's values.</summary>
    /// <param name="v">The vector, or null.</param>
    /// <param name="p">The caller's values, or NULL.</param>
    /// <param name="n">Their count.</param>
    /// <exception cref="InvalidOperationException">The member changed the vector's size.</exception>
    public static void CopyBack(Vector v, double* p, int n)
    {
        if (v is null || p == null)
        {
            return;
        }

        if (v.Size != n)
        {
            throw new InvalidOperationException($"the member changed the vector's size from {n} to {v.Size}: one written in place keeps its size");
        }

        v.AsSpan().CopyTo(new Span<double>(p, n));
    }

    /// <summary>A matrix the member wrote into, copied back into the caller's values by rows.</summary>
    /// <param name="m">The matrix, or null.</param>
    /// <param name="p">The caller's values, or NULL.</param>
    /// <param name="rows">The rows.</param>
    /// <param name="columns">The columns.</param>
    /// <exception cref="InvalidOperationException">The member changed the matrix's shape.</exception>
    public static void CopyBack(Matrix m, double* p, int rows, int columns)
    {
        if (m is null || p == null)
        {
            return;
        }

        if (m.Rows != rows || m.Columns != columns)
        {
            throw new InvalidOperationException($"the member changed the matrix's shape from {rows}x{columns} to {m.Rows}x{m.Columns}: one written in place keeps its shape");
        }

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                p[((long)i * columns) + j] = m[i, j];
            }
        }
    }

    /// <summary>A matrix for the caller, by rows; null gives NULL and zero counts.</summary>
    /// <param name="m">The matrix.</param>
    /// <param name="r">Receives the values.</param>
    /// <param name="rows">Receives the rows.</param>
    /// <param name="columns">Receives the columns.</param>
    public static void OutMatrix(Matrix m, double** r, int* rows, int* columns)
    {
        if (m is null)
        {
            *r = null;
            *rows = *columns = 0;
            return;
        }

        double* p = Alloc<double>(m.Rows * m.Columns);
        for (int i = 0; i < m.Rows; i++)
        {
            for (int j = 0; j < m.Columns; j++)
            {
                p[((long)i * m.Columns) + j] = m[i, j];
            }
        }

        *r = p;
        *rows = m.Rows;
        *columns = m.Columns;
    }

    /// <summary>A vector for the caller; null gives NULL and a zero count.</summary>
    /// <param name="v">The vector.</param>
    /// <param name="r">Receives the values.</param>
    /// <param name="n">Receives the count.</param>
    public static void OutVector(Vector v, double** r, int* n)
    {
        if (v is null)
        {
            *r = null;
            *n = 0;
            return;
        }

        double* p = Alloc<double>(v.Size);
        for (int i = 0; i < v.Size; i++)
        {
            p[i] = v[i];
        }

        *r = p;
        *n = v.Size;
    }

    /// <summary>A jagged array for the caller: its values flat by rows, and its rows' lengths.</summary>
    /// <param name="rows">The rows.</param>
    /// <param name="r">Receives the values.</param>
    /// <param name="lengths">Receives the rows' lengths.</param>
    /// <param name="n">Receives the rows' count.</param>
    public static void OutJagged(double[][] rows, double** r, int** lengths, int* n)
    {
        int total = rows.Sum(x => x.Length);
        double* p = Alloc<double>(total);
        int* l = Alloc<int>(rows.Length);
        int at = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            l[i] = rows[i].Length;
            rows[i].CopyTo(new Span<double>(p + at, rows[i].Length));
            at += rows[i].Length;
        }

        *r = p;
        *lengths = l;
        *n = rows.Length;
    }

    /// <summary>
    /// A loss distribution as its values, normalized first: its buckets' left edges, widths, densities, cumulative and
    /// excess probabilities, and averages.
    /// </summary>
    /// <param name="d">The distribution.</param>
    /// <param name="x">Receives the left edges.</param>
    /// <param name="dx">Receives the widths.</param>
    /// <param name="density">Receives the densities.</param>
    /// <param name="cumulative">Receives the cumulative probabilities.</param>
    /// <param name="excess">Receives the excess probabilities.</param>
    /// <param name="average">Receives the averages.</param>
    /// <param name="count">Receives the buckets' count.</param>
    public static void OutDistribution(Distribution d, double** x, double** dx, double** density, double** cumulative, double** excess, double** average, int* count)
    {
        d.Normalize();
        int k = d.Size;
        double* a = Alloc<double>(k), b = Alloc<double>(k), c = Alloc<double>(k), e = Alloc<double>(k), f = Alloc<double>(k), g = Alloc<double>(k);
        for (int i = 0; i < k; i++)
        {
            a[i] = d.X(i);
            b[i] = d.Dx(i);
            c[i] = d.Density(i);
            e[i] = d.Cumulative(i);
            f[i] = d.Excess(i);
            g[i] = d.Average(i);
        }

        *x = a;
        *dx = b;
        *density = c;
        *cumulative = e;
        *excess = f;
        *average = g;
        *count = k;
    }

    private static int Count(int n) => n >= 0 ? n : throw new ArgumentOutOfRangeException(nameof(n), n, "a count is not negative");

    private static TOut[] Map<TIn, TOut>(TIn* p, int n, Func<TIn, TOut> f)
        where TIn : unmanaged
    {
        if (p == null)
        {
            return null!;
        }

        var a = new TOut[Count(n)];
        for (int i = 0; i < a.Length; i++)
        {
            a[i] = f(p[i]);
        }

        return a;
    }
}
