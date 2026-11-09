// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using NanoQL.Bind.Model;
using NanoQL.Bind.Reading;
using NanoQL.Bind.Selecting;

namespace NanoQL.Bind.Emitting;

/// <summary>
/// The Excel names: the prefix, then the type's and the member's words run together; the names that meet regardless of
/// case form one group, numbered in its stated order, the first plain and the rest <c>2</c>, <c>3</c>, …; one numbering
/// over the whole filtered surface, the experimental API included, before the list and the stable build cut it; a
/// released name kept (functions.shipped.json), a new function taking its group's lowest unused number.
/// </summary>
internal static class ExcelNames
{
    /// <summary>The add-in's own functions, which no generated name may meet regardless of case.</summary>
    public static readonly IReadOnlyList<string> Own = ["nqlxlError", "nqlxlRepro", "nqlxlRecipe", "nqlxlVersion", "nqlxlStats", "nqlxlLog", "nqlxlDrawsValues", "nqlxlDrawsWeights", "nqlxlDrawsNext"];

    // A1 or R1C1: Excel's columns to XFD and rows to 1,048,576.
    private static readonly Regex A1 = new("^([A-Za-z]{1,3})([0-9]+)$", RegexOptions.CultureInvariant);
    private static readonly Regex R1C1 = new("^[Rr]([0-9]*)[Cc]([0-9]*)$", RegexOptions.CultureInvariant);

    /// <summary>Names every function of the whole filtered surface, a family by its generic type, in the naming
    /// order.</summary>
    /// <param name="all">The functions to name: every kept one, a family by its representative alone.</param>
    /// <param name="families">Each family's members by its representative.</param>
    /// <param name="shipped">The last release's names.</param>
    /// <returns>Each function's name.</returns>
    public static Dictionary<CFunction, string> Number(IReadOnlyList<CFunction> all, IReadOnlyDictionary<CFunction, List<CFunction>> families, ExcelShipped shipped)
    {
        var used = new HashSet<string>(shipped.Reserved, StringComparer.OrdinalIgnoreCase);
        var names = new Dictionary<CFunction, string>();
        var groups = all.GroupBy(f => Base(f, families), StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var g in groups)
        {
            if (Own.Contains(g.Key, StringComparer.OrdinalIgnoreCase))
            {
                throw new BindException($"{g.First().CName}: its Excel name {g.Key} meets the add-in's own {Own.First(o => string.Equals(o, g.Key, StringComparison.OrdinalIgnoreCase))} regardless of case");
            }

            // The naming order has no place for a function with no member or metadata token, so one in a numbered group
            // is refused.
            if (g.Count() > 1 && g.FirstOrDefault(f => f.Kind == "new" && f.Member == null) is { } tokenless)
            {
                throw new BindException($"{tokenless.CName}: a constructor with no member or metadata token in the numbered group {g.Key}; the naming order has no place for it");
            }

            // A released name stays its function's.
            foreach (CFunction f in g)
            {
                if (shipped.Names.TryGetValue(Identity(f, families), out string? frozen))
                {
                    names[f] = frozen;
                }
            }
        }

        foreach (var g in groups)
        {
            int n = 1;
            foreach (CFunction f in g.OrderBy(Order, OrderComparer.Instance))
            {
                if (names.ContainsKey(f))
                {
                    continue;
                }

                string name;
                do
                {
                    name = n == 1 ? g.Key : g.Key + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    n++;
                }
                while (!used.Add(name));
                names[f] = name;
            }
        }

        foreach (string name in names.Values)
        {
            if (IsCellReference(name))
            {
                throw new BindException($"the Excel name {name} reads as a cell reference");
            }

            if (name.Length > 255)
            {
                throw new BindException($"the Excel name {name[..40]}… is over 255 characters");
            }
        }

        return names;
    }

    /// <summary>A function's identity in functions.shipped.json: its C name, or its family's key.</summary>
    /// <param name="f">The function, a family's representative.</param>
    /// <param name="families">The families.</param>
    /// <returns>The identity.</returns>
    public static string Identity(CFunction f, IReadOnlyDictionary<CFunction, List<CFunction>> families)
        => families.ContainsKey(f) ? "family:" + ExcelSelection.FamilyKey(f) : "c:" + f.CName;

    /// <summary>Whether a name reads as a cell reference, A1 or R1C1.</summary>
    /// <param name="name">The name.</param>
    /// <returns>Whether it does.</returns>
    public static bool IsCellReference(string name)
    {
        Match m = A1.Match(name);
        if (m.Success)
        {
            int col = 0;
            foreach (char ch in m.Groups[1].Value.ToUpperInvariant())
            {
                col = (col * 26) + (ch - 'A' + 1);
            }

            return col <= 16384 && long.TryParse(m.Groups[2].Value, out long row) && row is >= 1 and <= 1048576;
        }

        return R1C1.IsMatch(name);
    }

    // The name before its number: the prefix, the type's C name (a family's generic type's) and the member's words.
    private static string Base(CFunction f, IReadOnlyDictionary<CFunction, List<CFunction>> families)
    {
        bool family = families.ContainsKey(f);
        string b = f.Member?.Name is { } mn && family ? (f.Kind == "new" ? "new" : f.Kind == "with" ? "with_" + mn : mn) : f.BaseName;
        string member = b == "new" ? string.Empty
            : b.StartsWith("get_", StringComparison.Ordinal) ? b[4..]
            : b.StartsWith("with_", StringComparison.Ordinal) ? "With" + b[5..]
            : b.StartsWith("op_", StringComparison.Ordinal) ? b[3..]
            : b;
        return f.Prefix + (family ? ExcelSelection.DefName(f.Type) : f.TypeName) + member;
    }

    // The naming order: the function's own type by full name, its own members before inherited ones, then the declaring
    // type's full name, then the member's metadata token; bind and bindon functions after the members, by their input
    // set's index; draws last; a struct's default constructor, which has no token, after its type's members.
    private static (string, int, string, long) Order(CFunction f) => f.Kind switch
    {
        "bind" or "bindon" => (f.Type.FullName!, 2, string.Empty, f.InputSet!.Index),
        "draws" => (f.Type.FullName!, 3, string.Empty, 0),
        "new" when f.Member == null => (f.Type.FullName!, 0, f.Type.FullName!, long.MaxValue),
        _ => (f.Type.FullName!, f.Member?.DeclaringType == f.Type ? 0 : 1, (f.Member?.DeclaringType ?? f.Type).FullName!, f.Member?.MetadataToken ?? 0),
    };

    private sealed class OrderComparer : IComparer<(string, int, string, long)>
    {
        public static readonly OrderComparer Instance = new();

        public int Compare((string, int, string, long) x, (string, int, string, long) y)
        {
            int c = string.CompareOrdinal(x.Item1, y.Item1);
            if (c != 0)
            {
                return c;
            }

            if (x.Item2 != y.Item2)
            {
                return x.Item2.CompareTo(y.Item2);
            }

            c = string.CompareOrdinal(x.Item3, y.Item3);
            return c != 0 ? c : x.Item4.CompareTo(y.Item4);
        }
    }
}
