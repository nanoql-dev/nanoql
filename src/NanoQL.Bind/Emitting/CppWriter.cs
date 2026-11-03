// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Reflection;
using System.Text;
using NanoQL.Bind.Model;
using NanoQL.Bind.Reading;

namespace NanoQL.Bind.Emitting;

/// <summary>
/// The C++ back end (docs/bindings.md, "C++"): <c>nanoql.hpp</c>, C++17, over <c>nanoql.h</c>. Its hand-written part,
/// <c>bindings/cpp/runtime.hpp</c>, follows the enums; then a class per type, every base and interface a virtual base
/// so an object converts to each; QuantLib's camelCase members with nanoql's defaults; <c>Date</c> and <c>Period</c> as
/// values; <c>std::string</c>, <c>std::vector</c>, <c>std::optional</c> and <c>array_view</c> at the boundary;
/// exceptions per status code. Each function's class, member and form are planned once, for the header and for the
/// replayer's dispatch (<see cref="CppReplayWriter"/>).
/// </summary>
internal sealed class CppWriter
{
    private static readonly HashSet<string> Keywords =
    [
        "alignas", "alignof", "and", "and_eq", "asm", "auto", "bitand", "bitor", "bool", "break", "case", "catch", "char", "char8_t", "char16_t",
        "char32_t", "class", "compl", "concept", "const", "consteval", "constexpr", "constinit", "const_cast", "continue", "co_await", "co_return",
        "co_yield", "decltype", "default", "delete", "do", "double", "dynamic_cast", "else", "enum", "explicit", "export", "extern", "false",
        "float", "for", "friend", "goto", "if", "inline", "int", "long", "mutable", "namespace", "new", "noexcept", "not", "not_eq", "nullptr",
        "operator", "or", "or_eq", "private", "protected", "public", "register", "reinterpret_cast", "requires", "return", "short", "signed",
        "sizeof", "static", "static_assert", "static_cast", "struct", "switch", "template", "this", "thread_local", "throw", "true", "try",
        "typedef", "typeid", "typename", "union", "unsigned", "using", "virtual", "void", "volatile", "wchar_t", "while", "xor", "xor_eq",

        // Macros a C++ program commonly has, and the names the generated bodies use.
        "NULL", "assert", "errno", "detail", "std", "nanoql", "h_", "serial_", "length_", "units_",
    ];

    // Names the runtime's classes already have: Object's members, Date's and Period's C forms.
    private static readonly HashSet<string> Taken = ["handle", "typeName", "toString", "hashCode", "raw", "serial"];

    // The runtime's own type names in the namespace.
    private static readonly HashSet<string> RuntimeTypes =
    [
        "Object", "BoundEngine", "Matrix", "Distribution", "array_view", "error", "argument_error", "argument_null_error",
        "argument_out_of_range_error", "handle_error", "object_cast", "liveHandles", "version", "abiVersion",
    ];

    private static readonly Dictionary<string, string> Operators = new(StringComparer.Ordinal)
    {
        ["op_Addition"] = "+",
        ["op_Subtraction"] = "-",
        ["op_Multiply"] = "*",
        ["op_Division"] = "/",
        ["op_Modulus"] = "%",
        ["op_LessThan"] = "<",
        ["op_LessThanOrEqual"] = "<=",
        ["op_GreaterThan"] = ">",
        ["op_GreaterThanOrEqual"] = ">=",
        ["op_UnaryNegation"] = "-",
        ["op_UnaryPlus"] = "+",
    };

    private readonly Surface surface;
    private readonly Dictionary<Type, TypeRow> types;
    private readonly Dictionary<Type, EnumRow> enums;
    private readonly HashSet<string> typeNames;
    private readonly List<TypeRow> classes;

    // The prefix whose header is being written: another prefix's classes and enums are named with their namespace,
    // since a user's class may take one of nanoql's names.
    private string context = "nql";

    /// <summary>Initializes a new instance of the <see cref="CppWriter"/> class, planning every function's member.</summary>
    /// <param name="surface">The surface.</param>
    public CppWriter(Surface surface)
    {
        this.surface = surface;
        types = surface.Types.ToDictionary(t => t.Type);
        enums = surface.Enums.ToDictionary(e => e.Type);
        classes = [.. Ordered()];
        typeNames = [.. surface.Types.Select(t => t.CName), .. surface.Enums.Select(e => e.CName), .. RuntimeTypes];
        Plan();
    }

    /// <summary>Gets each function's member, for the functions the layer offers.</summary>
    public Dictionary<CFunction, CppMember> Members { get; } = [];

    /// <summary>Gets how many forms took another name than their group's, their C++ signature being another form's.</summary>
    public int Renamed { get; private set; }

    /// <summary>A name as QuantLib spells it: nanoql capitalised a member's first letter, which C++ lowers back unless an acronym leads.</summary>
    /// <param name="name">The C# name.</param>
    /// <returns>Its camelCase.</returns>
    public static string Camel(string name) =>
        name.StartsWith("YoY", StringComparison.Ordinal) ? "yoy" + name[3..] // QuantLib's year-on-year is one word: yoyInflationIndex
        : name.Length > 1 && char.IsUpper(name[1]) ? name : name.Length > 0 ? char.ToLowerInvariant(name[0]) + name[1..] : name;

