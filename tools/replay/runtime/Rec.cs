// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NanoQL.Extensions.Conventions;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Replay;

/// <summary>
/// The twins' recorder: one JSON line per call into nanoql the vector suite makes, written as its C function's call,
/// with its arguments and its result in the C ABI's forms, objects by ids in order of appearance; a line per fact as it
/// starts. A struct behind a handle has its id by value: each use boxes a new copy, and an equal value is the same handle
/// to the replay. A call made from inside nanoql (a callback into the test code) is not written: nothing replays it. The trace
/// goes to the file <c>NANOQL_TRACE</c> names; without it nothing is written.
/// </summary>
public static class Rec
{
    private static readonly object Gate = new();
    private static readonly ConditionalWeakTable<object, StrongBox<long>> Ids = new();
    private static readonly Dictionary<object, long> Values = new(new ValueKey());
    private static readonly ConditionalWeakTable<object, StrongBox<object?>> Pendings = new();
    private static readonly ConditionalWeakTable<object, object> Shadows = new();
    private static readonly ConditionalWeakTable<object, object> Substitutes = new();
    private static readonly StreamWriter? Out = Open();
    private static long next;

    [ThreadStatic]
    private static int depth;

    /// <summary>Gets a value indicating whether a call now is written: a trace is open, and the call isn't inside another.</summary>
    public static bool On => Out != null && depth == 0;

    /// <summary>
    /// Gets a value indicating whether the conventions in force are QuantLib's, so a preset's lookup by name gives the
    /// object its constructor gave.
    /// </summary>
    public static bool UnderQuantLib => ConventionProvider.Current is ConventionSet s ? s.Equals(ConventionSet.QuantLib) : ReferenceEquals(ConventionProvider.Current, ConventionProvider.QuantLib);

    /// <summary>Starts a fact: every call after it, to the next, is the fact's.</summary>
    /// <param name="name">The fact's class and method.</param>
    public static void Fact(string name)
    {
        if (Out == null)
        {
            return;
        }

        lock (Gate)
        {
            Out.Write("{\"fact\":");
            Out.Write(System.Text.Json.JsonSerializer.Serialize(name));
            Out.Write("}\n");
        }
    }

    /// <summary>A new call's arguments.</summary>
    /// <returns>The writer.</returns>
    public static Args Args() => new();

    /// <summary>Marks a call into nanoql begun: the calls inside it aren't written.</summary>
    public static void Enter() => depth++;

    /// <summary>Marks a call into nanoql ended.</summary>
    public static void Leave() => depth--;

    /// <summary>Writes a call: its C function, its arguments and its result.</summary>
    /// <param name="function">The C function.</param>
    /// <param name="w">The arguments and the result.</param>
    public static void Call(string function, Args w) => Line(function, w.Text, null);

    /// <summary>Writes a refused call: its C function, its arguments and its status code.</summary>
    /// <param name="function">The C function.</param>
    /// <param name="w">The arguments.</param>
    /// <param name="e">The exception.</param>
    public static void Throws(string function, Args w, Exception e) => Line(function, w.Text, Code(e));

    /// <summary>An object's id, or null when the trace hasn't seen it.</summary>
    /// <param name="o">The object.</param>
    /// <returns>Its id.</returns>
    public static long? Id(object o)
    {
        if (o.GetType().IsValueType)
        {
            lock (Gate)
            {
                return Values.TryGetValue(o, out long v) ? v : null;
            }
        }

        return Ids.TryGetValue(o, out StrongBox<long>? id) ? id.Value : null;
    }

    /// <summary>A new object's id.</summary>
    /// <param name="o">The object.</param>
    /// <param name="fresh">Whether it was new to the trace.</param>
    /// <returns>Its id.</returns>
    public static long Assign(object o, out bool fresh)
    {
        lock (Gate)
        {
            if (o.GetType().IsValueType)
            {
                fresh = !Values.TryGetValue(o, out long v);
                return fresh ? Values[o] = ++next : v;
            }

            if (Ids.TryGetValue(o, out StrongBox<long>? id))
            {
                fresh = false;
                return id.Value;
            }

            fresh = true;
            long n = ++next;
            Ids.Add(o, new StrongBox<long>(n));
            Keep(o, o);
            return n;
        }
    }

