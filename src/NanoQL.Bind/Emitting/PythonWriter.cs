// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Reflection;
using System.Text;
using NanoQL.Bind.Model;
using NanoQL.Bind.Reading;

namespace NanoQL.Bind.Emitting;

/// <summary>
/// The Python back end (docs/bindings.md, "Python"): the package's generated modules over its hand-written runtime,
/// <c>bindings/python/nanoql/_runtime.py</c>. A class per type with its bases, an <c>IntEnum</c> per enum, members in
/// PEP 8's snake case with nanoql's defaults, an overload group as one method that picks its form by the arguments and
/// declares each with <c>typing.overload</c>; the experimental API in <c>nanoql.experimental</c>, whose import adds the
/// experimental members of stable classes. Each function's kinds go into a table the runtime makes its prototype from. A
/// user's assembly gets a package of its own, named by its prefix, whose classes extend nanoql's.
/// </summary>
internal sealed class PythonWriter(Surface surface)
{
    private static readonly HashSet<string> Keywords =
    [
        "False", "None", "True", "and", "as", "assert", "async", "await", "break", "class", "continue", "def", "del", "elif", "else",
        "except", "finally", "for", "from", "global", "if", "import", "in", "is", "lambda", "nonlocal", "not", "or", "pass", "raise",
        "return", "try", "while", "with", "yield", "match", "case", "type", "self", "args", "kwargs",
    ];

    // Names an Object already has, which a member doesn't take.
    private static readonly HashSet<string> Taken = ["close", "closed", "handle", "type_name"];

    private static readonly Dictionary<string, (string Binary, string? Reflected)> Operators = new(StringComparer.Ordinal)
    {
        ["op_Addition"] = ("__add__", "__radd__"),
        ["op_Subtraction"] = ("__sub__", "__rsub__"),
        ["op_Multiply"] = ("__mul__", "__rmul__"),
        ["op_Division"] = ("__truediv__", "__rtruediv__"),
        ["op_LessThan"] = ("__lt__", null),
        ["op_LessThanOrEqual"] = ("__le__", null),
        ["op_GreaterThan"] = ("__gt__", null),
        ["op_GreaterThanOrEqual"] = ("__ge__", null),
        ["op_UnaryNegation"] = ("__neg__", null),
        ["op_UnaryPlus"] = ("__pos__", null),
    };

    private readonly Dictionary<Type, TypeRow> types = surface.Types.ToDictionary(t => t.Type);
    private readonly Dictionary<Type, EnumRow> enums = surface.Enums.ToDictionary(e => e.Type);
    private readonly Dictionary<CFunction, string> lookupNames = [];
    private readonly Dictionary<string, (string Module, string Class, string Member, string Style, IReadOnlyList<string> Params)> rows = new(StringComparer.Ordinal);
    private int tables;
    private string prefix = "nql";

