// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Reflection;
using System.Text;
using NanoQL.Bind.Model;
using NanoQL.Bind.Reading;

namespace NanoQL.Bind.Emitting;

/// <summary>
/// The Java back end (docs/bindings.md, "Java"): the idiomatic layer over jextract's (<c>org.nanoql.ffi</c>), from the
/// surface. A class per type in a package per namespace (<c>org.nanoql.termstructures</c>), the experimental API's in
/// <c>org.nanoql.experimental</c>, a stable class's experimental members as statics of its
/// <c>org.nanoql.experimental.&lt;Class&gt;Experimental</c> (BIR7); interfaces with default methods; QuantLib's camelCase;
/// telescoping overloads for nanoql's defaults (BIR13); <c>LocalDate</c>, a <c>Period</c> record, boxed nullables,
/// primitive arrays of numbers and lists of the rest; exceptions per code (BIR21). Each function's class, member and
/// form are planned once, for the classes and for the replayer's dispatch (<c>JavaReplayWriter</c>). A user's prefix
/// (BIR9) calls through its own jextract class, <c>&lt;prefix&gt;.ffi.&lt;prefix&gt;_h</c> over its header, and
/// registers its classes through its own <c>&lt;prefix&gt;.internal.&lt;Prefix&gt;Types</c>, so its package sits beside
/// nanoql's jar.
/// </summary>
internal sealed class JavaWriter
{
    private const string N = "org.nanoql.internal.Native";
    private const string Ffi = "org.nanoql.ffi.nanoql_h";
    private const string Seg = "java.lang.foreign.MemorySegment";
    private const string VL = "java.lang.foreign.ValueLayout";
    private const string Fence = "java.lang.ref.Reference.reachabilityFence";

    private static readonly HashSet<string> Keywords =
    [
        "abstract", "assert", "boolean", "break", "byte", "case", "catch", "char", "class", "const", "continue", "default", "do", "double",
        "else", "enum", "extends", "final", "finally", "float", "for", "goto", "if", "implements", "import", "instanceof", "int", "interface",
        "long", "native", "new", "package", "private", "protected", "public", "return", "short", "static", "strictfp", "super", "switch",
        "synchronized", "this", "throw", "throws", "transient", "try", "void", "volatile", "while", "true", "false", "null", "var", "record",
        "yield", "sealed", "permits", "_", "java", "org",
    ];

    // Names Object and NanoObject have, which a member doesn't take, and the generated bodies' locals.
    private static readonly HashSet<string> Taken =
    [
        "equals", "hashCode", "toString", "getClass", "notify", "notifyAll", "wait", "clone", "finalize", "handle", "close", "isClosed",
        "typeName", "as", "a_", "out_", "self",
    ];

    // The runtime's classes in org.nanoql, whose names a generated class there doesn't take.
    private static readonly HashSet<string> RuntimeClasses =
    [
        "NanoObject", "NanoHandle", "NanoQL", "NanoQLError", "NanoQLException", "NanoQLArgumentException", "BoundEngine", "Distribution",
        "Draws", "SequenceDraws", "Tuple2", "Tuple3", "Complex", "NanoEnum",
    ];

    private readonly Surface surface;
    private readonly Dictionary<Type, TypeRow> types;
    private readonly Dictionary<Type, EnumRow> enums;
    private readonly List<TypeRow> classes;

    /// <summary>Initializes a new instance of the <see cref="JavaWriter"/> class, planning every function's member.</summary>
    /// <param name="surface">The surface.</param>
    public JavaWriter(Surface surface)
    {
        this.surface = surface;
        types = surface.Types.ToDictionary(t => t.Type);
        enums = surface.Enums.ToDictionary(e => e.Type);
        classes = [.. surface.Types.Where(IsClass).OrderBy(t => t.CName, StringComparer.Ordinal)];
        Plan();
    }

    /// <summary>Gets each function's member.</summary>
    public Dictionary<CFunction, JavaMember> Members { get; } = [];

    /// <summary>Gets how many forms took another name than their group's, their erased signature being another form's.</summary>
    public int Renamed { get; private set; }

    /// <summary>A type's Java package: <c>org.nanoql</c> and its namespace's segments, the experimental API's in
    /// <c>org.nanoql.experimental</c>; a user's by its namespace, its experimental ones in its <c>experimental</c>.</summary>
    /// <param name="ns">The .NET namespace.</param>
    /// <param name="experimental">Whether the type is experimental.</param>
    /// <param name="prefix">Its assembly's prefix.</param>
    /// <returns>The package.</returns>
    public static string Package(string? ns, bool experimental, string prefix)
    {
        string n = (ns ?? "NanoQL").ToLowerInvariant();
        string package = n == "nanoql" ? "org.nanoql" : n.StartsWith("nanoql.", StringComparison.Ordinal) ? "org." + n : n;
        return !experimental ? package : prefix == "nql" ? "org.nanoql.experimental" : package + ".experimental";
    }

    /// <summary>A type's class's fully qualified name.</summary>
    /// <param name="t">The type.</param>
    /// <returns>The name.</returns>
    public string Fqn(TypeRow t)
    {
        string package = Package(t.Type.Namespace, t.Experimental, t.Prefix);
        string name = Keywords.Contains(t.CName) || (package == "org.nanoql" && RuntimeClasses.Contains(t.CName)) ? t.CName + "_" : t.CName;
        return package + "." + name;
    }

    /// <summary>An enum's fully qualified name.</summary>
    /// <param name="e">The enum.</param>
    /// <returns>The name.</returns>
    public string Fqn(EnumRow e) => Package(e.Type.Namespace, e.Experimental, e.Prefix) + "." + e.CName;

    /// <summary>Writes the layer's sources into a folder, a directory per package; returns the files written.</summary>
    /// <param name="dir">The sources' root.</param>
    /// <param name="prefixes">The prefixes whose classes to write, every one when null: nanoql's (<c>nql</c>) for its
    /// jar, a user's alone for a package beside it.</param>
    /// <returns>The files written.</returns>
    public int Write(string dir, IReadOnlyCollection<string>? prefixes = null)
    {
        bool Mine(string prefix) => prefixes == null || prefixes.Contains(prefix);
        var files = new Dictionary<string, StringBuilder>(StringComparer.Ordinal);
        foreach (EnumRow e in surface.Enums.Where(e => Mine(e.Prefix)))
        {
            Enum(files, e);
        }

        var byType = Members.Values.GroupBy(m => m.Type.Type).ToDictionary(g => g.Key, g => g.ToList());
        foreach (TypeRow t in classes.Where(t => Mine(t.Prefix)))
        {
            Class(files, t, byType.GetValueOrDefault(t.Type, []));
        }

        foreach (TypeRow t in surface.Types.Where(t => t.Form == "value" && Mine(t.Prefix)))
        {
            Value(files, t, byType.GetValueOrDefault(t.Type, []));
        }

        foreach (IGrouping<string, JavaMember> holder in Members.Values.Where(m => m.Holder != Fqn(m.Type) && m.Type.Form != "value" && Mine(m.Type.Prefix)).GroupBy(m => m.Holder))
        {
            Holder(files, holder.Key, [.. holder]);
        }

        foreach (string prefix in classes.Select(t => t.Prefix).Distinct().Where(Mine))
        {
            Registry(files, prefix);
        }
        foreach ((string fqn, StringBuilder sb) in files)
        {
            string path = Path.Combine(dir, Path.Combine(fqn.Split('.')) + ".java");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, sb.ToString().ReplaceLineEndings("\n"));
        }

