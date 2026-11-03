// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Bind.Model;

namespace NanoQL.Bind.Emitting;

/// <summary>
/// The C parameters each kind expands to, in and out (docs/bindings.md, "Kinds"): a scalar is one, an array a pointer
/// and a count, a matrix a pointer by rows and its row and column counts, a nullable result its value and a
/// <c>has_value</c> flag, a tuple one per element, and a collection of tuples an array per element and a count.
/// </summary>
internal static class CForms
{
    private static readonly HashSet<string> Reserved =
    [
        "auto", "bool", "break", "case", "catch", "char", "class", "const", "continue", "default", "delete", "do", "double", "else",
        "enum", "explicit", "export", "extern", "false", "float", "for", "friend", "goto", "if", "inline", "int", "long", "namespace",
        "new", "operator", "private", "protected", "public", "register", "restrict", "return", "short", "signed", "sizeof", "static",
        "struct", "switch", "template", "this", "throw", "true", "try", "typedef", "union", "unsigned", "virtual", "void", "volatile",
        "while", "and", "or", "not", "xor", "self", "result", "has_value",
    ];

    /// <summary>A C# name as a C identifier, escaped where C, C++ or the generated names take it.</summary>
    /// <param name="name">The name.</param>
    /// <param name="isSelf">Whether it is the instance, which keeps the name <c>self</c>.</param>
    /// <returns>The identifier.</returns>
    public static string Ident(string name, bool isSelf = false) => isSelf ? "self" : Reserved.Contains(name) ? name + "_" : name;

    /// <summary>The C parameters a function's parameter expands to.</summary>
    /// <param name="p">The parameter.</param>
    /// <returns>Its slots.</returns>
    public static List<CSlot> In(CParam p)
    {
        string n = Ident(p.Name, p.IsSelf);
        CKind k = p.Kind;
        switch (k.Name)
        {
            case "double": return [new("double", n, "double")];
            case "int32": return [new("int32_t", n, "int")];
            case "int64": return [new("int64_t", n, "long")];
            case "bool": return [new("bool", n, "byte")];
            case "string": return [new("const char*", n, "byte*")];
            case "date" or "date?": return [new("nql_date", n, "int")];
            case "period": return [new("nql_period", n, "NqlPeriod")];
            case "enum": return [new("int32_t", n, "int")];
            case "complex": return [new("nql_complex", n, "NqlComplex")];
            case "double?": return [new("const double*", n, "double*")];
            case "int32?": return [new("const int32_t*", n, "int*")];
            case "int64?": return [new("const int64_t*", n, "long*")];
            case "bool?": return [new("const bool*", n, "byte*")];
            case "period?": return [new("const nql_period*", n, "NqlPeriod*")];
            case "enum?": return [new("const int32_t*", n, "int*")];
            case "complex?": return [new("const nql_complex*", n, "NqlComplex*")];
            case "handle" or "boxed" or "boxed?" or "snapshot" or "engine": return [new("nql_handle", n, "nint")];
            case "matrix": return [new("const double*", n, "double*"), new("int32_t", n + "_rows", "int"), new("int32_t", n + "_columns", "int")];
            case "vector": return [new("const double*", n, "double*"), new("int32_t", n + "_count", "int")];
            case "matrix&": return [new("double*", n, "double*"), new("int32_t", n + "_rows", "int"), new("int32_t", n + "_columns", "int")];
            case "vector&": return [new("double*", n, "double*"), new("int32_t", n + "_count", "int")];
            case "span":
                return [new(Element(k.Element!).CType + "*", n, Element(k.Element!).CsType + "*"), new("int32_t", n + "_count", "int")];
            case "tuple":
                // One parameter per element, in its own form.
                return [.. k.Items.SelectMany((item, i) => In(new CParam(ItemParam(p.Name, k, i), item, false, null)))];
            case "array" when k.Element!.Name == "tuple":
            {
                // One array per element, a nullable one with its flags, and one count.
                var slots = new List<CSlot>();
                CKind tk = k.Element;
                for (int i = 0; i < tk.Items.Count; i++)
                {
                    string en = ItemParam(p.Name, tk, i, true);
                    (string et, string es) = Element(tk.Items[i]);
                    slots.Add(new(tk.Items[i].Name == "string" ? "const char* const*" : "const " + et + "*", en, es + "*"));
                    if (tk.Items[i].Name is "double?" or "int32?")
                    {
                        slots.Add(new("const bool*", en + "_has_value", "byte*"));
                    }
                }

                slots.Add(new("int32_t", n + "_count", "int"));
                return slots;
            }

            case "array":
            {
                var slots = new List<CSlot>();
                (string ct, string cs) = Element(k.Element!);
                slots.Add(new(k.Element!.Name == "string" ? "const char* const*" : "const " + ct + "*", n, cs + "*"));
                if (k.Element.Name is "double?" or "int32?")
                {
                    slots.Add(new("const bool*", n + "_has_value", "byte*"));
                }

                slots.Add(new("int32_t", n + "_count", "int"));
                return slots;
            }

            case "jagged":
                return [new("const double*", n, "double*"), new("int32_t", n + "_count", "int"), new("const int32_t*", n + "_row_lengths", "int*"), new("int32_t", n + "_rows", "int")];
            case "map":
            {
                (string kt, string ks) = Element(k.Element!);
                (string vt, string vs) = Element(k.Value!);
                var slots = new List<CSlot>
                {
                    new(k.Element!.Name == "string" ? "const char* const*" : "const " + kt + "*", n + "_keys", ks + "*"),
                    new(k.Value!.Name == "string" ? "const char* const*" : "const " + vt + "*", n + "_values", vs + "*"),
                };
                if (k.Value.Name is "double?" or "int32?")
                {
                    slots.Add(new("const bool*", n + "_has_value", "byte*"));
                }

                slots.Add(new("int32_t", n + "_count", "int"));
                return slots;
            }
        }

        throw new InvalidOperationException("no C form for the parameter kind " + k.Text);
    }

