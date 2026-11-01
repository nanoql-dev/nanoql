// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Xml.Linq;

namespace NanoQL.Bind.Reading;

/// <summary>
/// The exceptions each member documents, read from the <c>&lt;exception cref&gt;</c> elements of the XML documentation
/// the build writes beside an assembly. A kind the C ABI's table doesn't name maps by a member's own documentation.
/// </summary>
internal sealed class ExceptionDocs
{
    private readonly Dictionary<string, string[]> byMember = new(StringComparer.Ordinal);
    private readonly HashSet<string> written = new(StringComparer.Ordinal);

    /// <summary>Reads the documentation beside each input; an input without it documents nothing.</summary>
    /// <param name="set">The inputs.</param>
    /// <returns>The documented exceptions.</returns>
    public static ExceptionDocs Read(MetadataSet set)
    {
        var docs = new ExceptionDocs();
        foreach ((_, Assembly assembly) in set.Assemblies)
        {
            string path = Path.ChangeExtension(assembly.Location, ".xml");
            if (!File.Exists(path))
            {
                continue;
            }

            foreach (XElement member in XDocument.Load(path).Descendants("member"))
            {
                string[] crefs =
                [
                    .. member.Elements("exception").Select(e => ((string?)e.Attribute("cref") ?? string.Empty).Replace("T:", string.Empty, StringComparison.Ordinal)),
                ];
                if (crefs.Length > 0)
                {
                    docs.byMember[(string)member.Attribute("name")!] = crefs;
                }

                // A parameter the member writes into says so: "The target, written".
                foreach (XElement p in member.Elements("param").Where(p => System.Text.RegularExpressions.Regex.IsMatch(p.Value, @"\bwritten\b")))
                {
                    docs.written.Add((string)member.Attribute("name")! + "|" + (string)p.Attribute("name")!);
                }
            }
        }

        return docs;
    }

    /// <summary>The exception types a member documents, by full name.</summary>
    /// <param name="m">The member.</param>
    /// <returns>Their names; empty when it documents none.</returns>
    public IReadOnlyList<string> Of(MemberInfo m) => byMember.TryGetValue(DocId(m), out string[]? crefs) ? crefs : [];

    /// <summary>Whether a member's documentation says it writes into a parameter ("The target, written").</summary>
    /// <param name="m">The member.</param>
    /// <param name="parameter">The parameter's name.</param>
    /// <returns>Whether it does.</returns>
    public bool SaysWritten(MemberInfo m, string parameter) => written.Contains(DocId(m) + "|" + parameter);

    /// <summary>A member's documentation id, as the C# compiler writes it.</summary>
    /// <param name="m">The member.</param>
    /// <returns>Its id.</returns>
    public static string DocId(MemberInfo m) => m switch
    {
        ConstructorInfo c => "M:" + TypeId(c.DeclaringType!) + ".#ctor" + Parameters(c),
        MethodInfo mi => "M:" + TypeId(mi.DeclaringType!) + "." + mi.Name + (mi.IsGenericMethodDefinition ? "``" + mi.GetGenericArguments().Length : string.Empty) + Parameters(mi)
            + (mi.Name is "op_Implicit" or "op_Explicit" ? "~" + Encode(mi.ReturnType) : string.Empty),
        PropertyInfo p => "P:" + TypeId(p.DeclaringType!) + "." + p.Name + (p.GetIndexParameters().Length > 0 ? "(" + string.Join(",", p.GetIndexParameters().Select(x => Encode(x.ParameterType))) + ")" : string.Empty),
        FieldInfo f => "F:" + TypeId(f.DeclaringType!) + "." + f.Name,
        Type t => "T:" + TypeId(t),
        _ => m.Name,
    };

    private static string Parameters(MethodBase m)
    {
        ParameterInfo[] ps = m.GetParameters();
        return ps.Length == 0 ? string.Empty : "(" + string.Join(",", ps.Select(p => Encode(p.ParameterType))) + ")";
    }

    private static string TypeId(Type t) => (t.FullName ?? t.Name).Replace('+', '.');

    private static string Encode(Type t)
    {
        if (t.IsByRef)
        {
            return Encode(t.GetElementType()!) + "@";
        }

        if (t.IsArray)
        {
            return Encode(t.GetElementType()!) + "[]";
        }

        if (t.IsGenericParameter)
        {
            return (t.DeclaringMethod != null ? "``" : "`") + t.GenericParameterPosition;
        }

        if (t.IsGenericType)
        {
            string def = t.GetGenericTypeDefinition().FullName!;
            def = def[..def.IndexOf('`', StringComparison.Ordinal)].Replace('+', '.');
            return def + "{" + string.Join(",", t.GetGenericArguments().Select(Encode)) + "}";
        }

        return TypeId(t);
    }
}
