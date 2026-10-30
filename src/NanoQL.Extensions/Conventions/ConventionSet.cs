// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// An immutable set of convention records, layered snapshot on snapshot, and the <see cref="ConventionProvider"/>
/// that answers from them: QuantLib's shipped presets (<see cref="QuantLib"/>) or none (<see cref="Empty"/>), with a
/// user's files on top.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="QuantLib"/> is the shipped records; <see cref="ConventionProvider.Default"/>,
/// <see cref="ConventionProvider.Current"/> and <see cref="ConventionProvider.Use"/> are the provider's, so a set
/// configures the system as <c>ConventionProvider.Default = ConventionSet.QuantLib.With(...)</c>.
/// </para>
/// <para>
/// A lookup builds a new object each time, inside the set's scope, so a preset comes out as its own class with the
/// set's values, and an index keeps the set as its provider. A field comes from its record, then the record's base
/// chain, then QuantLib's reference. Calendars, day counters and currencies are code kinds: a name the set doesn't
/// define, but a coded preset has, resolves to QuantLib's preset, with no edits, even on <see cref="Empty"/>.
/// </para>
/// <para>
/// A record named after a coded preset must be one the preset's class can take, since a coded construction can't
/// become another kind: a calendar preset takes its own preset record or a calendar record based on itself, a day
/// counter or currency preset a record of its own kind based on itself, and an alias between two presets only where
/// QuantLib shares their implementation. <see cref="With"/> refuses any other, and resolves every record of the new
/// set, so a name a later layer breaks, a cycle or a value the core refuses fails when the file is loaded.
/// </para>
/// <para>
/// Two sets are equal when they hold equal snapshots in the same order on the same base.
/// </para>
/// </remarks>
public sealed class ConventionSet : ConventionProvider, IEquatable<ConventionSet>
{
    private static readonly Lazy<ConventionSet> Shipped = new(
        () => Validated(new ConventionSet(true, [ConventionsXml.ReadPresets()]), 0));

    // The currencies whose data this thread is building, within one synchronous construction.
    [ThreadStatic]
    private static HashSet<string>? resolvingCurrencies;

    private readonly bool onQuantLib;
    private readonly ImmutableArray<ConventionSnapshot> layers;

    // Each name's records, topmost layer first, by value kind; an alias under its own kind.
    private readonly Dictionary<(ConventionKind Kind, string Name), List<Entry>> index = [];
    private readonly int hash;

    private ConventionSet(bool onQuantLib, ImmutableArray<ConventionSnapshot> layers)
    {
        this.onQuantLib = onQuantLib;
        this.layers = layers;
        var h = default(HashCode);
        h.Add(onQuantLib);
        for (int layer = 0; layer < layers.Length; ++layer)
        {
            h.Add(layers[layer]);
            ConventionSnapshot s = layers[layer];
            for (int i = 0; i < s.Records.Length; ++i)
            {
                ConventionRecord r = s.Records[i];
                var key = (ConventionKinds.Of(r), r.Name);
                if (!index.TryGetValue(key, out List<Entry>? entries))
                {
                    index[key] = entries = [];
                }

                entries.Insert(0, new Entry(layer, r, s.Sources[i]));
            }
        }

        hash = h.ToHashCode();
    }

    /// <summary>
    /// Gets QuantLib's shipped presets: every calendar, day counter and currency QuantLib codes, with QuantLib's
    /// values, and FpML's exact codes as aliases. It hides the provider's <see cref="ConventionProvider.QuantLib"/>,
    /// QuantLib's coded values, which give the same presets.
    /// </summary>
    public static new ConventionSet QuantLib => Shipped.Value;

    /// <summary>
    /// Gets a set without QuantLib's records, for files that define every convention themselves: calendars, day
    /// counters and currencies still resolve to QuantLib's coded presets, which are code.
    /// </summary>
    public static ConventionSet Empty { get; } = new(false, []);

