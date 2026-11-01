// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Bind.Reading;

namespace NanoQL.Bind.Selecting;

/// <summary>
/// Types' C names: the simple name without its generic arity; where two of one prefix's types share it, each takes its
/// namespace's last segment in front (<c>Experimental_PolarStudentTRng</c>). A released type keeps its released name,
/// and a new type whose simple name a released one holds takes the qualified name.
/// </summary>
internal sealed class TypeNamer
{
    private readonly Dictionary<Type, string> names = [];

    /// <summary>Initializes a new instance of the <see cref="TypeNamer"/> class over every top-level type of the inputs.</summary>
    /// <param name="set">The inputs.</param>
    /// <param name="shipped">The last release's surface.</param>
    public TypeNamer(MetadataSet set, ShippedSurface shipped)
    {
        var types = set.Assemblies.SelectMany(a => a.Assembly.GetExportedTypes().Where(t => !t.IsNested).Select(t => (a.Prefix, Type: t))).ToList();
        var counts = types.GroupBy(x => (x.Prefix, Simple(x.Type))).ToDictionary(g => g.Key, g => g.Count());
        var released = new HashSet<string>(shipped.TypeNames, StringComparer.Ordinal);
        foreach ((string prefix, Type t) in types)
        {
            string? kept = shipped.Type(t.FullName!);
            if (kept != null)
            {
                names[t] = kept;
                continue;
            }

            bool clash = counts[(prefix, Simple(t))] > 1 || released.Contains(Simple(t));
            names[t] = clash ? Qualified(t) : Simple(t);
        }
    }

    /// <summary>Gets the number of types whose name takes their namespace's segment.</summary>
    public int QualifiedCount => names.Count(kv => kv.Value != Simple(kv.Key));

    /// <summary>A type's C name; a closed generic type's is its definition's and its arguments' (R14):
    /// <c>CalibrationResults_Gsr</c>, <c>TimeSeries_double</c>.</summary>
    /// <param name="t">A top-level type of the inputs, or a closed construction of one.</param>
    /// <returns>Its name.</returns>
    public string Of(Type t) => t.IsConstructedGenericType
        ? names[t.GetGenericTypeDefinition()] + "_" + string.Join("_", t.GetGenericArguments().Select(Argument))
        : names[t];

    // A type argument's name in a construction's: a type of the inputs by its C name, a .NET scalar by C's.
    private string Argument(Type a) => a.FullName switch
    {
        "System.Double" => "double",
        "System.Int32" => "int",
        "System.Int64" => "long",
        "System.Boolean" => "bool",
        "System.String" => "string",
        _ when names.ContainsKey(a.IsConstructedGenericType ? a.GetGenericTypeDefinition() : a) => Of(a),
        _ => Simple(a),
    };

    /// <summary>A type's simple name, without its generic arity.</summary>
    /// <param name="t">The type.</param>
    /// <returns>The name.</returns>
    public static string Simple(Type t)
    {
        string n = t.Name;
        int tick = n.IndexOf('`', StringComparison.Ordinal);
        return tick >= 0 ? n[..tick] : n;
    }

    private static string Qualified(Type t)
    {
        string[] ns = (t.Namespace ?? string.Empty).Split('.');
        return ns.Length > 1 ? ns[^1] + "_" + Simple(t) : Simple(t);
    }
}
