// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NanoQL.Bind.Model;
using NanoQL.Bind.Reading;

namespace NanoQL.Bind.Emitting;

/// <summary>
/// Writes an Excel build's worksheet functions (docs/excel.md): one per selected function, named by XLR1, each argument
/// an Excel value converted by the runtime by its kind (XLR2), its loop mask (XLR15), its description and each
/// argument's from the XML documentation (XLD11); a family's construction chosen by its type arguments' names (XLD29);
/// the engine adapters the native host's shims share (XLD26); and each function's repro template, its nanoql call with
/// its arguments' places (XLR14).
/// </summary>
internal sealed class ExcelWriter
{
    private const int PerFile = 400;
    private const string X = "global::NanoQL.Excel.X";
    private const string Bound = "global::NanoQL.Excel.BoundEngine";

    // The kinds one cell holds, which a range given in their place loops over (XLR15).
    private static readonly HashSet<string> ScalarKinds =
    [
        "double", "int32", "int64", "bool", "string", "date", "period", "enum", "complex", "double?", "int32?", "int64?",
        "bool?", "date?", "period?", "enum?", "complex?", "handle", "boxed", "boxed?", "snapshot", "engine",
    ];

    private static readonly NullabilityInfoContext Nullability = new();

    private readonly Surface surface;
    private readonly ExcelSelection selection;
    private readonly ExcelOptions options;
    private readonly ExcelDocs docs = new();

    // With it set, each leaf conversion becomes a placeholder @@i@@ for argument i, so a renderer substitutes C# literals
    // and the call is the nanoql call the cell ran (the repro templates, B3-1).
    private bool template;

    /// <summary>Initializes a new instance of the <see cref="ExcelWriter"/> class.</summary>
    /// <param name="surface">The surface.</param>
    /// <param name="selection">The build's functions.</param>
    /// <param name="options">The build's options.</param>
    public ExcelWriter(Surface surface, ExcelSelection selection, ExcelOptions options)
    {
        this.surface = surface;
        this.selection = selection;
        this.options = options;
    }

    /// <summary>Gets the functions carrying a documented summary.</summary>
    public int Documented { get; private set; }

    /// <summary>Gets the help texts cut to Excel's limit.</summary>
    public int Cut => docs.Cut;

    /// <summary>Gets the summaries taken through <c>&lt;inheritdoc/&gt;</c>.</summary>
    public int Inherited => docs.Inherited;

    /// <summary>Gets the functions that loop over a range.</summary>
    public int Looping { get; private set; }

    /// <summary>Gets the required arguments C# annotates nullable, which an empty argument gives none.</summary>
    public int NullableLeftOut { get; private set; }

    /// <summary>Writes the build's generated C#, each file by its name.</summary>
    /// <returns>The files.</returns>
    public Dictionary<string, string> Write()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        List<ExcelUnit> units = selection.Units;
        int file = 0;
        for (int at = 0; at < units.Count; at += PerFile, file++)
        {
            StringBuilder sb = Header($"XlFunctions{file:D3}");
            for (int i = at; i < Math.Min(at + PerFile, units.Count); i++)
            {
                if (units[i].Family != null)
                {
                    Family(sb, units[i], i);
                }
                else
                {
                    Function(sb, units[i].Function, units[i].Name, i);
                }
            }

            sb.AppendLine("}");
            files[$"XlFunctions{file:D3}.g.cs"] = Text(sb);
        }

        StringBuilder adapters = Header("XlAdapters");
        new AdapterWriter(surface, Bound).Write(adapters);
        adapters.AppendLine("}");
        files["XlAdapters.g.cs"] = Text(adapters);
        if (options.TestBuild)
        {
            StringBuilder test = Header("XlTestFunctions");
            test.AppendLine("    [ExcelFunction(Name = \"nqlxlStats\", Category = \"nanoql\", IsThreadSafe = true, IsVolatile = true, Description = \"The live handles, calls, builds and hits (a test build's own)\")]")
                .AppendLine($"    public static string Stats() => {X}.StatsText();").AppendLine("}");
            files["XlTestFunctions.g.cs"] = Text(test);
        }