    /// <summary>
    /// Writes the headers: <c>nanoql.hpp</c>, and for each user prefix <c>&lt;prefix&gt;.hpp</c>, its classes in a
    /// namespace of that name over nanoql's (a user's classes extend nanoql's).
    /// </summary>
    /// <param name="runtime">The hand-written part's text (<c>bindings/cpp/runtime.hpp</c>).</param>
    /// <returns>Each header's text by its file name, LF line endings.</returns>
    public Dictionary<string, string> Write(string runtime)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["nanoql.hpp"] = Header("nql", runtime) };
        foreach (string prefix in surface.Assemblies.Select(a => a.Prefix).Distinct().Where(p => p != "nql"))
        {
            headers[prefix + ".hpp"] = Header(prefix, runtime);
        }

        return headers;
    }

    private string Header(string prefix, string runtime)
    {
        context = prefix;
        bool own = prefix == "nql";
        string file = own ? "nanoql.hpp" : prefix + ".hpp";
        string guard = own ? "NANOQL_HPP" : "NANOQL_" + prefix.ToUpperInvariant() + "_HPP";
        var h = new StringBuilder();

        // The generated header's tags, split so REUSE doesn't read them as this file's.
        h.AppendLine("// SPDX-" + "FileCopyrightText: 2026 The nanoql Authors");
        h.AppendLine("// SPDX-" + "License-Identifier: Apache-2.0");
        h.AppendLine();
        if (own)
        {
            h.AppendLine("// nanoql.hpp: nanoql's C++ layer over nanoql.h (docs/bindings.md, \"C++\"), C++17. Generated by nanoql-bind from the");
            h.AppendLine("// library's surface; do not edit. NANOQL_EXPERIMENTAL, defined before it is included, opts in to the experimental");
            h.AppendLine("// API (NQL0001).");
        }
        else
        {
            h.AppendLine($"// {file}: {prefix}'s C++ layer over {prefix}.h and nanoql.hpp, its classes extending nanoql's (docs/bindings.md, \"C++\").");
            h.AppendLine("// Generated by nanoql-bind; do not edit.");
        }

        h.AppendLine($"#ifndef {guard}").AppendLine($"#define {guard}").AppendLine();
        if (own)
        {
            foreach (string include in new[] { "complex", "cstddef", "cstdint", "initializer_list", "limits", "optional", "stdexcept", "string", "tuple", "type_traits", "utility", "vector" })
            {
                h.AppendLine($"#include <{include}>");
            }

            h.AppendLine().AppendLine("#include \"nanoql.h\"");
            h.AppendLine().AppendLine("namespace nanoql {").AppendLine();
        }
        else
        {
            h.AppendLine("#include \"nanoql.hpp\"").AppendLine($"#include \"{prefix}.h\"").AppendLine();
            h.AppendLine($"namespace {prefix} {{").AppendLine().AppendLine("using namespace ::nanoql;").AppendLine();
        }

        Guarded(h, surface.Enums.Where(e => e.Prefix == prefix).OrderBy(e => e.CName, StringComparer.Ordinal), e => e.Experimental, Enum);
        h.AppendLine();
        var defs = new StringBuilder();
        var free = new List<(bool Experimental, string Key, string Text)>();
        if (own)
        {
            foreach (string line in runtime.ReplaceLineEndings("\n").Split('\n').SkipWhile(l => l.StartsWith("// SPDX-", StringComparison.Ordinal) || l.Length == 0))
            {
                h.Append(line).Append('\n');
            }

            foreach (string value in new[] { "Period", "Date" })
            {
                if (surface.Types.FirstOrDefault(t => t.Form == "value" && t.CName == value) is { } row)
                {
                    ValueClass(h, defs, free, row);
                }
            }
        }

        List<TypeRow> mine = [.. classes.Where(t => t.Prefix == prefix)];
        h.AppendLine("// The classes, declared before any is defined.");
        foreach (TypeRow t in mine)
        {
            h.AppendLine($"class {t.CName};");
        }

        h.AppendLine();
        if (own)
        {
            h.AppendLine("// The members every class has, a user's header's too: null, adopting a handle, copies and moves (assigning Object,");
            h.AppendLine("// every base's one state).");
            h.AppendLine("#define NANOQL_CLASS_(C) \\");
            h.AppendLine("public: \\");
            h.AppendLine("    C(std::nullptr_t) noexcept {} \\");
            h.AppendLine("    C(::nanoql::detail::adopt_t, nql_handle h) noexcept : ::nanoql::Object(::nanoql::detail::adopt, h) {} \\");
            h.AppendLine("    C(const C&) = default; \\");
            h.AppendLine("    C(C&&) noexcept = default; \\");
            h.AppendLine("    C& operator=(const C& o) { ::nanoql::Object::operator=(o); return *this; } \\");
            h.AppendLine("    C& operator=(C&& o) noexcept { ::nanoql::Object::operator=(std::move(o)); return *this; }");
            h.AppendLine();
        }

        foreach (TypeRow t in mine)
        {
            Class(h, defs, free, t);
        }

        h.AppendLine("// The members, defined once every class is.");
        h.Append(defs);
        h.AppendLine("// Operators, through the members they name.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach ((bool exp, string key, string text) in free)
        {
            if (seen.Add(key))
            {
                h.Append(exp ? "#ifdef NANOQL_EXPERIMENTAL\n" + text + "#endif\n" : text);
            }
        }

        h.AppendLine();
        if (own)
        {
            h.AppendLine("template <class C>");
            h.AppendLine("const C& detail::null_of()");
            h.AppendLine("{");
            h.AppendLine("    static const C n(nullptr);");
            h.AppendLine("    return n;");
            h.AppendLine("}").AppendLine();
        }

        h.AppendLine($"}}  // namespace {(own ? "nanoql" : prefix)}").AppendLine().AppendLine($"#endif  // {guard}");
        return h.ToString().ReplaceLineEndings("\n");
    }

    /// <summary>Whether a type's class is experimental (NQL0001), its members compiled only under NANOQL_EXPERIMENTAL.</summary>
    /// <param name="t">The type.</param>
    /// <returns>Whether it is.</returns>
    public bool IsExperimentalClass(Type t) => types.TryGetValue(t, out TypeRow? r) && r.Experimental;

    /// <summary>A kind's C++ type as a parameter or a result.</summary>
    /// <param name="k">The kind.</param>
    /// <param name="p">The parameter, for a parameter's type; null for a result's.</param>
    /// <returns>The type.</returns>
    public string Type(CKind k, CParam? p)
    {
        bool result = p == null;
        bool nullDefault = p is { Optional: true } && p.Default is null or DBNull;
        return k.Name switch
        {
            "double" => "double",
            "int32" => "int32_t",
            "int64" => "int64_t",
            "bool" => "bool",
            "string" => result ? "std::string" : nullDefault ? "const std::optional<std::string>&" : "const std::string&",
            "date" or "date?" => "Date",
            "period" => "Period",
            "complex" => "std::complex<double>",
            "enum" => EnumName(k),
            "double?" or "int32?" or "int64?" or "bool?" or "enum?" => "std::optional<" + Type(k with { Name = k.Name.TrimEnd('?') }, null) + ">",
            "period?" or "complex?" => (result ? string.Empty : "const ") + "std::optional<" + Type(k with { Name = k.Name.TrimEnd('?') }, null) + ">" + (result ? string.Empty : "&"),
            "handle" or "boxed" or "boxed?" or "snapshot" => result ? ClassName(k) : "const " + ClassName(k) + "&",
            "engine" => "const BoundEngine&",
            "matrix" => result ? "Matrix" : nullDefault ? "const std::optional<Matrix>&" : "const Matrix&",
            "vector" => result ? "std::vector<double>" : "array_view<double>",
            "matrix&" => "Matrix&",
            "vector&" => "std::vector<double>&",
            "span" => "std::vector<" + Element(k.Element!) + ">&",
            "array" => result ? "std::vector<" + Element(k.Element!) + ">" : "array_view<" + Element(k.Element!) + ">",
            "jagged" => result ? "std::vector<std::vector<double>>" : "array_view<std::vector<double>>",
            "map" => (result ? "std::vector<" : "array_view<") + "std::pair<" + Element(k.Element!) + ", " + Element(k.Value!) + ">>",
            "tuple" => "std::tuple<" + string.Join(", ", k.Items.Select(i => Type(i, null))) + ">",
            "distribution" => "Distribution",
            _ => throw new InvalidOperationException("no C++ type for the kind " + k.Text),
        };
    }

    /// <summary>A collection element's C++ type.</summary>
    /// <param name="e">The element kind.</param>
    /// <returns>The type.</returns>
    public string Element(CKind e) => e.Name switch
    {
        "double" => "double",
        "int32" => "int32_t",
        "int64" => "int64_t",
        "bool" => "bool",
        "date" or "date?" => "Date",
        "period" => "Period",
        "complex" => "std::complex<double>",
        "string" => "std::string",
        "enum" => EnumName(e),
        "double?" => "std::optional<double>",
        "int32?" => "std::optional<int32_t>",
        "handle" => ClassName(e),
        "tuple" => "std::tuple<" + string.Join(", ", e.Items.Select(Element)) + ">",
        "tuple?" => "std::optional<std::tuple<" + string.Join(", ", e.Items.Select(Element)) + ">>",
        _ => throw new InvalidOperationException("no C++ element type for the kind " + e.Text),
    };

    /// <summary>A result's C++ type, the bindings' own kinds included.</summary>
    /// <param name="f">The function.</param>
    /// <returns>The type, <c>void</c> for none.</returns>
    public string Result(CFunction f) => f.Kind is "bind" or "bindon" ? "BoundEngine" : f.Return switch
    {
        null => "void",
        { Name: "draws" } => $"std::tuple<std::vector<double>, std::vector<double>, {ClassName(f.Type)}>",
        { Name: "sequences" } => $"std::tuple<Matrix, std::vector<double>, {ClassName(f.Type)}>",
        { } r => Type(r, null),
    };

    /// <summary>A handle kind's class: its type's, or <c>Object</c> for a type the layer has no class for.</summary>
    /// <param name="k">The kind.</param>
    /// <returns>The class's name.</returns>
    public string ClassName(CKind k) => ClassName(k.Type.NullableOf() ?? k.Type);

    private static string Escape(string name) => Keywords.Contains(name) ? name + "_" : name;

    private static string Str(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in s)
        {
            sb.Append(c switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ when c < ' ' => "\\x" + ((int)c).ToString("x2", CultureInfo.InvariantCulture),
                _ => c.ToString(),
            });
        }

        return sb.Append('"').ToString();
    }

    private static void Guarded<T>(StringBuilder sb, IEnumerable<T> items, Func<T, bool> experimental, Action<StringBuilder, T> write)
    {
        bool open = false;
        foreach (T item in items)
        {
            bool exp = experimental(item);
            if (exp != open)
            {
                sb.AppendLine(exp ? "#ifdef NANOQL_EXPERIMENTAL" : "#endif");
                open = exp;
            }

            write(sb, item);
        }

        if (open)
        {
            sb.AppendLine("#endif");
        }
    }

    // A function's member name before its group's collisions: QuantLib's camelCase, a getter by its property's name.
    private static string BaseMember(CFunction f)
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
            "with" => "with" + member,
            "set" => "set" + member,
            "operator" => f.Member is MethodInfo op ? op.Name : f.BaseName,
            "bind" => "bind",
            "bindon" => "bindOn",
            "draws" => "draws",
            _ => Camel(member),
        };
    }

    private string ClassName(Type t) =>
        types.TryGetValue(t, out TypeRow? r) && r.Form is not ("value" or "withheld" or "matrix" or "vector" or "distribution") ? Qualified(r.Prefix, r.CName) : "Object";

    private string EnumName(CKind k) => enums.TryGetValue(k.Type.NullableOf() ?? k.Type, out EnumRow? e) ? Qualified(e.Prefix, e.CName) : "int32_t";

    // A class's or enum's name in the header being written: its own prefix's bare, another's with its namespace.
    private string Qualified(string prefix, string name) => prefix == context ? name : "::" + (prefix == "nql" ? "nanoql" : prefix) + "::" + name;

    private void Enum(StringBuilder h, EnumRow e)
    {
        h.Append($"enum class {e.CName} : int32_t {{");
        h.Append(string.Join(",", e.Members.Select(m => $" {Escape(m.Name)} = {Int(m.Value)}")));
        h.AppendLine(" };");
        if (e.Flags)
        {
            foreach (string op in new[] { "|", "&", "^" })
            {
                h.AppendLine($"constexpr {e.CName} operator{op}({e.CName} a, {e.CName} b) noexcept {{ return static_cast<{e.CName}>(static_cast<int32_t>(a) {op} static_cast<int32_t>(b)); }}");
            }

            h.AppendLine($"constexpr {e.CName} operator~({e.CName} a) noexcept {{ return static_cast<{e.CName}>(~static_cast<int32_t>(a)); }}");
        }
    }

    private static string Int(long v) => v switch
    {
        int.MinValue => "(-2147483647 - 1)",
        _ => v.ToString(CultureInfo.InvariantCulture),
    };

    // The classes in an order where every class follows its bases: the handle types, nanoql's structs behind handles,
    // generators' snapshots, interfaces and static classes; Date and Period are values, the working types' other forms
    // and the withheld types no class.
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

        foreach (TypeRow t in surface.Types.Where(IsClass).OrderBy(t => t.CName, StringComparer.Ordinal))
        {
            Visit(t);
        }

        return order.Where(IsClass);
    }

    private static bool IsClass(TypeRow t) => t.Form is not ("withheld" or "matrix" or "vector" or "distribution" or "value");

    // A class's bases: its base and interfaces the layer has classes for, less any an earlier base already reaches.
    private List<TypeRow> Bases(TypeRow t)
    {
        var rows = new[] { t.Base }.Concat(t.Interfaces).OfType<string>()
            .Select(b => surface.Types.FirstOrDefault(r => r.Type.FullName == b)).OfType<TypeRow>().Where(IsClass).ToList();
        return [.. rows.Where((r, i) => !rows.Where((o, j) => j != i).Any(o => Reaches(o, r)))];
    }

    private bool Reaches(TypeRow from, TypeRow to)
    {
        foreach (string b in new[] { from.Base }.Concat(from.Interfaces).OfType<string>())
        {
            if (b == to.Type.FullName || (surface.Types.FirstOrDefault(r => r.Type.FullName == b) is { } r && Reaches(r, to)))
            {
                return true;
            }
        }

        return false;
    }

    // Each function's class, name, form and defaults. A form whose C++ parameters are another's of its name takes its C
    // overload suffix; at each argument count the forms whose leading parameters agree offer it once, through the
    // shortest (§2.2's rule), the defaults starting where a form's offered counts run to its end.
    private void Plan()
    {
        var holders = surface.Types.Where(t => IsClass(t) || t.Form == "value").ToDictionary(t => t.Type);
        var byClass = new Dictionary<string, List<CppMember>>(StringComparer.Ordinal);
        foreach (CFunction f in surface.Functions)
        {
            if (!holders.TryGetValue(f.Type, out TypeRow? t) || (f.Kind == "new" && t.Shape is "abstract" or "interface" or "static"))
            {
                continue;
            }

            bool value = t.Form == "value";
            context = t.Prefix;
            bool instance = f.Params.Count > 0 && f.Params[0].IsSelf;
            string style = f.Kind == "new" ? "new" : f.Kind == "indexer" && instance && f.Params.Count == 2 ? "index" : instance ? "instance" : "static";
            string name = style switch
            {
                "new" => t.CName,
                "index" => "operator[]",
                _ => BaseMember(f),
            };
            if (style is "instance" or "static")
            {
                name = Escape(name);
                while (Taken.Contains(name) || typeNames.Contains(name) || name == t.CName)
                {
                    name += "_";
                }
            }

            var ps = f.Params.Where(p => !p.IsSelf).ToList();
            List<string> names = ParamNames(ps);
            var m = new CppMember(f, t.CName, name, style, value, names, [.. ps.Select(p => Type(p.Kind, p))]);
            // By prefix and name: a user's class may take one of nanoql's names.
            if (!byClass.TryGetValue(t.Prefix + "." + t.CName, out List<CppMember>? list))
            {
                byClass[t.Prefix + "." + t.CName] = list = [];
            }

            list.Add(m);
            Members[f] = m;
        }

        foreach ((string entry, List<CppMember> ms) in byClass)
        {
            string cls = entry[(entry.IndexOf('.', StringComparison.Ordinal) + 1)..];
            // The fixed constructors: null, adopting, the copy, the move.
            var used = new HashSet<string>(StringComparer.Ordinal) { "new|std::nullptr_t", "new|detail::adopt_t,nql_handle", $"new|const {cls}&", $"new|{cls}&&" };
            foreach (CppMember m in ms.OrderBy(m => m.Types.Count).ThenBy(m => m.Function.CName, StringComparer.Ordinal))
            {
                string key = (m.Style == "new" ? "new" : m.Name) + "|" + string.Join(",", m.Types.Select(Unqualified));
                if (used.Add(key))
                {
                    continue;
                }

                // A constructor whose signature another has becomes a static create; any other form takes its suffix.
                Renamed++;
                string baseName = m.Style == "new" ? "create" : m.Name;
                string suffix = m.Function.CName.Contains("__", StringComparison.Ordinal) ? m.Function.CName[(m.Function.CName.LastIndexOf("__", StringComparison.Ordinal) + 2)..] : string.Empty;
                string renamed = suffix.Length > 0 ? baseName + "_" + suffix : baseName;
                while (!used.Add(renamed + "|" + string.Join(",", m.Types.Select(Unqualified))))
                {
                    renamed += "_";
                }

                m.Style = m.Style == "new" ? "static" : m.Style == "index" ? "instance" : m.Style;
                m.Name = m.Style == "index" ? "item" : renamed;
                m.Create = baseName == "create";
            }

            foreach (IGrouping<string, CppMember> group in ms.GroupBy(m => m.Style == "new" ? "new" : m.Name))
            {
                List<CppMember> forms = [.. group.OrderBy(m => m.Types.Count).ThenBy(m => m.Function.CName, StringComparer.Ordinal)];
                List<int> from = DefaultsFrom(forms);
                for (int i = 0; i < forms.Count; i++)
                {
                    forms[i].DefaultsFrom = from[i];
                }
            }
        }
    }

    private static string Unqualified(string type) => type.Replace("const ", string.Empty, StringComparison.Ordinal).TrimEnd('&');

    // Per form, the first parameter that takes its default: the start of the counts it is offered at that run to its
    // whole signature, no lower than its required parameters.
    private static List<int> DefaultsFrom(List<CppMember> forms)
    {
        var owner = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < forms.Count; i++)
        {
            List<CParam> ps = [.. forms[i].Function.Params.Where(p => !p.IsSelf)];
            int required = ps.FindLastIndex(p => !p.Optional) + 1;
            for (int n = required; n <= ps.Count; n++)
            {
                string call = n + "|" + string.Join(",", forms[i].Types.Take(n).Select(Unqualified));
                if (!owner.TryGetValue(call, out int j) || forms[i].Types.Count < forms[j].Types.Count)
                {
                    owner[call] = i;
                }
            }
        }

        var result = new List<int>();
        for (int i = 0; i < forms.Count; i++)
        {
            int n = forms[i].Types.Count;
            string Call(int k) => k + "|" + string.Join(",", forms[i].Types.Take(k).Select(Unqualified));
            int from = n;
            while (from > 0 && owner.TryGetValue(Call(from - 1), out int j) && j == i)
            {
                from--;
            }

            result.Add(owner.TryGetValue(Call(n), out int whole) && whole == i ? from : n);
        }

        return result;
    }

    // A form's parameters' names: C#'s, a keyword, a type's name or a generated local's escaped.
    private List<string> ParamNames(List<CParam> ps)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (CParam p in ps)
        {
            string n = Escape(p.Name);
            while (typeNames.Contains(n) || n.StartsWith("out_", StringComparison.Ordinal) || n.StartsWith("in_", StringComparison.Ordinal) || !used.Add(n))
            {
                n += "_";
            }

            result.Add(n);
        }

        return result;
    }

    // A parameter's default in C++.
    private string Default(CParam p)
    {
        object? d = p.Default;
        string kind = p.Kind.Name;
        if (d is null or DBNull)
        {
            return kind switch
            {
                "handle" or "boxed" or "boxed?" or "snapshot" => $"detail::null_of<{ClassName(p.Kind)}>()",
                "engine" => "BoundEngine(nullptr)",
                "date" or "date?" => "Date()",
                "period" => "Period()",
                "array" or "vector" or "jagged" or "map" => "nullptr",
                "tuple" => "{}",
                _ => "std::nullopt",
            };
        }

        if (kind is "enum" or "enum?")
        {
            long v = Convert.ToInt64(d, CultureInfo.InvariantCulture);
            string type = EnumName(p.Kind);
            if (enums.TryGetValue(p.Kind.Type.NullableOf() ?? p.Kind.Type, out EnumRow? e) && e.Members.FirstOrDefault(m => m.Value == v).Name is { } member)
            {
                return type + "::" + Escape(member);
            }

            return $"static_cast<{type}>({Int(v)})";
        }

        return d switch
        {
            bool b => b ? "true" : "false",
            double x when double.IsNaN(x) => "std::numeric_limits<double>::quiet_NaN()",
            double x when double.IsPositiveInfinity(x) => "std::numeric_limits<double>::infinity()",
            double x when double.IsNegativeInfinity(x) => "-std::numeric_limits<double>::infinity()",
            double x => Double(x),
            float x => Double(x),
            string s => Str(s),
            long x when x == long.MinValue => "(-9223372036854775807LL - 1)",
            long x => x.ToString(CultureInfo.InvariantCulture) + "LL",
            IFormattable n when kind is "double" or "double?" => Double(Convert.ToDouble(n, CultureInfo.InvariantCulture)),
            IFormattable n when kind is "int64" or "int64?" => Convert.ToInt64(n, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) + "LL",
            IFormattable n => Int(Convert.ToInt64(n, CultureInfo.InvariantCulture)),
            _ => "{}",
        };
    }

    private static string Double(double x)
    {
        string s = x.ToString("R", CultureInfo.InvariantCulture);
        return s.Contains('.') || s.Contains('E') ? s.Replace("E+", "e+", StringComparison.Ordinal).Replace("E-", "e-", StringComparison.Ordinal).Replace("E", "e", StringComparison.Ordinal) : s + ".0";
    }

    // A member's parameter list, with defaults from its DefaultsFrom when declaring.
    private string Params(CppMember m, bool declare) =>
        string.Join(", ", m.Types.Select((t, i) =>
        {
            CParam p = m.Function.Params.Where(q => !q.IsSelf).ElementAt(i);
            string s = t + " " + m.Names[i];
            return declare && i >= m.DefaultsFrom && p.Optional ? s + " = " + Default(p) : s;
        }));

    private void ValueClass(StringBuilder h, StringBuilder defs, List<(bool, string, string)> free, TypeRow t)
    {
        bool period = t.CName == "Period";
        var ms = Members.Values.Where(m => m.Function.Type == t.Type && !m.Function.Experimental).OrderBy(m => m.Function.CName, StringComparer.Ordinal).ToList();
        h.AppendLine(period
            ? "// A period: a length and its units, as nanoql's Period; its members are nanoql's."
            : "// A date: QuantLib's serial (Excel's), 0 for no date (Date()); its members are nanoql's.");
        h.AppendLine($"class {t.CName} {{").AppendLine("public:");
        h.AppendLine($"    {t.CName}() noexcept = default;");
        if (period)
        {
            h.AppendLine("    Period(detail::adopt_t, nql_period p) noexcept : length_(p.length), units_(static_cast<TimeUnit>(p.units)) {}");
            h.AppendLine("    // The C form.");
            h.AppendLine("    nql_period raw() const noexcept { return {length_, static_cast<int32_t>(units_)}; }");
        }
        else
        {
            h.AppendLine("    Date(detail::adopt_t, nql_date serial) noexcept : serial_(serial) {}");
            h.AppendLine("    // The C form, QuantLib's serial.");
            h.AppendLine("    nql_date serial() const noexcept { return serial_; }");
        }

        foreach (CppMember m in ms)
        {
            Declare(h, t, m);
            Define(defs, t, m, m.Function.Deprecated != null);
            Operator(free, m);
        }

        h.AppendLine("private:");
        h.AppendLine(period ? "    int32_t length_ = 0;\n    TimeUnit units_ = TimeUnit{};" : "    nql_date serial_ = 0;");
        h.AppendLine("};").AppendLine();
    }

    private void Class(StringBuilder h, StringBuilder defs, List<(bool, string, string)> free, TypeRow t)
    {
        var ms = Members.Values.Where(m => m.Function.Type == t.Type).OrderBy(m => m.Function.CName, StringComparer.Ordinal).ToList();
        if (t.Experimental)
        {
            h.AppendLine("#ifdef NANOQL_EXPERIMENTAL");
            defs.AppendLine("#ifdef NANOQL_EXPERIMENTAL");
        }

        if (t.Form == "static")
        {
            h.AppendLine($"class {t.CName} {{").AppendLine("public:").AppendLine($"    {t.CName}() = delete;");
        }
        else
        {
            List<TypeRow> bases = Bases(t);
            string list = bases.Count == 0 ? "public virtual Object" : string.Join(", ", bases.Select(b => "public virtual " + Qualified(b.Prefix, b.CName)));
            h.AppendLine($"class {t.CName} : {list} {{");
            h.AppendLine($"    NANOQL_CLASS_({t.CName})");
            // A constructor callable with no arguments is the class's default one; else a protected one, for derived classes.
            if (!ms.Any(m => m.Style == "new" && m.DefaultsFrom == 0))
            {
                h.AppendLine("protected:").AppendLine($"    {t.CName}() = default;").AppendLine("public:");
            }

            // A base's members of a name this class declares too, and a name two bases declare, stay callable here.
            foreach (string line in Usings(t, ms))
            {
                h.AppendLine(line);
            }
        }

        foreach (CppMember m in ms)
        {
            bool exp = m.Function.Experimental && !t.Experimental;
            if (exp)
            {
                h.AppendLine("#ifdef NANOQL_EXPERIMENTAL");
                defs.AppendLine("#ifdef NANOQL_EXPERIMENTAL");
            }

            Declare(h, t, m);
            Define(defs, t, m, m.Function.Deprecated != null);
            Operator(free, m, t.Experimental || exp);
            if (exp)
            {
                h.AppendLine("#endif");
                defs.AppendLine("#endif");
            }
        }

        h.AppendLine("};");
        if (t.Experimental)
        {
            h.AppendLine("#endif");
            defs.AppendLine("#endif");
        }

        h.AppendLine();
    }

    // The using-declarations a class needs: for each name it declares that an ancestor declares too, and each name two
    // of its bases' lines declare, the nearest ancestors declaring it on each line. A name the class declares only in
    // experimental forms, or an ancestor only in experimental ones, isn't brought in (its guard would differ).
    private List<string> Usings(TypeRow t, List<CppMember> own)
    {
        var mine = own.Where(m => m.Style is "instance" or "static" && !m.Function.Experimental).Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
        var lines = new List<string>();
        var names = new HashSet<string>(mine, StringComparer.Ordinal);
        foreach (TypeRow a in Ancestors(t))
        {
            names.UnionWith(Declared(a));
        }

        foreach (string name in names.Order(StringComparer.Ordinal))
        {
            List<TypeRow> nearest = Nearest(t, name);
            if (nearest.Count > 0 && (mine.Contains(name) || nearest.Count > 1))
            {
                lines.AddRange(nearest.Select(a => $"    using {Qualified(a.Prefix, a.CName)}::{name};"));
            }
        }

        return lines;
    }

    private HashSet<string> Declared(TypeRow t) =>
        [.. Members.Values.Where(m => m.Function.Type == t.Type && m.Style is "instance" or "static" && !m.Function.Experimental && !t.Experimental).Select(m => m.Name)];

    private IEnumerable<TypeRow> Ancestors(TypeRow t)
    {
        var seen = new HashSet<TypeRow>();
        var stack = new Stack<TypeRow>(Bases(t));
        while (stack.Count > 0)
        {
            TypeRow a = stack.Pop();
            if (seen.Add(a))
            {
                yield return a;
                foreach (TypeRow b in Bases(a))
                {
                    stack.Push(b);
                }
            }
        }
    }

    // The ancestors declaring a name with no other declaring ancestor between them and the class.
    private List<TypeRow> Nearest(TypeRow t, string name)
    {
        var declaring = Ancestors(t).Where(a => Declared(a).Contains(name)).ToList();
        return [.. declaring.Where(a => !declaring.Any(o => o != a && Ancestors(o).Contains(a))).OrderBy(a => a.CName, StringComparer.Ordinal)];
    }

    private void Declare(StringBuilder h, TypeRow t, CppMember m)
    {
        if (m.Function.Deprecated != null)
        {
            h.AppendLine($"    [[deprecated({Str(m.Function.Deprecated)})]]");
        }

        string ps = Params(m, true);
        switch (m.Style)
        {
            case "new":
                // explicit wherever one argument can call it: C++ converts no argument C# wouldn't.
                h.AppendLine($"    {(m.Types.Count >= 1 && m.DefaultsFrom <= 1 ? "explicit " : string.Empty)}{t.CName}({ps});");
                return;
            case "static":
                h.AppendLine($"    static {(m.Create ? t.CName : Result(m.Function))} {m.Name}({ps});");
                return;
            default:
                h.AppendLine($"    {Result(m.Function)} {m.Name}({ps}) const;");
                return;
        }
    }

    // A member's definition: its arguments in their C forms, the call, the result taken.
    private void Define(StringBuilder d, TypeRow t, CppMember m, bool deprecated)
    {
        // A deprecated member's definition calls its deprecated C function: not a warning in the user's build.
        if (deprecated)
        {
            d.AppendLine("NANOQL_ALLOW_DEPRECATED_BEGIN_");
            Define(d, t, m, false);
            d.AppendLine("NANOQL_ALLOW_DEPRECATED_END_");
            return;
        }

        CFunction f = m.Function;
        string ps = Params(m, false);
        var pre = new List<string>();
        var args = new List<string>();
        if (f.Params.Count > 0 && f.Params[0].IsSelf)
        {
            args.Add(m.Value ? (t.CName == "Period" ? "raw()" : "serial_") : "h_");
        }

        var named = f.Params.Where(p => !p.IsSelf).ToList();
        for (int i = 0; i < named.Count; i++)
        {
            In(named[i], m.Names[i], "in_" + i.ToString(CultureInfo.InvariantCulture), pre, args);
        }

        if (m.Style == "new" || m.Create)
        {
            string make = m.Value ? $"detail::value<{(t.CName == "Period" ? "nql_period" : "nql_date")}>" : "detail::make";
            string outType = m.Value ? (t.CName == "Period" ? "nql_period*" : "nql_date*") : "nql_handle*";
            string lambda = $"[&]({outType} out_) {{ {string.Concat(pre.Select(s => s + " "))}return {f.CName}({string.Join(", ", args.Append("out_"))}); }}";
            if (m.Create)
            {
                d.AppendLine($"inline {t.CName} {t.CName}::{m.Name}({ps})");
                d.AppendLine("{").AppendLine($"    return {t.CName}(detail::adopt, {make}({lambda}));").AppendLine("}");
            }
            else
            {
                string init = m.Value ? $"{t.CName}(detail::adopt, {make}({lambda}))" : $"Object(detail::adopt, {make}({lambda}))";
                d.AppendLine($"inline {t.CName}::{t.CName}({ps})");
                d.AppendLine($"    : {init}").AppendLine("{").AppendLine("}");
            }

            return;
        }

        string result = Result(f);
        string qualifier = m.Style == "static" ? string.Empty : " const";
        d.AppendLine($"inline {result} {t.CName}::{m.Name}({ps}){qualifier}").AppendLine("{");
        foreach (string s in pre)
        {
            d.AppendLine("    " + s);
        }

        if (f.Kind is "bind" or "bindon")
        {
            d.AppendLine("    nql_handle out_ = nullptr;");
            d.AppendLine($"    detail::check({f.CName}({string.Join(", ", args.Append("&out_"))}));");
            d.AppendLine("    return BoundEngine(detail::adopt, out_);");
        }
        else if (f.Return == null)
        {
            d.AppendLine($"    detail::check({f.CName}({string.Join(", ", args)}));");
        }
        else
        {
            var decls = new List<string>();
            var outs = new List<string>();
            string take = Out(f.Return, "out_", decls, outs, f);
            foreach (string s in decls)
            {
                d.AppendLine("    " + s);
            }

            d.AppendLine($"    detail::check({f.CName}({string.Join(", ", args.Concat(outs))}));");
            d.AppendLine($"    return {take};");
        }

        d.AppendLine("}");
    }

    // A parameter's C arguments, with the locals that keep a collection's C form for the call.
    private void In(CParam p, string n, string local, List<string> pre, List<string> args)
    {
        CKind k = p.Kind;
        switch (k.Name)
        {
            case "double" or "int32" or "int64" or "bool": args.Add(n); return;
            case "string": args.Add(Type(k, p).Contains("optional", StringComparison.Ordinal) ? $"detail::opt_string({n})" : n + ".c_str()"); return;
            case "date" or "date?": args.Add(n + ".serial()"); return;
            case "period": args.Add(n + ".raw()"); return;
            case "enum": args.Add($"static_cast<int32_t>({n})"); return;
            case "complex": args.Add($"detail::complex_in({n})"); return;
            case "double?" or "int32?" or "int64?" or "bool?": args.Add($"detail::opt({n})"); return;
            case "enum?": pre.Add($"detail::opt_enum<{EnumName(k)}> {local}({n});"); args.Add(local + ".ptr()"); return;
            case "period?": pre.Add($"detail::opt_period {local}({n});"); args.Add(local + ".ptr()"); return;
            case "complex?": pre.Add($"detail::opt_complex {local}({n});"); args.Add(local + ".ptr()"); return;
            case "handle" or "boxed" or "boxed?" or "snapshot" or "engine": args.Add(n + ".handle()"); return;
            case "matrix": args.AddRange([$"detail::in_matrix({n})", $"detail::rows({n})", $"detail::columns({n})"]); return;
            case "matrix&": args.AddRange([$"detail::inout({n})", $"{n}.rows()", $"{n}.columns()"]); return;
            case "vector&" or "span": args.AddRange([$"detail::inout({n})", $"static_cast<int32_t>({n}.size())"]); return;
            case "vector":
                pre.Add($"auto {local} = detail::pack_numbers({n});");
                args.AddRange([local + ".ptr()", local + ".count()"]);
                return;
            case "tuple":
                // One C argument per element, in its own form.
                for (int i = 0; i < k.Items.Count; i++)
                {
                    CKind e = k.Items[i];
                    string x = $"std::get<{i}>({n})";
                    args.Add(e.Name switch
                    {
                        "double?" or "int32?" or "int64?" or "bool?" => $"detail::opt({x})",
                        "handle" => x + ".handle()",
                        _ => ToC(e, x),
                    });
                }

                return;
            case "array" when k.Element!.Name == "tuple":
                // One C array per element, a nullable one with its flags, then the count.
                for (int i = 0; i < k.Element.Items.Count; i++)
                {
                    CKind e = k.Element.Items[i];
                    if (e.Name == "bool")
                    {
                        pre.Add($"auto {local}{i} = detail::pack<unsigned char>({n}, [](const auto& e) {{ return static_cast<unsigned char>(std::get<{i}>(e)); }});");
                        args.Add($"reinterpret_cast<const bool*>({local}{i}.ptr())");
                        continue;
                    }

                    pre.Add($"auto {local}{i} = detail::pack<{CElement(e)}>({n}, [](const auto& e) {{ return {ToC(e, $"std::get<{i}>(e)")}; }});");
                    args.Add($"{local}{i}.ptr()");
                    if (e.Name is "double?" or "int32?")
                    {
                        pre.Add($"auto {local}{i}h = detail::pack<unsigned char>({n}, [](const auto& e) {{ return static_cast<unsigned char>(std::get<{i}>(e).has_value()); }});");
                        args.Add($"reinterpret_cast<const bool*>({local}{i}h.ptr())");
                    }
                }

                args.Add(local + "0.count()");
                return;
            case "array":
                pre.Add($"auto {local} = {Pack(k.Element!, n)};");
                args.Add(local + ".ptr()");
                if (k.Element!.Name is "double?" or "int32?")
                {
                    args.Add(local + ".flags()");
                }

                args.Add(local + ".count()");
                return;
            case "jagged":
                pre.Add($"auto {local} = detail::pack_jagged({n});");
                args.AddRange([local + ".ptr()", local + ".count()", local + ".row_lengths()", local + ".rows()"]);
                return;
            case "map":
                pre.Add($"auto {local}k = detail::pack<{CElement(k.Element!)}>({n}, [](const auto& e) {{ return {ToC(k.Element!, "e.first")}; }});");
                pre.Add($"auto {local}v = detail::pack_values<{CElement(k.Value!)}>({n}, [](const auto& e) {{ return {ToC(k.Value!, "e.second")}; }});");
                args.AddRange([local + "k.ptr()", local + "v.ptr()"]);
                if (k.Value!.Name is "double?" or "int32?")
                {
                    args.Add(local + "v.flags()");
                }

                args.Add(local + "k.count()");
                return;
        }

        throw new InvalidOperationException("no C++ argument for the kind " + k.Text);
    }

    // An array's packing into its C form.
    private string Pack(CKind e, string n) => e.Name switch
    {
        "double" or "int32" or "int64" => $"detail::pack_numbers({n})",
        "bool" => $"detail::pack_bools({n})",
        "string" => $"detail::pack_strings({n})",
        "enum" => $"detail::pack_enums({n})",
        "complex" => $"detail::pack_complexes({n})",
        "double?" or "int32?" => $"detail::pack_optionals({n})",
        "handle" => $"detail::pack_handles({n})",
        _ => $"detail::pack<{CElement(e)}>({n}, [](const auto& x) {{ return {ToC(e, "x")}; }})",
    };

    // An element's C type, and a C++ element's conversion to it.
    private static string CElement(CKind e) => e.Name switch
    {
        "string" => "const char*",
        "double?" => "double",
        "int32?" or "enum" => "int32_t",
        _ => CForms.Element(e).CType,
    };

    private static string ToC(CKind e, string x) => e.Name switch
    {
        "date" or "date?" => x + ".serial()",
        "period" => x + ".raw()",
        "enum" => $"static_cast<int32_t>({x})",
        "complex" => $"detail::complex_in({x})",
        "string" => x + ".c_str()",
        "handle" => x + ".handle()",
        "double?" or "int32?" => $"{x}.value_or(0)",
        _ => x,
    };

    // A C element's conversion to its C++ element, taking a handle or a string.
    private string FromC(CKind e, string x) => e.Name switch
    {
        "date" or "date?" => $"Date(detail::adopt, {x})",
        "period" => $"Period(detail::adopt, {x})",
        "enum" => $"static_cast<{EnumName(e)}>({x})",
        "complex" => $"detail::complex_out({x})",
        "string" => $"detail::take({x})",
        "handle" => $"{ClassName(e)}(detail::adopt, {x})",
        "bool" => $"static_cast<bool>({x})",
        _ => x,
    };

    // A result's out locals and their addresses for the call, and the expression that takes the result from them.
    private string Out(CKind k, string o, List<string> decls, List<string> outs, CFunction f)
    {
        switch (k.Name)
        {
            case "double" or "int32" or "int64" or "bool":
                decls.Add($"{Type(k, null)} {o}{{}};");
                outs.Add("&" + o);
                return o;
            case "string":
                decls.Add($"char* {o} = nullptr;");
                outs.Add("&" + o);
                return $"detail::take({o})";
            case "date" or "date?" or "period" or "enum" or "complex":
                decls.Add($"{CForms.Out(k, o)[0].CType.TrimEnd('*')} {o}{{}};");
                outs.Add("&" + o);
                return FromC(k, o);
            case "double?" or "int32?" or "int64?" or "bool?" or "period?" or "enum?" or "complex?":
            {
                CKind inner = k with { Name = k.Name.TrimEnd('?') };
                decls.Add($"{CForms.Out(inner, o)[0].CType.TrimEnd('*')} {o}{{}};");
                decls.Add($"bool {o}has = false;");
                outs.AddRange(["&" + o, $"&{o}has"]);
                string t = Type(inner, null);
                return $"{o}has ? std::optional<{t}>({FromC(inner, o)}) : std::nullopt";
            }

            case "handle" or "boxed" or "boxed?" or "snapshot":
                decls.Add($"nql_handle {o} = nullptr;");
                outs.Add("&" + o);
                return $"{ClassName(k)}(detail::adopt, {o})";
            case "matrix":
                decls.Add($"double* {o} = nullptr;");
                decls.Add($"int32_t {o}rows = 0, {o}columns = 0;");
                outs.AddRange(["&" + o, $"&{o}rows", $"&{o}columns"]);
                return $"detail::take_matrix({o}, {o}rows, {o}columns)";
            case "vector":
                decls.Add($"double* {o} = nullptr;");
                decls.Add($"int32_t {o}count = 0;");
                outs.AddRange(["&" + o, $"&{o}count"]);
                return $"detail::take_numbers({o}, {o}count)";
            case "array" when k.Element!.Name is "tuple" or "tuple?":
            {
                // One out array per element, a nullable tuple's flags, the count: a std::vector of std::tuple, of
                // std::optional ones for nullable tuples.
                CKind te = k.Element;
                bool nullable = te.Name == "tuple?";
                var parts = new List<string>();
                for (int i = 0; i < te.Items.Count; i++)
                {
                    CKind e = te.Items[i];
                    string et = e.Name == "string" ? "char*" : CForms.Element(e).CType;
                    decls.Add($"{et}* {o}{i} = nullptr;");
                    outs.Add($"&{o}{i}");
                    parts.Add(FromC(e, $"{o}{i}[i_]"));
                }

                if (nullable)
                {
                    decls.Add($"bool* {o}has = nullptr;");
                    outs.Add($"&{o}has");
                }

                decls.Add($"int32_t {o}count = 0;");
                outs.Add($"&{o}count");
                string tt = "std::tuple<" + string.Join(", ", te.Items.Select(Element)) + ">";
                string make = $"{tt}({string.Join(", ", parts)})";
                string elem = nullable ? $"std::optional<{tt}>" : tt;
                string push = nullable ? $"{o}has[i_] ? {elem}({make}) : {elem}()" : make;
                string frees = string.Concat(Enumerable.Range(0, te.Items.Count).Select(i => $" nql_free({o}{i});")) + (nullable ? $" nql_free({o}has);" : string.Empty);
                return $"[&] {{ std::vector<{elem}> v_; v_.reserve({o}count > 0 ? {o}count : 0); for (int32_t i_ = 0; i_ < {o}count; i_++) v_.push_back({push});{frees} return v_; }}()";
            }

            case "array":
            {
                CKind e = k.Element!;
                string ct = e.Name == "string" ? "char*" : CForms.Element(e).CType;
                decls.Add($"{ct}* {o} = nullptr;");
                bool nullable = e.Name is "double?" or "int32?";
                if (nullable)
                {
                    decls.Add($"bool* {o}has = nullptr;");
                }

                decls.Add($"int32_t {o}count = 0;");
                outs.Add("&" + o);
                if (nullable)
                {
                    outs.Add($"&{o}has");
                }

                outs.Add($"&{o}count");
                return e.Name switch
                {
                    "double" or "int32" or "int64" => $"detail::take_numbers({o}, {o}count)",
                    "bool" => $"detail::take_bools({o}, {o}count)",
                    "string" => $"detail::take_strings({o}, {o}count)",
                    "double?" or "int32?" => $"detail::take_optionals({o}, {o}has, {o}count)",
                    _ => $"detail::take_array<{Element(e)}>({o}, {o}count, []({ct} x) {{ return {FromC(e, "x")}; }})",
                };
            }

            case "jagged":
                decls.Add($"double* {o} = nullptr;");
                decls.Add($"int32_t* {o}lengths = nullptr;");
                decls.Add($"int32_t {o}rows = 0;");
                outs.AddRange(["&" + o, $"&{o}lengths", $"&{o}rows"]);
                return $"detail::take_jagged({o}, {o}lengths, {o}rows)";
            case "map":
            {
                string kt = k.Element!.Name == "string" ? "char*" : CForms.Element(k.Element).CType;
                string vt = k.Value!.Name == "string" ? "char*" : CForms.Element(k.Value).CType;
                bool nullable = k.Value.Name is "double?" or "int32?";
                decls.Add($"{kt}* {o}keys = nullptr;");
                decls.Add($"{vt}* {o}values = nullptr;");
                decls.Add($"bool* {o}has = nullptr;");
                decls.Add($"int32_t {o}count = 0;");
                outs.AddRange([$"&{o}keys", $"&{o}values"]);
                if (nullable)
                {
                    outs.Add($"&{o}has");
                }

                outs.Add($"&{o}count");
                string value = nullable ? $"h ? std::optional<{Type(k.Value with { Name = k.Value.Name.TrimEnd('?') }, null)}>(v) : std::nullopt" : FromC(k.Value, "v");
                return $"detail::take_map<{Element(k.Element)}, {Element(k.Value)}>({o}keys, {o}values, {o}has, {o}count, []({kt} x) {{ return {FromC(k.Element, "x")}; }}, []({vt} v, bool h) {{ (void)h; return {value}; }})";
            }

            case "tuple":
            {
                var parts = new List<string>();
                for (int i = 0; i < k.Items.Count; i++)
                {
                    parts.Add(Out(k.Items[i], o + i.ToString(CultureInfo.InvariantCulture) + "_", decls, outs, f));
                }

                return $"{Type(k, null)}({string.Join(", ", parts)})";
            }

            case "distribution":
            {
                string[] parts = ["x", "dx", "density", "cumulative", "excess", "average"];
                decls.Add("double " + string.Join(", ", parts.Select(p => $"*{o}{p} = nullptr")) + ";");
                decls.Add($"int32_t {o}count = 0;");
                outs.AddRange(parts.Select(p => $"&{o}{p}").Append($"&{o}count"));
                return $"detail::take_distribution({string.Join(", ", parts.Select(p => o + p))}, {o}count)";
            }

            case "draws" or "sequences":
            {
                string count = Members[f].Names[0];
                decls.Add($"double *{o}values = nullptr, *{o}weights = nullptr;");
                decls.Add($"nql_handle {o}next = nullptr;");
                outs.AddRange([$"&{o}values", $"&{o}weights"]);
                string values = $"detail::take_numbers({o}values, {count})";
                if (k.Name == "sequences")
                {
                    decls.Add($"int32_t {o}dimension = 0;");
                    outs.Add($"&{o}dimension");
                    values = $"detail::take_matrix({o}values, {count}, {o}dimension)";
                }

                outs.Add($"&{o}next");
                return $"{Result(f)}({values}, detail::take_numbers({o}weights, {count}), {ClassName(f.Type)}(detail::adopt, {o}next))";
            }
        }

        throw new InvalidOperationException("no C++ result for the kind " + k.Text);
    }

    // A free operator for an arithmetic or ordering member, and for Date's and Period's equality and steps.
    private void Operator(List<(bool, string, string)> free, CppMember m, bool experimental = false)
    {
        CFunction f = m.Function;
        if (f.Kind != "operator" || f.Member is not MethodInfo op || m.Style != "static")
        {
            return;
        }

        string? symbol = Operators.GetValueOrDefault(op.Name);
        if (m.Value)
        {
            symbol ??= op.Name switch { "op_Equality" => "==", "op_Inequality" => "!=", _ => null };
        }

        string result = Result(f);
        string cls = m.Class;
        if (m.Value && op.Name is "op_Increment" or "op_Decrement")
        {
            string s = op.Name == "op_Increment" ? "++" : "--";
            free.Add((experimental, s + "|" + cls, $"inline {cls}& operator{s}({cls}& a)\n{{\n    a = {cls}::{m.Name}(a);\n    return a;\n}}\n" +
                $"inline {cls} operator{s}({cls}& a, int)\n{{\n    {cls} before = a;\n    a = {cls}::{m.Name}(a);\n    return before;\n}}\n"));
            return;
        }

        if (symbol == null || m.Types.Count is < 1 or > 2)
        {
            return;
        }

        string ps = Params(m, false);
        string key = symbol + "|" + string.Join(",", m.Types.Select(Unqualified));
        string text = $"inline {result} operator{symbol}({ps})\n{{\n    return {cls}::{m.Name}({string.Join(", ", m.Names)});\n}}\n";
        free.Add((experimental, key, f.Deprecated != null ? "NANOQL_ALLOW_DEPRECATED_BEGIN_\n" + text + "NANOQL_ALLOW_DEPRECATED_END_\n" : text));
    }
}