    /// <summary>Gets a value indicating whether the set is built on QuantLib's shipped presets.</summary>
    public bool IsOnQuantLib => onQuantLib;

    /// <summary>Gets the snapshots the set layers, bottom first: QuantLib's shipped one first on
    /// <see cref="QuantLib"/>.</summary>
    public ImmutableArray<ConventionSnapshot> Layers => layers;

    /// <summary>
    /// A set with a snapshot layered on top: its records override the set's of the same names, and the rest stay.
    /// </summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <returns>The new set; the receiver and <see cref="ConventionProvider.Default"/> are unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// A record names an unknown record, takes a coded preset's name in a kind its class can't take, edits a preset's
    /// QuantLib name, defines a currency QuantLib doesn't have, lacks a field it can't take from a base, or is part
    /// of a cycle; or the core refuses a record's value.
    /// </exception>
    public ConventionSet With(ConventionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Validated(new ConventionSet(onQuantLib, layers.Add(snapshot)), layers.Length);
    }

    /// <summary>A calendar by name, built in the set: a preset as its own class with the set's edits.</summary>
    /// <param name="name">The calendar's name: a preset's, a record's or an alias.</param>
    /// <returns>A new calendar.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No calendar has the name.</exception>
    public Calendar Calendar(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        using (Use(this))
        {
            return CalendarOf(name, layers.Length, [], nameof(name));
        }
    }

    /// <summary>A day counter by name, built in the set.</summary>
    /// <param name="name">The day counter's name: a preset's, a record's or an alias.</param>
    /// <returns>A new day counter.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">No day counter has the name.</exception>
    public DayCounter DayCounter(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        using (Use(this))
        {
            return DayCounterOf(name, layers.Length, [], nameof(name));
        }
    }

    /// <summary>A currency by its code, built in the set: the preset's class with the set's data.</summary>
    /// <param name="code">The currency's ISO 4217 code, or an alias.</param>
    /// <returns>A new currency.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    /// <exception cref="ArgumentException">No currency has the code.</exception>
    public Currency Currency(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        using (Use(this))
        {
            return CurrencyOf(code, [], nameof(code));
        }
    }

    /// <summary>
    /// Where each name the set defines comes from, QuantLib's presets or a file's record, and a warning for each
    /// record that overrides no name and that no record references, as a likely misspelling.
    /// </summary>
    /// <returns>The report.</returns>
    public ConventionProvenance Provenance()
    {
        var origins = new List<ConventionOrigin>();
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (ConventionSnapshot s in layers)
        {
            foreach (ConventionRecord r in s.Records)
            {
                foreach (string name in References(r))
                {
                    referenced.Add(name);
                }
            }
        }

        var warnings = new List<string>();
        foreach (((ConventionKind kind, string name), List<Entry> entries) in index.OrderBy(e => e.Key.Kind).ThenBy(e => e.Key.Name, StringComparer.Ordinal))
        {
            Entry top = entries[0];
            bool shipped = onQuantLib && top.Layer == 0;
            origins.Add(new ConventionOrigin(kind, name, ConventionKinds.Element(top.Record), shipped ? null : top.Source));
            bool overrides = entries.Count > 1 || (onQuantLib && top.Layer > 0 && IsShippedName(name)) || IsCodedPreset(name);
            if (!shipped && !overrides && !referenced.Contains(name) && top.Record is not AliasRecord)
            {
                warnings.Add($"{ConventionKinds.Element(top.Record)} {name}{Where(top.Source)} overrides no name and nothing references it");
            }
        }

        return new ConventionProvenance(origins, warnings);
    }

