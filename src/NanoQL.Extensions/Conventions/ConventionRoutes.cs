// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

// What the instrument conventions' routes share: a field a route can't take is refused unless the route uses the
// record's value anyway, a field a route ties to another is taken where the two agree, and a field a route needs and
// no record gives is refused; each refusal names the route, the kind, the record and the field.
internal static class ConventionRoutes
{
    // A field the route can't take: refused where the record gives a value other than the one the route uses.
    public static void Untaken<T>(string route, ConventionRecord r, string field, T? given, T uses)
    {
        if (given is not null && !EqualityComparer<T>.Default.Equals(given, uses))
        {
            throw Refusal(route, r, field, $"it uses {Show(uses)}, not {Show(given)}");
        }
    }

    // A field the route can't take and uses no value of: refused where the record gives one.
    public static void Untaken(string route, ConventionRecord r, string field, object? given)
    {
        if (given is not null)
        {
            throw Refusal(route, r, field, "it takes none");
        }
    }

    // Two fields the route takes as one: their value where they agree, refused where they don't.
    public static T Tied<T>(string route, ConventionRecord r, string field, T value, string otherField, T other) =>
        EqualityComparer<T>.Default.Equals(value, other)
            ? value
            : throw Refusal(route, r, field, $"it takes one value for {field} and {otherField}, which are {Show(value)} and {Show(other)}");

    // A field the route needs where no record gives it and the builder has no default.
    public static T Required<T>(ConventionRecord r, string field, T? value)
        where T : class =>
        value ?? throw new ArgumentException($"the {ConventionKinds.Element(r)} {r.Name} gives no {field}", "name");

    public static T Required<T>(ConventionRecord r, string field, T? value)
        where T : struct =>
        value ?? throw new ArgumentException($"the {ConventionKinds.Element(r)} {r.Name} gives no {field}", "name");

    public static ArgumentException Refusal(string route, ConventionRecord r, string field, string why) =>
        new($"{route} can't take the {ConventionKinds.Element(r)} {r.Name}'s {field}: {why}", "name");

    // An Ibor index a record names, an index or a family at a tenor, built in the set on its forwarding curve.
    public static IborIndex Ibor(ConventionSet set, string name, Period? tenor, YieldTermStructure? h) =>
        tenor is Period t ? set.IborIndex(name, t, h) : set.IborIndex(name, h);

    // A swap index a record names, a swap index family's or an overnight-indexed swap index's at a tenor, built in the
    // set on its curves: one curve, or an exogenous discount curve beside it, which an overnight-indexed one takes none of.
    public static SwapIndex SwapIndexOf(ConventionSet set, string name, Period tenor, YieldTermStructure? forwarding, YieldTermStructure? discounting)
    {
        if (set.IsOvernightIndexedSwapIndex(name))
        {
            return discounting is null
                ? set.OvernightIndexedSwapIndex(name, tenor, forwarding)
                : throw new ArgumentException($"the overnight-indexed swap index {name} takes no discount curve", nameof(discounting));
        }

        return discounting is null ? set.SwapIndex(name, tenor, forwarding) : set.SwapIndex(name, tenor, forwarding, discounting);
    }

    // A name a record gives, resolved through the set, or none.
    public static Calendar? CalendarOf(ConventionSet set, string? name) => name is null ? null : set.Calendar(name);

    public static DayCounter? DayCounterOf(ConventionSet set, string? name) => name is null ? null : set.DayCounter(name);

    private static string Show(object? value) => value switch
    {
        null => "none",
        Calendar c => c.Name,
        DayCounter d => d.Name,
        _ => value.ToString() ?? "",
    };
}