        return files.Count;
    }

    /// <summary>A prefix's jextract class: nanoql's <c>org.nanoql.ffi.nanoql_h</c>, a user's
    /// <c>&lt;prefix&gt;.ffi.&lt;prefix&gt;_h</c>.</summary>
    /// <param name="prefix">The prefix.</param>
    /// <returns>The class's fully qualified name.</returns>
    public static string FfiOf(string prefix) => prefix == "nql" ? Ffi : prefix + ".ffi." + prefix + "_h";

    /// <summary>A prefix's registry of its classes: nanoql's <c>org.nanoql.internal.NanoqlTypes</c>, which
    /// <c>Types</c> runs, a user's <c>&lt;prefix&gt;.internal.&lt;Prefix&gt;Types</c>, which its classes' static
    /// initializers run.</summary>
    /// <param name="prefix">The prefix.</param>
    /// <returns>The class's fully qualified name.</returns>
    public static string RegistryOf(string prefix) =>
        prefix == "nql" ? "org.nanoql.internal.NanoqlTypes" : prefix + ".internal." + char.ToUpperInvariant(prefix[0]) + prefix[1..] + "Types";

    /// <summary>A kind's Java type, as a parameter (<paramref name="result"/> false) or a result.</summary>
    /// <param name="k">The kind.</param>
    /// <param name="result">Whether it is a result's.</param>
    /// <returns>The type.</returns>
    public string Type(CKind k, bool result) => k.Name switch
    {
        "double" => "double",
        "int32" => "int",
        "int64" => "long",
        "bool" => "boolean",
        "string" => "java.lang.String",
        "date" or "date?" => "java.time.LocalDate",
        "period" or "period?" => PeriodName,
        "complex" or "complex?" => "org.nanoql.Complex",
        "enum" or "enum?" => EnumName(k),
        "double?" => "java.lang.Double",
        "int32?" => "java.lang.Integer",
        "int64?" => "java.lang.Long",
        "bool?" => "java.lang.Boolean",
        "handle" or "boxed" or "boxed?" or "snapshot" => ClassName(k.Type.NullableOf() ?? k.Type),
        "engine" => "org.nanoql.BoundEngine",
        "matrix" or "matrix&" => "double[][]",
        "vector" or "vector&" => "double[]",
        "span" => k.Element!.Name == "double" ? "double[]" : "int[]",
        "array" => k.Element!.Name switch
        {
            "double" => "double[]",
            "int32" => "int[]",
            "int64" => "long[]",
            "bool" => "boolean[]",
            "handle" when !result => "java.util.List<? extends " + Boxed(k.Element) + ">",
            _ => "java.util.List<" + Boxed(k.Element) + ">",
        },
        "jagged" => "java.util.List<double[]>",
        "map" => "java.util.Map<" + Boxed(k.Element!) + ", " + Boxed(k.Value!) + ">",
        "tuple" or "tuple?" => (k.Items.Count == 2 ? "org.nanoql.Tuple2<" : "org.nanoql.Tuple3<") + string.Join(", ", k.Items.Select(Boxed)) + ">",
        "distribution" => "org.nanoql.Distribution",
        _ => throw new InvalidOperationException("no Java type for the kind " + k.Text),
    };

    /// <summary>A result's Java type, the bindings' own kinds included.</summary>
    /// <param name="f">The function.</param>
    /// <returns>The type, <c>void</c> for none.</returns>
    public string Result(CFunction f) => f.Kind is "bind" or "bindon" ? "org.nanoql.BoundEngine" : f.Return switch
    {
        null => "void",
        { Name: "draws" } => $"org.nanoql.Draws<{ClassName(f.Type)}>",
        { Name: "sequences" } => $"org.nanoql.SequenceDraws<{ClassName(f.Type)}>",
        { } r => Type(r, true),
    };

    /// <summary>A Java type erased, as Java's overload rules compare signatures.</summary>
    /// <param name="type">The type.</param>
    /// <returns>Its erasure.</returns>
    public static string Erased(string type)
    {
        int at = type.IndexOf('<', StringComparison.Ordinal);
        return at < 0 ? type : type[..at] + type[(type.LastIndexOf('>') + 1)..];
    }

    /// <summary>QuantLib's spelling of a member, as C++'s.</summary>
    /// <param name="name">The C# name.</param>
    /// <returns>Its camelCase.</returns>
    public static string Camel(string name) => CppWriter.Camel(name);

    private string PeriodName => surface.Types.FirstOrDefault(t => t.Form == "value" && t.CName == "Period") is { } p ? Package(p.Type.Namespace, false, p.Prefix) + ".Period" : "org.nanoql.time.Period";

    private string Boxed(CKind e) => e.Name switch
    {
        "double" => "java.lang.Double",
        "int32" => "java.lang.Integer",
        "int64" => "java.lang.Long",
        "bool" => "java.lang.Boolean",
        _ => Type(e, true),
    };

    private string ClassName(Type t) =>
        types.TryGetValue(t, out TypeRow? r) && IsClass(r) ? Fqn(r) : "org.nanoql.NanoObject";

    private string EnumName(CKind k) => enums.TryGetValue(k.Type.NullableOf() ?? k.Type, out EnumRow? e) ? Fqn(e) : "java.lang.Integer";

    private static bool IsClass(TypeRow t) => t.Form is not ("withheld" or "matrix" or "vector" or "distribution" or "value");

    private static string Escape(string name) => Keywords.Contains(name) || Taken.Contains(name) ? name + "_" : name;

    // Each function's class, name, form and telescoping. A form whose erased parameters another's of its name are takes its
    // C overload suffix; a constructor's becomes a static create_...; at each argument count the forms whose leading
    // parameters agree offer it once, through the shortest (§2.2's rule), the telescoped overloads starting where a
    // form's offered counts run to its end.
    private void Plan()
    {
        var byClass = new Dictionary<string, List<JavaMember>>(StringComparer.Ordinal);
        foreach (CFunction f in surface.Functions)
        {
            if (!types.TryGetValue(f.Type, out TypeRow? t) || !(IsClass(t) || t.Form == "value") || (f.Kind == "new" && t.Shape is "abstract" or "interface" or "static"))
            {
                continue;
            }

            bool instance = f.Params.Count > 0 && f.Params[0].IsSelf;
            bool attached = f.Experimental && !t.Experimental && t.Form != "value";
            bool value = t.Form == "value";
            string holder = attached ? ExperimentalHolder(t) : value ? Package(t.Type.Namespace, false, t.Prefix) + "." + t.CName : Fqn(t);
            string style = f.Kind == "new" && !attached && !(value && !IsPeriodCanonical(t, f)) ? "new" : instance ? (attached || (value && t.CName == "Date") ? "self" : "instance") : "static";
            // A value's other constructors are factories, of (LocalDate's way); a stable class's experimental one a create.
            string name = style == "new" ? t.CName : f.Kind == "new" ? (value ? "of" : "create") : Escape(BaseMember(f));
            var ps = f.Params.Where(p => !p.IsSelf).ToList();
            var m = new JavaMember(f, t, holder, name, style, ParamNames(ps), [.. ps.Select(p => Type(p.Kind, false))]);
            Members[f] = m;
            if (!byClass.TryGetValue(holder, out List<JavaMember>? list))
            {
                byClass[holder] = list = [];
            }

            list.Add(m);
        }

        foreach ((string holder, List<JavaMember> ms) in byClass)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (JavaMember m in ms.OrderBy(m => m.Types.Count).ThenBy(m => m.Function.CName, StringComparer.Ordinal))
            {
                string key = (m.Style == "new" ? "<init>" : m.Name) + "|" + Signature(m);
                if (used.Add(key))
                {
                    continue;
                }

                Renamed++;
                string baseName = m.Style == "new" ? "create" : m.Name;
                string suffix = m.Function.CName.Contains("__", StringComparison.Ordinal) ? m.Function.CName[(m.Function.CName.LastIndexOf("__", StringComparison.Ordinal) + 2)..] : string.Empty;
                string renamed = suffix.Length > 0 ? baseName + "_" + suffix : baseName;
                while (!used.Add(renamed + "|" + Signature(m)))
                {
                    renamed += "_";
                }

                m.Style = m.Style == "new" ? "static" : m.Style;
                m.Name = renamed;
            }

        }

        Inherited(byClass);
        foreach (List<JavaMember> ms in byClass.Values)
        {
            foreach (IGrouping<string, JavaMember> group in ms.GroupBy(m => m.Style == "new" ? "<init>" : m.Name))
            {
                List<JavaMember> forms = [.. group.OrderBy(m => m.Types.Count).ThenBy(m => m.Function.CName, StringComparer.Ordinal)];
                List<int> from = DefaultsFrom(forms);
                for (int i = 0; i < forms.Count; i++)
                {
                    forms[i].DefaultsFrom = from[i];
                }
            }
        }
    }

    // A member Java's overriding rules refuse: one C# hides with new and a return type that isn't the inherited one's
    // (OvernightIndexedSwapIndex.UnderlyingSwap), or a static of an ancestor's instance member's signature, or the
    // reverse. It takes a trailing underscore. Ancestors are settled first, a class's own renames before its heirs'.
    private void Inherited(Dictionary<string, List<JavaMember>> byClass)
    {
        var done = new HashSet<TypeRow>();
        void Visit(TypeRow t)
        {
            if (!done.Add(t))
            {
                return;
            }

            List<TypeRow> ancestors = Ancestors(t);
            foreach (TypeRow a in ancestors)
            {
                Visit(a);
            }

            if (!byClass.TryGetValue(Fqn(t), out List<JavaMember>? own))
            {
                return;
            }

            var inherited = ancestors.SelectMany(a => byClass.GetValueOrDefault(Fqn(a), [])).Where(m => m.Style is "instance" or "static").ToList();
            foreach (JavaMember m in own.Where(m => m.Style is "instance" or "static"))
            {
                while (inherited.Any(i => i.Name == m.Name && Signature(i) == Signature(m) && !Overrides(m, i)))
                {
                    m.Name += "_";
                    Renamed++;
                }
            }
        }

        foreach (TypeRow t in classes)
        {
            Visit(t);
        }
    }

    // Whether a member overrides or hides an inherited one Java's way: both instance members or both statics, its result
    // the same or a class deriving from the inherited's (ConventionSet.QuantLib hides ConventionProvider.QuantLib).
    private bool Overrides(JavaMember m, JavaMember inherited)
    {
        if (m.Style != inherited.Style)
        {
            return false;
        }

        string r = Result(m.Function), ri = Result(inherited.Function);
        if (r == ri)
        {
            return true;
        }

        return m.Function.Return is { } k && k.Name is "handle" or "boxed" or "snapshot" && types.TryGetValue(k.Type, out TypeRow? rt)
            && Ancestors(rt).Any(a => Fqn(a) == ri);
    }

    // A type's ancestors among the classes: its base and interfaces, theirs, and so on.
    private List<TypeRow> Ancestors(TypeRow t)
    {
        var result = new List<TypeRow>();
        var stack = new Stack<string>(new[] { t.Base }.Concat(t.Interfaces).OfType<string>());
        while (stack.Count > 0)
        {
            string name = stack.Pop();
            if (surface.Types.FirstOrDefault(r => r.Type.FullName == name) is { } a && IsClass(a) && !result.Contains(a))
            {
                result.Add(a);
                foreach (string b in new[] { a.Base }.Concat(a.Interfaces).OfType<string>())
                {
                    stack.Push(b);
                }
            }
        }

        return result;
    }

    // A stable class's experimental members' holder: org.nanoql.experimental.<Class>Experimental (BIR7).
    private string ExperimentalHolder(TypeRow t) => Package(t.Type.Namespace, true, t.Prefix) + "." + t.CName + "Experimental";

    // Period's canonical constructor, (length, units), is the record's own.
    private static bool IsPeriodCanonical(TypeRow t, CFunction f) => t.CName == "Period" && f.Params.Count == 2;

    private static string Signature(JavaMember m) => string.Join(",", m.Types.Select(Erased));

    // Per form, the first parameter a telescoped overload leaves out: the start of the counts it is offered at that run
    // to its whole signature, no lower than its required parameters.
    private static List<int> DefaultsFrom(List<JavaMember> forms)
    {
        var owner = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < forms.Count; i++)
        {
            List<CParam> ps = [.. forms[i].Function.Params.Where(p => !p.IsSelf)];
            int required = ps.FindLastIndex(p => !p.Optional) + 1;
            for (int n = required; n <= ps.Count; n++)
            {
                string call = n + "|" + string.Join(",", forms[i].Types.Take(n).Select(Erased));
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
            string Call(int k) => k + "|" + string.Join(",", forms[i].Types.Take(k).Select(Erased));
            int from = n;
            while (from > 0 && owner.TryGetValue(Call(from - 1), out int j) && j == i)
            {
                from--;
            }

            result.Add(owner.TryGetValue(Call(n), out int whole) && whole == i ? from : n);
        }

        return result;
    }

    // A function's member name: QuantLib's camelCase, a getter by its property's.
    private static string BaseMember(CFunction f)
    {
        string member = f.Member switch
        {
            PropertyInfo p => p.Name,
            FieldInfo fi => fi.Name,
            MethodBase mb => mb.Name.StartsWith("get_", StringComparison.Ordinal) || mb.Name.StartsWith("set_", StringComparison.Ordinal) ? mb.Name[4..] : mb.Name,
            _ => f.BaseName,
        };
        return f.Kind switch
        {
            "with" => "with" + member,
            "set" => "set" + member,
            "operator" => f.Member is MethodInfo op ? "op" + op.Name[3..] : f.BaseName,
            "indexer" => "get",
            "bind" => "bind",
            "bindon" => "bindOn",
            "draws" => "draws",
            _ => Camel(member),
        };
    }

    // A form's parameters' names: C#'s, a keyword or a generated local's escaped.
    private static List<string> ParamNames(List<CParam> ps)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (CParam p in ps)
        {
            string n = Escape(p.Name);
            while (n.StartsWith("in_", StringComparison.Ordinal) || n.StartsWith("out_", StringComparison.Ordinal) || !used.Add(n))
            {
                n += "_";
            }

            result.Add(n);
        }

        return result;
    }

    private static StringBuilder Source(Dictionary<string, StringBuilder> files, string fqn, string what)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// SPDX-" + "FileCopyrightText: 2026 The nanoql Authors");
        sb.AppendLine("// SPDX-" + "License-Identifier: Apache-2.0");
        sb.AppendLine();
        sb.AppendLine($"// {what} Generated by nanoql-bind from the library's surface; do not edit.");
        sb.AppendLine($"package {fqn[..fqn.LastIndexOf('.')]};").AppendLine();
        files[fqn] = sb;
        return sb;
    }

    private static string Simple(string fqn) => fqn[(fqn.LastIndexOf('.') + 1)..];

    private void Enum(Dictionary<string, StringBuilder> files, EnumRow e)
    {
        string fqn = Fqn(e);
        StringBuilder sb = Source(files, fqn, $"nanoql's {e.CName}.");
        if (e.Experimental)
        {
            sb.AppendLine("/** Experimental (NQL0001): its API and results may change without a deprecation cycle. */");
        }

        string name = Simple(fqn);
        if (e.Flags)
        {
            Flags(sb, e, name);
            return;
        }

        sb.AppendLine($"public enum {name} implements org.nanoql.NanoEnum {{");
        var members = e.Members.Select(m => $"    {(Keywords.Contains(m.Name) ? m.Name + "_" : m.Name)}({Int(m.Value)})").ToList();
        sb.AppendLine(members.Count == 0 ? "    ;" : string.Join(",\n", members) + ";");
        sb.AppendLine();
        sb.AppendLine("    private final int value;").AppendLine();
        sb.AppendLine($"    {name}(int value) {{").AppendLine("        this.value = value;").AppendLine("    }").AppendLine();
        sb.AppendLine("    /** The value nanoql gives the constant. @return the value */");
        sb.AppendLine("    @Override").AppendLine("    public int value() {").AppendLine("        return value;").AppendLine("    }").AppendLine();
        sb.AppendLine("    /** The constant of a value. @param value the value @return the constant */");
        sb.AppendLine($"    public static {name} of(int value) {{");
        sb.AppendLine($"        for ({name} c : values()) {{").AppendLine("            if (c.value == value) {").AppendLine("                return c;").AppendLine("            }").AppendLine("        }");
        sb.AppendLine($"        throw new java.lang.IllegalArgumentException(\"no {name} has the value \" + value);").AppendLine("    }");
        sb.AppendLine("}");
    }

    // A flags enum: a value holding any combination of its constants, as C#'s, C++'s operators and Python's IntFlag do;
    // a Java enum holds its constants only.
    private static void Flags(StringBuilder sb, EnumRow e, string name)
    {
        sb.AppendLine("/**");
        sb.AppendLine($" * A flags enum: any combination of its constants, made with {{@link #or}}.");
        sb.AppendLine(" *");
        sb.AppendLine(" * @param value the combination's value");
        sb.AppendLine(" */");
        sb.AppendLine($"public record {name}(int value) implements org.nanoql.NanoEnum {{");
        foreach ((string member, long v) in e.Members)
        {
            sb.AppendLine($"    public static final {name} {(Keywords.Contains(member) ? member + "_" : member)} = new {name}({Int(v)});");
        }

        sb.AppendLine();
        sb.AppendLine("    private static final " + name + "[] CONSTANTS = {" + string.Join(", ", e.Members.Select(m => Keywords.Contains(m.Name) ? m.Name + "_" : m.Name)) + "};");
        sb.AppendLine("    private static final String[] NAMES = {" + string.Join(", ", e.Members.Select(m => Str(m.Name))) + "};").AppendLine();
        sb.AppendLine("    /** The combination of a value, any value. @param value the value @return the combination */");
        sb.AppendLine($"    public static {name} of(int value) {{");
        sb.AppendLine($"        for ({name} c : CONSTANTS) {{").AppendLine("            if (c.value == value) {").AppendLine("                return c;").AppendLine("            }").AppendLine("        }");
        sb.AppendLine($"        return new {name}(value);").AppendLine("    }").AppendLine();
        sb.AppendLine("    /** The flags of both. @param other the other @return the combination */");
        sb.AppendLine($"    public {name} or({name} other) {{").AppendLine("        return of(value | other.value);").AppendLine("    }").AppendLine();
        sb.AppendLine("    /** The flags the two share. @param other the other @return the combination */");
        sb.AppendLine($"    public {name} and({name} other) {{").AppendLine("        return of(value & other.value);").AppendLine("    }").AppendLine();
        sb.AppendLine("    /** Whether every flag of another is set. @param other the other @return whether it is */");
        sb.AppendLine($"    public boolean has({name} other) {{").AppendLine("        return (value & other.value) == other.value;").AppendLine("    }").AppendLine();
        sb.AppendLine("    /** The constant's name, or the set flags' names joined by |, or the value. @return the text */");
        sb.AppendLine("    @Override").AppendLine("    public String toString() {");
        sb.AppendLine("        StringBuilder b = new StringBuilder();");
        sb.AppendLine("        int rest = value;");
        sb.AppendLine("        for (int i = 0; i < CONSTANTS.length; i++) {");
        sb.AppendLine("            if (CONSTANTS[i].value == value) {").AppendLine("                return NAMES[i];").AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        for (int i = 0; i < CONSTANTS.length; i++) {");
        sb.AppendLine("            int v = CONSTANTS[i].value;");
        sb.AppendLine("            if (v != 0 && (value & v) == v && (rest & v) != 0) {");
        sb.AppendLine("                b.append(b.length() == 0 ? \"\" : \" | \").append(NAMES[i]);");
        sb.AppendLine("                rest &= ~v;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        return rest == 0 && b.length() > 0 ? b.toString() : Integer.toString(value);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    private void Class(Dictionary<string, StringBuilder> files, TypeRow t, List<JavaMember> ms)
    {
        string fqn = Fqn(t);
        string name = Simple(fqn);
        StringBuilder sb = Source(files, fqn, $"nanoql's {t.CName}.");
        if (t.Experimental)
        {
            sb.AppendLine("/** Experimental (NQL0001): its API and results may change without a deprecation cycle. */");
        }

        var own = ms.Where(m => m.Holder == fqn).OrderBy(m => m.Function.CName, StringComparer.Ordinal).ToList();
        var bases = new[] { t.Base }.Concat(t.Interfaces).OfType<string>().Select(b => surface.Types.FirstOrDefault(r => r.Type.FullName == b)).OfType<TypeRow>().Where(IsClass).ToList();
        if (t.Shape == "interface")
        {
            var parents = bases.Where(b => b.Shape == "interface").Select(Fqn).Prepend("org.nanoql.NanoHandle");
            sb.AppendLine($"public interface {name} extends {string.Join(", ", parents)} {{");
            foreach (JavaMember m in own)
            {
                Method(sb, m, interfaceMember: true);
            }

            sb.AppendLine("    /** An object of this interface whose own class the layer doesn't know. */");
            sb.AppendLine($"    final class Of extends org.nanoql.NanoObject implements {name} {{");
            if (t.Prefix != "nql")
            {
                sb.AppendLine("        static {").AppendLine($"            {RegistryOf(t.Prefix)}.register();").AppendLine("        }").AppendLine();
            }

            sb.AppendLine("        /** Adopts a handle. @param tag the adoption's tag @param h the handle */");
            sb.AppendLine($"        public Of({N}.Adopt tag, {Seg} h) {{").AppendLine("            super(tag, h);").AppendLine("        }").AppendLine("    }");
            sb.AppendLine("}");
            return;
        }

        if (t.Form == "static")
        {
            sb.AppendLine($"public final class {name} {{");
            StaticInit(sb, t.Prefix);
            sb.AppendLine($"    private {name}() {{").AppendLine("    }");
            foreach (JavaMember m in own)
            {
                Method(sb, m);
            }

            sb.AppendLine("}");
            return;
        }

        TypeRow? baseClass = bases.FirstOrDefault(b => b.Shape != "interface" && b.Type.FullName == t.Base);
        var interfaces = bases.Where(b => b.Shape == "interface").Select(Fqn).ToList();
        sb.AppendLine($"public class {name} extends {(baseClass != null ? Fqn(baseClass) : "org.nanoql.NanoObject")}{(interfaces.Count > 0 ? " implements " + string.Join(", ", interfaces) : string.Empty)} {{");
        StaticInit(sb, t.Prefix);
        sb.AppendLine("    /** Adopts a handle nanoql returned. @param tag the adoption's tag @param h the handle */");
        sb.AppendLine($"    public {name}({N}.Adopt tag, {Seg} h) {{").AppendLine("        super(tag, h);").AppendLine("    }");
        foreach (JavaMember m in own)
        {
            if (m.Style == "new")
            {
                Constructor(sb, m, name);
            }
            else
            {
                Method(sb, m);
            }
        }

        sb.AppendLine("}");
    }

    // Date's members, static functions taking the date first (LocalDate takes none), and Period, a record with nanoql's
    // members, operators as statics, and nanoql's equality.
    private void Value(Dictionary<string, StringBuilder> files, TypeRow t, List<JavaMember> ms)
    {
        string fqn = Package(t.Type.Namespace, false, t.Prefix) + "." + t.CName;
        var own = ms.Where(m => m.Holder == fqn && m.Style != "new" && !m.Function.Experimental).OrderBy(m => m.Function.CName, StringComparer.Ordinal).ToList();
        if (t.CName == "Period")
        {
            StringBuilder sb = Source(files, fqn, "nanoql's Period, a value.");
            string unit = EnumName(new CKind("enum", surface.Enums.First(e => e.CName == "TimeUnit").Type));
            sb.AppendLine("/**");
            sb.AppendLine(" * A period: a length and its units. Its members and its equality are nanoql's: a year equals twelve months.");
            sb.AppendLine(" *");
            sb.AppendLine(" * @param length the length");
            sb.AppendLine(" * @param units the units");
            sb.AppendLine(" */");
            sb.AppendLine($"public record Period(int length, {unit} units) {{");
            sb.AppendLine("    static {").AppendLine("        org.nanoql.internal.Library.load();").AppendLine("    }");
            foreach (JavaMember m in own.Where(m => !(m.Style == "instance" && m.Types.Count == 0 && m.Name is "length" or "units")))
            {
                Method(sb, m);
            }

            sb.AppendLine();
            sb.AppendLine("    @Override").AppendLine("    public boolean equals(java.lang.Object o) {");
            sb.AppendLine("        return o instanceof Period p && opEquality(this, p);").AppendLine("    }").AppendLine();
            sb.AppendLine("    @Override").AppendLine("    public int hashCode() {");
            sb.AppendLine("        // Equal periods hash alike: years as months, weeks as days, an empty period as no days.");
            sb.AppendLine("        int u = units.value();");
            sb.AppendLine("        return length == 0 ? 0 : u == 3 ? java.util.Objects.hash(length * 12, 2) : u == 1 ? java.util.Objects.hash(length * 7, 0) : java.util.Objects.hash(length, u);");
            sb.AppendLine("    }").AppendLine("}");
            return;
        }

        StringBuilder d = Source(files, fqn, "nanoql's Date's members.");
        d.AppendLine("/** nanoql's Date's members, each taking a date first: a date is a {@code java.time.LocalDate}, null for none. */");
        d.AppendLine($"public final class {t.CName} {{");
        d.AppendLine("    static {").AppendLine("        org.nanoql.internal.Library.load();").AppendLine("    }").AppendLine();
        d.AppendLine($"    private {t.CName}() {{").AppendLine("    }");
        foreach (JavaMember m in own)
        {
            Method(d, m);
        }

        d.AppendLine("}");
    }

    // A stable class's experimental members: statics of org.nanoql.experimental.<Class>Experimental, the instance first (BIR7).
    private void Holder(Dictionary<string, StringBuilder> files, string fqn, List<JavaMember> ms)
    {
        StringBuilder sb = Source(files, fqn, $"{ms[0].Type.CName}'s experimental members.");
        sb.AppendLine($"/** {ms[0].Type.CName}'s experimental members (NQL0001), each a static taking the object first. */");
        sb.AppendLine($"public final class {Simple(fqn)} {{");
        StaticInit(sb, ms[0].Type.Prefix);
        sb.AppendLine($"    private {Simple(fqn)}() {{").AppendLine("    }");
        foreach (JavaMember m in ms.OrderBy(m => m.Function.CName, StringComparer.Ordinal))
        {
            Method(sb, m);
        }

        sb.AppendLine("}");
    }

    // A class's static initializer: the library loaded, and a user prefix's registry run once.
    private static void StaticInit(StringBuilder sb, string prefix)
    {
        sb.AppendLine("    static {").AppendLine("        org.nanoql.internal.Library.load();");
        if (prefix != "nql")
        {
            sb.AppendLine($"        {RegistryOf(prefix)}.register();");
        }

        sb.AppendLine("    }").AppendLine();
    }

    // A prefix's classes' adopting constructors by their .NET types, for the most derived class a result can wrap as.
    // nanoql's registry Types runs; a user's registers in its own class initializer, which its classes' static
    // initializers run through register().
    private void Registry(Dictionary<string, StringBuilder> files, string prefix)
    {
        const int Chunk = 400;
        var rows = classes.Where(t => t.Shape != "static" && t.Prefix == prefix).ToList();
        string fqn = RegistryOf(prefix), name = Simple(fqn);
        bool nanoql = prefix == "nql";
        StringBuilder sb = Source(files, fqn, "Each class's adopting constructor by its .NET type.");
        sb.AppendLine(nanoql ? $"final class {name} {{" : $"public final class {name} {{");
        if (!nanoql)
        {
            sb.Append("    static {");
            for (int i = 0; i * Chunk < rows.Count; i++)
            {
                sb.AppendLine().Append($"        register{i.ToString(CultureInfo.InvariantCulture)}();");
            }

            sb.AppendLine().AppendLine("    }").AppendLine();
        }

        sb.AppendLine($"    private {name}() {{").AppendLine("    }").AppendLine();
        if (nanoql)
        {
            sb.AppendLine("    static void register() {");
            for (int i = 0; i * Chunk < rows.Count; i++)
            {
                sb.AppendLine($"        register{i.ToString(CultureInfo.InvariantCulture)}();");
            }

            sb.AppendLine("    }");
        }
        else
        {
            sb.AppendLine("    /** Registers the prefix's classes, once, in this class's initializer: its classes' static initializers call it. */");
            sb.AppendLine("    public static void register() {").AppendLine("    }");
        }
        for (int i = 0; i * Chunk < rows.Count; i++)
        {
            sb.AppendLine().AppendLine($"    private static void register{i.ToString(CultureInfo.InvariantCulture)}() {{");
            foreach (TypeRow t in rows.Skip(i * Chunk).Take(Chunk))
            {
                string c = Fqn(t);
                string adopt = nanoql ? "Native.ADOPT" : N + ".ADOPT";
                string make = t.Shape == "interface" ? $"h -> new {c}.Of({adopt}, h)" : $"h -> new {c}({adopt}, h)";
                sb.AppendLine($"        {(nanoql ? "Types" : "org.nanoql.internal.Types")}.register(\"{t.Type.FullName}\", {c}.class, {make});");
            }

            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
    }

    private static string Int(long v) => v == int.MinValue ? "java.lang.Integer.MIN_VALUE" : v.ToString(CultureInfo.InvariantCulture);

    // A member's parameter list, the instance first for a static taking it.
    private string Params(JavaMember m, int count)
    {
        var ps = Enumerable.Range(0, count).Select(i => m.Types[i] + " " + m.Names[i]);
        return string.Join(", ", m.Style == "self" ? ps.Prepend(SelfType(m) + " self") : ps);
    }

    private string SelfType(JavaMember m) => m.Type.Form == "value" ? (m.Type.CName == "Date" ? "java.time.LocalDate" : PeriodName) : Fqn(m.Type);

    private void Constructor(StringBuilder sb, JavaMember m, string name)
    {
        CFunction f = m.Function;
        Deprecated(sb, f);
        sb.AppendLine($"    public {name}({Params(m, m.Types.Count)}) {{");
        var pre = new List<string>();
        var args = new List<string>();
        var after = new List<string>();
        Args(m, pre, args, after);
        List<string> fences = Fences(m);
        string i = fences.Count > 0 ? "    " : string.Empty;
        sb.AppendLine($"        super({N}.ADOPT, {N}.make(a_ -> {{");
        if (fences.Count > 0)
        {
            sb.AppendLine("            try {");
        }

        foreach (string s in pre)
        {
            sb.AppendLine(i + "            " + s);
        }

        sb.AppendLine($"{i}            {Seg} out_ = a_.allocate({VL}.ADDRESS);");
        sb.AppendLine($"{i}            {N}.check({FfiOf(f.Prefix)}.{f.CName}({string.Join(", ", args.Append("out_"))}));");
        sb.AppendLine($"{i}            return out_.get({VL}.ADDRESS, 0);");
        if (fences.Count > 0)
        {
            sb.AppendLine("            } finally {");
            foreach (string n in fences)
            {
                sb.AppendLine($"                {Fence}({n});");
            }

            sb.AppendLine("            }");
        }

        sb.AppendLine("        }));").AppendLine("    }");
        Telescoped(sb, m, name, string.Empty, isNew: true);
    }

    private void Method(StringBuilder sb, JavaMember m, bool interfaceMember = false)
    {
        CFunction f = m.Function;
        string result = m.Style == "static" && f.Kind == "new" && m.Type.Form != "value" ? Fqn(m.Type) : Result(f);
        string modifiers = m.Style is "static" or "self" ? (interfaceMember ? "static " : "public static ") : interfaceMember ? "default " : "public ";
        Deprecated(sb, f);
        sb.AppendLine($"    {modifiers}{result} {m.Name}({Params(m, m.Types.Count)}) {{");
        var pre = new List<string>();
        var args = new List<string>();
        var after = new List<string>();
        if (f.Params.Count > 0 && f.Params[0].IsSelf)
        {
            string self = m.Style == "self" ? "self" : "this";
            args.Add(m.Type.Form == "value" ? (m.Type.CName == "Date" ? $"{N}.date({self})" : $"{N}.period(a_, {self}.length(), {self}.units().value())") : $"{N}.h({self})");
        }

        Args(m, pre, args, after);
        sb.AppendLine($"        try (java.lang.foreign.Arena a_ = java.lang.foreign.Arena.ofConfined()) {{");
        foreach (string s in pre)
        {
            sb.AppendLine("            " + s);
        }

        if (f.Kind == "new" && m.Type.Form != "value")
        {
            sb.AppendLine($"            {Seg} out_ = a_.allocate({VL}.ADDRESS);");
            sb.AppendLine($"            {N}.check({FfiOf(f.Prefix)}.{f.CName}({string.Join(", ", args.Append("out_"))}));");
            sb.AppendLine($"            return new {Fqn(m.Type)}({N}.ADOPT, out_.get({VL}.ADDRESS, 0));");
        }
        else if (f.Kind is "bind" or "bindon")
        {
            sb.AppendLine($"            {Seg} out_ = a_.allocate({VL}.ADDRESS);");
            sb.AppendLine($"            {N}.check({FfiOf(f.Prefix)}.{f.CName}({string.Join(", ", args.Append("out_"))}));");
            sb.AppendLine($"            return new org.nanoql.BoundEngine({N}.ADOPT, out_.get({VL}.ADDRESS, 0));");
        }
        else if (f.Return == null)
        {
            sb.AppendLine($"            {N}.check({FfiOf(f.Prefix)}.{f.CName}({string.Join(", ", args)}));");
            foreach (string s in after)
            {
                sb.AppendLine("            " + s);
            }
        }
        else
        {
            var decls = new List<string>();
            var outs = new List<string>();
            string take = Out(f.Return, "out_", decls, outs, m);
            foreach (string s in decls)
            {
                sb.AppendLine("            " + s);
            }

            sb.AppendLine($"            {N}.check({FfiOf(f.Prefix)}.{f.CName}({string.Join(", ", args.Concat(outs))}));");
            foreach (string s in after)
            {
                sb.AppendLine("            " + s);
            }

            sb.AppendLine($"            return {take};");
        }

        sb.Append("        }");
        List<string> fences = Fences(m);
        if (fences.Count > 0)
        {
            sb.AppendLine(" finally {");
            foreach (string n in fences)
            {
                sb.AppendLine($"            {Fence}({n});");
            }

            sb.Append("        }");
        }

        sb.AppendLine().AppendLine("    }");
        Telescoped(sb, m, m.Name, modifiers, isNew: false, result);
    }

    // What a member's call reads a handle from, kept reachable until the call returns: the object, each object argument,
    // each list, map or tuple holding objects, and each list of such tuples. Compiled code ends a local's liveness at its last use, so without a fence a
    // cleaner may release a temporary's handle between the read and the call.
    private static List<string> Fences(JavaMember m)
    {
        var names = new List<string>();
        CFunction f = m.Function;
        if (f.Params.Count > 0 && f.Params[0].IsSelf && m.Type.Form != "value")
        {
            names.Add(m.Style == "self" ? "self" : "this");
        }

        List<CParam> ps = [.. f.Params.Where(p => !p.IsSelf)];
        for (int k = 0; k < ps.Count; k++)
        {
            if (HoldsHandles(ps[k].Kind))
            {
                names.Add(m.Names[k]);
            }
        }

        return names;
    }

    private static bool HoldsHandles(CKind k) => k.Name switch
    {
        "handle" or "boxed" or "boxed?" or "snapshot" or "engine" => true,
        "array" => HoldsHandles(k.Element!),
        "map" => HoldsHandles(k.Element!) || HoldsHandles(k.Value!),
        "tuple" => k.Items.Any(HoldsHandles),
        _ => false,
    };

    private static void Deprecated(StringBuilder sb, CFunction f)
    {
        if (f.Deprecated != null)
        {
            sb.AppendLine($"    /** @deprecated {f.Deprecated.Replace("*/", "* /", StringComparison.Ordinal)} */");
            sb.AppendLine("    @java.lang.Deprecated");
        }
    }

    // The telescoped overloads of a form: its trailing parameters left at nanoql's defaults, from where its offered counts
    // start (BIR13; Java has no default arguments).
    private void Telescoped(StringBuilder sb, JavaMember m, string name, string modifiers, bool isNew, string result = "")
    {
        List<CParam> ps = [.. m.Function.Params.Where(p => !p.IsSelf)];
        for (int n = m.DefaultsFrom; n < m.Types.Count; n++)
        {
            var call = new List<string>();
            if (m.Style == "self")
            {
                call.Add("self");
            }

            call.AddRange(m.Names.Take(n));
            call.AddRange(ps.Skip(n).Select(p => Default(p, m.Types[ps.IndexOf(p)])));
            Deprecated(sb, m.Function);
            if (isNew)
            {
                sb.AppendLine($"    public {name}({Params(m, n)}) {{").AppendLine($"        this({string.Join(", ", call)});").AppendLine("    }");
            }
            else
            {
                string target = modifiers.Contains("static", StringComparison.Ordinal) ? string.Empty : "this.";
                sb.AppendLine($"    {modifiers}{result} {name}({Params(m, n)}) {{");
                sb.AppendLine($"        {(result == "void" ? string.Empty : "return ")}{target}{name}({string.Join(", ", call)});").AppendLine("    }");
            }
        }
    }

    // A parameter's default in Java, cast to the parameter's type where a literal is ambiguous (null).
    private string Default(CParam p, string type)
    {
        object? d = p.Default;
        string kind = p.Kind.Name;
        if (d is null or DBNull)
        {
            return kind switch
            {
                "period" => $"new {PeriodName}(0, {EnumName(new CKind("enum", surface.Enums.First(e => e.CName == "TimeUnit").Type))}.Days)",
                _ => $"({type}) null",
            };
        }

        if (kind is "enum" or "enum?")
        {
            long v = Convert.ToInt64(d, CultureInfo.InvariantCulture);
            if (enums.TryGetValue(p.Kind.Type.NullableOf() ?? p.Kind.Type, out EnumRow? e) && e.Members.FirstOrDefault(x => x.Value == v).Name is { } member)
            {
                return Fqn(e) + "." + (Keywords.Contains(member) ? member + "_" : member);
            }

            return $"{EnumName(p.Kind)}.of({Int(v)})";
        }

        string literal = d switch
        {
            bool b => b ? "true" : "false",
            double x when double.IsNaN(x) => "java.lang.Double.NaN",
            double x when double.IsPositiveInfinity(x) => "java.lang.Double.POSITIVE_INFINITY",
            double x when double.IsNegativeInfinity(x) => "java.lang.Double.NEGATIVE_INFINITY",
            double x => Double(x),
            float x => Double(x),
            string s => Str(s),
            long x when x == long.MinValue => "java.lang.Long.MIN_VALUE",
            long x => x.ToString(CultureInfo.InvariantCulture) + "L",
            IFormattable n when kind is "double" or "double?" => Double(Convert.ToDouble(n, CultureInfo.InvariantCulture)),
            IFormattable n when kind is "int64" or "int64?" => Convert.ToInt64(n, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) + "L",
            IFormattable n => Int(Convert.ToInt64(n, CultureInfo.InvariantCulture)),
            _ => "null",
        };
        // Parenthesized: a cast before a negative literal would read as a subtraction.
        return kind.EndsWith('?') ? $"({type}) ({literal})" : literal;
    }

    private static string Double(double x)
    {
        string s = x.ToString("R", CultureInfo.InvariantCulture);
        return s.Contains('.') || s.Contains('E') ? s.Replace("E+", "e+", StringComparison.Ordinal).Replace("E-", "e-", StringComparison.Ordinal).Replace("E", "e", StringComparison.Ordinal) : s + ".0";
    }

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
                _ when c < ' ' || c > '~' => "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture),
                _ => c.ToString(),
            });
        }

        return sb.Append('"').ToString();
    }

    // Each parameter's C arguments, with the statements before the call that make them and those after it that copy an
    // in-out array back.
    private void Args(JavaMember m, List<string> pre, List<string> args, List<string> after)
    {
        var ps = m.Function.Params.Where(p => !p.IsSelf).ToList();
        for (int i = 0; i < ps.Count; i++)
        {
            In(ps[i].Kind, m.Names[i], "in" + i.ToString(CultureInfo.InvariantCulture) + "_", pre, args, after);
        }
    }

    private void In(CKind k, string n, string local, List<string> pre, List<string> args, List<string> after)
    {
        switch (k.Name)
        {
            case "double" or "int32" or "int64" or "bool": args.Add(n); return;
            case "string": args.Add($"{N}.str(a_, {n})"); return;
            case "date" or "date?": args.Add($"{N}.date({n})"); return;
            case "period": args.Add($"{N}.period(a_, {n}.length(), {n}.units().value())"); return;
            case "period?": args.Add($"({n} == null ? {Seg}.NULL : {N}.period(a_, {n}.length(), {n}.units().value()))"); return;
            case "enum": args.Add($"{n}.value()"); return;
            case "enum?": args.Add($"({n} == null ? {Seg}.NULL : a_.allocateFrom({VL}.JAVA_INT, {n}.value()))"); return;
            case "complex": args.Add($"{N}.complex(a_, {n}.re(), {n}.im())"); return;
            case "complex?": args.Add($"({n} == null ? {Seg}.NULL : {N}.complex(a_, {n}.re(), {n}.im()))"); return;
            case "double?": args.Add($"{N}.optDouble(a_, {n})"); return;
            case "int32?": args.Add($"{N}.optInt(a_, {n})"); return;
            case "int64?": args.Add($"{N}.optLong(a_, {n})"); return;
            case "bool?": args.Add($"{N}.optBool(a_, {n})"); return;
            case "handle" or "boxed" or "boxed?" or "snapshot" or "engine": args.Add($"{N}.h({n})"); return;
            case "matrix": args.AddRange([$"{N}.matrix(a_, {n})", $"{N}.rows({n})", $"{N}.columns({n})"]); return;
            case "matrix&":
                pre.Add($"{Seg} {local} = {N}.matrix(a_, {n});");
                args.AddRange([local, $"{N}.rows({n})", $"{N}.columns({n})"]);
                after.Add($"if ({n} != null) {{ {N}.copyBack({local}, {n}); }}");
                return;
            case "vector": args.AddRange([$"{N}.doubles(a_, {n})", $"{N}.count({n})"]); return;
            case "vector&" or "span":
            {
                string make = k.Name == "span" && k.Element!.Name != "double" ? "ints" : "doubles";
                pre.Add($"{Seg} {local} = {N}.{make}(a_, {n});");
                args.AddRange([local, $"{N}.count({n})"]);
                after.Add($"if ({n} != null) {{ {N}.copyBack({local}, {n}); }}");
                return;
            }

            case "tuple":
                // One C argument per element (B2R3): null is C#'s default, each element's zero value, and a null in an
                // element that takes none is refused by name (B2D7).
                for (int i = 0; i < k.Items.Count; i++)
                {
                    CKind e = k.Items[i];
                    string item = NonNull(e, $"{n}.item{i + 1}()", $"{n}: its {k.ItemNames[i]} is null");
                    In(e, $"({n} == null ? {Zero(e)} : {item})", local + i, pre, args, after);
                }

                return;
            case "array" when k.Element!.Name == "tuple":
            {
                // One C array per element, a nullable one with its flags, then the count (B2R3): each element's list
                // taken from the tuples, a null in an element that takes none refused by name (B2D7).
                CKind te = k.Element;
                for (int i = 0; i < te.Items.Count; i++)
                {
                    CKind e = te.Items[i];
                    string column = local + "e" + i;
                    string item = NonNull(e, $"t_.item{i + 1}()", $"{n}: an element's {te.ItemNames[i]} is null");
                    pre.Add($"java.util.List<{Boxed(e)}> {column} = {n} == null ? null : {n}.stream().map(t_ -> {item}).toList();");
                    if (e.Name is "double?" or "int32?")
                    {
                        string layout = e.Name == "double?" ? "JAVA_DOUBLE" : "JAVA_INT";
                        string zero = e.Name == "double?" ? "0.0" : "0";
                        args.Add($"{N}.list(a_, {column}, {VL}.{layout}, (s_, i_, v_) -> s_.setAtIndex({VL}.{layout}, i_, v_ == null ? {zero} : v_))");
                        args.Add($"{N}.list(a_, {column}, {VL}.JAVA_BOOLEAN, (s_, i_, v_) -> s_.set({VL}.JAVA_BOOLEAN, i_, v_ != null))");
                    }
                    else
                    {
                        args.Add(ListIn(e, column));
                    }
                }

                args.Add($"{N}.count({n})");
                return;
            }

            case "array":
            {
                CKind e = k.Element!;
                switch (e.Name)
                {
                    case "double": args.AddRange([$"{N}.doubles(a_, {n})", $"{N}.count({n})"]); return;
                    case "int32": args.AddRange([$"{N}.ints(a_, {n})", $"{N}.count({n})"]); return;
                    case "int64": args.AddRange([$"{N}.longs(a_, {n})", $"{N}.count({n})"]); return;
                    case "bool": args.AddRange([$"{N}.bools(a_, {n})", $"{N}.count({n})"]); return;
                    case "double?" or "int32?":
                    {
                        string layout = e.Name == "double?" ? "JAVA_DOUBLE" : "JAVA_INT";
                        string zero = e.Name == "double?" ? "0.0" : "0";
                        args.Add($"{N}.list(a_, {n}, {VL}.{layout}, (s_, i_, v_) -> s_.setAtIndex({VL}.{layout}, i_, v_ == null ? {zero} : v_))");
                        args.Add($"{N}.list(a_, {n}, {VL}.JAVA_BOOLEAN, (s_, i_, v_) -> s_.set({VL}.JAVA_BOOLEAN, i_, v_ != null))");
                        args.Add($"{N}.count({n})");
                        return;
                    }

                    default:
                        args.Add(ListIn(e, n));
                        args.Add($"{N}.count({n})");
                        return;
                }
            }

            case "jagged": args.AddRange([$"{N}.jagged(a_, {n})", $"{N}.jaggedCount({n})", $"{N}.rowLengths(a_, {n})", $"({n} == null ? 0 : {n}.size())"]); return;
            case "map":
            {
                CKind kk = k.Element!, vk = k.Value!;
                pre.Add($"java.util.List<{Boxed(kk)}> {local}k = {n} == null ? null : new java.util.ArrayList<>({n}.keySet());");
                pre.Add($"java.util.List<{Boxed(vk)}> {local}v = {n} == null ? null : new java.util.ArrayList<>({n}.values());");
                args.Add(ListIn(kk, local + "k"));
                if (vk.Name is "double?" or "int32?")
                {
                    string layout = vk.Name == "double?" ? "JAVA_DOUBLE" : "JAVA_INT";
                    string zero = vk.Name == "double?" ? "0.0" : "0";
                    args.Add($"{N}.list(a_, {local}v, {VL}.{layout}, (s_, i_, v_) -> s_.setAtIndex({VL}.{layout}, i_, v_ == null ? {zero} : v_))");
                    args.Add($"{N}.list(a_, {local}v, {VL}.JAVA_BOOLEAN, (s_, i_, v_) -> s_.set({VL}.JAVA_BOOLEAN, i_, v_ != null))");
                }
                else
                {
                    args.Add(ListIn(vk, local + "v"));
                }

                args.Add($"{N}.count({local}k)");
                return;
            }
        }

        throw new InvalidOperationException("no Java argument for the kind " + k.Text);
    }

    // A tuple element's value: as it is where it takes a null, else refused when null.
    private static string NonNull(CKind e, string item, string message) =>
        e.IsNullableValue || e.Name is "handle" or "string" or "date" ? item : $"java.util.Objects.requireNonNull({item}, \"{message}\")";

    // A tuple element's zero value, C#'s default for the element of a tuple parameter given as null.
    private string Zero(CKind e) => e.Name switch
    {
        "double" => "0.0",
        "int32" => "0",
        "int64" => "0L",
        "bool" => "false",
        "enum" => $"{EnumName(e)}.of(0)",
        "period" => $"new {PeriodName}(0, {UnitName}.Days)",
        "complex" => "new org.nanoql.Complex(0.0, 0.0)",
        _ => $"({Type(e, false)}) null",
    };

    // A list's elements in their C form: one, four or eight bytes each, a period two ints, a complex number two doubles.
    private string ListIn(CKind e, string n) => e.Name switch
    {
        "double" => $"{N}.list(a_, {n}, {VL}.JAVA_DOUBLE, (s_, i_, v_) -> s_.setAtIndex({VL}.JAVA_DOUBLE, i_, v_))",
        "int32" => $"{N}.list(a_, {n}, {VL}.JAVA_INT, (s_, i_, v_) -> s_.setAtIndex({VL}.JAVA_INT, i_, v_))",
        "int64" => $"{N}.list(a_, {n}, {VL}.JAVA_LONG, (s_, i_, v_) -> s_.setAtIndex({VL}.JAVA_LONG, i_, v_))",
        "bool" => $"{N}.list(a_, {n}, {VL}.JAVA_BOOLEAN, (s_, i_, v_) -> s_.set({VL}.JAVA_BOOLEAN, i_, v_))",
        "date" or "date?" => $"{N}.list(a_, {n}, {VL}.JAVA_INT, (s_, i_, v_) -> s_.setAtIndex({VL}.JAVA_INT, i_, {N}.date(v_)))",
        "enum" => $"{N}.list(a_, {n}, {VL}.JAVA_INT, (s_, i_, v_) -> s_.setAtIndex({VL}.JAVA_INT, i_, v_.value()))",
        "string" => $"{N}.list(a_, {n}, {VL}.ADDRESS, (s_, i_, v_) -> s_.setAtIndex({VL}.ADDRESS, i_, {N}.str(a_, v_)))",
        "handle" => $"{N}.list(a_, {n}, {VL}.ADDRESS, (s_, i_, v_) -> s_.setAtIndex({VL}.ADDRESS, i_, {N}.h(v_)))",
        "period" => $"{N}.pairs(a_, {n}, (s_, i_, v_) -> {{ s_.setAtIndex({VL}.JAVA_INT, 2L * i_, v_.length()); s_.setAtIndex({VL}.JAVA_INT, 2L * i_ + 1, v_.units().value()); }})",
        "complex" => $"{N}.complexes(a_, {n})",
        _ => throw new InvalidOperationException("no Java list element for the kind " + e.Text),
    };

    // A result's out locals and the call's arguments for them, and the expression that takes the result.
    private string Out(CKind k, string o, List<string> decls, List<string> outs, JavaMember m)
    {
        switch (k.Name)
        {
            case "double" or "int32" or "int64" or "bool" or "date" or "date?" or "enum":
            {
                string layout = k.Name switch { "double" => "JAVA_DOUBLE", "int64" => "JAVA_LONG", "bool" => "JAVA_BOOLEAN", _ => "JAVA_INT" };
                decls.Add($"{Seg} {o} = a_.allocate({VL}.{layout});");
                outs.Add(o);
                return Take(k, o, null);
            }

            case "string" or "handle" or "boxed" or "boxed?" or "snapshot":
                decls.Add($"{Seg} {o} = a_.allocate({VL}.ADDRESS);");
                outs.Add(o);
                return Take(k, o, null);
            case "period":
                decls.Add($"{Seg} {o} = org.nanoql.ffi.nql_period.allocate(a_);");
                outs.Add(o);
                return Take(k, o, null);
            case "complex":
                decls.Add($"{Seg} {o} = org.nanoql.ffi.nql_complex.allocate(a_);");
                outs.Add(o);
                return Take(k, o, null);
            case "double?" or "int32?" or "int64?" or "bool?" or "period?" or "enum?" or "complex?":
            {
                CKind inner = k with { Name = k.Name.TrimEnd('?') };
                Out(inner, o, decls, outs, m);
                decls.Add($"{Seg} {o}has = a_.allocate({VL}.JAVA_BOOLEAN);");
                outs.Add(o + "has");
                string boxed = Boxed(inner);
                string cast = boxed.StartsWith("java.lang.", StringComparison.Ordinal) ? "(" + boxed + ") " : string.Empty;
                return $"{o}has.get({VL}.JAVA_BOOLEAN, 0) ? {cast}{Take(inner, o, null)} : null";
            }

            case "matrix":
                decls.Add($"{Seg} {o} = a_.allocate({VL}.ADDRESS), {o}rows = a_.allocate({VL}.JAVA_INT), {o}columns = a_.allocate({VL}.JAVA_INT);");
                outs.AddRange([o, o + "rows", o + "columns"]);
                return $"{N}.takeMatrix({o}.get({VL}.ADDRESS, 0), {o}rows.get({VL}.JAVA_INT, 0), {o}columns.get({VL}.JAVA_INT, 0))";
            case "vector":
                decls.Add($"{Seg} {o} = a_.allocate({VL}.ADDRESS), {o}count = a_.allocate({VL}.JAVA_INT);");
                outs.AddRange([o, o + "count"]);
                return $"{N}.takeDoubles({o}.get({VL}.ADDRESS, 0), {o}count.get({VL}.JAVA_INT, 0))";
            case "array" when k.Element!.Name is "tuple" or "tuple?":
            {
                CKind te = k.Element;
                bool nullable = te.Name == "tuple?";
                for (int i = 0; i < te.Items.Count; i++)
                {
                    decls.Add($"{Seg} {o}{i} = a_.allocate({VL}.ADDRESS);");
                    outs.Add(o + i);
                }

                decls.Add($"{Seg} {o}has = a_.allocate({VL}.ADDRESS), {o}count = a_.allocate({VL}.JAVA_INT);");
                if (nullable)
                {
                    outs.Add(o + "has");
                }

                outs.Add(o + "count");
                string n = $"{o}count.get({VL}.JAVA_INT, 0)";
                var lists = Enumerable.Range(0, te.Items.Count).Select(i => ListOut(te.Items[i], $"{o}{i}.get({VL}.ADDRESS, 0)", n));
                string has = nullable ? $"{N}.takeBools({o}has.get({VL}.ADDRESS, 0), {n})" : "(boolean[]) null";
                return $"{N}.tuples({string.Join(", ", lists)}, {has})";
            }

            case "array":
            {
                CKind e = k.Element!;
                bool nullable = e.Name is "double?" or "int32?";
                decls.Add($"{Seg} {o} = a_.allocate({VL}.ADDRESS), {o}has = a_.allocate({VL}.ADDRESS), {o}count = a_.allocate({VL}.JAVA_INT);");
                outs.Add(o);
                if (nullable)
                {
                    outs.Add(o + "has");
                }

                outs.Add(o + "count");
                string p = $"{o}.get({VL}.ADDRESS, 0)", n = $"{o}count.get({VL}.JAVA_INT, 0)";
                return e.Name switch
                {
                    "double" => $"{N}.takeDoubles({p}, {n})",
                    "int32" => $"{N}.takeInts({p}, {n})",
                    "int64" => $"{N}.takeLongs({p}, {n})",
                    "bool" => $"{N}.takeBools({p}, {n})",
                    "double?" => $"{N}.takeOptDoubles({p}, {o}has.get({VL}.ADDRESS, 0), {n})",
                    "int32?" => $"{N}.takeOptInts({p}, {o}has.get({VL}.ADDRESS, 0), {n})",
                    _ => ListOut(e, p, n),
                };
            }

            case "jagged":
                decls.Add($"{Seg} {o} = a_.allocate({VL}.ADDRESS), {o}lengths = a_.allocate({VL}.ADDRESS), {o}rows = a_.allocate({VL}.JAVA_INT);");
                outs.AddRange([o, o + "lengths", o + "rows"]);
                return $"{N}.takeJagged({o}.get({VL}.ADDRESS, 0), {o}lengths.get({VL}.ADDRESS, 0), {o}rows.get({VL}.JAVA_INT, 0))";
            case "map":
            {
                CKind kk = k.Element!, vk = k.Value!;
                bool nullable = vk.Name is "double?" or "int32?";
                decls.Add($"{Seg} {o}keys = a_.allocate({VL}.ADDRESS), {o}values = a_.allocate({VL}.ADDRESS), {o}has = a_.allocate({VL}.ADDRESS), {o}count = a_.allocate({VL}.JAVA_INT);");
                outs.AddRange([o + "keys", o + "values"]);
                if (nullable)
                {
                    outs.Add(o + "has");
                }

                outs.Add(o + "count");
                string n = $"{o}count.get({VL}.JAVA_INT, 0)";
                string keys = kk.Name switch
                {
                    "double" => $"{N}.boxed({N}.takeDoubles({o}keys.get({VL}.ADDRESS, 0), {n}))",
                    _ => ListOut(kk, $"{o}keys.get({VL}.ADDRESS, 0)", n),
                };
                string values = vk.Name switch
                {
                    "double" => $"{N}.boxed({N}.takeDoubles({o}values.get({VL}.ADDRESS, 0), {n}))",
                    "double?" => $"{N}.takeOptDoubles({o}values.get({VL}.ADDRESS, 0), {o}has.get({VL}.ADDRESS, 0), {n})",
                    "int32?" => $"{N}.takeOptInts({o}values.get({VL}.ADDRESS, 0), {o}has.get({VL}.ADDRESS, 0), {n})",
                    _ => ListOut(vk, $"{o}values.get({VL}.ADDRESS, 0)", n),
                };
                return $"{N}.zip({keys}, {values})";
            }

            case "tuple":
            {
                var parts = new List<string>();
                for (int i = 0; i < k.Items.Count; i++)
                {
                    parts.Add(Out(k.Items[i], o + i.ToString(CultureInfo.InvariantCulture) + "_", decls, outs, m));
                }

                return $"new {Type(k, true)}({string.Join(", ", parts)})";
            }

            case "distribution":
            {
                string[] parts = ["x", "dx", "density", "cumulative", "excess", "average"];
                decls.Add($"{Seg} " + string.Join(", ", parts.Select(p => $"{o}{p} = a_.allocate({VL}.ADDRESS)")) + $", {o}count = a_.allocate({VL}.JAVA_INT);");
                outs.AddRange(parts.Select(p => o + p).Append(o + "count"));
                return $"new org.nanoql.Distribution({string.Join(", ", parts.Select(p => $"{N}.takeDoubles({o}{p}.get({VL}.ADDRESS, 0), {o}count.get({VL}.JAVA_INT, 0))"))})";
            }

            case "draws" or "sequences":
            {
                string count = m.Names[0];
                string g = ClassName(m.Function.Type);
                decls.Add($"{Seg} {o}values = a_.allocate({VL}.ADDRESS), {o}weights = a_.allocate({VL}.ADDRESS), {o}dimension = a_.allocate({VL}.JAVA_INT), {o}next = a_.allocate({VL}.ADDRESS);");
                outs.AddRange([o + "values", o + "weights"]);
                if (k.Name == "sequences")
                {
                    outs.Add(o + "dimension");
                }

                outs.Add(o + "next");
                string next = $"new {g}({N}.ADOPT, {o}next.get({VL}.ADDRESS, 0))";
                string weights = $"{N}.takeDoubles({o}weights.get({VL}.ADDRESS, 0), {count})";
                return k.Name == "draws"
                    ? $"new org.nanoql.Draws<>({N}.takeDoubles({o}values.get({VL}.ADDRESS, 0), {count}), {weights}, {next})"
                    : $"new org.nanoql.SequenceDraws<>({N}.takeMatrix({o}values.get({VL}.ADDRESS, 0), {count}, {o}dimension.get({VL}.JAVA_INT, 0)), {weights}, {next})";
            }
        }

        throw new InvalidOperationException("no Java result for the kind " + k.Text);
    }

    // A scalar result read from its out-parameter.
    private string Take(CKind k, string o, string? unused)
    {
        _ = unused;
        return k.Name switch
        {
            "double" => $"{o}.get({VL}.JAVA_DOUBLE, 0)",
            "int32" => $"{o}.get({VL}.JAVA_INT, 0)",
            "int64" => $"{o}.get({VL}.JAVA_LONG, 0)",
            "bool" => $"{o}.get({VL}.JAVA_BOOLEAN, 0)",
            "date" or "date?" => $"{N}.toDate({o}.get({VL}.JAVA_INT, 0))",
            "enum" => $"{EnumName(k)}.of({o}.get({VL}.JAVA_INT, 0))",
            "string" => $"{N}.takeString({o}.get({VL}.ADDRESS, 0))",
            "period" => $"new {PeriodName}(org.nanoql.ffi.nql_period.length({o}), {UnitName}.of(org.nanoql.ffi.nql_period.units({o})))",
            "complex" => $"new org.nanoql.Complex(org.nanoql.ffi.nql_complex.re({o}), org.nanoql.ffi.nql_complex.im({o}))",
            "handle" or "boxed" or "boxed?" or "snapshot" => Wrap(k.Type.NullableOf() ?? k.Type, $"{o}.get({VL}.ADDRESS, 0)"),
            _ => throw new InvalidOperationException("no Java scalar result for the kind " + k.Text),
        };
    }

    private string UnitName => EnumName(new CKind("enum", surface.Enums.First(e => e.CName == "TimeUnit").Type));

    // A handle result as the most derived class the layer knows, the declared class's otherwise.
    private string Wrap(Type t, string h)
    {
        if (!types.TryGetValue(t, out TypeRow? r) || !IsClass(r))
        {
            return $"{N}.wrap({h}, org.nanoql.NanoObject.class, x_ -> new org.nanoql.NanoObject({N}.ADOPT, x_))";
        }

        string c = Fqn(r);
        return r.Shape == "interface"
            ? $"{N}.wrap({h}, {c}.class, x_ -> new {c}.Of({N}.ADOPT, x_))"
            : $"{N}.wrap({h}, {c}.class, x_ -> new {c}({N}.ADOPT, x_))";
    }

    // A list nanoql returned, each element read from its C form.
    private string ListOut(CKind e, string p, string n) => e.Name switch
    {
        "double" => $"{N}.boxed({N}.takeDoubles({p}, {n}))",
        "int32" => $"{N}.takeList({p}, {n}, 4, (s_, i_) -> s_.getAtIndex({VL}.JAVA_INT, i_))",
        "int64" => $"{N}.takeList({p}, {n}, 8, (s_, i_) -> s_.getAtIndex({VL}.JAVA_LONG, i_))",
        "bool" => $"{N}.takeList({p}, {n}, 1, (s_, i_) -> s_.get({VL}.JAVA_BOOLEAN, i_))",
        "date" or "date?" => $"{N}.takeList({p}, {n}, 4, (s_, i_) -> {N}.toDate(s_.getAtIndex({VL}.JAVA_INT, i_)))",
        "enum" => $"{N}.takeList({p}, {n}, 4, (s_, i_) -> {EnumName(e)}.of(s_.getAtIndex({VL}.JAVA_INT, i_)))",
        "string" => $"{N}.takeList({p}, {n}, 8, (s_, i_) -> {N}.takeString(s_.getAtIndex({VL}.ADDRESS, i_)))",
        "handle" => $"{N}.takeList({p}, {n}, 8, (s_, i_) -> {Wrap(e.Type, $"s_.getAtIndex({VL}.ADDRESS, i_)")})",
        "period" => $"{N}.takeList({p}, {n}, 8, (s_, i_) -> new {PeriodName}(s_.getAtIndex({VL}.JAVA_INT, 2L * i_), {UnitName}.of(s_.getAtIndex({VL}.JAVA_INT, 2L * i_ + 1))))",
        "complex" => $"{N}.takeList({p}, {n}, 16, (s_, i_) -> new org.nanoql.Complex(s_.getAtIndex({VL}.JAVA_DOUBLE, 2L * i_), s_.getAtIndex({VL}.JAVA_DOUBLE, 2L * i_ + 1)))",
        _ => throw new InvalidOperationException("no Java list element out for the kind " + e.Text),
    };
}
