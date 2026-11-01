// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Time;
using Vector = NanoQL.Numerics.Vector;

namespace NanoQL.Replay;

/// <summary>
/// One call's arguments, then its result, as JSON in the C ABI's forms: a double as its 16 hexadecimal digits, an
/// integer or a date's serial as a number, a period as its length and unit, an object as its id, null for null, and an
/// object the trace hasn't seen as <c>{"x": its type}</c>, where a replay stops. A writable span's values after the call
/// follow the result.
/// </summary>
public sealed class Args
{
    private readonly StringBuilder sb = new("\"a\":[");
    private bool spoiled;
    private bool first = true;
    private bool result;
    private int depth;
    private bool substituted;

    /// <summary>Gets the call's JSON members: its arguments, its result and its spans after the call.</summary>
    public string Text
    {
        get
        {
            if (spoiled)
            {
                return "\"a\":[{\"x\":\"a capture failed\"}],\"r\":null";
            }

            if (!result)
            {
                sb.Append(']');
                result = true;
            }

            return sb.ToString();
        }
    }

    /// <summary>Marks the call unreplayable: an argument or the result couldn't be written.</summary>
    public void Spoil() => spoiled = true;

    /// <summary>Writes the result of a call that returns nothing.</summary>
    public void Void() => Raw("null");

    /// <summary>Opens the writable spans' values after the call, each a list in order.</summary>
    public void BeginAfter()
    {
        sb.Append(",\"w\":[");
        first = true;
    }

    /// <summary>Closes the writable spans' values after the call.</summary>
    public void EndAfter()
    {
        sb.Append(']');
        first = false;
    }

    /// <summary>Ends the arguments; what's written next is the result.</summary>
    public void Result()
    {
        sb.Append("],\"r\":");
        result = true;
        first = true;
    }

    /// <summary>Opens a tuple's elements.</summary>
    public void Open()
    {
        Sep();
        sb.Append('[');
        depth++;
        first = true;
    }

    /// <summary>Closes a tuple's elements.</summary>
    public void Close()
    {
        sb.Append(']');
        depth--;
        first = false;
    }

    /// <summary>A double, by its bits.</summary>
    /// <param name="x">The value.</param>
    public void Double(double x) => Raw("\"" + BitConverter.DoubleToInt64Bits(x).ToString("x16", CultureInfo.InvariantCulture) + "\"");

    /// <summary>An integer.</summary>
    /// <param name="x">The value.</param>
    public void Int(int x) => Raw(x.ToString(CultureInfo.InvariantCulture));

    /// <summary>A long integer.</summary>
    /// <param name="x">The value.</param>
    public void Long(long x) => Raw(x.ToString(CultureInfo.InvariantCulture));

    /// <summary>A boolean.</summary>
    /// <param name="x">The value.</param>
    public void Bool(bool x) => Raw(x ? "true" : "false");

    /// <summary>A string, or null.</summary>
    /// <param name="x">The value.</param>
    public void Str(string? x) => Raw(x == null ? "null" : JsonSerializer.Serialize(x));

    /// <summary>A date's serial; 0 is QuantLib's <c>Date()</c>.</summary>
    /// <param name="x">The date.</param>
    public void Date(Date x) => Int(x.SerialNumber);

    /// <summary>A nullable date's serial; 0 is none.</summary>
    /// <param name="x">The date.</param>
    public void OptDate(Date? x) => Int(x?.SerialNumber ?? 0);

    /// <summary>A period: its length and unit.</summary>
    /// <param name="x">The period.</param>
    public void Period(Period x) => Raw($"[{x.Length},{(int)x.Units}]");

    /// <summary>A nullable period.</summary>
    /// <param name="x">The period.</param>
    public void OptPeriod(Period? x)
    {
        if (x is { } p)
        {
            Period(p);
        }
        else
        {
            Raw("null");
        }
    }

    /// <summary>A complex number: its parts' bits.</summary>
    /// <param name="x">The value.</param>
    public void Complex(Complex x) => Raw($"[\"{BitConverter.DoubleToInt64Bits(x.Real):x16}\",\"{BitConverter.DoubleToInt64Bits(x.Imaginary):x16}\"]");

