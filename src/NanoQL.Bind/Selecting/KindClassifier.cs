// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Bind.Model;
using NanoQL.Bind.Reading;

namespace NanoQL.Bind.Selecting;

/// <summary>
/// Maps a C# parameter or result type to the kind it crosses as (docs/bindings.md, "Kinds"), or says why it doesn't.
/// </summary>
internal sealed class KindClassifier(WellKnown known)
{
    private static readonly HashSet<string> Sequences =
    [
        "System.Collections.Immutable.ImmutableArray`1", "System.Collections.Generic.IEnumerable`1",
        "System.Collections.Generic.IReadOnlyList`1", "System.Collections.Generic.IReadOnlyCollection`1",
        "System.Collections.Generic.IList`1", "System.Collections.Generic.ICollection`1", "System.Collections.Generic.List`1",
        "System.ReadOnlySpan`1", "System.Span`1", "System.Collections.Immutable.ImmutableSortedSet`1",
        "System.Collections.Immutable.ImmutableHashSet`1", "System.Collections.Generic.IReadOnlySet`1",
        "System.Collections.Immutable.IImmutableList`1", "System.Collections.Immutable.ImmutableList`1",
    ];

    private static readonly HashSet<string> Maps =
    [
        "System.Collections.Generic.IReadOnlyDictionary`2", "System.Collections.Generic.IDictionary`2",
        "System.Collections.Generic.Dictionary`2", "System.Collections.Immutable.ImmutableDictionary`2",
        "System.Collections.Immutable.ImmutableSortedDictionary`2", "System.Collections.Immutable.IImmutableDictionary`2",
        "System.Collections.Frozen.FrozenDictionary`2",
    ];

    /// <summary>The kind a type crosses as.</summary>
    /// <param name="t">The parameter's or result's type.</param>
    /// <param name="p">The parameter, for <c>in</c> and tuple names; null for a result.</param>
    /// <param name="isReturn">Whether it is a result.</param>
    /// <param name="engine">A delegate's engine shape, when the delegate is one engines fit; null otherwise.</param>
    /// <param name="why">Why it doesn't cross, when the result is null.</param>
    /// <returns>The kind, or null.</returns>
    public CKind? Classify(Type t, ParameterInfo? p, bool isReturn, Func<Type, EngineParameter?>? engine, out string? why)
    {
        why = null;
        if (t.IsByRef)
        {
            if (p != null && p.IsIn && !isReturn)
            {
                t = t.GetElementType()!;
            }
            else
            {
                why = "by-ref or out parameter";
                return null;
            }
        }

        if (t.IsPointer)
        {
            why = "pointer";
            return null;
        }

        if (t.IsDelegate())
        {
            if (!isReturn && engine?.Invoke(t) is { } shape)
            {
                return new CKind("engine", t) { Engine = shape };
            }

            why = isReturn ? "returns a delegate (a function nanoql made)" : "delegate (the caller's own function)";
            return null;
        }

        if (t.IsGenericParameter || t.ContainsGenericParameters)
        {
            why = "open generic in signature";
            return null;
        }

        if (Scalar(t) is { } scalar)
        {
            return scalar;
        }

        if (t == known.Distribution && known.IsWorking(t))
        {
            if (isReturn)
            {
                return new CKind("distribution", t);
            }

            why = "takes a loss distribution (a working type)";
            return null;
        }

        if (t.NullableOf() is { } u)
        {
            CKind? inner = Scalar(u);
            if (inner is { Name: "double" or "int32" or "int64" or "bool" or "date" or "period" or "enum" or "complex" })
            {
                return new CKind(inner.Name + "?", t);
            }

            // A marked struct stays off handles nullable too: Nullable<W> carries no mark of its own.
            if (known.IsWorking(u))
            {
                why = "takes or returns a working type: " + TypeNamer.Simple(u) + "?";
                return null;
            }

            if (known.Set.Ours(u) && u.IsValueType)
            {
                return new CKind("boxed?", t);
            }

            why = "nullable of " + u.Name;
            return null;
        }

        if (t.IsGenericType && Maps.Contains(t.GetGenericTypeDefinition().FullName!))
        {
            return MapOf(t, t.GetGenericArguments()[0], t.GetGenericArguments()[1], out why);
        }

        if (t.IsGenericType && t.GetGenericTypeDefinition().FullName!.StartsWith("System.ValueTuple`", StringComparison.Ordinal))
        {
            if (!isReturn)
            {
                why = "tuple parameter";
                return null;
            }

            return TupleOf(t, p, out why);
        }

        if (t.IsArray || (t.IsGenericType && Sequences.Contains(t.GetGenericTypeDefinition().FullName!)))
        {
            return SequenceOf(t, isReturn, out why);
        }

        if (known.IsWorking(t))
        {
            if (known.IsGenerator(t) && !t.IsConstructedGenericType)
            {
                return new CKind("snapshot", t);
            }

            why = "takes or returns a working type: " + TypeNamer.Simple(t);
            return null;
        }

        if (t.IsValueType)
        {
            if (known.Set.Ours(t))
            {
                return new CKind("boxed", t);
            }

            why = "BCL struct " + TypeNamer.Simple(t);
            return null;
        }

        if (t.IsTrait())
        {
            why = "trait interface (static abstract members) as a value";
            return null;
        }

        if (t.FullName == "System.Object" || known.Set.Ours(t))
        {
            return new CKind("handle", t);
        }

        why = (t.Namespace ?? string.Empty).StartsWith("System.Collections.Immutable", StringComparison.Ordinal)
            ? "immutable collection " + TypeNamer.Simple(t)
            : "BCL class " + TypeNamer.Simple(t);
        return null;
    }

