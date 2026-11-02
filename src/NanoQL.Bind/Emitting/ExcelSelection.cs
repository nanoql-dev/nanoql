// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.RegularExpressions;
using NanoQL.Bind.Model;
using NanoQL.Bind.Reading;
using NanoQL.Bind.Selecting;

namespace NanoQL.Bind.Emitting;

/// <summary>
/// What an Excel build holds (XLR5, XLR6): the tool's surface under Excel's filter, a generic type's constructions folded
/// into families, every function named over that whole filtered surface; then each prefix's list with what a listed type
/// inherits and the extension methods on it (XLD33), the completeness check to a fixpoint (XLD34), the list's entries
/// checked, and the stable build's cut with its own fixpoint.
/// </summary>
internal sealed class ExcelSelection
{
    private static readonly Regex PathLike = new("^(path|paths|file|files|filename|folder|directory)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly Surface surface;
    private readonly HashSet<CFunction> inheritedKept = [];
    private readonly HashSet<string> inheritedTypes = new(StringComparer.Ordinal);

    private ExcelSelection(Surface surface) => this.surface = surface;

    /// <summary>The file readers XLR3 lists by member, whose path-like parameters are taken; any other is refused.</summary>
    public static IReadOnlyList<string> Readers { get; } = ["nql_ConventionsXml_Read__string", "nql_ConventionsXml_Read__stringArray"];

    /// <summary>Gets the build's worksheet functions, in the surface's order.</summary>
    public List<ExcelUnit> Units { get; } = [];

    /// <summary>Gets every function's name over the whole filtered surface, a family by its representative.</summary>
    public Dictionary<CFunction, string> Names { get; private set; } = [];

    /// <summary>
    /// Gets the Excel names of the members reporting a read file's path (XLR14, round 6's F6-1), over the whole surface
    /// so every build carries the same list: the generated assembly hands them to the runtime, whose export refuses a tree
    /// calling one, since a replay reads the embedded file from another path.
    /// </summary>
    public List<string> PathReporters { get; } = [];

    /// <summary>Gets each family's members by its representative, over the whole filtered surface.</summary>
    public Dictionary<CFunction, List<CFunction>> Families { get; } = [];

    /// <summary>Gets the listed functions the completeness check withholds, the stable build's own fixpoint's included.</summary>
    public List<ExcelWithheld> Withheld { get; } = [];

    /// <summary>Gets the functions Excel's filter withholds, counted by why.</summary>
    public Dictionary<string, int> Skips { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the kept functions a listed type inherits.</summary>
    public int Inherited { get; private set; }

    /// <summary>Gets the kept extension methods on listed types.</summary>
    public int Extensions { get; private set; }

    /// <summary>Gets the functions the stable build's own fixpoint withholds after its cut.</summary>
    public int StableWithheld { get; private set; }

    /// <summary>Selects a build's functions.</summary>
    /// <param name="surface">The surface.</param>
    /// <param name="options">The build's options.</param>
    /// <param name="shipped">The last release's names.</param>
    /// <returns>The selection.</returns>
    public static ExcelSelection Select(Surface surface, ExcelOptions options, ExcelShipped shipped)
    {
        var s = new ExcelSelection(surface);
        s.Run(options, shipped);
        return s;
    }

    /// <summary>A family's key (XLD29): the generic type's definition, the member's kind, name and parameter kinds.</summary>
    /// <param name="f">A construction's function.</param>
    /// <returns>The key.</returns>
    public static string FamilyKey(CFunction f) =>
        f.Type.GetGenericTypeDefinition().FullName + "|" + f.Kind + "|" + (f.Member?.Name ?? f.BaseName) + "|" + string.Join(",", f.Params.Select(p => p.Kind.Text));

    /// <summary>A generic type's name without its arity.</summary>
    /// <param name="t">A construction of it.</param>
    /// <returns>The name.</returns>
    public static string DefName(Type t) => t.GetGenericTypeDefinition().Name.Split('`')[0];

    /// <summary>A type argument's name: its piece of the construction's C name, a nested generic's arguments joined by underscores.</summary>
    /// <param name="t">The type argument.</param>
    /// <returns>The name.</returns>
    public static string ArgName(Type t) => t.IsGenericType
        ? DefName(t) + "_" + string.Join("_", t.GetGenericArguments().Select(ArgName))
        : t.Name;

    /// <summary>A type's name as a list names it: a generic type by its definition.</summary>
    /// <param name="t">The type.</param>
    /// <returns>The name.</returns>
    public static string ListedName(Type t) => (t.IsConstructedGenericType ? t.GetGenericTypeDefinition() : t).FullName!.Split('[')[0];

    /// <summary>The types a kind names: a handle's, a nullable struct's, a snapshot's, inside collections and tuples too.</summary>
    /// <param name="k">The kind.</param>
    /// <returns>The types.</returns>
    public static IEnumerable<Type> KindTypes(CKind? k)
    {
        if (k == null)
        {
            yield break;
        }

        if (k.Name is "handle" or "boxed" or "boxed?" or "snapshot")
        {
            yield return k.Type.NullableOf() ?? k.Type;
        }

        foreach (CKind i in k.Items.Concat(k.Element != null ? [k.Element] : []))
        {
            foreach (Type t in KindTypes(i))
            {
                yield return t;
            }
        }
    }

    /// <summary>
    /// Excel's filter (XLR6): what Excel doesn't need or can't call safely: equality and increment operators, results
    /// records' constructors and <c>with</c>, writable spans and in-out parameters (R15), setters, the file writer and
    /// the clock (XLD20), members returning nothing but a validation, and the engine hooks (F11-1).
    /// </summary>
    /// <param name="f">The function.</param>
    /// <returns>Why it is withheld, or null.</returns>
    public static string? Skip(CFunction f)
    {
        string m = f.Member?.Name ?? f.BaseName;
        if (f.Kind == "operator" && m is "op_Equality" or "op_Inequality")
        {
            return "equality operator";
        }

        if (f.Kind == "operator" && m is "op_Increment" or "op_Decrement")
        {
            return "increment operator";
        }

        if (f.Kind is "new" or "with" && f.Type.Name.Split('`')[0].EndsWith("Results", StringComparison.Ordinal))
        {
            return "results record's constructor or with";
        }

        if (f.Params.Any(p => p.Kind.Name == "span"))
        {
            return "writable span";
        }

        if (f.Params.Any(p => p.Kind.Name.EndsWith('&')))
        {
            return "in-out (R15)";
        }

        // XLD20 extended (round 7): a member returning nothing other than a validation can only change an object's state.
        if (f.Kind is "method" or "static" && f.Return == null && !Regex.IsMatch(m, "^(Validate|Check)", RegexOptions.CultureInvariant))
        {
            return "state-changing (XLD20)";
        }

        // Round 11's F11-1: an instrument's engine hooks and a provider's resolution hook, which no worksheet calls.
        if (m is "FetchResults" or "SetupExpired" or "ResolveCalendarEdits")
        {
            return "engine hook (XLD20)";
        }

        if (f.Kind == "set")
        {
            return "setter (XLD20)";
        }

        if (f.CName is "nql_ConventionsXml_WriteDefaults" or "nql_Date_TodaysDate")
        {
            return "writes files or reads the clock (XLD20)";
        }

        return null;
    }

    private static bool IsClosed(CFunction f) => f.Type.IsConstructedGenericType && (f.Type.Namespace ?? string.Empty).StartsWith("NanoQL", StringComparison.Ordinal);

    private static string MemberKey(CFunction f)
        => f.Kind + "|" + (f.Member?.Name ?? f.BaseName) + "|" + string.Join(",", f.Params.Where(p => !p.IsSelf).Select(p => p.Kind.Text + ":" + p.Kind.Type?.FullName));

    private static IEnumerable<Type> Made(CFunction f)
    {
        if (f.Kind == "new")
        {
            yield return f.Type;
        }

        foreach (Type t in KindTypes(f.Return))
        {
            yield return t;
        }
    }

    private static IEnumerable<Type> Required(CFunction f) => f.Params.Where(p => p.IsSelf || !p.Optional).SelectMany(p => KindTypes(p.Kind));

    // XLR14's path reporters, by member: a call through one gives the path of the file a snapshot was read from. Each is
    // nanoql's extensions' own, so a surface binding that assembly has them.
    private static readonly (string Type, string Member)[] Reporters =
    [
        ("NanoQL.Extensions.Conventions.ConventionSnapshot", "get_Sources"),
        ("NanoQL.Extensions.Conventions.ConventionSet", "Provenance"),
    ];

    private static bool Reports(CFunction f, (string Type, string Member) r) => f.Member!.DeclaringType!.FullName == r.Type && f.Member.Name == r.Member;

    private void Run(ExcelOptions options, ExcelShipped shipped)
    {
        // XLD31: a struct's implicit default constructor (no member) is withheld where its type declares a constructor or
        // a static factory making it; where it is its type's only maker it is kept.
        var declared = surface.Functions.Where(g => g.Kind == "new" && g.Member != null).Select(g => g.Type).ToHashSet();
        var factories = surface.Functions.Where(g => g.Kind == "static" && g.Return?.Type == g.Type).Select(g => g.Type).ToHashSet();
        var filtered = new List<CFunction>();
        foreach (CFunction f in surface.Functions)
        {
            string? why = f.Kind == "new" && f.Member == null && (declared.Contains(f.Type) || factories.Contains(f.Type)) ? "default constructor beside a declared maker (XLD31)" : Skip(f);
            if (why != null)
            {
                Skips[why] = Skips.GetValueOrDefault(why) + 1;
            }
            else
            {
                filtered.Add(f);
            }
        }

        // XLD29: a generic type's constructions fold into one function per family, over the whole filtered surface.
        var folded = new HashSet<CFunction>();
        foreach (var g in filtered.Where(IsClosed).GroupBy(FamilyKey))
        {
            List<CFunction> members = [.. g.OrderBy(f => f.CName, StringComparer.Ordinal)];
            Families[members[0]] = members;
            folded.UnionWith(members.Skip(1));
        }

        Names = ExcelNames.Number([.. filtered.Where(f => !folded.Contains(f))], Families, shipped);

        // The reporters listed by member, as XLR3 lists the readers; a listed member the surface binding nanoql's extensions
        // doesn't have refuses the build before a file is written, so the export's refusal can't silently lose one.
        var named = Names.Keys.Where(f => f.Member != null).ToList();
        bool extensions = named.Any(f => f.Member!.DeclaringType!.Assembly.GetName().Name == "NanoQL.Extensions");
        var missing = extensions ? Reporters.Where(r => !named.Any(f => Reports(f, r))).ToList() : [];
        if (missing.Count > 0)
        {
            throw new BindException($"the path reporters' list names {string.Join(", ", missing.Select(r => r.Type + "." + r.Member))}, which the surface doesn't have: the export's refusal (XLR14) needs each");
        }

        PathReporters.AddRange(named.Where(f => Reporters.Any(r => Reports(f, r))).Select(f => Names[f]).Order(StringComparer.Ordinal));

        // XLD33: each prefix's list, a listed type bringing the instance members it inherits and the extension methods on it.
        Inherit(options);
        var kept = new List<CFunction>();
        foreach (CFunction f in filtered)
        {
            ExcelList? list = options.Lists.GetValueOrDefault(f.Prefix);
            if (list == null || list.Types.Contains(ListedName(f.Type)) || inheritedKept.Contains(f) || Extends(f, list))
            {
                kept.Add(f);
            }
            else
            {
                Skips["not on the list (XLR5)"] = Skips.GetValueOrDefault("not on the list (XLR5)") + 1;
            }
        }

        // The check runs where a list applies, over every kept function, a user's included (XLD35); a build with no
        // list is the whole filtered surface, as the tool's surface keeps what no binding can make (its coverage.md).
        bool listed = options.Lists.Values.Any(l => l != null);
        if (listed)
        {
            kept = Reachable(kept);
        }

        CheckEntries(options, kept);
        Inherited = kept.Count(f => inheritedKept.Contains(f) && options.Lists.GetValueOrDefault(f.Prefix) is { } l && !l.Types.Contains(ListedName(f.Type)));
        Extensions = kept.Count(f => options.Lists.GetValueOrDefault(f.Prefix) is { } l && !l.Types.Contains(ListedName(f.Type)) && !inheritedKept.Contains(f) && Extends(f, l));

        var keptSet = kept.ToHashSet();
        bool Experimental(IReadOnlyList<CFunction> fs) => fs.All(m => m.Experimental);
        var units = new List<ExcelUnit>();
        foreach (CFunction f in kept)
        {
            if (folded.Contains(f))
            {
                continue;
            }

            if (Families.TryGetValue(f, out List<CFunction>? members))
            {
                List<CFunction> held = [.. members.Where(keptSet.Contains)];
                units.Add(new ExcelUnit(Names[f], held[0], held, [], Experimental(held)));
            }
            else
            {
                units.Add(new ExcelUnit(Names[f], f, null, [], f.Experimental));
            }
        }

        // A family whose representative the list or the check withheld, with other constructions kept.
        foreach ((CFunction rep, List<CFunction> members) in Families)
        {
            if (!keptSet.Contains(rep) && members.Where(keptSet.Contains).ToList() is { Count: > 0 } held)
            {
                units.Add(new ExcelUnit(Names[rep], held[0], held, [], Experimental(held)));
            }
        }

        if (!options.Experimental)
        {
            // The stable build: a family holds its stable constructions, an experimental one refused naming the
            // experimental build (round 3's B3-2); then its own fixpoint.
            units = [.. units.Where(u => !u.Experimental).Select(u => u.Family == null ? u : u with
            {
                Family = [.. u.Family.Where(m => !m.Experimental)],
                Refused = [.. u.Family.Where(m => m.Experimental)],
                Function = u.Family.First(m => !m.Experimental),
            })];
            List<CFunction> stable = [.. units.SelectMany(u => u.Functions)];
            int before = Withheld.Count;
            var reachable = (listed ? Reachable(stable) : stable).ToHashSet();
            StableWithheld = Withheld.Count - before;
            units = [.. units.Where(u => u.Functions.Any(reachable.Contains)).Select(u => u.Family == null ? u : u with
            {
                Family = [.. u.Family.Where(reachable.Contains)],
                Function = u.Family.First(reachable.Contains),
            })];
        }

        foreach (CFunction f in units.SelectMany(u => u.Functions))
        {
            if (f.Params.Any(p => PathLike.IsMatch(p.Name) && p.Kind.Text is "string" or "array:string") && !Readers.Contains(f.CName))
            {
                throw new BindException($"{f.CName}: a path-like parameter outside the file readers' list (XLR3)");
            }
        }

        Units.AddRange(units.OrderBy(u => u.Functions.Select(f => f.CName).Min(StringComparer.Ordinal), StringComparer.Ordinal));
    }

    // The members a listed type inherits: for each member (its kind, name and parameters) the nearest declaration in its
    // chain, its own first, then its base classes', an interface's only where no class declares it (round 11's F11-1);
    // constructors and statics of every kind stay with their own type.
    private void Inherit(ExcelOptions options)
    {
        var listed = options.Lists.Values.Where(l => l != null).SelectMany(l => l!.Types).ToHashSet(StringComparer.Ordinal);
        if (listed.Count == 0)
        {
            return;
        }

        var byType = surface.Functions.Where(f => f.Kind is not ("new" or "static") && !f.Static).GroupBy(f => f.Type.FullName ?? string.Empty).ToDictionary(g => g.Key, g => g.ToList());
        var seen = surface.Functions.SelectMany(f => f.Params.SelectMany(p => KindTypes(p.Kind)).Concat(KindTypes(f.Return)).Append(f.Type)).Distinct();
        foreach (Type t in seen.Where(t => t.FullName != null && listed.Contains(ListedName(t))))
        {
            var chain = new List<Type>();
            for (Type? b = t; b != null && b.FullName != null && b.FullName != "System.Object"; b = b.BaseType)
            {
                chain.Add(b);
            }

            chain.AddRange(t.GetInterfaces().Where(i => i.FullName != null).OrderBy(i => i.FullName, StringComparer.Ordinal));
            var taken = new HashSet<string>(StringComparer.Ordinal);
            foreach (Type d in chain)
            {
                if (d != t)
                {
                    inheritedTypes.Add(ListedName(d));
                }

                foreach (CFunction f in byType.GetValueOrDefault(d.FullName!) ?? [])
                {
                    if (taken.Add(MemberKey(f)) && d != t)
                    {
                        inheritedKept.Add(f);
                    }
                }
            }
        }
    }

    // An extension method belongs to the type it extends: kept where its this type is listed or inherited by a listed one.
    private bool Extends(CFunction f, ExcelList? list)
        => list != null && f.Kind == "static" && f.Member is MethodInfo m
            && m.CustomAttributes.Any(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.ExtensionAttribute")
            && f.Params.Count > 0
            && KindTypes(f.Params[0].Kind).Any(t => t.FullName != null && (list.Types.Contains(ListedName(t)) || inheritedTypes.Contains(ListedName(t))));

    // XLD34: a kept function whose required input (self included) no kept function makes, by its type or a type derived
    // from it, nor a kept lookup by name constructs, or whose required engine no kept bind can fill, is withheld, to a
    // fixpoint, each with what it lacks.
    private List<CFunction> Reachable(List<CFunction> kept)
    {
        // The least fixpoint: a function counts as a maker once every input it requires is made, so a type whose only
        // maker needs one already stays unmade (C1.1's case), as a chain of makers is followed from what needs nothing.
        var required = kept.ToDictionary(f => f, f => Required(f).Distinct().ToList());
        var pending = required.Values.SelectMany(t => t).ToHashSet();
        var satisfied = new HashSet<Type>();
        var lookups = surface.Presets.Where(p => p.Lookup != null).GroupBy(p => p.Lookup!).ToDictionary(g => g.Key, g => g.Select(p => p.Construction.Constructor.DeclaringType!).ToList());
        var sets = new HashSet<EngineInputSet>();
        var live = new HashSet<CFunction>();
        void Make(Type m)
        {
            foreach (Type t in pending.Where(t => !satisfied.Contains(t) && t.IsAssignableFrom(m)).ToList())
            {
                satisfied.Add(t);
            }
        }

        bool EngineUnmade(CFunction f) => f.Params.Any(p => !p.Optional && p.Kind.Name == "engine")
            && !surface.EnginePairs.Any(x => x.Member == f && x.Formed && sets.Contains(x.Set));
        for (bool changed = true; changed;)
        {
            changed = false;
            foreach (CFunction f in kept)
            {
                if (live.Contains(f) || !required[f].All(satisfied.Contains) || EngineUnmade(f))
                {
                    continue;
                }

                live.Add(f);
                changed = true;
                foreach (Type m in Made(f).Concat(lookups.GetValueOrDefault(f) ?? []))
                {
                    Make(m);
                }

                if (f.Kind is "bind" or "bindon" && f.InputSet != null)
                {
                    sets.Add(f.InputSet);
                }
            }
        }

        foreach (CFunction f in kept.Where(f => !live.Contains(f)))
        {
            List<string> lacks = [.. required[f].Where(t => !satisfied.Contains(t)).Select(t => t.FullName!)];
            if (EngineUnmade(f))
            {
                lacks.Add("an engine no listed engine binds");
            }

            string name = Names.TryGetValue(f, out string? n) ? n : Names[Families.First(kv => kv.Value.Contains(f)).Key];
            Withheld.Add(new ExcelWithheld(f, name, lacks));
        }

        return [.. kept.Where(live.Contains)];
    }

    // XLD34's entries' checks: an entry the surface lacks, an entry keeping no function, and a deprecated function the
    // list would keep, each refused.
    private void CheckEntries(ExcelOptions options, List<CFunction> kept)
    {
        var surfaceTypes = surface.Functions.Select(f => ListedName(f.Type)).ToHashSet(StringComparer.Ordinal);
        var keptTypes = kept.Select(f => ListedName(f.Type)).ToHashSet(StringComparer.Ordinal);
        var errors = new List<string>();
        foreach ((string prefix, ExcelList? list) in options.Lists)
        {
            if (list == null)
            {
                continue;
            }

            errors.AddRange(list.Entries.Where(e => !surfaceTypes.Contains(e.Type)).Select(e => $"{list.Source}: {e.Type} isn't on the surface (a renamed or removed type)"));
            errors.AddRange(list.Entries.Where(e => surfaceTypes.Contains(e.Type) && !keptTypes.Contains(e.Type)).Select(e => $"{list.Source}: {e.Type} keeps no function"));
            errors.AddRange(kept.Where(f => f.Prefix == prefix && f.Deprecated != null).Select(f => $"{list.Source}: it would keep {f.CName}, which is deprecated ({f.Deprecated})"));
        }

        if (errors.Count > 0)
        {
            throw new BindException(string.Join(Environment.NewLine, errors));
        }
    }
}