    /// <summary>The C out-parameters a result expands to.</summary>
    /// <param name="k">The result's kind.</param>
    /// <param name="r">The result's name: <c>result</c>, or a tuple element's.</param>
    /// <returns>Its slots.</returns>
    public static List<CSlot> Out(CKind k, string r = "result")
    {
        switch (k.Name)
        {
            case "double": return [new("double*", r, "double*")];
            case "int32" or "enum": return [new("int32_t*", r, "int*")];
            case "int64": return [new("int64_t*", r, "long*")];
            case "bool": return [new("bool*", r, "byte*")];
            case "string": return [new("char**", r, "byte**")];
            case "date" or "date?": return [new("nql_date*", r, "int*")];
            case "period": return [new("nql_period*", r, "NqlPeriod*")];
            case "complex": return [new("nql_complex*", r, "NqlComplex*")];
            case "double?" or "int32?" or "int64?" or "bool?" or "period?" or "enum?" or "complex?":
            {
                CSlot v = Out(k with { Name = k.Name.TrimEnd('?') }, r)[0];
                return [v, new("bool*", r + "_has_value", "byte*")];
            }

            case "handle" or "boxed" or "boxed?" or "snapshot": return [new("nql_handle*", r, "nint*")];
            case "matrix": return [new("double**", r, "double**"), new("int32_t*", r + "_rows", "int*"), new("int32_t*", r + "_columns", "int*")];
            case "vector": return [new("double**", r, "double**"), new("int32_t*", r + "_count", "int*")];
            case "array" when k.Element!.Name is "tuple" or "tuple?":
            {
                // One array per element, named by the element; a nullable tuple's flags; the count.
                CKind te = k.Element;
                var slots = new List<CSlot>();
                for (int i = 0; i < te.Items.Count; i++)
                {
                    (string et, string es) = Element(te.Items[i]);
                    et = te.Items[i].Name == "string" ? "char*" : et;
                    slots.Add(new(et + "**", ItemResult(te, i, true), es + "**"));
                }

                if (te.Name == "tuple?")
                {
                    slots.Add(new("bool**", "has_value", "byte**"));
                }

                slots.Add(new("int32_t*", "count", "int*"));
                return slots;
            }

            case "array":
            {
                (string ct, string cs) = Element(k.Element!);
                ct = k.Element!.Name == "string" ? "char*" : ct;
                var slots = new List<CSlot> { new(ct + "**", r, cs + "**") };
                if (k.Element.Name is "double?" or "int32?")
                {
                    slots.Add(new("bool**", r + "_has_value", "byte**"));
                }

                slots.Add(new("int32_t*", r + "_count", "int*"));
                return slots;
            }

            case "jagged": return [new("double**", r, "double**"), new("int32_t**", r + "_row_lengths", "int**"), new("int32_t*", r + "_rows", "int*")];
            case "map":
            {
                (string kt, string ks) = Element(k.Element!);
                (string vt, string vs) = Element(k.Value!);
                var slots = new List<CSlot>
                {
                    new((k.Element!.Name == "string" ? "char*" : kt) + "**", r + "_keys", ks + "**"),
                    new((k.Value!.Name == "string" ? "char*" : vt) + "**", r + "_values", vs + "**"),
                };
                if (k.Value.Name is "double?" or "int32?")
                {
                    slots.Add(new("bool**", r + "_has_value", "byte**"));
                }

                slots.Add(new("int32_t*", r + "_count", "int*"));
                return slots;
            }

            case "tuple":
                return [.. k.Items.SelectMany((item, i) => Out(item, ItemResult(k, i)))];
            case "distribution":
                return
                [
                    new("double**", r + "_x", "double**"), new("double**", r + "_dx", "double**"), new("double**", r + "_density", "double**"),
                    new("double**", r + "_cumulative", "double**"), new("double**", r + "_excess", "double**"), new("double**", r + "_average", "double**"),
                    new("int32_t*", r + "_count", "int*"),
                ];
            case "draws":
                return [new("double**", "values", "double**"), new("double**", "weights", "double**"), new("nql_handle*", "next", "nint*")];
            case "sequences":
                return [new("double**", "values", "double**"), new("double**", "weights", "double**"), new("int32_t*", "dimension", "int*"), new("nql_handle*", "next", "nint*")];
        }

        throw new InvalidOperationException("no C form for the result kind " + k.Text);
    }