    /// <summary>A nullable complex number.</summary>
    /// <param name="x">The value.</param>
    public void OptComplex(Complex? x)
    {
        if (x is { } c)
        {
            Complex(c);
        }
        else
        {
            Raw("null");
        }
    }

    /// <summary>A nullable double.</summary>
    /// <param name="x">The value.</param>
    public void OptDouble(double? x)
    {
        if (x is { } d)
        {
            Double(d);
        }
        else
        {
            Raw("null");
        }
    }

    /// <summary>A nullable integer.</summary>
    /// <param name="x">The value.</param>
    public void OptInt(int? x) => Raw(x is { } i ? i.ToString(CultureInfo.InvariantCulture) : "null");

    /// <summary>A nullable long integer.</summary>
    /// <param name="x">The value.</param>
    public void OptLong(long? x) => Raw(x is { } i ? i.ToString(CultureInfo.InvariantCulture) : "null");

    /// <summary>A nullable boolean.</summary>
    /// <param name="x">The value.</param>
    public void OptBool(bool? x) => Raw(x is { } b ? (b ? "true" : "false") : "null");

    /// <summary>An argument object: its id, null, or the type of an object the trace hasn't seen, where a replay stops.</summary>
    /// <param name="o">The object.</param>
    public void Obj(object? o)
    {
        // A call on a preset the trace wrote as its set's lookup: the lookup's object may hold other objects than the
        // constructor's (a currency's rounding, shared among the coded presets), so its results start new ids.
        if (first && !result && depth == 0 && o != null && Rec.IsSubstituted(o))
        {
            substituted = true;
        }

        if (o is null)
        {
            Raw("null");
        }
        else if (Rec.IdOrMaterialize(o) is { } id)
        {
            Long(id);
        }
        else
        {
            Opaque(o.GetType().Name);
        }
    }

    /// <summary>
    /// A result object: new to the trace (<c>{"n": id, "y": type}</c>, its .NET type's full name, as
    /// <c>nql_type_name</c> gives it), or one it has seen (<c>{"s": id}</c>).
    /// </summary>
    /// <param name="o">The object.</param>
    public void New(object? o)
    {
        if (o is null)
        {
            Raw("null");
            return;
        }

        long id = Rec.Assign(o, out bool fresh);
        if (substituted)
        {
            if (!fresh)
            {
                id = Rec.NewId();
                Rec.Rename(id, o);
                fresh = true;
            }

            Rec.Substitute(o);
        }

        // Reached through a substituted preset, the object may be of another class than C#'s, as it may be another object
        // (the set's currencies hold a Rounding where the coded ones hold a ClosestRounding): no class to check.
        Raw(!fresh ? $"{{\"s\":{id}}}" : substituted ? $"{{\"n\":{id}}}" : $"{{\"n\":{id},\"y\":{JsonSerializer.Serialize(o.GetType().FullName)}}}");
    }

    /// <summary>A result object C# never had: one a pending record's construction makes on the way.</summary>
    /// <returns>Its id.</returns>
    public long NewTemporary()
    {
        long id = Rec.NewId();
        Raw($"{{\"n\":{id}}}");
        return id;
    }

    /// <summary>An argument a replay can't make: a delegate, an object the trace hasn't seen.</summary>
    /// <param name="what">What it is.</param>
    public void Opaque(string what) => Raw("{\"x\":" + JsonSerializer.Serialize(what) + "}");

    /// <summary>Doubles.</summary>
    /// <param name="x">The values, or null.</param>
    public void Doubles(IEnumerable<double>? x)
    {
        if (x is null)
        {
            Raw("null");
            return;
        }

        List(x, (w, v) => w.Double(v));
    }

    /// <summary>Doubles in a span.</summary>
    /// <param name="x">The values.</param>
    public void Doubles(ReadOnlySpan<double> x) => List(x.ToArray(), (w, v) => w.Double(v));

    /// <summary>Doubles in a writable span.</summary>
    /// <param name="x">The values.</param>
    public void Doubles(Span<double> x) => List(x.ToArray(), (w, v) => w.Double(v));

