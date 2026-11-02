// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using NanoQL.Extensions.Conventions;
using NanoQL.Time;

namespace NanoQL.Excel;

/// <summary>The convention set at the cell (XLR9) and the names of calendars and day counters in it (XLR16).</summary>
public static partial class X
{
    // Each name built once per provider: the objects are immutable, and the provider's identity is in every key.
    private static readonly ConcurrentDictionary<(string Provider, string Kind, string Name), object> Named = new();
    private static long namedBuilds;

    /// <summary>
    /// The convention set a member is called on: an empty argument reads <see cref="ConventionProvider.Current"/> as a
    /// set, the current set or QuantLib's while Current is QuantLib's provider, as the native host reads a NULL set.
    /// </summary>
    /// <param name="a">The Excel value.</param>
    /// <returns>The set.</returns>
    public static ConventionSet SelfSet(object a)
    {
        if (!IsMissing(One(a)))
        {
            return Obj<ConventionSet>(a, "self");
        }

        if (XlSettings.LoadError is { } error)
        {
            throw new XlSettingsException(error);
        }

        ConventionProvider current = ConventionProvider.Current;
        return current as ConventionSet
            ?? (ReferenceEquals(current, ConventionProvider.QuantLib)
                ? ConventionSet.QuantLib
                : throw new InvalidOperationException("the current convention provider is not a convention set: it has no names to look up; pass a set"));
    }

    // A calendar or a day counter by its name in the current set, as an empty set argument reads it (XLR16).
    private static object ByName(Type t, string name, string p)
    {
        bool calendar = t == typeof(Calendar);
        return Named.GetOrAdd((ProviderId, t.Name, name), _ =>
        {
            ConventionSet set = SelfSet(ExcelDna.Integration.ExcelMissing.Value);
            Interlocked.Increment(ref namedBuilds);
            try
            {
                return calendar ? set.Calendar(name) : set.DayCounter(name);
            }
            catch (ArgumentException e) when (e is not XlHandleException)
            {
                throw new ArgumentException($"{p}: no {(calendar ? "calendar" : "day counter")} named \"{name}\" in the current conventions ({e.Message})", p, e);
            }
        });
    }
}