    /// <summary>
    /// Marks a record its object initializer or with-expression is still building: the first call that takes it writes
    /// it as the C ABI builds it (<see cref="Twins.Materialize"/>).
    /// </summary>
    /// <param name="o">The record.</param>
    /// <param name="baseRecord">The record a with-expression copied, or null for a constructor.</param>
    public static void Pending(object o, object? baseRecord)
    {
        if (Out != null)
        {
            Pendings.AddOrUpdate(o, new StrongBox<object?>(baseRecord));
        }
    }

    /// <summary>An object's id, writing a pending record's construction first; null when the trace can't make it.</summary>
    /// <param name="o">The object.</param>
    /// <returns>Its id, or null.</returns>
    public static long? IdOrMaterialize(object o)
    {
        if (Id(o) is { } id)
        {
            return id;
        }

        if (!Pendings.TryGetValue(o, out StrongBox<object?>? b))
        {
            return null;
        }

        Pendings.Remove(o);
        return Twins.Materialize(o, b.Value!) ? Id(o) : null;
    }

    /// <summary>A new id for an object the replay makes on the way to another, which C# never had.</summary>
    /// <returns>The id.</returns>
    public static long NewId()
    {
        lock (Gate)
        {
            return ++next;
        }
    }

    /// <summary>Forgets an object: a later call that takes it stops its fact's replay.</summary>
    /// <param name="o">The object, or null.</param>
    public static void Forget(object? o)
    {
        if (o != null && o.GetType().IsValueType)
        {
            lock (Gate)
            {
                Values.Remove(o);
            }
        }
        else if (o != null)
        {
            Ids.Remove(o);
        }
    }

    /// <summary>
    /// Marks an object the trace wrote as another call's (a preset as its set's lookup), or reached from one: what it
    /// holds may be other objects than C#'s.
    /// </summary>
    /// <param name="o">The object.</param>
    public static void Substitute(object o)
    {
        if (!o.GetType().IsValueType)
        {
            Substitutes.AddOrUpdate(o, o);
        }
    }

    /// <summary>Whether the trace wrote an object as another call's, or reached it from one.</summary>
    /// <param name="o">The object.</param>
    /// <returns>Whether it did.</returns>
    public static bool IsSubstituted(object o) => !o.GetType().IsValueType && Substitutes.TryGetValue(o, out _);

    /// <summary>Whether a call gives its result without throwing, unwritten: a preset's lookup in place of its constructor.</summary>
    /// <param name="f">The call.</param>
    /// <returns>Whether it does.</returns>
    public static bool Gives(Func<object> f)
    {
        Enter();
        try
        {
            _ = f();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            Leave();
        }
    }

    /// <summary>A clone's copy of the generator as the trace has it: its source's.</summary>
    /// <param name="clone">The clone.</param>
    /// <param name="source">The generator it cloned.</param>
    public static void KeepFrom(object clone, object source)
    {
        if (Shadows.TryGetValue(source, out object? s))
        {
            Keep(clone, s);
        }
        else
        {
            Forget(clone);
        }
    }

    /// <summary>
    /// Whether a generator's draw is the one the trace's copy of it gives, the copy drawing too: false when C# moved the
    /// generator where the trace can't follow.
    /// </summary>
    /// <param name="g">The generator.</param>
    /// <param name="r">Its draw.</param>
    /// <returns>Whether the trace follows the generator.</returns>
    public static bool Follows(IRng g, Sample<double> r)
    {
        if (!Shadows.TryGetValue(g, out object? s))
        {
            return false;
        }

        Sample<double> t = ((IRng)s).Next();
        return Same(t.Value, r.Value) && Same(t.Weight, r.Weight);
    }

