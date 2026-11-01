// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Conventions;
using NanoQL.Numerics;

namespace NanoQL.Native;

/// <summary>
/// The runtime the generated export shims call: objects through handles (this file), values and strings, collections,
/// and errors (the other <c>Rt.*.cs</c> files).
/// </summary>
internal static unsafe partial class Rt
{
    /// <summary>Issues a handle to an object.</summary>
    /// <param name="o">The object; null gives NULL.</param>
    /// <returns>The handle, the caller's.</returns>
    public static nint New(object? o) => HandleTable.New(o);

    /// <summary>The object a parameter's handle refers to, or null for NULL.</summary>
    /// <typeparam name="T">The parameter's type.</typeparam>
    /// <param name="h">The handle.</param>
    /// <param name="param">The parameter's name.</param>
    /// <returns>The object.</returns>
    public static T Get<T>(nint h, string param)
        where T : class
    {
        if (h == 0)
        {
            return null!;
        }

        object o = HandleTable.Resolve(h, param);
        return o as T ?? throw new HandleException(10, $"{param}: a {TypeName(o)} where a {typeof(T).Name} is expected", param);
    }

    /// <summary>The instance a member is called on: NULL is refused as the parameter <c>self</c>.</summary>
    /// <typeparam name="T">The member's type.</typeparam>
    /// <param name="h">The handle.</param>
    /// <returns>The object.</returns>
    public static T Self<T>(nint h)
        where T : class => h == 0 ? throw new ArgumentNullException("self") : Get<T>(h, "self");

    /// <summary>A parameter no NULL may take.</summary>
    /// <typeparam name="T">The parameter's type.</typeparam>
    /// <param name="h">The handle.</param>
    /// <param name="param">The parameter's name.</param>
    /// <returns>The object.</returns>
    public static T Required<T>(nint h, string param)
        where T : class => h == 0 ? throw new ArgumentNullException(param) : Get<T>(h, param);

    /// <summary>
    /// The convention set a lookup is called on: NULL is the current provider read as a set, QuantLib's set where it is
    /// the core's own provider, and refused where it is a user's provider, which has no names.
    /// </summary>
    /// <param name="h">The set's handle, or NULL.</param>
    /// <returns>The set.</returns>
    public static ConventionSet SelfSet(nint h)
    {
        if (h != 0)
        {
            return Get<ConventionSet>(h, "self");
        }

        ConventionProvider current = ConventionProvider.Current;
        return current as ConventionSet
            ?? (ReferenceEquals(current, ConventionProvider.QuantLib)
                ? ConventionSet.QuantLib
                : throw new InvalidOperationException("the current convention provider is not a convention set: it has no names to look up; pass a set"));
    }

    /// <summary>A nanoql struct behind a handle.</summary>
    /// <typeparam name="T">The struct.</typeparam>
    /// <param name="h">The handle, not NULL.</param>
    /// <param name="param">The parameter's name.</param>
    /// <returns>The value.</returns>
    public static T Unbox<T>(nint h, string param)
        where T : struct
    {
        if (h == 0)
        {
            throw new ArgumentNullException(param);
        }

        object o = HandleTable.Resolve(h, param);
        return o is T t ? t : throw new HandleException(10, $"{param}: a {TypeName(o)} where a {typeof(T).Name} is expected", param);
    }

    /// <summary>A private copy of a snapshot's generator, for a member that takes a generator (a composite copies what it's given).</summary>
    /// <typeparam name="T">The parameter's type.</typeparam>
    /// <param name="h">The snapshot's handle, or NULL.</param>
    /// <param name="param">The parameter's name.</param>
    /// <returns>The copy.</returns>
    public static T Generator<T>(nint h, string param)
        where T : class
    {
        if (h == 0)
        {
            return null!;
        }

        object g = Get<GeneratorSnapshot>(h, param).Generator;
        return Clone(g) as T ?? throw new HandleException(10, $"{param}: a {TypeName(g)} snapshot where a {typeof(T).Name} is expected", param);
    }

    /// <summary>The generator a snapshot holds, never drawn from itself.</summary>
    /// <typeparam name="T">The generator's type.</typeparam>
    /// <param name="h">The snapshot's handle.</param>
    /// <returns>The generator.</returns>
    public static T SnapshotOf<T>(nint h)
        where T : class
    {
        if (h == 0)
        {
            throw new ArgumentNullException("self");
        }

        object g = Get<GeneratorSnapshot>(h, "self").Generator;
        return g as T ?? throw new HandleException(10, $"self: a {TypeName(g)} snapshot where a {typeof(T).Name} is expected", "self");
    }

    /// <summary>A snapshot of a generator a member returns: a private copy.</summary>
    /// <param name="generator">The generator.</param>
    /// <returns>The snapshot's handle.</returns>
    public static nint NewSnapshot(object? generator) => generator is null ? 0 : New(new GeneratorSnapshot(Clone(generator)));

    /// <summary>A handle's object's .NET type, a snapshot's its generator's.</summary>
    /// <param name="o">The object.</param>
    /// <returns>The type's full name.</returns>
    public static string TypeName(object o) => (o is GeneratorSnapshot s ? s.Generator : o).GetType().FullName!;

    /// <summary><c>++</c> on a value, for an operator that crosses as a function.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The next one.</returns>
    public static NanoQL.Time.Date Increment(NanoQL.Time.Date d) => ++d;

    /// <summary><c>--</c> on a value, for an operator that crosses as a function.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The previous one.</returns>
    public static NanoQL.Time.Date Decrement(NanoQL.Time.Date d) => --d;

    private static object Clone(object g) => g switch
    {
        IRng r => r.Clone(),
        IRsg s => s.Clone(),
        _ => throw new HandleException(10, $"a {g.GetType().Name} is not a generator", null),
    };
}
