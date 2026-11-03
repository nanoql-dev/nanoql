// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using NanoQL.Experimental;
using NanoQL.Extensions.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// The no-bypass walk: a set whose every calendar carries a marker holiday, whose every currency a marker symbol and
/// whose every unit of measure a marker name, and a walk over everything an object reaches, by reflection over its
/// fields, that names each calendar, day counter, currency and unit the set didn't give. The allowlist is the technical
/// <see cref="NullCalendar"/> and the <see cref="Actual365Fixed"/> an engine or term structure builds for itself.
/// </summary>
internal static class ConventionWalk
{
    /// <summary>The marker holiday: the first Wednesday from June 2187 that is a business day on every calendar
    /// preset.</summary>
    public static readonly Date Marker = FirstCommonBusinessDay();

    /// <summary>The marker symbol every currency carries.</summary>
    public const string MarkerSymbol = "marker";

    /// <summary>The marker every unit of measure's name ends with.</summary>
    public const string MarkerUnit = " (marker)";

    /// <summary>Gets the marker set: QuantLib's presets, each calendar preset with the marker holiday, each currency
    /// with the marker symbol, each unit of measure with the marker name.</summary>
    public static ConventionSet Set { get; } = ConventionSet.QuantLib.With(MarkerSnapshot());

    /// <summary>The day counter every index record of <see cref="IndexSet"/> takes, which no index preset uses.</summary>
    public const string MarkerDayCounter = "SimpleDayCounter";

    /// <summary>Gets the marker set with each index preset's record on the marker day counter, its fixed leg's too.</summary>
    public static ConventionSet IndexSet { get; } = Set.With(IndexMarkerSnapshot());

    /// <summary>The calendars, day counters, currencies and units an object reaches that the marker set didn't give.</summary>
    /// <param name="root">The object.</param>
    /// <param name="dayCounters">Whether a day counter must be the marker one, the <see cref="Actual365Fixed"/> an
    /// engine or term structure builds for itself apart: an index's, not a calendar's or a currency's.</param>
    /// <returns>Each one's path from the root and its value, empty when the set gave all.</returns>
    public static IReadOnlyList<string> Unmarked(object root, bool dayCounters = false)
    {
        var found = new List<string>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Walk(root, root.GetType().Name, found, seen, 0, dayCounters);
        return found;
    }

    private static void Walk(object value, string path, List<string> found, HashSet<object> seen, int depth, bool dayCounters)
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
            case UnitOfMeasure u when !u.Name.EndsWith(MarkerUnit, StringComparison.Ordinal):
                found.Add($"{path}: {u.Code}");
                break;
            case DayCounter d when dayCounters && d is not SimpleDayCounter && d is not Actual365Fixed:
                found.Add($"{path}: {d.Name}");
                break;
        }

        if (value is IEnumerable items and not string)
        {
            int i = 0;
            foreach (object? item in items)
            {
                if (item is not null)
                {
                    Walk(item, $"{path}[{i}]", found, seen, depth + 1, dayCounters);
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
                    Walk(child, $"{path}.{f.Name}", found, seen, depth + 1, dayCounters);
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

    // Each index preset's record, on its own, with the marker day counter.
    private static ConventionSnapshot IndexMarkerSnapshot()
    {
        ConventionSnapshot s = ConventionSnapshot.Empty;
        foreach (ConventionRecord r in ConventionSet.QuantLib.Layers[0].Records)
        {
            ConventionRecord? marked = r switch
            {
                IborIndexRecord => new IborIndexRecord(r.Name, r.Name) { DayCounter = MarkerDayCounter },
                IborFamilyRecord => new IborFamilyRecord(r.Name, r.Name) { DayCounter = MarkerDayCounter },
                OvernightIndexRecord => new OvernightIndexRecord(r.Name, r.Name) { DayCounter = MarkerDayCounter },
                SwapIndexRecord => new SwapIndexRecord(r.Name, r.Name) { FixedLegDayCounter = MarkerDayCounter },
                _ => null,
            };
            if (marked is not null)
            {
                s = s.Add(marked);
            }
        }

        return s;
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

        foreach (UnitOfMeasureRecord u in ConventionSet.QuantLib.Layers[0].Records.OfType<UnitOfMeasureRecord>())
        {
            s = s.Add(new UnitOfMeasureRecord(u.Name, u.Name) { UnitName = u.UnitName + MarkerUnit });
        }

        return s;
    }
}