        return files;
    }

    /// <summary>
    /// Each function's repro template (XLR14): its nanoql call with its arguments' places, their kinds, C# types and
    /// defaults, its loop mask; a family's per construction, a bind's input set, a draws function's generator.
    /// </summary>
    /// <returns>The templates' JSON.</returns>
    public string Templates()
    {
        var t = new List<string>();
        foreach (ExcelUnit u in selection.Units)
        {
            CFunction f = u.Function;
            string head = $"\"excel\":{Js(u.Name)},\"c\":{Js(f.CName)},\"kind\":{Js(f.Kind)}";
            if (u.Family != null)
            {
                t.Add("{" + head + ",\"family\":[" + string.Join(",", u.Family.Select(m =>
                    $"{{\"key\":{Js(string.Join("|", m.Type.GetGenericArguments().Select(ExcelSelection.ArgName)).ToLowerInvariant())},\"c\":{Js(m.CName)},{Body(m)}{Loops(Lift(m.Params, m.Return))}}}")) + "]}");
            }
            else if (f.Kind is "bind" or "bindon")
            {
                t.Add("{" + head + $",\"set\":{Js(f.InputSet!.Id)},\"params\":[" + string.Join(",", f.Params.Select(ParamJson)) + "]" + Loops(Lift(f.Params, null)) + "}");
            }
            else if (f.Kind == "draws")
            {
                t.Add("{" + head + $",\"generator\":{Js(CsNames.Of(f.Type))},\"sequences\":{(f.Return!.Name == "sequences" ? "true" : "false")}" + Loops(Lift(f.Params, null)) + "}");
            }
            else
            {
                t.Add("{" + head + "," + Body(f) + Loops(Lift(f.Params, f.Return)) + "}");
            }
        }

        return "{\"functions\":[\n" + string.Join(",\n", t) + "\n]}\n";
    }

    /// <summary>A function's loop mask (XLR15): a character per argument, '1' where a range loops; null where none does.</summary>
    /// <param name="u">The function.</param>
    /// <returns>The mask.</returns>
    public static string? Mask(ExcelUnit u)
    {
        CFunction f = u.Function;
        return u.Family != null
            ? Lift(f.Params, f.Return, f.Type.GetGenericArguments().Length)
            : Lift(f.Params, f.Kind is "draws" or "bind" or "bindon" ? null : f.Return);
    }

    // A function loops only when its result is one value or one object, since Excel spills no array of arrays.
    private static string? Lift(IEnumerable<CParam> ps, CKind? ret, int lead = 0)
    {
        if (ret != null && !IsObjectKind(ret) && !ScalarKinds.Contains(ret.Name))
        {
            return null;
        }

        string mask = new string('0', lead) + string.Concat(ps.Select(p => ScalarKinds.Contains(p.Kind.Name) ? '1' : '0'));
        return mask.Contains('1', StringComparison.Ordinal) ? mask : null;
    }

    private static string Loops(string? mask) => mask == null ? string.Empty : $",\"loops\":{Js(mask)}";

    private static bool IsObjectKind(CKind? k) => k != null && k.Name is "handle" or "boxed" or "boxed?" or "snapshot" or "engine";

    private static string Js(string? s) => JsonSerializer.Serialize(s);

    private static string Text(StringBuilder sb) => sb.ToString().ReplaceLineEndings("\n");

    private static string Operator(string name, List<string> a, CFunction f)
    {
        string? binary = name switch
        {
            "op_Addition" => "+", "op_Subtraction" => "-", "op_Multiply" => "*", "op_Division" => "/", "op_Modulus" => "%",
            "op_LessThan" => "<", "op_GreaterThan" => ">", "op_LessThanOrEqual" => "<=", "op_GreaterThanOrEqual" => ">=",
            "op_BitwiseAnd" => "&", "op_BitwiseOr" => "|", "op_ExclusiveOr" => "^", "op_LeftShift" => "<<", "op_RightShift" => ">>",
            "op_UnsignedRightShift" => ">>>",
            _ => null,
        };
        if (binary != null)
        {
            return $"({a[0]}) {binary} ({a[1]})";
        }

        return name switch
        {
            "op_UnaryNegation" => $"-({a[0]})",
            "op_UnaryPlus" => $"+({a[0]})",
            "op_LogicalNot" => $"!({a[0]})",
            "op_OnesComplement" => $"~({a[0]})",
            "op_Implicit" or "op_Explicit" => $"({CsNames.Of(((MethodInfo)f.Member!).ReturnType)})({a[0]})",
            _ => throw new InvalidOperationException($"{f.CName}: no syntax for the operator {name}"),
        };
    }

    private static string ParamJson(CParam p)
    {
        CKind k = p.Kind;
        string? Cs(CKind? x) => x?.Type == null ? null : CsNames.Of(x.Type.NullableOf() ?? x.Type);
        string? def = null;
        if (p.Optional)
        {
            try
            {
                def = CsNames.Literal(k.Type, p.Default);
            }
            catch (Exception e) when (e is NotSupportedException or InvalidOperationException or ArgumentException or InvalidCastException)
            {
                def = "default";
            }
        }

        Type? inner = k.Name == "jagged" ? (k.Type.IsArray ? k.Type.GetElementType() : k.Type.GetGenericArguments()[0]) : null;
        CKind? tk = k.Name == "tuple" ? k : k.Name == "array" && k.Element?.Name == "tuple" ? k.Element : null;
        string items = tk == null ? "null" : "[" + string.Join(",", tk.Items.Select(i => $"{{\"kind\":{Js(i.Text)},\"cs\":{Js(Cs(i))}}}")) + "]";
        return $"{{\"name\":{Js(p.Name)},\"kind\":{Js(k.Text)},\"cs\":{Js(Cs(k))},\"self\":{(p.IsSelf ? "true" : "false")},\"optional\":{(p.Optional ? "true" : "false")},\"default\":{Js(def)}," +
               $"\"el\":{Js(Cs(k.Element))},\"elKind\":{Js(k.Element?.Name)},\"val\":{Js(Cs(k.Value))},\"valKind\":{Js(k.Value?.Name)},\"inner\":{Js(inner == null ? null : CsNames.Of(inner))}," +
               $"\"items\":{items},\"adapter\":{(k.Name == "engine" ? k.Engine!.Index.ToString(CultureInfo.InvariantCulture) : "null")}}}";
    }

    private static string Marker(string a)
    {
        Match m = Regex.Match(a, @"^[ab]\[(\d+)\]$");
        return m.Success ? "@@" + m.Groups[1].Value + "@@" : throw new InvalidOperationException("no argument index in " + a);
    }

    private static string TupleTypes(CKind k) => string.Join(", ", k.Items.Select(i => CsNames.Of(i.Type)));

    private static string Wrap(Type collection, string element, string array)
    {
        if (collection.IsArray)
        {
            return array;
        }

        return collection.GetGenericTypeDefinition().FullName switch
        {
            "System.ReadOnlySpan`1" => $"new global::System.ReadOnlySpan<{element}>({array})",
            "System.Collections.Immutable.ImmutableArray`1" => $"global::System.Collections.Immutable.ImmutableArray.Create<{element}>({array})",
            "System.Collections.Generic.List`1" => $"new global::System.Collections.Generic.List<{element}>({array})",
            "System.Collections.Immutable.ImmutableSortedSet`1" => $"global::System.Collections.Immutable.ImmutableSortedSet.Create<{element}>({array})",
            "System.Collections.Immutable.ImmutableHashSet`1" => $"global::System.Collections.Immutable.ImmutableHashSet.Create<{element}>({array})",
            "System.Collections.Generic.IReadOnlySet`1" => $"new global::System.Collections.Generic.HashSet<{element}>({array})",
            "System.Collections.Immutable.ImmutableList`1" or "System.Collections.Immutable.IImmutableList`1" => $"global::System.Collections.Immutable.ImmutableList.Create<{element}>({array})",
            _ => array,
        };
    }

    // A generator's draws from its snapshot: the values, their weights and the next snapshot, behind one handle (XLR2).
    private static void Draws(StringBuilder sb, CFunction f)
    {
        bool sequences = f.Return!.Name == "sequences";
        string iface = sequences ? "global::NanoQL.Numerics.IRsg" : "global::NanoQL.Numerics.IRng";
        sb.AppendLine($"            var g_ = ({iface})(({iface}){X}.SnapshotOf<{CsNames.Of(f.Type)}>(a[0], \"self\")).Clone();");
        sb.AppendLine($"            int n_ = {X}.Int(a[1], \"count\");");
        sb.AppendLine("            global::System.ArgumentOutOfRangeException.ThrowIfNegative(n_, \"count\");");
        if (sequences)
        {
            sb.AppendLine("            int d_ = g_.Dimension; var v_ = new double[n_, d_]; var w_ = new double[n_];");
            sb.AppendLine("            for (int i_ = 0; i_ < n_; i_++) { var s_ = g_.NextSequence(); var sp_ = s_.Value.Span; for (int j_ = 0; j_ < d_; j_++) v_[i_, j_] = sp_[j_]; w_[i_] = s_.Weight; }");
        }
        else
        {
            sb.AppendLine("            var v_ = new double[n_]; var w_ = new double[n_];");
            sb.AppendLine("            for (int i_ = 0; i_ < n_; i_++) { var s_ = g_.Next(); v_[i_] = s_.Value; w_[i_] = s_.Weight; }");
        }

        sb.AppendLine("            return new global::NanoQL.Excel.XlDraws(v_, w_, new global::NanoQL.Excel.XlSnapshot(g_));");
    }

    // A required parameter C# annotates nullable (T?): an empty argument gives none (XLR2's nullables; C0's finding).
    private static bool NullableRef(CFunction f, CParam p)
    {
        if (f.Member is not MethodBase mb)
        {
            return false;
        }

        ParameterInfo? pi = mb.GetParameters().FirstOrDefault(x => x.Name == p.Name);
        if (pi == null || pi.ParameterType.IsValueType)
        {
            return false;
        }

        lock (Nullability)
        {
            return Nullability.Create(pi).WriteState == NullabilityState.Nullable;
        }
    }

    private string Category(Type t) => "nanoql." + (t.Namespace ?? "NanoQL").Replace("NanoQL.", string.Empty, StringComparison.Ordinal);

    private string LiftArg(string? documented, string? mask)
    {
        if (mask == null)
        {
            return documented ?? string.Empty;
        }

        Looping++;
        return (string.IsNullOrEmpty(documented) ? ", null" : documented) + ", \"" + mask + "\"";
    }

    // An argument's help: its documentation, then the form a cell gives it (XLD11).
    private string ArgHelp(CParam p, IReadOnlyDictionary<string, string> pdocs)
    {
        string form = p.Kind.Name switch
        {
            "double" or "double?" => "a number",
            "int32" or "int32?" or "int64" or "int64?" => "a whole number",
            "bool" or "bool?" => "TRUE or FALSE",
            "string" => "text",
            "date" or "date?" => "a date",
            "period" or "period?" => "a period such as \"6M\"",
            "enum" or "enum?" => "a " + (p.Kind.Type.NullableOf() ?? p.Kind.Type).Name + " name",
            "complex" or "complex?" => "a complex number such as \"3+4i\"",
            "handle" when p.Kind.Type.FullName is "NanoQL.Time.Calendar" or "NanoQL.Time.DayCounter" => "a " + p.Kind.Type.Name + " handle or its name",
            "handle" or "boxed" or "boxed?" or "snapshot" => "a " + p.Kind.Type.Name.Split('`')[0] + " handle",
            "engine" => "a bound engine's handle",
            "matrix" => "a range of numbers by rows",
            "vector" => "a row or column of numbers",
            "array" when p.Kind.Element?.Name == "tuple" => "a range, a row per element: " + string.Join(", ", p.Kind.Element.ItemNames),
            "array" => "a row or column",
            "jagged" => "a range, a row per element",
            "map" => "two columns, the keys and the values",
            "tuple" => "a row of " + p.Kind.Items.Count.ToString(CultureInfo.InvariantCulture) + " cells: " + string.Join(", ", p.Kind.ItemNames),
            _ => p.Kind.Text,
        };
        string text = pdocs.TryGetValue(p.Name, out string? d) && !string.IsNullOrWhiteSpace(d) ? d.TrimEnd('.') + " (" + form + ")" : form;
        return docs.Fit((p.Optional ? "Optional. " : string.Empty) + text);
    }

    private void Function(StringBuilder sb, CFunction f, string name, int index)
    {
        (string? summary, IReadOnlyDictionary<string, string> pdocs) = docs.Of(f.Member);
        Documented += summary != null ? 1 : 0;
        var ps = f.Params.Select((p, i) => $"[ExcelArgument(Name = \"{p.Name}\", Description = {Js(ArgHelp(p, pdocs))})] object a{i}").ToList();
        sb.AppendLine($"    [ExcelFunction(Name = \"{name}\", Category = \"{Category(f.Type)}\", IsThreadSafe = true, Description = {Js(docs.Fit(summary ?? Describe.Of(f)))})]");
        sb.AppendLine($"    public static object F{index}({string.Join(", ", ps)})");
        string? mask = Lift(f.Params, f.Kind is "draws" or "bind" or "bindon" ? null : f.Return);
        string arr = f.Params.Count == 0 ? "global::System.Array.Empty<object>()" : $"new object[] {{ {string.Join(", ", f.Params.Select((_, i) => "a" + i))} }}";
        switch (f.Kind)
        {
            case "draws":
                sb.AppendLine($"        => {X}.Run(\"{name}\", {arr}, static a =>").AppendLine("        {");
                Draws(sb, f);
                sb.AppendLine($"        }}{LiftArg(null, mask)});");
                return;
            case "bind":
            case "bindon":
                sb.AppendLine($"        => {X}.Run(\"{name}\", {arr}, static a =>").AppendLine("        {");
                Bind(sb, f);
                sb.AppendLine($"        }}{LiftArg(null, mask)});");
                return;
        }

        string call = Call(f);
        if (f.CName == ExcelSelection.Readers[0])
        {
            // The single-path read parses the very bytes its key hashed (XLR3).
            sb.AppendLine($"        => {X}.RunReader(\"{name}\", {arr}, static a => global::NanoQL.Extensions.Conventions.ConventionsXml.Read({X}.HashedFile(a[0], \"path\"), {X}.AbsPath(a[0], \"path\")){(mask == null ? string.Empty : $", \"{mask}\"")});");
            Looping += mask == null ? 0 : 1;
            return;
        }

        if (f.CName == ExcelSelection.Readers[1])
        {
            // The multi-path read refuses a file changed between its key and its read (XLR3).
            sb.AppendLine($"        => {X}.RunReader(\"{name}\", {arr}, static a => {X}.ReadChecked(a[0], \"paths\"){(mask == null ? string.Empty : $", \"{mask}\"")});");
            Looping += mask == null ? 0 : 1;
            return;
        }

        if (f.Return?.Type != null && (f.Return.Type.IsConstructionOf("System.ReadOnlySpan`1") || f.Return.Type.IsConstructionOf("System.Span`1")))
        {
            call = "(" + call + ").ToArray()";
        }

        string run = IsObjectKind(f.Return) ? "Run" : "RunVal";

        // The codes the member documents for kinds outside the C ABI's table, as its shims catch them (F4-2).
        string documented = f.DocumentedCodes.Count == 0 ? string.Empty
            : ", static e_ => e_ switch { " + string.Join(", ", f.DocumentedCodes.Select(d => $"global::{d.Exception} => {d.Code}")) + ", _ => 0 }";
        if (f.Return == null)
        {
            sb.AppendLine($"        => {X}.RunVal(\"{name}\", {arr}, static a => {{ {call}; return {X}.Done; }}{LiftArg(documented, mask)});");
        }
        else if (f.Return.Name == "snapshot")
        {
            sb.AppendLine($"        => {X}.Run(\"{name}\", {arr}, static a => {X}.NewSnapshot({call}){LiftArg(documented, mask)});");
        }
        else
        {
            sb.AppendLine($"        => {X}.{run}(\"{name}\", {arr}, static a => (object)({call}){LiftArg(documented, mask)});");
        }
    }

    // A family: its type arguments by name, in any case, choosing the construction; another construction's refused with
    // the list of constructions, an experimental one in the stable build naming the experimental build (XLD29).
    private void Family(StringBuilder sb, ExcelUnit u, int index)
    {
        List<CFunction> fam = [.. u.Family!];
        CFunction r = u.Function;
        Type[] tparams = r.Type.GetGenericTypeDefinition().GetGenericArguments();
        int nT = tparams.Length;
        (string? summary, IReadOnlyDictionary<string, string> pdocs) = docs.Of(r.Member);
        Documented += summary != null ? 1 : 0;
        string TypeArgHelp(int i) => docs.Fit("One of " + string.Join(", ", fam.Select(m => ExcelSelection.ArgName(m.Type.GetGenericArguments()[i])).Distinct(StringComparer.Ordinal)) + " (text)");
        var ps = tparams.Select((tp, i) => $"[ExcelArgument(Name = \"{tp.Name}\", Description = {Js(TypeArgHelp(i))})] object t{i}")
            .Concat(r.Params.Select((p, i) => $"[ExcelArgument(Name = \"{p.Name}\", Description = {Js(ArgHelp(p, pdocs))})] object a{i}")).ToList();
        var all = Enumerable.Range(0, nT).Select(i => "t" + i).Concat(r.Params.Select((_, i) => "a" + i));
        string description = docs.Fit((summary != null ? summary.TrimEnd('.') + ". " : string.Empty) + $"{ExcelSelection.DefName(r.Type)}: {fam.Count} constructions");
        sb.AppendLine($"    [ExcelFunction(Name = \"{u.Name}\", Category = \"{Category(r.Type)}\", IsThreadSafe = true, Description = {Js(description)})]");
        sb.AppendLine($"    public static object F{index}({string.Join(", ", ps)})");
        string run = IsObjectKind(r.Return) ? "Run" : "RunVal";
        string? mask = Lift(r.Params, r.Return, nT);
        sb.AppendLine($"        => {X}.{run}(\"{u.Name}\", new object[] {{ {string.Join(", ", all)} }}, static a =>").AppendLine("        {");
        sb.AppendLine($"            string k_ = string.Join(\"|\", {string.Join(", ", Enumerable.Range(0, nT).Select(i => $"{X}.Str(a[{i}], \"{tparams[i].Name}\")"))}).ToLowerInvariant();");
        sb.AppendLine($"            var b = a[{nT}..];").AppendLine("            switch (k_)").AppendLine("            {");
        var rx = new Regex(@"(?<![\w])a\[");
        foreach (CFunction m in fam)
        {
            string call = rx.Replace(Call(m), "b[");
            string key = string.Join("|", m.Type.GetGenericArguments().Select(ExcelSelection.ArgName)).ToLowerInvariant();
            string body = m.Return == null ? $"{{ {call}; return {X}.Done; }}" : m.Return.Name == "snapshot" ? $"return {X}.NewSnapshot({call});" : $"return (object)({call});";
            sb.AppendLine($"                case \"{key}\": {body}");
        }

        foreach (CFunction m in u.Refused)
        {
            string shown = string.Join("/", m.Type.GetGenericArguments().Select(ExcelSelection.ArgName));
            string key = string.Join("|", m.Type.GetGenericArguments().Select(ExcelSelection.ArgName)).ToLowerInvariant();
            sb.AppendLine($"                case \"{key}\": throw new global::System.ArgumentException(\"{u.Name}: the construction {shown} is experimental: it is in the experimental build (nanoql-experimental64.xll)\");");
        }

        string list = string.Join(", ", fam.Select(m => string.Join("/", m.Type.GetGenericArguments().Select(ExcelSelection.ArgName))));
        sb.AppendLine($"                default: throw new global::System.ArgumentException(\"{u.Name}: no construction \" + k_ + \"; one of {list}\");");
        sb.AppendLine("            }").AppendLine($"        }}{LiftArg(null, mask)});");
    }

    private string Call(CFunction f)
    {
        var args = f.Params.Select((p, i) => (p, i)).Where(t => !t.p.IsSelf).Select(t => Arg(t.p, t.i, f)).ToList();

        // The member's optional parameters the C signature leaves out, each passed its default, null.
        foreach (int dropped in f.Dropped)
        {
            args.Insert(dropped, "null");
        }

        string self = f.Params.Count > 0 && f.Params[0].IsSelf ? Self(f.Params[0]) : string.Empty;
        string holder = CsNames.Of(f.Holder);
        string generic = f.TypeArguments.Count > 0 ? "<" + string.Join(", ", f.TypeArguments.Select(CsNames.Of)) + ">" : string.Empty;
        if (f.Kind == "new" && f.Member == null)
        {
            return $"new {CsNames.Of(f.Type)}()";  // a struct's default constructor, which reflection doesn't list
        }

        string name = f.Member!.Name;
        switch (f.Kind)
        {
            case "new":
                var required = f.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.Has("System.Runtime.CompilerServices.RequiredMemberAttribute")).OrderBy(p => p.MetadataToken).ToList();
                int nCtor = ((ConstructorInfo)f.Member).GetParameters().Length;
                return !((ConstructorInfo)f.Member).Has("System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute") && required.Count > 0
                    ? $"new {CsNames.Of(f.Type)}({string.Join(", ", args.Take(nCtor))}) {{ {string.Join(", ", required.Select((r, i) => r.Name + " = " + args[nCtor + i]))} }}"
                    : $"new {CsNames.Of(f.Type)}({string.Join(", ", args)})";
            case "static":
                return $"{holder}.{name}{generic}({string.Join(", ", args)})";
            case "operator":
                return Operator(name, args, f);
            case "method":
                return $"(({holder}){self}).{name}{generic}({string.Join(", ", args)})";
            case "get":
                return f.Static ? $"{holder}.{name[4..]}" : $"(({holder}){self}).{name[4..]}";
            case "indexer":
                return $"(({holder}){self})[{string.Join(", ", args)}]";
            case "field":
                return f.Static ? $"{holder}.{name}" : $"(({holder}){self}).{name}";
            case "with":
                return $"({self}) with {{ {name} = {args[0]} }}";
            default:
                throw new InvalidOperationException(f.Kind);
        }
    }

    private string Self(CParam p) => template ? "@@0@@" : p.Kind.Name switch
    {
        "date" => $"{X}.Date(a[0], \"self\")",
        "period" => $"{X}.Per(a[0], \"self\")",
        "boxed" => $"{X}.Obj<{CsNames.Of(p.Kind.Type)}>(a[0], \"self\")",
        "handle" when p.Kind.Type.FullName == "NanoQL.Extensions.Conventions.ConventionSet" => $"{X}.SelfSet(a[0])",
        "handle" => $"{X}.Obj<{CsNames.Of(p.Kind.Type)}>(a[0], \"self\")",
        _ => throw new InvalidOperationException("self of kind " + p.Kind.Name),
    };

    // A function's repro template: its call with placeholders, its parameters' kinds, C# types and defaults, its result.
    private string Body(CFunction f)
    {
        template = true;
        string call;
        try
        {
            call = Call(f);
        }
        finally
        {
            template = false;
        }

        bool span = f.Return?.Type != null && (f.Return.Type.IsConstructionOf("System.ReadOnlySpan`1") || f.Return.Type.IsConstructionOf("System.Span`1"));
        string ret = f.Return == null ? "null" : $"{{\"kind\":{Js(f.Return.Text)},\"cs\":{Js(f.Return.Type != null ? CsNames.Of(f.Return.Type) : null)}}}";
        return $"\"tpl\":{Js(span ? "(" + call + ").ToArray()" : call)},\"returns\":{ret},\"params\":[{string.Join(",", f.Params.Select(ParamJson))}]";
    }

    // An argument's C# value from its Excel value, by kind; an optional one left out takes its default, and an object
    // left out none where C# lets it be (XLR2).
    private string Arg(CParam p, int i, CFunction f)
    {
        string v = Conv(p.Kind, $"a[{i}]", p.Name, f);
        if (template)
        {
            return v;
        }

        if (p.Optional && !p.Kind.IsNullableValue && p.Kind.Name is not ("boxed?" or "handle" or "snapshot" or "engine"))
        {
            return $"({X}.IsMissing(a[{i}]) ? {CsNames.Literal(p.Kind.Type, p.Default)} : {v})";
        }

        if (p.Optional && p.Default is not null and not DBNull && p.Kind.IsNullableValue)
        {
            return $"({X}.IsMissing(a[{i}]) ? {CsNames.Literal(p.Kind.Type, p.Default)} : {v})";
        }

        // An optional handle or snapshot left out is none, its C# default (XLP84's finding).
        if (p.Optional && p.Kind.Name is "handle" or "snapshot")
        {
            return $"({X}.IsMissing(a[{i}]) ? null : {v})";
        }

        // A required one C# annotates nullable (T?) left out is none too (C0's finding).
        if (!p.Optional && !p.IsSelf && p.Kind.Name is "handle" or "snapshot" && NullableRef(f, p))
        {
            NullableLeftOut++;
            return $"({X}.IsMissing(a[{i}]) ? null : {v})";
        }

        return v;
    }

    private string Conv(CKind k, string a, string name, CFunction f)
    {
        string Cs() => CsNames.Of(k.Type);
        string pn = "\"" + name + "\"";
        if (template && k.Name is not ("array" or "jagged" or "map"))
        {
            return Marker(a);
        }

        switch (k.Name)
        {
            case "double": return $"{X}.Dbl({a}, {pn})";
            case "int32": return $"{X}.Int({a}, {pn})";
            case "int64": return $"{X}.Long({a}, {pn})";
            case "bool": return $"{X}.Bool({a}, {pn})";
            case "string": return $"{X}.Str({a}, {pn})";
            case "date": return $"{X}.Date({a}, {pn})";
            case "period": return $"{X}.Per({a}, {pn})";
            case "enum": return $"{X}.Enum<{Cs()}>({a}, {pn})";
            case "complex": return $"{X}.Cplx({a}, {pn})";
            case "double?": return $"{X}.OptDbl({a}, {pn})";
            case "int32?": return $"{X}.OptInt({a}, {pn})";
            case "int64?": return $"{X}.OptLong({a}, {pn})";
            case "bool?": return $"{X}.OptBool({a}, {pn})";
            case "date?": return $"{X}.OptDate({a}, {pn})";
            case "period?": return $"{X}.OptPer({a}, {pn})";
            case "enum?": return $"{X}.OptEnum<{CsNames.Of(k.Type.NullableOf() ?? k.Type)}>({a}, {pn})";
            case "complex?": return $"{X}.OptCplx({a}, {pn})";
            case "handle": return $"{X}.Obj<{Cs()}>({a}, {pn})";
            case "boxed": return $"{X}.Obj<{Cs()}>({a}, {pn})";
            case "boxed?": return $"{X}.OptBoxed<{CsNames.Of(k.Type.NullableOf() ?? k.Type)}>({a}, {pn})";
            case "snapshot": return $"{X}.Generator<{Cs()}>({a}, {pn})";
            case "engine": return $"XlAdapters.A{k.Engine!.Index}({X}.Engine({a}, {pn}), \"{f.CName}\")";
            case "tuple": return $"{X}.Tuple<{TupleTypes(k)}>({a}, {pn}, {TupleItems(k, name, f)})";
            case "matrix": return $"{X}.Mat({a}, {pn})";
            case "vector": return $"{X}.Vec({a}, {pn})";
            case "array":
            {
                string el = Elements(k.Element!, a, name, f);
                return k.Type.IsConstructionOf("System.ReadOnlySpan`1")
                    ? $"new global::System.ReadOnlySpan<{CsNames.Of(k.Element!.Type)}>({el})"
                    : Wrap(k.Type, CsNames.Of(k.Element!.Type), el);
            }

            case "jagged":
            {
                Type inner = k.Type.IsArray ? k.Type.GetElementType()! : k.Type.GetGenericArguments()[0];
                string rows = template ? Marker(a) : $"{X}.Jagged({a}, {pn})";
                if (inner.IsConstructionOf("System.Collections.Immutable.ImmutableArray`1"))
                {
                    rows = $"global::System.Linq.Enumerable.ToArray(global::System.Linq.Enumerable.Select({rows}, r_ => global::System.Collections.Immutable.ImmutableArray.Create(r_)))";
                }

                return Wrap(k.Type, CsNames.Of(inner), rows);
            }

            case "map":
            {
                string kc = CsNames.Of(k.Element!.Type), vc = CsNames.Of(k.Value!.Type);
                string dict = template ? Marker(a) : $"{X}.Map<{kc}, {vc}>({a}, {pn}, static (x_, p_) => {Conv(k.Element!, "x_", name + " key", f).Replace(pn, "p_", StringComparison.Ordinal)}, static (x_, p_) => {Conv(k.Value!, "x_", name + " value", f).Replace(pn, "p_", StringComparison.Ordinal)})";
                string def = k.Type.IsGenericType ? k.Type.GetGenericTypeDefinition().FullName! : string.Empty;
                return def switch
                {
                    "System.Collections.Immutable.ImmutableDictionary`2" or "System.Collections.Immutable.IImmutableDictionary`2" => $"global::System.Collections.Immutable.ImmutableDictionary.ToImmutableDictionary({dict})",
                    "System.Collections.Immutable.ImmutableSortedDictionary`2" => $"global::System.Collections.Immutable.ImmutableSortedDictionary.ToImmutableSortedDictionary({dict})",
                    "System.Collections.Frozen.FrozenDictionary`2" => $"global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary({dict})",
                    "System.Collections.Generic.IReadOnlyDictionary`2" or "System.Collections.Generic.IDictionary`2" or "System.Collections.Generic.Dictionary`2" => dict,
                    _ => Wrap(k.Type, $"global::System.Collections.Generic.KeyValuePair<{kc}, {vc}>", $"global::System.Linq.Enumerable.ToArray({dict})"),
                };
            }
        }

        throw new InvalidOperationException($"{f.CName}: no Excel form for the kind {k.Text}");
    }

    // A tuple's items' conversions, each taking the cell and its place's name.
    private string TupleItems(CKind k, string name, CFunction f)
        => string.Join(", ", k.Items.Select(i => $"static (x_, p_) => {Conv(i, "x_", name, f).Replace("\"" + name + "\"", "p_", StringComparison.Ordinal)}"));

    // An array of a range's elements, by the element's kind; nullable elements keep the range's shape (XLR2).
    private string Elements(CKind e, string a, string name, CFunction f)
    {
        string cs = CsNames.Of(e.Type);
        string pn = "\"" + name + "\"";
        if (template)
        {
            return Marker(a);
        }

        if (e.Name == "tuple")
        {
            return $"{X}.Tuples<{TupleTypes(e)}>({a}, {pn}, {TupleItems(e, name, f)})";
        }

        return e.Name switch
        {
            "double" => $"{X}.Dbls({a}, {pn})",
            "int32" => $"{X}.Ints({a}, {pn})",
            "int64" => $"{X}.Longs({a}, {pn})",
            "bool" => $"{X}.Bools({a}, {pn})",
            "date" => $"{X}.Dates({a}, {pn})",
            "date?" => $"{X}.OptDates({a}, {pn})",
            "period" => $"{X}.Pers({a}, {pn})",
            "enum" => $"{X}.Enums<{cs}>({a}, {pn})",
            "string" => $"{X}.Strs({a}, {pn})",
            "complex" => $"{X}.Cplxs({a}, {pn})",
            "handle" or "boxed" => $"{X}.Objs<{cs}>({a}, {pn})",
            "double?" => $"{X}.OptDbls({a}, {pn})",
            "int32?" => $"{X}.OptInts({a}, {pn})",
            _ => throw new InvalidOperationException($"{f.CName}: no Excel form for an array of {e.Name}"),
        };
    }

    // A bind or bindon function: the engine with the inputs given, behind a handle (XLR10).
    private void Bind(StringBuilder sb, CFunction f)
    {
        EngineInputSet set = f.InputSet!;
        sb.AppendLine("            var d_ = new global::System.Collections.Generic.Dictionary<string, object>(global::System.StringComparer.Ordinal);");
        string instrument = "null";
        for (int i = 0; i < f.Params.Count; i++)
        {
            CParam p = f.Params[i];
            if (p.Name == "instrument" && f.Kind == "bindon" && i == 0)
            {
                instrument = $"{X}.Obj<{CsNames.Of(p.Kind.Type)}>(a[0], \"instrument\")";
                continue;
            }

            ParameterInfo input = set.Inputs.Single(x => x.Name == p.Name);
            string value = Conv(p.Kind, $"a[{i}]", p.Name, f);
            string stored = AdapterWriter.Stored(input.ParameterType);
            if (stored != CsNames.Of(input.ParameterType))
            {
                value = $"({value}).ToArray()";
            }

            sb.AppendLine($"            if (!{X}.IsMissing(a[{i}])) d_[\"{p.Name}\"] = ({stored})({value});");
        }

        sb.AppendLine($"            return new {Bound}(\"{set.Id}\", {instrument}, d_);");
    }

    private StringBuilder Header(string cls) => new StringBuilder()
        .AppendLine("// <auto-generated/>").AppendLine("#nullable disable").AppendLine("#pragma warning disable")
        .AppendLine("using ExcelDna.Integration;").AppendLine($"namespace {options.Namespace};")
        .AppendLine($"public static class {cls}").AppendLine("{");
}