    /// <summary>Every C parameter of a function, its inputs then its results.</summary>
    /// <param name="f">The function.</param>
    /// <returns>Its slots.</returns>
    public static List<CSlot> All(CFunction f) => [.. f.Params.SelectMany(In), .. f.Return is { } r ? Out(r) : []];

    /// <summary>An element's C type and the shim's C# type for it.</summary>
    /// <param name="e">The element kind.</param>
    /// <returns>The two types.</returns>
    public static (string CType, string CsType) Element(CKind e) => e.Name switch
    {
        "double" or "double?" => ("double", "double"),
        "int32" or "int32?" or "enum" => ("int32_t", "int"),
        "int64" => ("int64_t", "long"),
        "bool" => ("bool", "byte"),
        "date" or "date?" => ("nql_date", "int"),
        "period" => ("nql_period", "NqlPeriod"),
        "complex" => ("nql_complex", "NqlComplex"),
        "string" => ("const char*", "byte*"),
        "handle" => ("nql_handle", "nint"),
        _ => throw new InvalidOperationException("no C form for the element kind " + e.Text),
    };

    /// <summary>A tuple element's C parameter: the parameter's name, then the element's in snake case (<c>calibrationSet_option</c>).</summary>
    /// <param name="param">The C# parameter's name.</param>
    /// <param name="tuple">The tuple's kind.</param>
    /// <param name="i">The element's index.</param>
    /// <param name="collection">Whether the tuple is a collection's, whose count and flags take the names <c>count</c> and <c>has_value</c>.</param>
    /// <returns>The C parameter's name.</returns>
    public static string ItemParam(string param, CKind tuple, int i, bool collection = false) => Ident(param + "_" + ItemSnake(tuple, i, collection));

    /// <summary>A returned tuple element's C out-parameter: the element's name in snake case.</summary>
    /// <param name="tuple">The tuple's kind.</param>
    /// <param name="i">The element's index.</param>
    /// <param name="collection">Whether the tuple is a collection's, whose count and flags take the names <c>count</c> and <c>has_value</c>.</param>
    /// <returns>The C out-parameter's name.</returns>
    public static string ItemResult(CKind tuple, int i, bool collection = false) => Ident(ItemSnake(tuple, i, collection));

    // An element's name in snake case; in a collection's tuple, count and has_value take an underscore, as the
    // collection's own count and flags have those names.
    private static string ItemSnake(CKind tuple, int i, bool collection)
    {
        string s = Snake(tuple.ItemNames[i]);
        return collection && s is "count" or "has_value" ? s + "_" : s;
    }

    /// <summary>A name in snake case, as C's out-parameters are named.</summary>
    /// <param name="name">The name.</param>
    /// <returns>Its snake case.</returns>
    public static string Snake(string name) => string.Concat(name.Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}