    /// <inheritdoc/>
    public override CalendarEdits ResolveCalendarEdits(string preset, CalendarEdits reference)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return EditsOf(preset, layers.Length) ?? reference;
    }

    /// <inheritdoc/>
    public override CurrencyData ResolveCurrency(string code, Func<CurrencyData> reference)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(reference);
        if (MergedCurrency(code, layers.Length) is not { } r)
        {
            return reference();
        }

        // a triangulation currency is built through this callback again, so a cycle of them is caught here
        HashSet<string> resolving = resolvingCurrencies ??= new(StringComparer.Ordinal);
        if (!resolving.Add(code))
        {
            throw new ArgumentException($"the currency {code} is part of a cycle of triangulation currencies", "snapshot");
        }

        try
        {
            return new CurrencyData(
                r.QuantLibName!,
                code,
                r.NumericCode!.Value,
                r.Symbol!,
                r.FractionSymbol!,
                r.FractionsPerUnit!.Value,
                r.Rounding!.ToRounding(),
                r.TriangulationCurrency is { Length: > 0 } tri ? CurrencyOf(tri, [], "code") : null);
        }
        finally
        {
            resolving.Remove(code);
        }
    }

    /// <inheritdoc/>
    public override Calendar ResolveBusiness252Calendar(Func<Calendar> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return Business252CalendarOf(layers.Length) is string calendar
            ? CalendarOf(calendar, layers.Length, [], "calendar")
            : reference();
    }

    /// <inheritdoc/>
    public bool Equals(ConventionSet? other) =>
        other is not null && (ReferenceEquals(this, other)
            || (hash == other.hash && onQuantLib == other.onQuantLib && layers.SequenceEqual(other.layers)));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ConventionSet other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => hash;

    // The shipped presets' names, a coded preset's or an FpML code's: names a user's file may reuse though they hold '/'.
    private static bool IsShippedName(string name) =>
        Shipped.Value.index.Keys.Any(k => k.Name == name);

    // A name ConventionRecords.Describe generates for a part, name/n, beside the record of its name in one snapshot.
    private static bool IsGenerated(string name, ConventionSnapshot snapshot)
    {
        int slash = name.LastIndexOf('/');
        string root = name[..slash], n = name[(slash + 1)..];
        return slash > 0 && n.Length > 0 && n[0] != '0' && n.All(char.IsAsciiDigit)
            && snapshot.Records.Any(r => r.Name == root);
    }

    private static bool IsCodedPreset(string name) =>
        PresetFactories.Calendars.ContainsKey(name) || PresetFactories.DayCounters.ContainsKey(name)
        || PresetFactories.Currencies.ContainsKey(name);

    // QuantLib's own aliases between coded presets, its shared implementations: the only aliases a preset's name takes.
    private static string? QuantLibAlias(string name) =>
        Shipped.Value.index.TryGetValue((ConventionKind.Alias, name), out List<Entry>? e) && IsCodedPreset(name)
            ? e[0].Record.Base
            : null;

    private static ConventionSet Validated(ConventionSet set, int from)
    {
        for (int layer = from; layer < set.layers.Length; ++layer)
        {
            ConventionSnapshot s = set.layers[layer];
            for (int i = 0; i < s.Records.Length; ++i)
            {
                set.Check(s.Records[i], s.Sources[i], layer);
            }
        }

        // every record of the new set, its earlier layers' included, resolves
        foreach (((ConventionKind kind, string name), List<Entry> entries) in set.index)
        {
            try
            {
                using (Use(set))
                {
                    _ = kind switch
                    {
                        ConventionKind.Calendar => (object)set.CalendarOf(name, set.layers.Length, [], "snapshot"),
                        ConventionKind.DayCounter => set.DayCounterOf(name, set.layers.Length, [], "snapshot"),
                        ConventionKind.Currency => set.CurrencyOf(name, [], "snapshot"),
                        _ => set.AliasTarget(name, []),
                    };
                }
            }
            catch (Exception e) when (e is ArgumentException or NanoQLException)
            {
                Entry top = entries[0];
                throw new ArgumentException(
                    $"{ConventionKinds.Element(top.Record)} {name}{Where(top.Source)}: {e.Message}", "snapshot", e);
            }
        }

        return set;
    }

    private static string Where(string? source) => source is null ? "" : $" ({source})";

    private static ArgumentException Unknown(string kind, string name, string paramName) =>
        new($"unknown {kind} {name}", paramName);

    // The names a record refers to.
    private static IEnumerable<string> References(ConventionRecord r)
    {
        if (r.Base is string b && b != r.Name)
        {
            yield return b;
        }

        switch (r)
        {
            case JointCalendarRecord j:
                foreach (string m in j.Members)
                {
                    yield return m;
                }

                break;
            case DayCounterPresetRecord { Calendar: string c }:
                yield return c;
                break;
            case DayCounterRecord { Calendar: string c }:
                yield return c;
                break;
            case CurrencyPresetRecord { TriangulationCurrency: { Length: > 0 } t }:
                yield return t;
                break;
        }
    }

    private static CalendarEdits Combine(CalendarEdits lower, ImmutableArray<Date> added, ImmutableArray<Date> removed)
    {
        ImmutableSortedSet<Date> a = lower.Added.Except(removed).Union(added);
        ImmutableSortedSet<Date> r = lower.Removed.Except(added).Union(removed);
        return new CalendarEdits(a, r);
    }

    // A record's own rules, checked when its snapshot is layered on.
    private void Check(ConventionRecord r, string? source, int layer)
    {
        string where = $"{ConventionKinds.Element(r)} {r.Name}{Where(source)}";
        void Refuse(string why) => throw new ArgumentException($"{where}: {why}", "snapshot");

        bool shippedLayer = onQuantLib && layer == 0;
        if (!shippedLayer && r.Name.Contains('/') && !IsShippedName(r.Name) && !IsGenerated(r.Name, layers[layer]))
        {
            Refuse("a name of a user's own may not hold '/', which the names ConventionRecords.Describe generates take");
        }

        string? alias = shippedLayer ? null : QuantLibAlias(r.Name);
        switch (r)
        {
            case CalendarPresetRecord p:
                if (!PresetFactories.Calendars.ContainsKey(p.Name))
                {
                    Refuse("no calendar preset has the name");
                }

                if (alias is not null)
                {
                    Refuse($"QuantLib's calendar shares {alias}'s implementation: edit {alias}");
                }

                CheckBase(p, Refuse);
                if (p.Base is null && p.QuantLibName is null)
                {
                    Refuse("QuantLibName is missing");
                }

                CheckQuantLibName(p.QuantLibName, () => PresetFactories.Calendars[p.Name]().Name, Refuse);
                CheckEdits(p.Name, p.AddedHolidays, p.RemovedHolidays, Refuse);
                break;
            case CalendarRecord c:
                if (PresetFactories.Calendars.ContainsKey(c.Name))
                {
                    if (alias is not null)
                    {
                        Refuse($"QuantLib's calendar shares {alias}'s implementation: edit {alias}");
                    }

                    if (c.Base != c.Name)
                    {
                        Refuse("a calendar preset takes only a calendar record based on itself");
                    }
                }

                CheckEdits(c.Name, c.AddedHolidays, c.RemovedHolidays, Refuse);
                break;
            case JointCalendarRecord or BespokeCalendarRecord:
                if (PresetFactories.Calendars.ContainsKey(r.Name))
                {
                    Refuse("a calendar preset takes no joint or bespoke calendar");
                }

                break;
            case DayCounterPresetRecord d:
                if (!PresetFactories.DayCounters.ContainsKey(d.Name))
                {
                    Refuse("no day counter preset has the name");
                }

                if (alias is not null)
                {
                    Refuse($"QuantLib's day counter shares {alias}'s implementation: use {alias}");
                }

                CheckBase(d, Refuse);
                if (d.Base is null && d.QuantLibName is null)
                {
                    Refuse("QuantLibName is missing");
                }

                CheckQuantLibName(d.QuantLibName, () => PresetFactories.DayCounters[d.Name]().Name, Refuse);
                if (d.Calendar is not null && d.Name != "Business252")
                {
                    Refuse("only Business252 counts a calendar's business days");
                }

                break;
            case DayCounterRecord d:
                if (PresetFactories.DayCounters.ContainsKey(d.Name) && d.Base != d.Name)
                {
                    Refuse("a day counter preset takes only a day counter record based on itself");
                }

                break;
            case CurrencyPresetRecord cur:
                if (!PresetFactories.Currencies.ContainsKey(cur.Name))
                {
                    Refuse("no currency of QuantLib's has the code: a user may not define one");
                }

                CheckBase(cur, Refuse);
                if (cur.Base is null && (cur.QuantLibName is null || cur.NumericCode is null || cur.Symbol is null
                    || cur.FractionSymbol is null || cur.FractionsPerUnit is null || cur.Rounding is null))
                {
                    Refuse("a field is missing: QuantLibName, NumericCode, Symbol, FractionSymbol, FractionsPerUnit and Rounding are required without a base");
                }

                CheckQuantLibName(cur.QuantLibName, () => PresetFactories.Currencies[cur.Name]().Name, Refuse);
                CheckQuantLibNumericCode(cur.NumericCode, () => PresetFactories.Currencies[cur.Name]().NumericCode, Refuse);
                if (cur.Rounding is { } rounding)
                {
                    _ = rounding.ToRounding();
                }

                break;
            case AliasRecord a:
                // the shipped aliases are the ones QuantLib's shared implementations allow
                if (!shippedLayer && IsCodedPreset(a.Name) && alias != a.Base)
                {
                    Refuse("a preset's name takes an alias only where QuantLib shares the presets' implementation");
                }

                break;
        }
    }

    // A preset record's base: none, or its own name for the record it replaces.
    private static void CheckBase(ConventionRecord r, Action<string> refuse)
    {
        if (r.Base is string b && b != r.Name)
        {
            refuse("a preset record takes only its own name as its base");
        }
    }

    // QuantLib's numeric code of a currency, built with QuantLib's conventions: with the code and the name, the
    // currency's identity, which its equality and the exchange-rate registries read.
    private static void CheckQuantLibNumericCode(int? given, Func<int> quantLibs, Action<string> refuse)
    {
        if (given is null)
        {
            return;
        }

        int expected;
        using (Use(ConventionProvider.QuantLib))
        {
            expected = quantLibs();
        }

        if (given != expected)
        {
            refuse($"QuantLib's numeric code is {expected}, not {given}");
        }
    }

    // QuantLib's name of a coded preset, built with QuantLib's conventions.
    private static void CheckQuantLibName(string? given, Func<string> quantLibs, Action<string> refuse)
    {
        if (given is null)
        {
            return;
        }

        string expected;
        using (Use(ConventionProvider.QuantLib))
        {
            expected = quantLibs();
        }

        if (given != expected)
        {
            refuse($"QuantLib's name is {expected}, not {given}");
        }
    }

    private static void CheckEdits(string name, ImmutableArray<Date> added, ImmutableArray<Date> removed, Action<string> refuse)
    {
        if (name == "NullCalendar" && !(added.IsDefaultOrEmpty && removed.IsDefaultOrEmpty))
        {
            refuse("NullCalendar is a technical placeholder and takes no edits");
        }

        if (added.Intersect(removed).FirstOrDefault() is { SerialNumber: > 0 } both)
        {
            refuse($"{both} is both added and removed");
        }
    }

    // The topmost record of a value kind, or an alias, named name in the layers below upTo.
    private Entry? Top(ConventionKind kind, string name, int upTo)
    {
        Entry? best = null;
        foreach (ConventionKind k in new[] { kind, ConventionKind.Alias })
        {
            if (index.TryGetValue((k, name), out List<Entry>? entries))
            {
                foreach (Entry e in entries)
                {
                    if (e.Layer < upTo)
                    {
                        if (best is null || e.Layer > best.Value.Layer)
                        {
                            best = e;
                        }

                        break;
                    }
                }
            }
        }

        return best;
    }

    private static void Enter(HashSet<string> visiting, string key, string name)
    {
        if (!visiting.Add(key))
        {
            throw new ArgumentException($"{name} is part of a cycle", "snapshot");
        }
    }

    private Calendar CalendarOf(string name, int upTo, HashSet<string> visiting, string paramName)
    {
        string key = $"calendar:{name}:{upTo}";
        Enter(visiting, key, name);
        try
        {
            if (Top(ConventionKind.Calendar, name, upTo) is not Entry e)
            {
                return PresetFactories.Calendars.TryGetValue(name, out Func<Calendar>? preset)
                    ? preset()
                    : throw Unknown("calendar", name, paramName);
            }

            switch (e.Record)
            {
                case AliasRecord a:
                    return CalendarOf(a.Base!, layers.Length, visiting, paramName);
                case CalendarPresetRecord:
                    // its edits, through the chain, come through ResolveCalendarEdits
                    return PresetFactories.Calendars[name]();
                case CalendarRecord c when c.Base == name:
                    return PresetFactories.Calendars.TryGetValue(name, out Func<Calendar>? coded)
                        ? coded()
                        : Edit(CalendarOf(name, e.Layer, visiting, paramName), c);
                case CalendarRecord c:
                    return Edit(CalendarOf(c.Base!, layers.Length, visiting, paramName), c);
                case JointCalendarRecord j:
                {
                    (JointCalendarRule rule, ImmutableArray<string> members) = JointParts(j, e.Layer, visiting);
                    if (members.Length < 2)
                    {
                        throw new ArgumentException($"the joint calendar {name} needs two or more members", paramName);
                    }

                    return new JointCalendar([.. members.Select(m => CalendarOf(m, layers.Length, visiting, paramName))], rule);
                }

                case BespokeCalendarRecord b:
                    return Bespoke(b, e.Layer, visiting);
                default:
                    throw Unknown("calendar", name, paramName);
            }
        }
        finally
        {
            visiting.Remove(key);
        }
    }

    private static Calendar Edit(Calendar calendar, CalendarRecord c)
    {
        foreach (Date d in c.AddedHolidays)
        {
            calendar = calendar.AddHoliday(d);
        }

        foreach (Date d in c.RemovedHolidays)
        {
            calendar = calendar.RemoveHoliday(d);
        }

        return calendar;
    }

    // A joint record's rule and members, each from the record, else its base's.
    private (JointCalendarRule Rule, ImmutableArray<string> Members) JointParts(JointCalendarRecord j, int layer, HashSet<string> visiting)
    {
        if (j.Base is null)
        {
            return (j.Rule ?? JointCalendarRule.JoinHolidays, j.Members);
        }

        string key = $"joint:{j.Name}:{layer}";
        Enter(visiting, key, j.Name);
        try
        {
            int upTo = j.Base == j.Name ? layer : layers.Length;
            if (Top(ConventionKind.Calendar, j.Base, upTo) is not { Record: JointCalendarRecord b } e)
            {
                throw new ArgumentException($"the joint calendar {j.Name}'s base {j.Base} is no joint calendar", "snapshot");
            }

            (JointCalendarRule rule, ImmutableArray<string> members) = JointParts(b, e.Layer, visiting);
            return (j.Rule ?? rule, j.Members.IsDefaultOrEmpty ? members : j.Members);
        }
        finally
        {
            visiting.Remove(key);
        }
    }

    private Calendar Bespoke(BespokeCalendarRecord b, int layer, HashSet<string> visiting)
    {
        var weekends = new List<Weekday>();
        var holidays = new List<Date>();
        var removed = new List<Date>();
        void Collect(BespokeCalendarRecord r, int at)
        {
            if (r.Base is string baseName)
            {
                string key = $"bespoke:{r.Name}:{at}";
                Enter(visiting, key, r.Name);
                int upTo = baseName == r.Name ? at : layers.Length;
                if (Top(ConventionKind.Calendar, baseName, upTo) is not { Record: BespokeCalendarRecord br } e)
                {
                    throw new ArgumentException($"the bespoke calendar {r.Name}'s base {baseName} is no bespoke calendar", "snapshot");
                }

                Collect(br, e.Layer);
                visiting.Remove(key);
            }

            weekends.AddRange(r.Weekends);
            holidays.AddRange(r.Holidays);
            removed.AddRange(r.RemovedHolidays);
        }

        Collect(b, layer);
        BespokeCalendar bespoke = new(b.Name);
        foreach (Weekday w in weekends)
        {
            bespoke = bespoke.AddWeekend(w);
        }

        Calendar calendar = bespoke;
        foreach (Date d in holidays)
        {
            calendar = calendar.AddHoliday(d);
        }

        foreach (Date d in removed)
        {
            calendar = calendar.RemoveHoliday(d);
        }

        return calendar;
    }

    // A calendar preset's edits through its records' chain below upTo, or null where the set has no record of it.
    private CalendarEdits? EditsOf(string preset, int upTo)
    {
        Entry? found = null;
        if (index.TryGetValue((ConventionKind.Calendar, preset), out List<Entry>? entries))
        {
            found = entries.FirstOrDefault(e => e.Layer < upTo);
            found = found is { Record: not null } ? found : null;
        }

        return found switch
        {
            { Record: CalendarPresetRecord p } f => Combine(
                p.Base == preset ? EditsOf(preset, f.Layer) ?? CalendarEdits.None : CalendarEdits.None,
                p.AddedHolidays,
                p.RemovedHolidays),
            { Record: CalendarRecord c } f => Combine(EditsOf(preset, f.Layer) ?? CalendarEdits.None, c.AddedHolidays, c.RemovedHolidays),
            _ => null,
        };
    }

    // Business252's calendar's name through its records' chain below upTo, or null for QuantLib's reference.
    private string? Business252CalendarOf(int upTo)
    {
        if (!index.TryGetValue((ConventionKind.DayCounter, "Business252"), out List<Entry>? entries)
            || entries.FirstOrDefault(e => e.Layer < upTo) is not { Record: not null } found)
        {
            return null;
        }

        string? calendar = found.Record switch
        {
            DayCounterPresetRecord p => p.Calendar,
            DayCounterRecord d => d.Calendar,
            _ => null,
        };
        return calendar ?? (found.Record.Base == "Business252" ? Business252CalendarOf(found.Layer) : null);
    }

    private DayCounter DayCounterOf(string name, int upTo, HashSet<string> visiting, string paramName)
    {
        string key = $"daycounter:{name}:{upTo}";
        Enter(visiting, key, name);
        try
        {
            if (Top(ConventionKind.DayCounter, name, upTo) is not Entry e)
            {
                return PresetFactories.DayCounters.TryGetValue(name, out Func<DayCounter>? preset)
                    ? preset()
                    : throw Unknown("day counter", name, paramName);
            }

            switch (e.Record)
            {
                case AliasRecord a:
                    return DayCounterOf(a.Base!, layers.Length, visiting, paramName);
                case DayCounterPresetRecord:
                    return PresetFactories.DayCounters[name]();
                case DayCounterRecord d when d.Base == name && PresetFactories.DayCounters.TryGetValue(name, out Func<DayCounter>? coded):
                    return coded();
                case DayCounterRecord d:
                {
                    DayCounter b = d.Base == name
                        ? DayCounterOf(name, e.Layer, visiting, paramName)
                        : DayCounterOf(d.Base!, layers.Length, visiting, paramName);
                    if (d.Calendar is not string calendar)
                    {
                        return b;
                    }

                    return b is Business252
                        ? new Business252(CalendarOf(calendar, layers.Length, visiting, paramName))
                        : throw new ArgumentException($"the day counter {name}'s base {d.Base} counts no calendar's days: only Business252 does", paramName);
                }

                default:
                    throw Unknown("day counter", name, paramName);
            }
        }
        finally
        {
            visiting.Remove(key);
        }
    }

    private Currency CurrencyOf(string code, HashSet<string> visiting, string paramName)
    {
        string key = $"currency:{code}";
        Enter(visiting, key, code);
        try
        {
            Entry? e = Top(ConventionKind.Currency, code, layers.Length);
            if (e?.Record is AliasRecord a)
            {
                return CurrencyOf(a.Base!, visiting, paramName);
            }

            return PresetFactories.Currencies.TryGetValue(code, out Func<Currency>? preset)
                ? preset()
                : throw Unknown("currency", code, paramName);
        }
        finally
        {
            visiting.Remove(key);
        }
    }

    // A currency preset's record merged through its chain below upTo, or null where the set has none.
    private CurrencyPresetRecord? MergedCurrency(string code, int upTo)
    {
        if (!index.TryGetValue((ConventionKind.Currency, code), out List<Entry>? entries)
            || entries.FirstOrDefault(e => e.Layer < upTo) is not { Record: CurrencyPresetRecord r } found)
        {
            return null;
        }

        if (r.Base != code || MergedCurrency(code, found.Layer) is not { } lower)
        {
            return r;
        }

        return r with
        {
            QuantLibName = r.QuantLibName ?? lower.QuantLibName,
            NumericCode = r.NumericCode ?? lower.NumericCode,
            Symbol = r.Symbol ?? lower.Symbol,
            FractionSymbol = r.FractionSymbol ?? lower.FractionSymbol,
            FractionsPerUnit = r.FractionsPerUnit ?? lower.FractionsPerUnit,
            Rounding = r.Rounding ?? lower.Rounding,
            TriangulationCurrency = r.TriangulationCurrency ?? lower.TriangulationCurrency,
        };
    }

    // An alias's target, in whichever kind its base resolves: a calendar, day counter or currency, or a member of a
    // business-day convention.
    private object AliasTarget(string name, HashSet<string> visiting)
    {
        if (Top(ConventionKind.Alias, name, layers.Length) is not { Record: AliasRecord a })
        {
            throw Unknown("alias", name, "snapshot");
        }

        string b = a.Base!;
        if (Enum.TryParse(b, false, out BusinessDayConvention c) && Enum.IsDefined(c) && c.ToString() == b)
        {
            return c;
        }

        bool calendar = Top(ConventionKind.Calendar, b, layers.Length) is not null || PresetFactories.Calendars.ContainsKey(b);
        bool dayCounter = Top(ConventionKind.DayCounter, b, layers.Length) is not null || PresetFactories.DayCounters.ContainsKey(b);
        bool currency = Top(ConventionKind.Currency, b, layers.Length) is not null || PresetFactories.Currencies.ContainsKey(b);
        return (calendar, dayCounter, currency) switch
        {
            (true, false, false) => CalendarOf(name, layers.Length, visiting, "snapshot"),
            (false, true, false) => DayCounterOf(name, layers.Length, visiting, "snapshot"),
            (false, false, true) => CurrencyOf(name, visiting, "snapshot"),
            (false, false, false) => throw Unknown("calendar, day counter, currency or business-day convention", b, "snapshot"),
            _ => throw new ArgumentException($"the alias {name}'s base {b} names values of more than one kind", "snapshot"),
        };
    }

    private readonly record struct Entry(int Layer, ConventionRecord Record, string? Source);
}