    /// <summary>
    /// Writes <c>_generated.py</c> and <c>experimental/__init__.py</c> into nanoql's package folder, and beside it a package
    /// per user prefix: its <c>__init__.py</c>, which loads the user's library from its <c>lib/</c> when there is one, and
    /// its own two modules.
    /// </summary>
    /// <param name="package">nanoql's package folder (<c>nanoql/</c>).</param>
    /// <returns>The functions, classes and enums written.</returns>
    public (int Functions, int Classes, int Enums) Write(string package)
    {
        (int functions, int classes, int enumCount) = WritePackage(package, "nql");
        foreach (string prefix in surface.Assemblies.Select(a => a.Prefix).Distinct().Where(p => p != "nql"))
        {
            string folder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(package).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))!, prefix);
            (int f, int c, int e) = WritePackage(folder, prefix);
            File.WriteAllText(Path.Combine(folder, "__init__.py"), UserInit(prefix).ReplaceLineEndings("\n"));
            functions += f;
            classes += c;
            enumCount += e;
        }

        return (functions, classes, enumCount);
    }

    /// <summary>
    /// Each C function's Python member as the generated modules write it, for the Excel back end's repro templates: its
    /// module, nanoql's or a user prefix's and its experimental module where its class is experimental, its class, its
    /// name, its style and its parameters' Python names. Nothing is written.
    /// </summary>
    /// <returns>The members by C name.</returns>
    public IReadOnlyDictionary<string, (string Module, string Class, string Member, string Style, IReadOnlyList<string> Params)> Table()
    {
        foreach (string p in surface.Assemblies.Select(a => a.Prefix).Distinct())
        {
            Generate(p);
        }

        return rows;
    }

    // One prefix's modules: nanoql's own, or a user's over nanoql's (its classes' bases and its defaults' enums are
    // nanoql's, its functions registered in nanoql's runtime).
    private (int Functions, int Classes, int Enums) WritePackage(string package, string prefix)
    {
        (string stable, string experimental, int functions, int classes, int enumCount) = Generate(prefix);
        Directory.CreateDirectory(Path.Combine(package, "experimental"));
        File.WriteAllText(Path.Combine(package, "_generated.py"), stable);
        File.WriteAllText(Path.Combine(package, "experimental", "__init__.py"), experimental);
        return (functions, classes, enumCount);
    }

    private (string Stable, string Experimental, int Functions, int Classes, int Enums) Generate(string prefix)
    {
        this.prefix = prefix;
        bool own = prefix == "nql";
        var stable = new StringBuilder();
        var experimental = new StringBuilder();
        Header(stable, own ? "nanoql's types, enums and members, over its runtime." : $"{prefix}'s types, enums and members, over nanoql's.", own, false);
        Header(experimental, own
            ? "nanoql's experimental API (NQL0001): importing this module opts in, adding the experimental members of stable classes."
            : $"{prefix}'s experimental API: importing this module opts in, with nanoql's, adding the experimental members of stable classes.", own, true);
        experimental.AppendLine("from .. import _generated as _g");
        experimental.AppendLine("from .._generated import *  # noqa: F401,F403 - the stable classes, for the experimental ones' bases");
        experimental.AppendLine();
        if (own)
        {
            stable.AppendLine($"_expect_abi({Versions.Abi})");
            stable.AppendLine();
        }

        foreach (EnumRow e in surface.Enums.Where(e => e.Prefix == prefix).OrderBy(e => e.CName, StringComparer.Ordinal))
        {
            StringBuilder sb = e.Experimental ? experimental : stable;
            sb.AppendLine($"class {e.CName}({(e.Flags ? "IntFlag" : "IntEnum")}):");
            foreach ((string name, long value) in e.Members)
            {
                sb.AppendLine($"    {Escape(name)} = {value.ToString(CultureInfo.InvariantCulture)}");
            }

            sb.AppendLine().AppendLine($"_runtime._enums[{Str(e.Prefix + "." + e.CName)}] = {e.CName}").AppendLine();
        }

        if (own && surface.Enums.Any(e => e.CName == "TimeUnit" && !e.Experimental))
        {
            stable.AppendLine("_runtime._time_unit = TimeUnit").AppendLine();
        }

        var byType = surface.Functions.GroupBy(f => f.Type).ToDictionary(g => g.Key, g => g.ToList());
        var stableAll = new List<string>();
        var experimentalAll = new List<string>();
        var py = new List<string>();
        foreach (TypeRow t in Ordered().Where(t => t.Prefix == prefix))
        {
            List<CFunction> fs = byType.GetValueOrDefault(t.Type, []);
            if (t.Form == "value")
            {
                Value(t, fs, stable, experimental, py, stableAll);
                continue;
            }

            StringBuilder sb = t.Experimental ? experimental : stable;
            (t.Experimental ? experimentalAll : stableAll).Add(t.CName);
            string[] bases = [.. new[] { t.Base }.Concat(t.Interfaces).Where(b => b != null && types.Values.Any(r => r.Type.FullName == b && r.Form != "value"))
                .Select(b => Name(types.Values.First(r => r.Type.FullName == b)))];
            sb.AppendLine($"class {t.CName}(*_bases({(bases.Length == 0 ? "Object" : string.Join(", ", bases))})):");
            sb.AppendLine("    __slots__ = ()");
            sb.AppendLine($"    _type = {Str(t.Type.FullName!)}");
            sb.AppendLine($"    _key = {Str(t.Prefix + "." + t.CName)}");
            if (t.Shape == "sealed" || t.Type.IsValueType)
            {
                sb.AppendLine("    _sealed = True");
            }

            if (!fs.Any(f => f.Kind == "new" && (t.Experimental || !f.Experimental)))
            {
                // No base's constructor builds this class: the runtime's refusal, a preset's naming its lookup. An
                // experimental constructor attached on import replaces it.
                sb.AppendLine("    __init__ = Object.__init__");
                if (MadeBy(t.Type) is { } lookup)
                {
                    sb.AppendLine($"    _made_by = {Str(lookup)}");
                }
            }

            var attached = new StringBuilder();
            foreach (IGrouping<string, CFunction> group in Groups(fs))
            {
                List<CFunction> forms = [.. group];
                if (t.Experimental || forms.All(f => !f.Experimental))
                {
                    Member(sb, t, group.Key, forms, "    ", py);
                }
                else
                {
                    if (forms.Any(f => !f.Experimental))
                    {
                        Member(sb, t, group.Key, [.. forms.Where(f => !f.Experimental)], "    ", py);
                    }

                    Member(attached, t, group.Key, forms, "    ", py);
                }
            }

            sb.AppendLine();
            if (attached.Length > 0)
            {
                // The experimental members of a stable class, added on import.
                experimental.AppendLine($"class _{t.CName}Experimental:");
                experimental.Append(attached);
                experimental.AppendLine();
                experimental.AppendLine($"_attach(_g.{t.CName}, _{t.CName}Experimental)");
                experimental.AppendLine();
            }
        }

        foreach ((StringBuilder sb, bool exp) in new[] { (stable, false), (experimental, true) })
        {
            sb.AppendLine("_register({");
            foreach (CFunction f in surface.Functions.Where(f => f.Prefix == prefix && (f.Experimental || (types.TryGetValue(f.Type, out TypeRow? r) && r.Experimental)) == exp))
            {
                string ins = string.Concat(f.Params.Select(p => Str(Token(p.Kind)) + ", "));
                // A constructor's result is the raw handle its object takes; a value type's (Date, Period) is its value.
                bool value = types.TryGetValue(f.Type, out TypeRow? vt) && vt.Form == "value";
                string outs = f.Kind == "new" && !value ? Str("new") : f.Kind is "bind" or "bindon" ? Str("handle@nql.BoundEngine") : f.Return is { } k ? Str(Token(k)) : "None";
                sb.AppendLine($"    {Str(f.CName)}: (({ins}), {outs}),");
            }

            sb.AppendLine("})").AppendLine();
        }

        stable.AppendLine("# Each C function's Python member: its class, its name, its style, and its form and its group's table, for the replayer.");
        stable.AppendLine("_PY = {");
        foreach (string line in py)
        {
            stable.AppendLine(line);
        }

        stable.AppendLine("}").AppendLine();
        stable.AppendLine("__all__ = [" + string.Join(", ", surface.Enums.Where(e => e.Prefix == prefix && !e.Experimental).Select(e => Str(e.CName)).Concat(stableAll.Select(Str)).Order(StringComparer.Ordinal)) + "]");
        experimental.AppendLine("__all__ = [" + string.Join(", ", surface.Enums.Where(e => e.Prefix == prefix && e.Experimental).Select(e => Str(e.CName)).Concat(experimentalAll.Select(Str)).Order(StringComparer.Ordinal)) + "]");

        return (stable.ToString().ReplaceLineEndings("\n"), experimental.ToString().ReplaceLineEndings("\n"), surface.Functions.Count(f => f.Prefix == prefix),
            stableAll.Count + experimentalAll.Count, surface.Enums.Count(e => e.Prefix == prefix));
    }

    // A user's package's __init__.py: the superset library beside it loaded in place of nanoql's own, before any
    // object, unless NANOQL_LIBRARY names one; then its classes.
    private static string UserInit(string prefix)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# SPDX-" + "FileCopyrightText: 2026 The nanoql Authors");
        sb.AppendLine("# SPDX-" + "License-Identifier: Apache-2.0");
        sb.AppendLine($"\"\"\"{prefix}: an assembly built on nanoql, its classes extending nanoql's. Generated by nanoql-bind; do not edit.");
        sb.AppendLine();
        sb.AppendLine("Its library, beside the package in lib/, holds nanoql's assemblies and its own; importing the package loads it in place");
        sb.AppendLine("of nanoql's own, so import it before making any nanoql object.");
        sb.AppendLine("\"\"\"");
        sb.AppendLine();
        sb.AppendLine("import os as _os");
        sb.AppendLine("import sys as _sys");
        sb.AppendLine();
        sb.AppendLine("import nanoql as _nanoql");
        sb.AppendLine();
        sb.AppendLine("_NAMES = {\"win32\": \"nanoql.dll\", \"darwin\": \"libnanoql.dylib\"}");
        sb.AppendLine("_LIBRARY = _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), \"lib\", _NAMES.get(_sys.platform, \"libnanoql.so\"))");
        sb.AppendLine("if _os.path.exists(_LIBRARY) and not _os.environ.get(\"NANOQL_LIBRARY\"):");
        sb.AppendLine("    _nanoql.use_library(_LIBRARY)");
        sb.AppendLine();
        sb.AppendLine("from ._generated import *  # noqa: E402,F401,F403");
        sb.AppendLine("from ._generated import __all__  # noqa: E402,F401");
        return sb.ToString();
    }

    /// <summary>A name in PEP 8's snake case: <c>FairRate</c> is <c>fair_rate</c>, <c>NPV</c> <c>npv</c>, <c>ATMRate</c> <c>atm_rate</c>.</summary>
    /// <param name="name">The C# name.</param>
    /// <returns>Its snake case, a keyword escaped.</returns>
    public static string Snake(string name)
    {
        // QuantLib's year-on-year is one word: YoYInflationIndex is yoy_inflation_index.
        name = name.Replace("YoY", "Yoy", StringComparison.Ordinal);
        var sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            bool upper = char.IsUpper(c);
            if (upper && i > 0 && name[i - 1] != '_'
                && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]) && char.IsUpper(name[i - 1]))))
            {
                sb.Append('_');
            }

            sb.Append(char.ToLowerInvariant(c));
        }

        return Escape(sb.ToString());
    }

    /// <summary>A kind's token in the functions' table: its text, with the class an object or enum takes after <c>@</c>.</summary>
    /// <param name="k">The kind.</param>
    /// <returns>The token.</returns>
    public string Token(CKind k) => k.Name switch
    {
        "handle" or "boxed" or "boxed?" or "snapshot" => types.TryGetValue(k.Type.NullableOf() ?? k.Type, out TypeRow? t) && t.Form != "value" ? k.Name + "@" + t.Prefix + "." + t.CName : k.Name,
        "enum" or "enum?" => enums.TryGetValue(k.Type.NullableOf() ?? k.Type, out EnumRow? e) ? k.Name + "@" + e.Prefix + "." + e.CName : k.Name,
        "array" or "span" or "jagged" => k.Name + ":" + Token(k.Element!),
        "map" => "map:" + Token(k.Element!) + ":" + Token(k.Value!),
        "tuple" or "tuple?" => k.Name + ":" + string.Join(",", k.ItemNames.Select(n => Snake(n).TrimEnd('_'))) + "|" + string.Join(",", k.Items.Select(Token)),
        _ => k.Text,
    };

    /// <summary>The overload groups of a type's functions: each Python member's name and its forms.</summary>
    /// <param name="fs">The type's functions.</param>
    /// <returns>The groups, in name order.</returns>
    public IEnumerable<IGrouping<string, CFunction>> Groups(IEnumerable<CFunction> fs)
    {
        var named = fs.Select(f => (Name: PyName(f), F: f)).ToList();

        // Two kinds under one name (a property and a method), or static and instance forms of one name: the later kind
        // takes its kind's suffix. Two C# members under one name (a record's init property and a method both
        // with_price_threshold): the later member takes its kind's suffix too (_init for an init property).
        var owner = new Dictionary<string, (string Style, string Member)>(StringComparer.Ordinal);
        for (int i = 0; i < named.Count; i++)
        {
            string style = Style(named[i].F);
            string member = MemberOf(named[i].F);
            string n = named[i].Name;
            while (owner.TryGetValue(n, out (string Style, string Member) o) && o != (style, member))
            {
                n += "_" + (o.Style != style ? style : named[i].F.Kind == "with" ? "init" : named[i].F.Kind);
            }

            owner[n] = (style, member);
            named[i] = (n, named[i].F);
        }

        return named.GroupBy(x => x.Name, x => x.F).OrderBy(g => g.Key, StringComparer.Ordinal);
    }

    /// <summary>
    /// The argument counts each form of a group is offered at: from its required parameters to all of them, where at a
    /// count the forms whose leading parameters are the same offer it once, through the form with the fewest
    /// parameters, the first of those in order (§2.2's rule: a defaulted form another form's whole signature has isn't
    /// offered).
    /// </summary>
    /// <param name="forms">The group's forms, in order.</param>
    /// <returns>Per form, its offered counts.</returns>
    public List<List<int>> Offered(List<CFunction> forms)
    {
        var sigs = forms.Select(f => f.Params.Select(p => Token(p.Kind)).ToList()).ToList();
        var owner = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < forms.Count; i++)
        {
            for (int n = forms[i].Params.Count(p => !p.Optional); n <= forms[i].Params.Count; n++)
            {
                string call = n + "|" + string.Join(",", sigs[i].Take(n));
                if (!owner.TryGetValue(call, out int j) || sigs[i].Count < sigs[j].Count)
                {
                    owner[call] = i;
                }
            }
        }

        var result = forms.Select(_ => new List<int>()).ToList();
        foreach ((string call, int i) in owner)
        {
            result[i].Add(int.Parse(call[..call.IndexOf('|', StringComparison.Ordinal)], CultureInfo.InvariantCulture));
        }

        result.ForEach(r => r.Sort());
        return result;
    }

    // A class's name in the module being written: a user's module names another prefix's class through its module, as
    // the user's own may take the same name.
    private string Name(TypeRow t) => Name(t.Prefix, t.CName, t.Experimental);

    private string Name(string owner, string name, bool experimental) => owner == prefix ? name : (experimental ? "_nx." : "_n.") + name;

    // The C# member a function calls: forms of one overloaded member share it, and every constructor, a struct's implicit
    // one included, is one.
    internal static string MemberOf(CFunction f) => f.Kind == "new" ? "new" : f.Kind + ":" + (f.Member?.Name ?? f.BaseName);

    private static void Header(StringBuilder sb, string what, bool own, bool experimental)
    {
        string runtime = own ? (experimental ? ".." : ".") : "nanoql.";
        // The generated module's tags, split so REUSE doesn't read them as this file's.
        sb.AppendLine("# SPDX-" + "FileCopyrightText: 2026 The nanoql Authors");
        sb.AppendLine("# SPDX-" + "License-Identifier: Apache-2.0");
        sb.AppendLine($"\"\"\"{what} Generated by nanoql-bind from the library's surface; do not edit.\"\"\"");
        sb.AppendLine("# ruff: noqa");
        sb.AppendLine("from __future__ import annotations");
        sb.AppendLine();
        sb.AppendLine("import datetime");
        sb.AppendLine("from enum import IntEnum, IntFlag");
        sb.AppendLine("from typing import Any, Sequence, overload as _overload");
        sb.AppendLine();
        sb.AppendLine(own ? $"from {runtime} import _runtime" : "from nanoql import _runtime");
        sb.AppendLine($"from {runtime}_runtime import BoundEngine, Object, Period, _REQUIRED, _attach, _bases, _deprecated, _dispatch, _expect_abi, _functions as _F, _register");
        if (!own)
        {
            // nanoql's classes and enums, a user's classes' bases and its defaults' values.
            sb.AppendLine(experimental ? "from nanoql.experimental import *  # noqa: F401,F403" : "from nanoql._generated import *  # noqa: F401,F403");
            sb.AppendLine("import nanoql._generated as _n");
            if (experimental)
            {
                sb.AppendLine("import nanoql.experimental as _nx");
            }
        }

        sb.AppendLine();
    }

    private static string Escape(string name) => Keywords.Contains(name) || Taken.Contains(name) ? name + "_" : name;

    private static string Str(string s) => "\"" + s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    // A withheld preset's lookups by name, as Python calls them (ConventionSet.currency), or null for none.
    private string? MadeBy(Type preset)
    {
        string[] lookups =
        [
            .. surface.Presets.Where(p => p.Lookup != null && p.Construction.Constructor.DeclaringType == preset)
                .Select(p => LookupName(p.Lookup!)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal),
        ];
        return lookups.Length == 0 ? null : string.Join(" or ", lookups);
    }

    // A lookup's Python member, its class's name and its group's.
    private string LookupName(CFunction lookup)
    {
        if (!lookupNames.TryGetValue(lookup, out string? name))
        {
            TypeRow holder = types[lookup.Type];
            foreach (IGrouping<string, CFunction> g in Groups(surface.Functions.Where(f => f.Type == lookup.Type)))
            {
                foreach (CFunction f in g)
                {
                    lookupNames[f] = holder.CName + "." + Escape(g.Key);
                }
            }

            name = lookupNames[lookup];
        }

        return name;
    }

    // A function's Python name, before its group's collisions.
    private static string PyName(CFunction f)
    {
        string member = f.Member switch
        {
            PropertyInfo p => p.Name,
            FieldInfo fi => fi.Name,
            MethodBase m => m.Name.StartsWith("get_", StringComparison.Ordinal) || m.Name.StartsWith("set_", StringComparison.Ordinal) ? m.Name[4..] : m.Name,
            _ => f.BaseName,
        };
        return f.Kind switch
        {
            "new" => "__init__",
            "with" => "with_" + Snake(member).TrimEnd('_'),
            "set" => "set_" + Snake(member).TrimEnd('_'),
            "operator" => Snake(member).TrimEnd('_'),
            "indexer" => "__getitem__",
            "bind" => "bind",
            "bindon" => "bind_on",
            "draws" => "draws",
            _ => Snake(member),
        };
    }

    // How a function is called from Python: a constructor, a method on the instance, a property, or a static function.
    private static string Style(CFunction f)
    {
        bool instance = f.Params.Count > 0 && f.Params[0].IsSelf;
        return f.Kind switch
        {
            "new" => "new",
            "get" or "field" when instance && f.Params.Count == 1 => "property",
            "indexer" => "index",
            "draws" => "method",
            _ => instance ? "method" : "static",
        };
    }

    // The types in an order where every class follows its bases.
    private IEnumerable<TypeRow> Ordered()
    {
        var byName = surface.Types.ToDictionary(t => t.Type.FullName!, StringComparer.Ordinal);
        var done = new HashSet<string>(StringComparer.Ordinal);
        var order = new List<TypeRow>();
        void Visit(TypeRow t)
        {
            if (!done.Add(t.Type.FullName!))
            {
                return;
            }

            foreach (string b in new[] { t.Base }.Concat(t.Interfaces).OfType<string>())
            {
                if (byName.TryGetValue(b, out TypeRow? r))
                {
                    Visit(r);
                }
            }

            order.Add(t);
        }

        foreach (TypeRow t in surface.Types.Where(t => t.Form is not ("withheld" or "matrix" or "vector" or "distribution")).OrderBy(t => t.CName, StringComparer.Ordinal))
        {
            Visit(t);
        }

        return order.Where(t => t.Form is not ("withheld" or "matrix" or "vector" or "distribution"));
    }

    // A value type's members (Date, Period): Date's as static functions of a class of its own, the date first, since
    // datetime.date takes no members; Period's on the runtime's Period.
    private void Value(TypeRow t, List<CFunction> fs, StringBuilder stable, StringBuilder experimental, List<string> py, List<string> all)
    {
        bool period = t.CName == "Period";
        string holder = period ? "_PeriodMembers" : t.CName;
        stable.AppendLine($"class {holder}:");
        stable.AppendLine($"    \"\"\"{(period ? "Period's members, added to nanoql.Period." : "Date's members, each taking the date first: Date.end_of_month(d).")}\"\"\"");
        foreach (IGrouping<string, CFunction> group in Groups(fs.Where(f => !f.Experimental && f.Kind != "new")))
        {
            Member(stable, t, group.Key, [.. group], "    ", py, valueHolder: holder, asStatic: !period);
        }

        stable.AppendLine("    pass").AppendLine();
        if (period)
        {
            stable.AppendLine("_attach(Period, _PeriodMembers)");
            stable.AppendLine();
        }
        else
        {
            all.Add(t.CName);
        }
    }

    // A group's Python member: one def for a form, or the forms' typing.overload declarations and a dispatching def.
    private void Member(StringBuilder sb, TypeRow t, string name, List<CFunction> forms, string ind, List<string> py, string? valueHolder = null, bool asStatic = false)
    {
        forms = [.. forms.OrderBy(f => f.Params.Count).ThenBy(f => f.CName, StringComparer.Ordinal)];
        string style = asStatic ? "static" : Style(forms[0]);
        string cls = valueHolder ?? t.CName;
        string? table = forms.Count == 1 ? null : "_D" + (tables++).ToString(CultureInfo.InvariantCulture);
        string module = (prefix == "nql" ? "nanoql" : prefix) + (t.Experimental ? ".experimental" : string.Empty);
        for (int i = 0; i < forms.Count; i++)
        {
            py.Add($"    {Str(forms[i].CName)}: ({Str(cls)}, {Str(name)}, {Str(style)}, {(table == null ? "None" : i.ToString(CultureInfo.InvariantCulture))}, {(table == null ? "None" : Str(table))}),");
            rows.TryAdd(forms[i].CName, (module, cls, name, style, [.. Names(forms[i]).Select(x => x.Name)]));
        }

        if (forms.Count == 1)
        {
            CFunction f = forms[0];
            List<(string Name, CParam P)> ps = Names(f);
            string sig = string.Join(", ", (style == "new" ? ["self"] : Array.Empty<string>()).Concat(ps.Select(Param)));
            string call = $"_F[{Str(f.CName)}]({string.Join(", ", ps.Select(x => x.Name))})";
            Decorate(sb, ind, style);
            sb.AppendLine($"{ind}def {name}({sig}){Returns(f)}:");
            if (f.Deprecated != null)
            {
                sb.AppendLine($"{ind}    _deprecated({Str(f.Deprecated)})");
            }

            sb.AppendLine(style == "new" ? $"{ind}    self._h = {call}" : $"{ind}    return {call}");
            OperatorAliases(sb, ind, name, forms, style);
            return;
        }

        List<List<int>> offered = Offered(forms);
        foreach (CFunction f in forms)
        {
            Decorate(sb, ind, style, overload: true);
            sb.AppendLine($"{ind}def {name}({string.Join(", ", (style == "new" ? ["self"] : Array.Empty<string>()).Concat(Names(f).Select(Param)))}){Returns(f)}: ...");
        }

        Decorate(sb, ind, style);
        string self = style is "method" or "property" or "new" or "index" ? "self, " : string.Empty;
        string args = style is "method" or "property" or "index" ? "(self, *args)" : style == "new" ? "args" : "args";
        sb.AppendLine($"{ind}def {name}({self}*args, **kwargs):");
        sb.AppendLine(style == "new" ? $"{ind}    self._h = _dispatch({cls}.{table}, {args}, kwargs)" : $"{ind}    return _dispatch({cls}.{table}, {args}, kwargs)");
        sb.Append($"{ind}{table} = (");
        for (int i = 0; i < forms.Count; i++)
        {
            CFunction f = forms[i];
            List<(string Name, CParam P)> ps = Names(f);
            string names = "(" + string.Concat(ps.Select(x => Str(x.Name) + ", ")) + ")";
            string kinds = "(" + string.Concat(ps.Select(x => Str(Token(x.P.Kind)) + ", ")) + ")";
            string defaults = "(" + string.Concat(ps.Select(x => (x.P.Optional ? Default(x.P) : "_REQUIRED") + ", ")) + ")";
            string counts = "(" + string.Concat(offered[i].Select(n => n.ToString(CultureInfo.InvariantCulture) + ", ")) + ")";
            string fn = f.Deprecated != null ? $"lambda *a: (_deprecated({Str(f.Deprecated)}), _F[{Str(f.CName)}](*a))[1]" : $"lambda *a: _F[{Str(f.CName)}](*a)";
            sb.Append($"\n{ind}    ({fn}, {names}, {kinds}, {defaults}, {counts}, {Str(f.CName)}),");
        }

        sb.AppendLine($"\n{ind})");
        OperatorAliases(sb, ind, name, forms, style);
    }

    // The dunders a binary or unary operator gives: __add__ for op_Addition on its first operand's class, __radd__ where
    // the class is the second operand.
    private void OperatorAliases(StringBuilder sb, string ind, string name, List<CFunction> forms, string style)
    {
        if (forms[0].Kind != "operator" || forms[0].Member is not MethodInfo m || !Operators.TryGetValue(m.Name, out (string Binary, string? Reflected) d))
        {
            return;
        }

        Type own = forms[0].Type;
        bool unary = forms.All(f => f.Params.Count == 1);
        if (unary)
        {
            sb.AppendLine($"{ind}def {d.Binary}(self):");
            sb.AppendLine($"{ind}    return type(self).{name}(self)");
            return;
        }

        sb.AppendLine($"{ind}def {d.Binary}(self, other):");
        sb.AppendLine($"{ind}    try:");
        sb.AppendLine($"{ind}        return type(self).{name}(self, other)");
        sb.AppendLine($"{ind}    except TypeError:");
        sb.AppendLine($"{ind}        return NotImplemented");
        if (d.Reflected != null && forms.Any(f => f.Params.Count == 2 && f.Params[1].Kind.Type == own && f.Params[0].Kind.Type != own))
        {
            sb.AppendLine($"{ind}def {d.Reflected}(self, other):");
            sb.AppendLine($"{ind}    try:");
            sb.AppendLine($"{ind}        return type(self).{name}(other, self)");
            sb.AppendLine($"{ind}    except TypeError:");
            sb.AppendLine($"{ind}        return NotImplemented");
        }
    }

    private static void Decorate(StringBuilder sb, string ind, string style, bool overload = false)
    {
        if (overload)
        {
            sb.AppendLine($"{ind}@_overload");
        }

        if (style == "static")
        {
            sb.AppendLine($"{ind}@staticmethod");
        }
        else if (style == "property")
        {
            sb.AppendLine($"{ind}@property");
        }
    }

    // A form's parameters' Python names, self first where the function takes the instance.
    private static List<(string Name, CParam P)> Names(CFunction f)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<(string, CParam)>();
        foreach (CParam p in f.Params)
        {
            string n = p.IsSelf ? "self" : Snake(p.Name);
            while (!p.IsSelf && !used.Add(n))
            {
                n += "_";
            }

            result.Add((n, p));
        }

        return result;
    }

    // A parameter in a def: its name, annotation and default.
    private string Param((string Name, CParam P) x) =>
        x.Name == "self" && x.P.IsSelf ? "self" : x.Name + ": " + Hint(x.P.Kind) + (x.P.Optional ? " = " + Default(x.P) : string.Empty);

    private string Returns(CFunction f) => f.Kind == "new" ? " -> None" : f.Kind is "bind" or "bindon" ? " -> BoundEngine" : f.Return is { } r ? " -> " + Hint(r, result: true) : " -> None";

    // A kind's annotation.
    private string Hint(CKind k, bool result = false) => k.Name switch
    {
        "double" => "float",
        "int32" or "int64" => "int",
        "bool" => "bool",
        "string" => "str | None",
        "date" => "datetime.date",
        "date?" => "datetime.date | None",
        "period" => result ? "Period" : "Period | tuple[int, int]",
        "complex" => "complex",
        "double?" => "float | None",
        "int32?" or "int64?" => "int | None",
        "bool?" => "bool | None",
        "period?" => "Period | None",
        "complex?" => "complex | None",
        "enum" or "enum?" => (enums.TryGetValue(k.Type.NullableOf() ?? k.Type, out EnumRow? e) ? Name(e.Prefix, e.CName, e.Experimental) : "int") + (k.Name == "enum?" ? " | None" : string.Empty),
        "handle" or "boxed" or "boxed?" or "snapshot" => (types.TryGetValue(k.Type.NullableOf() ?? k.Type, out TypeRow? t) && t.Form != "value" ? Name(t) : "Object") + " | None",
        "matrix" or "vector" or "jagged" or "span" or "distribution" or "draws" or "sequences" => "Any",
        "array" when k.Element!.Name is "tuple" or "tuple?" => result
            ? "list[" + TupleHint(k.Element, true) + (k.Element.Name == "tuple?" ? " | None]" : "]")
            : "Sequence[" + TupleHint(k.Element, false) + "]",
        "array" => result ? (k.Element!.Name is "double" or "int32" or "int64" or "bool" ? "Any" : "list[Any]") : "Sequence[Any]",
        "map" => result ? "dict[Any, Any]" : "Any",
        "tuple" => result ? TupleHint(k, true) : TupleHint(k, false) + " | None",
        _ => "Any",
    };

    // A tuple's annotation, its elements' own (an object or a string not None unless the element is nullable): the
    // named tuples a getter returns are tuples too.
    private string TupleHint(CKind k, bool result) =>
        "tuple[" + string.Join(", ", k.Items.Select(i => i.Name is "handle" or "string" ? Hint(i, result)[..^" | None".Length] : Hint(i, result))) + "]";

    // A parameter's default in Python.
    private string Default(CParam p)
    {
        object? d = p.Default;
        if (d is null or DBNull)
        {
            return "None";
        }

        string kind = p.Kind.Name;
        if (kind is "enum" or "enum?")
        {
            long v = Convert.ToInt64(d, CultureInfo.InvariantCulture);
            if (enums.TryGetValue(p.Kind.Type.NullableOf() ?? p.Kind.Type, out EnumRow? e))
            {
                string? member = e.Members.FirstOrDefault(m => m.Value == v).Name;
                string name = Name(e.Prefix, e.CName, e.Experimental);
                return member != null ? name + "." + Escape(member) : name + "(" + v.ToString(CultureInfo.InvariantCulture) + ")";
            }

            return v.ToString(CultureInfo.InvariantCulture);
        }

        return d switch
        {
            bool b => b ? "True" : "False",
            double x when double.IsPositiveInfinity(x) => "float(\"inf\")",
            double x when double.IsNegativeInfinity(x) => "float(\"-inf\")",
            double x when double.IsNaN(x) => "float(\"nan\")",
            double x => x.ToString("R", CultureInfo.InvariantCulture) is var s && (s.Contains('.') || s.Contains('E')) ? s.Replace("E+", "e+").Replace("E-", "e-") : s + ".0",
            float x => ((double)x).ToString("R", CultureInfo.InvariantCulture),
            string s => Str(s),
            IFormattable n when kind is "double" or "double?" => Convert.ToDouble(n, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture) + (n.ToString(null, CultureInfo.InvariantCulture).Contains('.') ? string.Empty : ".0"),
            IFormattable n => n.ToString(null, CultureInfo.InvariantCulture),
            _ => "None",
        };
    }
}