    private CKind? Scalar(Type t) => t.FullName switch
    {
        "System.Double" => new CKind("double", t),
        "System.Int32" => new CKind("int32", t),
        "System.Int64" => new CKind("int64", t),
        "System.Boolean" => new CKind("bool", t),
        "System.String" => new CKind("string", t),
        "System.Numerics.Complex" => new CKind("complex", t),
        _ when t == known.Date => new CKind("date", t),
        _ when t == known.Period => new CKind("period", t),
        // Matrix and Vector cross as arrays because they're working types: unmarked, either would be a handle.
        _ when t == known.Matrix && known.IsWorking(t) => new CKind("matrix", t),
        _ when t == known.Vector && known.IsWorking(t) => new CKind("vector", t),
        _ when t.IsEnum && known.Set.Ours(t) => new CKind("enum", t),
        _ => null,
    };

    // An element of a collection: a scalar, a nullable double or integer, or an object.
    private CKind? ElementOf(Type el)
    {
        if (Scalar(el) is { Name: not ("matrix" or "vector") } s)
        {
            return s;
        }

        if (el.NullableOf() is { } u && Scalar(u) is { Name: "double" or "int32" or "date" } n)
        {
            return new CKind(n.Name + "?", el);
        }

        if (!el.IsValueType && !el.IsGenericParameter && !el.ContainsGenericParameters && known.Set.Ours(el) && !el.IsDelegate()
            && !el.IsTrait() && !known.IsWorking(el))
        {
            return new CKind("handle", el);
        }

        return null;
    }

    private CKind? SequenceOf(Type t, bool isReturn, out string? why)
    {
        why = null;
        if (t.IsArray && t.GetArrayRank() != 1)
        {
            why = "multi-dimensional array";
            return null;
        }

        Type el = t.IsArray ? t.GetElementType()! : t.GetGenericArguments()[0];
        bool span = t.IsConstructionOf("System.Span`1");
        bool readOnlySpan = t.IsConstructionOf("System.ReadOnlySpan`1");
        if (el.IsConstructionOf("System.Collections.Generic.KeyValuePair`2"))
        {
            return span ? Fail("writable span of pairs", out why) : MapOf(t, el.GetGenericArguments()[0], el.GetGenericArguments()[1], out why);
        }

        bool inner = el.IsArray || (el.IsGenericType && Sequences.Contains(el.GetGenericTypeDefinition().FullName!));
        if (inner && !span)
        {
            Type innerEl = el.IsArray ? el.GetElementType()! : el.GetGenericArguments()[0];
            if (innerEl.FullName == "System.Double" && !el.IsConstructionOf("System.Span`1") && !el.IsConstructionOf("System.ReadOnlySpan`1"))
            {
                return new CKind("jagged", t) { Element = new CKind("double", innerEl) };
            }
        }

        CKind? ek = ElementOf(el);
        if (ek == null)
        {
            why = "collection of " + (el.IsGenericType ? TypeNamer.Simple(el) : el.IsArray ? "arrays" : TypeNamer.Simple(el));
            return null;
        }

        if (span)
        {
            if (ek.Name is not ("double" or "int32"))
            {
                return Fail("writable span of " + ek.Name, out why);
            }

            if (isReturn)
            {
                return Fail("returned span", out why);
            }

            return new CKind("span", t) { Element = ek };
        }

        if (readOnlySpan && isReturn && ek.Name == "handle")
        {
            return Fail("returned span of objects", out why);
        }

        return new CKind("array", t) { Element = ek };
    }

    private CKind? MapOf(Type t, Type key, Type value, out string? why)
    {
        why = null;
        CKind? k = ElementOf(key);
        CKind? v = ElementOf(value);
        if (k == null || v == null || k.Name.EndsWith('?') || k.Name == "handle")
        {
            why = "dictionary of " + TypeNamer.Simple(key) + " to " + TypeNamer.Simple(value);
            return null;
        }

        return new CKind("map", t) { Element = k, Value = v };
    }

    private CKind? TupleOf(Type t, ParameterInfo? p, out string? why)
    {
        why = null;
        Type[] args = t.GetGenericArguments();
        if (args.Length > 7)
        {
            return Fail("tuple of more than seven elements", out why);
        }

        var items = new List<CKind>();
        foreach (Type a in args)
        {
            CKind? k = Classify(a, null, true, null, out string? inner);
            if (k == null || k.Name is "tuple" or "distribution")
            {
                why = "tuple element: " + (inner ?? k!.Name);
                return null;
            }

            items.Add(k);
        }

        // The tuple's declared names, from the result's TupleElementNames, else item1...
        string?[] names = new string?[args.Length];
        if (p?.Member is MethodInfo m && m.ReturnParameter.GetCustomAttributesData()
                .FirstOrDefault(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.TupleElementNamesAttribute") is { } attr
            && attr.ConstructorArguments[0].Value is IReadOnlyCollection<CustomAttributeTypedArgument> declared)
        {
            names = [.. declared.Take(args.Length).Select(d => (string?)d.Value)];
        }

        return new CKind("tuple", t) { Items = items, ItemNames = [.. names.Select((n, i) => n ?? "item" + (i + 1))] };
    }

    private static CKind? Fail(string reason, out string? why)
    {
        why = reason;
        return null;
    }
}
