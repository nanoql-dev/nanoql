// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Bind.Model;
using NanoQL.Bind.Reading;

namespace NanoQL.Bind.Selecting;

/// <summary>
/// Functions' C names (docs/bindings.md, "Names"): <c>prefix_Type_member</c>, and for an overloaded member two
/// underscores and the shortest run of its parameters' type names that tells it from the others. Overloads whose C
/// signatures are the same cross once. A released function keeps its released name; a new one is named against the
/// names in use, so a member that gains its first overload keeps its short name and the newcomer takes the suffix.
/// </summary>
internal static class OverloadNamer
{
    /// <summary>Names one member group's functions, dropping overloads whose C signature another already has.</summary>
    /// <param name="group">The functions of one type's member, by base name.</param>
    /// <param name="shipped">The last release's surface.</param>
    /// <param name="used">The C names already in use, to which the group's names are added.</param>
    /// <param name="dropped">Receives each overload dropped for a C signature another has.</param>
    /// <returns>The group's kept functions, named.</returns>
    public static List<CFunction> Name(List<CFunction> group, ShippedSurface shipped, HashSet<string> used, Action<CFunction, CFunction> dropped)
    {
        var kept = new List<CFunction>();
        foreach (IGrouping<string, CFunction> same in group.GroupBy(CSignature))
        {
            // A member the type declares with `new` hides its base's of the same signature, as C#'s lookup binds it (B2R1:
            // a named class's own Create over its closed generic's); then the overload taking a ReadOnlySpan, else an
            // ImmutableArray, else the first in metadata order.
            CFunction best = same.OrderBy(f => Hides(f) ? 0 : 1).ThenBy(Preference).ThenBy(f => f.Member?.MetadataToken ?? 0).First();
            kept.Add(best);
            foreach (CFunction other in same.Where(f => f != best))
            {
                dropped(other, best);
            }
        }

        kept = [.. kept.OrderBy(f => f.Member?.MetadataToken ?? 0).ThenBy(f => string.Join(",", f.SignatureNames), StringComparer.Ordinal)];
        string Short(CFunction f) => $"{f.Prefix}_{f.TypeName}_{f.BaseName}";
        var fresh = new List<CFunction>();
        foreach (CFunction f in kept)
        {
            if (shipped.Function(f.Key) is { } released)
            {
                f.CName = released;
                f.Overloaded = released != Short(f);
                used.Add(released);
            }
            else
            {
                fresh.Add(f);
            }
        }

        if (fresh.Count == 0)
        {
            return kept;
        }

        if (kept.Count == 1 && !used.Contains(Short(fresh[0])))
        {
            fresh[0].CName = Short(fresh[0]);
            used.Add(fresh[0].CName);
            return kept;
        }

        List<List<string>> sigs = [.. kept.Select(f => f.SignatureNames)];
        foreach (CFunction f in fresh)
        {
            List<string> sig = f.SignatureNames;
            int k = 0;
            while (k <= sig.Count && sigs.Count(o => o.Take(k).SequenceEqual(sig.Take(k))) > 1)
            {
                k++;
            }

            string suffix = string.Join("_", sig.Take(k));
            string name = $"{Short(f)}__{(suffix.Length == 0 ? "void" : suffix)}";
            for (int n = 0; used.Contains(name); n++)
            {
                name = $"{Short(f)}__{(suffix.Length == 0 ? "void" : suffix)}_{n}";
            }

            f.CName = name;
            f.Overloaded = true;
            used.Add(name);
        }

        return kept;
    }

    /// <summary>The name the overload rule reads for a parameter's type.</summary>
    /// <param name="t">The type.</param>
    /// <returns>Its name: <c>double</c>, <c>int</c>, <c>Date</c>, <c>OptDate</c>, <c>doubleArray</c>, a type's simple name.</returns>
    public static string SigName(Type t)
    {
        if (t.IsByRef)
        {
            t = t.GetElementType()!;
        }

        switch (t.FullName)
        {
            case "System.Double": return "double";
            case "System.Int32": return "int";
            case "System.Int64": return "long";
            case "System.Boolean": return "bool";
            case "System.String": return "string";
        }

        if (t.IsArray)
        {
            return SigName(t.GetElementType()!) + "Array";
        }

        if (t.IsGenericType)
        {
            Type d = t.GetGenericTypeDefinition();
            Type[] a = t.GetGenericArguments();
            if (d.FullName == "System.Nullable`1")
            {
                return "Opt" + SigName(a[0]);
            }

            string n = TypeNamer.Simple(d);
            if (n is "ImmutableArray" or "IEnumerable" or "IReadOnlyList" or "ReadOnlySpan" or "IReadOnlyCollection" or "IList")
            {
                return SigName(a[0]) + "Array";
            }

            return n == "Span" ? SigName(a[0]) + "Span" : n + string.Concat(a.Select(SigName));
        }

        return t.Name;
    }

    // Overloads with the same C signature: the parameters' kinds, an object's by its type.
    private static string CSignature(CFunction f) => string.Join(",", f.Params.Select(p => p.Kind.Name is "handle" or "boxed" or "boxed?" or "snapshot"
        ? p.Kind.Name + ":" + p.Kind.Type.KeyName()
        : p.Kind.Text));

    private static int Preference(CFunction f) => f.Params.Sum(p =>
        p.Kind.Type.IsConstructionOf("System.ReadOnlySpan`1") ? 0 : p.Kind.Type.IsConstructionOf("System.Collections.Immutable.ImmutableArray`1") ? 1 : p.Kind.Name == "array" ? 2 : 0);

    // A member the type itself declares that hides its base's (C#'s `new`: non-virtual, or virtual in a new slot), not
    // one that overrides a virtual, whose call sites name the virtual's declaration.
    private static bool Hides(CFunction f) =>
        f.Holder == f.Type && (f.Member is not MethodInfo m || !m.IsVirtual || (m.Attributes & MethodAttributes.NewSlot) != 0);
}