    /// <summary>Integers.</summary>
    /// <param name="x">The values, or null.</param>
    public void Ints(IEnumerable<int>? x)
    {
        if (x is null)
        {
            Raw("null");
            return;
        }

        List(x, (w, v) => w.Int(v));
    }

    /// <summary>Integers in a span.</summary>
    /// <param name="x">The values.</param>
    public void Ints(ReadOnlySpan<int> x) => List(x.ToArray(), (w, v) => w.Int(v));

    /// <summary>Integers in a writable span.</summary>
    /// <param name="x">The values.</param>
    public void Ints(Span<int> x) => List(x.ToArray(), (w, v) => w.Int(v));

    /// <summary>A collection, each element by its writer.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="x">The elements, or null.</param>
    /// <param name="element">The element's writer.</param>
    public void List<T>(IEnumerable<T>? x, Action<Args, T> element)
    {
        if (x is null)
        {
            Raw("null");
            return;
        }

        if (x is System.Collections.Immutable.ImmutableArray<T> { IsDefault: true })
        {
            // A default immutable array: the C ABI has no form for it.
            Opaque("default ImmutableArray");
            return;
        }

        Sep();
        sb.Append('[');
        depth++;
        first = true;
        foreach (T v in x)
        {
            element(this, v);
        }

        sb.Append(']');
        depth--;
        first = false;
    }

    /// <summary>A jagged array: its rows.</summary>
    /// <typeparam name="TRow">The row's type.</typeparam>
    /// <param name="x">The rows, or null.</param>
    public void Jagged<TRow>(IEnumerable<TRow>? x)
        where TRow : IEnumerable<double> => List(x, (w, row) => w.Doubles(row));

    /// <summary>A dictionary or pairs: each pair as its key and value.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="x">The pairs, or null.</param>
    /// <param name="key">The key's writer.</param>
    /// <param name="value">The value's writer.</param>
    public void Map<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>>? x, Action<Args, TKey> key, Action<Args, TValue> value) =>
        List(x, (w, kv) =>
        {
            w.Open();
            key(w, kv.Key);
            value(w, kv.Value);
            w.Close();
        });

    /// <summary>A matrix: its rows, columns and values by rows.</summary>
    /// <param name="m">The matrix, or null.</param>
    public void Matrix(Matrix? m)
    {
        if (m is null)
        {
            Raw("null");
            return;
        }

        var v = new double[m.Rows * m.Columns];
        for (int i = 0; i < m.Rows; i++)
        {
            for (int j = 0; j < m.Columns; j++)
            {
                v[(i * m.Columns) + j] = m[i, j];
            }
        }

        Sep();
        sb.Append($"{{\"r\":{m.Rows},\"c\":{m.Columns},\"v\":");
        first = true;
        Doubles(v);
        sb.Append('}');
        first = false;
    }

    /// <summary>A vector's values.</summary>
    /// <param name="v">The vector, or null.</param>
    public void Vector(Vector? v) => Doubles(v is null ? null : Enumerable.Range(0, v.Size).Select(i => v[i]).ToArray());

    /// <summary>A loss distribution's values, normalized: edges, widths, densities, cumulative, excess, averages.</summary>
    /// <param name="d">The distribution.</param>
    public void Distribution(Distribution d)
    {
        d.Normalize();
        int k = d.Size;
        Open();
        Doubles(Enumerable.Range(0, k).Select(d.X).ToArray());
        Doubles(Enumerable.Range(0, k).Select(i => d.Dx(i)).ToArray());
        Doubles(Enumerable.Range(0, k).Select(d.Density).ToArray());
        Doubles(Enumerable.Range(0, k).Select(d.Cumulative).ToArray());
        Doubles(Enumerable.Range(0, k).Select(d.Excess).ToArray());
        Doubles(Enumerable.Range(0, k).Select(d.Average).ToArray());
        Close();
    }

    private void Sep()
    {
        if (!first)
        {
            sb.Append(',');
        }

        first = false;
    }

    private void Raw(string s)
    {
        Sep();
        sb.Append(s);
    }
}
