// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using NanoQL.Extensions.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// The no-bypass walk (CVD34): a set whose every calendar carries a marker holiday and whose every currency a marker
/// symbol, and a walk over everything an object reaches, by reflection over its fields, that names each calendar, day
/// counter and currency the set didn't give. The allowlist is the technical <see cref="NullCalendar"/> and the
/// <see cref="Actual365Fixed"/> an engine or term structure builds for itself.
/// </summary>
internal static class ConventionWalk
{
    /// <summary>The marker holiday: the first Wednesday from June 2187 that is a business day on every calendar
    /// preset.</summary>
    public static readonly Date Marker = FirstCommonBusinessDay();

    /// <summary>The marker symbol every currency carries.</summary>
    public const string MarkerSymbol = "marker";

    /// <summary>Gets the marker set: QuantLib's presets, each calendar preset with the marker holiday, each currency
    /// with the marker symbol.</summary>
    public static ConventionSet Set { get; } = ConventionSet.QuantLib.With(MarkerSnapshot());

    /// <summary>The calendars, day counters and currencies an object reaches that the marker set didn't give.</summary>
    /// <param name="root">The object.</param>
    /// <returns>Each one's path from the root and its value, empty when the set gave all.</returns>
    public static IReadOnlyList<string> Unmarked(object root)
    {
        var found = new List<string>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Walk(root, root.GetType().Name, found, seen, 0);
        return found;
    }

    private static void Walk(object value, string path, List<string> found, HashSet<object> seen, int depth)
    {
        Type type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || value is string or Date || depth > 40 || (!type.IsValueType && !seen.Add(value)))
        {
            return;
        }

        switch (value)
        {
            case NullCalendar:
                return;
            case Calendar c when c.IsBusinessDay(Marker):
                found.Add($"{path}: {c.Name}");
                break;
            case Currency c when c.Symbol != MarkerSymbol:
                found.Add($"{path}: {c.Code}");
                break;
        }

        if (value is IEnumerable items and not string)
        {
            int i = 0;
            foreach (object? item in items)
            {
                if (item is not null)
                {
                    Walk(item, $"{path}[{i}]", found, seen, depth + 1);
                }

                ++i;
            }

            if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>)))
            {
                return;
            }
        }

        for (Type? t = type; t is not null && t != typeof(object); t = t.BaseType)
        {
            foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (f.FieldType.IsPointer || f.FieldType.IsByRefLike || typeof(Delegate).IsAssignableFrom(f.FieldType))
                {
                    continue;
                }

                if (f.GetValue(value) is { } child)
                {
                    Walk(child, $"{path}.{f.Name}", found, seen, depth + 1);
                }
            }
        }
    }

    private static Date FirstCommonBusinessDay()
    {
        Calendar[] presets = [.. CodedPresets.Calendars.Where(p => p.Key != "NullCalendar").Select(p => p.Value())];
        for (Date d = new(6, Month.June, 2187); ; d += 7)
        {
            if (presets.All(c => c.IsBusinessDay(d)))
            {
                return d;
            }
        }
    }

    private static ConventionSnapshot MarkerSnapshot()
    {
        ConventionSnapshot s = ConventionSnapshot.Empty;
        foreach (CalendarPresetRecord p in ConventionSet.QuantLib.Layers[0].Records.OfType<CalendarPresetRecord>().Where(p => p.Name != "NullCalendar"))
        {
            s = s.Add(new CalendarRecord(p.Name, p.Name) { AddedHolidays = [Marker] });
        }

        foreach (CurrencyPresetRecord c in ConventionSet.QuantLib.Layers[0].Records.OfType<CurrencyPresetRecord>())
        {
            s = s.Add(new CurrencyPresetRecord(c.Name, c.Name) { Symbol = MarkerSymbol });
        }

        return s;
    }
}