    /// <summary>
    /// Whether a generator's draw is the one the trace's copy of it gives, the copy drawing too: false when C# moved the
    /// generator where the trace can't follow.
    /// </summary>
    /// <param name="g">The generator.</param>
    /// <param name="r">Its draw.</param>
    /// <returns>Whether the trace follows the generator.</returns>
    public static bool Follows(IRsg g, Sample<ReadOnlyMemory<double>> r)
    {
        if (!Shadows.TryGetValue(g, out object? s))
        {
            return false;
        }

        Sample<ReadOnlyMemory<double>> t = ((IRsg)s).NextSequence();
        ReadOnlySpan<double> a = t.Value.Span;
        ReadOnlySpan<double> b = r.Value.Span;
        if (a.Length != b.Length || !Same(t.Weight, r.Weight))
        {
            return false;
        }

        for (int i = 0; i < a.Length; i++)
        {
            if (!Same(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gives an object an id the trace already wrote.</summary>
    /// <param name="id">The id.</param>
    /// <param name="o">The object.</param>
    public static void Rename(long id, object o)
    {
        if (o.GetType().IsValueType)
        {
            lock (Gate)
            {
                Values[o] = id;
            }
        }
        else
        {
            Ids.AddOrUpdate(o, new StrongBox<long>(id));
        }
    }

    /// <summary>Whether a constructor is a type's with the parameter types given.</summary>
    /// <param name="c">The constructor.</param>
    /// <param name="t">The type.</param>
    /// <param name="ps">The parameter types.</param>
    /// <returns>Whether it is.</returns>
    public static bool Is(ConstructorInfo c, Type t, params Type[] ps)
    {
        if (c.DeclaringType != t)
        {
            return false;
        }

        ParameterInfo[] q = c.GetParameters();
        if (q.Length != ps.Length)
        {
            return false;
        }

        for (int i = 0; i < q.Length; i++)
        {
            if (q[i].ParameterType != ps[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary><see cref="Activator.CreateInstance(Type)"/>, a preset through its twin.</summary>
    /// <param name="t">The type.</param>
    /// <returns>The object.</returns>
    public static object CreateInstance(Type t)
    {
        if (t.GetConstructor(Type.EmptyTypes) is { } c)
        {
            object? r;
            try
            {
                r = Twins.Reflected(c, []);
            }
            catch (Exception e)
            {
                throw new TargetInvocationException(e);
            }

            if (r != null)
            {
                return r;
            }
        }

        return Activator.CreateInstance(t)!;
    }

    /// <summary><see cref="ConstructorInfo.Invoke(object[])"/>, a preset through its twin.</summary>
    /// <param name="c">The constructor.</param>
    /// <param name="a">The arguments.</param>
    /// <returns>The object.</returns>
    public static object Invoke(ConstructorInfo c, object?[]? a)
    {
        object? r;
        try
        {
            r = Twins.Reflected(c, a ?? []);
        }
        catch (Exception e)
        {
            throw new TargetInvocationException(e);
        }

        return r ?? c.Invoke(a);
    }

    /// <summary>
    /// <see cref="ConstructorInfo.Invoke(BindingFlags, Binder, object[], CultureInfo)"/>, a preset through its twin.
    /// </summary>
    /// <param name="c">The constructor.</param>
    /// <param name="f">The binding flags.</param>
    /// <param name="b">The binder.</param>
    /// <param name="a">The arguments.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The object.</returns>
    public static object Invoke(ConstructorInfo c, BindingFlags f, Binder? b, object?[]? a, CultureInfo? culture)
    {
        object? r;
        try
        {
            r = Twins.Reflected(c, a ?? []);
        }
        catch (Exception e) when ((f & BindingFlags.DoNotWrapExceptions) == 0)
        {
            throw new TargetInvocationException(e);
        }

        return r ?? c.Invoke(f, b, a, culture);
    }

    /// <summary>A Matrix's or a Vector's values, copied, or null.</summary>
    /// <param name="o">The matrix or vector.</param>
    /// <returns>Its values.</returns>
    public static double[]? Contents(object? o) => o switch
    {
        NanoQL.Numerics.Vector v => v.AsSpan().ToArray(),
        NanoQL.Numerics.Matrix m => m.AsSpan().ToArray(),
        _ => null,
    };

    /// <summary>Whether a Matrix or a Vector still holds the values it held, bit for bit.</summary>
    /// <param name="o">The matrix or vector.</param>
    /// <param name="before">Its values before.</param>
    /// <returns>Whether they are the same.</returns>
    public static bool SameContents(object? o, double[]? before)
    {
        double[]? now = Contents(o);
        if (now == null || before == null)
        {
            return now == before;
        }

        return global::System.Runtime.InteropServices.MemoryMarshal.AsBytes(now.AsSpan()).SequenceEqual(global::System.Runtime.InteropServices.MemoryMarshal.AsBytes(before.AsSpan()));
    }

    /// <summary><c>++</c> on a date, for a twin of its operator.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The next date.</returns>
    public static NanoQL.Time.Date Increment(NanoQL.Time.Date d) => ++d;

    /// <summary><c>--</c> on a date, for a twin of its operator.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The previous date.</returns>
    public static NanoQL.Time.Date Decrement(NanoQL.Time.Date d) => --d;

    // A struct's key: its own equality and its doubles' bits, at any depth (a quantity of -0 isn't one of 0).
    private sealed class ValueKey : IEqualityComparer<object>
    {
        private static readonly ConcurrentDictionary<Type, FieldInfo[]> Fields = new();

        public new bool Equals(object? x, object? y) =>
            ReferenceEquals(x, y) || (x != null && y != null && x.GetType() == y.GetType() && x.Equals(y) && SameBits(x, y));

        public int GetHashCode(object o) => o.GetHashCode();

        private static bool SameBits(object x, object y)
        {
            foreach (FieldInfo f in Fields.GetOrAdd(x.GetType(), t => t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)))
            {
                Type ft = f.FieldType;
                if (ft == typeof(double) || ft == typeof(float) || (ft.IsValueType && !ft.IsPrimitive && !ft.IsEnum))
                {
                    object? a = f.GetValue(x);
                    object? b = f.GetValue(y);
                    bool same = (a, b) switch
                    {
                        (double p, double q) => BitConverter.DoubleToInt64Bits(p) == BitConverter.DoubleToInt64Bits(q),
                        (float p, float q) => BitConverter.SingleToInt32Bits(p) == BitConverter.SingleToInt32Bits(q),
                        (null, null) => true,
                        ({ } p, { } q) => SameBits(p, q),
                        _ => false,
                    };
                    if (!same)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    // Keeps a generator's copy as the trace has it: the replay draws from that copy.
    private static void Keep(object o, object state)
    {
        if (state is IRsg s)
        {
            Shadows.AddOrUpdate(o, s.Clone());
        }
        else if (state is IRng g)
        {
            Shadows.AddOrUpdate(o, g.Clone());
        }
    }

    private static bool Same(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

    // The C ABI's code for an exception's kind (docs/bindings.md, "Errors").
    private static int Code(Exception e) => e switch
    {
        NanoQLException => 1,
        ArgumentNullException => 3,
        ArgumentOutOfRangeException => 4,
        ArgumentException => 2,
        InvalidCastException => 6,
        KeyNotFoundException => 8,
        System.Xml.Schema.XmlSchemaValidationException or System.Xml.XmlException or FormatException => 13,
        IOException or UnauthorizedAccessException => 12,
        NotSupportedException => 7,
        InvalidOperationException => 5,
        OutOfMemoryException => 11,
        _ => 99,
    };

    private static void Line(string function, string text, int? code)
    {
        var sb = new StringBuilder(text.Length + function.Length + 16);
        sb.Append("{\"f\":\"").Append(function).Append("\",").Append(text);
        if (code is { } c)
        {
            sb.Append(",\"t\":").Append(c);
        }

        sb.Append("}\n");
        lock (Gate)
        {
            Out!.Write(sb.ToString());
        }
    }

    private static StreamWriter? Open()
    {
        string? path = Environment.GetEnvironmentVariable("NANOQL_TRACE");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var w = new StreamWriter(path, false, new UTF8Encoding(false), 1 << 20);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            lock (Gate)
            {
                w.Flush();
            }
        };
        return w;
    }
}
